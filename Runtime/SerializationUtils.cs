using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.RuntimeSceneSerialization.Prefabs;
using Unity.XRTools.Utils;
using Unity.Properties;
using Unity.Properties.Internal;
using Unity.RuntimeSceneSerialization.Internal;
using Unity.Serialization.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = Unity.Serialization.Json.EventType;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization
{
    public static class SerializationUtils
    {
        static readonly List<DeserializationEvent> k_Events = new List<DeserializationEvent>();

#if !NET_DOTS && !ENABLE_IL2CPP
        static readonly MethodInfo k_RegisterPropertyBagsForPropertiesMethod = typeof(SerializationUtils).GetMethod(nameof(RegisterPropertyBagsForProperties), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly Dictionary<Type, MethodInfo> k_RegisterPropertyBagsMethods = new Dictionary<Type, MethodInfo>();
        static readonly object[] k_RegisterPropertyBagsArguments = new object[1];
#endif

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Component> k_Components = new List<Component>();
        static readonly List<GameObject> k_Roots = new List<GameObject>();

        public static void SortComponentList(List<Component> components, List<KeyValuePair<Component, bool>> sortedComponents)
        {
            // Remove PrefabMetadata because it is runtime-only
            for (var i = 0; i < components.Count; i++)
            {
                if (components[i].GetType() == typeof(PrefabMetadata))
                    components.Remove(components[i]);
            }

            //TODO: handle chained dependencies
            sortedComponents.Clear();
            foreach (var component in components)
            {
                if (!component) // Check for missing scripts
                    continue;

                var pair = new KeyValuePair<Component, bool>(component, false);
                var customAttributes = component.GetType().GetCustomAttributes(typeof(RequireComponent), true);
                if (customAttributes.Length > 0)
                {
                    var requireAttribute = (RequireComponent)customAttributes[0];
                    if (requireAttribute.m_Type0 != typeof(Transform) || requireAttribute.m_Type1 != null && requireAttribute.m_Type1 != typeof(Transform))
                    {
                        pair = new KeyValuePair<Component, bool>(component, true);
                    }
                }

                sortedComponents.Add(pair);
            }

            sortedComponents.Sort((a, b) => a.Value.CompareTo(b.Value));
        }

        public static void DeserializeScene(string jsonString, ref SceneContainer value, JsonSerializationParameters parameters = default)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
            {
                using (var reader = new SerializedObjectReader(stream))
                {
                    reader.Read(out var document);
                    var container = new PropertyWrapper<SceneContainer>(value);
                    k_Events.Clear();
                    PropertyContainer.Visit(ref value, new SceneVisitor(value.SceneRootTransform, document.AsUnsafe(), k_Events, parameters), out var errorCode);
                    value = container.Value;
                    var result = CreateResult(k_Events);
                    if (!result.DidSucceed() || errorCode != VisitErrorCode.Ok)
                    {
                        foreach (var @event in k_Events)
                        {
                            Debug.LogError(@event);
                        }

                        throw new Exception("Failed to deserialize scene");
                    }
                }
            }
        }

        static DeserializationResult CreateResult(List<DeserializationEvent> events)
            => events.Count > 0 ? new DeserializationResult(events.ToList()) : default;

        public static void ImportScene(string jsonText, AssetPack assetPack = null)
        {
            EditorMetadata.Reset();
            EditorMetadata.SceneReferenceActions.Clear();
            var container = new SceneContainer(SceneManager.GetActiveScene(), false);
            var sceneRoot = new GameObject();
            var sceneRootTransform = sceneRoot.transform;
            container.SceneRootTransform = sceneRootTransform;

            // Set root inactive so that we can activate everything at once
            sceneRoot.SetActive(false);
            AssetPack.CurrentAssetPack = assetPack;
            try
            {
                DeserializeScene(jsonText, ref container);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                container = null;
            }

            if (container != null)
            {
                k_Roots.Clear();
                foreach (Transform child in sceneRootTransform)
                {
                    k_Roots.Add(child.gameObject);
                }

                EditorMetadata.SetupEditorMetadata(k_Roots);
                container.SetSceneReferences();
                foreach (var root in k_Roots)
                {
                    // Moving this root object out of its parent will activate all newly created GameObjects
                    root.transform.SetParent(null, false);
                }
            }

            AssetPack.CurrentAssetPack = null;
            UnityObjectUtils.Destroy(sceneRoot);
        }

        public static string GetTransformPath(this Transform root, Transform target)
        {
            var path = string.Empty;
            while (target != null && root != target)
            {
                var name = target.name;
                if (name.Contains('/'))
                {
                    name = name.Replace('/', '_');
                    Debug.LogWarning("Encountered GameObject with name that contains '/'. This may cause issues when deserializing prefab overrides");
                }

                path = string.IsNullOrEmpty(path) ? name : $"{name}/{path}";

                target = target.parent;
            }

            if (target == null)
                Debug.LogError($"Could not find target transform {target} in {root}");

            return path;
        }

        public static Transform GetTransformAtPath(this Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            var names = path.Split('/');
            foreach (var name in names)
            {
                var found = false;
                foreach (Transform child in root)
                {
                    var childName = child.name;
                    if (childName.Contains('/'))
                    {
                        childName = name.Replace('/', '_');
                        Debug.LogWarning("Encountered GameObject with name that contains '/'. This may cause issues when deserializing prefab overrides");
                    }

                    if (childName == name)
                    {
                        root = child;
                        found = true;
                    }
                }

                if (!found)
                {
                    Debug.LogError($"Could not find {name} in {root.name}");
                    return null;
                }
            }

            return root;
        }

        public static UnityObject GetTargetObjectWithComponentIndex(this GameObject gameObject, int index)
        {
            if (index < 0)
                return gameObject;

            gameObject.GetComponents(k_Components);
            if (index >= k_Components.Count)
            {
                Debug.LogError($"Component at {index} not found on {gameObject}");
                return null;
            }

            return k_Components[index];
        }

        public static void GetTransformPathAndComponentIndex(Transform root, UnityObject target,
            out string transformPath, out int componentIndex)
        {
            if (target is GameObject targetGameObject)
            {
                transformPath = root.GetTransformPath(targetGameObject.transform);
                componentIndex = -1;
                return;
            }

            if (target is Component targetComponent)
            {
                transformPath = root.GetTransformPath(targetComponent.transform);
                targetComponent.GetComponents(k_Components);
                var index = k_Components.IndexOf(targetComponent);
                if (index < 0)
                {
                    Debug.LogError($"Could not get find {targetComponent} on {targetComponent.gameObject}");
                    componentIndex = -1;
                    return;
                }

                componentIndex = index;
                return;
            }

            Debug.LogError($"Could not get transform path and component index for object {target}");
            transformPath = string.Empty;
            componentIndex = -1;
        }

        /// <summary>
        /// Alternative method to JsonSerialization.ToJson which uses JsonSceneWriter
        /// Use this if you need to support `ISerializationCallbacks`
        /// </summary>
        /// <param name="value">The value to serialize</param>
        /// <typeparam name="T">The type of the value being serialized</typeparam>
        /// <returns>A string containing the Json serialized representation of `value`</returns>
        public static string ToJson<T>(T value)
        {
            var parameters = new JsonSerializationParameters
            {
                DisableRootAdapters = true,
                DisableSerializedReferences = true
            };

            using (var writer = new JsonStringBuffer(parameters.InitialCapacity, Allocator.Temp))
            {
                var container = new PropertyWrapper<T>(value);

                var visitor = new JsonSceneWriter();

                visitor.SetStringWriter(writer);
                visitor.SetSerializedType(parameters.SerializedType);
                visitor.SetMinified(parameters.Minified);
                visitor.SetSimplified(parameters.Simplified);

                using (visitor.Lock()) PropertyContainer.Visit(ref container, visitor);
                return writer.ToString();
            }
        }

        /// <summary>
        /// Alternative version of JsonSerialization.FromJson which uses JsonSceneReader
        /// </summary>
        /// <param name="jsonString">The Json string to be deserialized</param>
        /// <typeparam name="T">The type of value represented by the Json string</typeparam>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown if serialization failed</exception>
        public static T FromJson<T>(string jsonString)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
            {
                using (var reader = new SerializedObjectReader(stream))
                {
                    reader.Read(out var document);

                    k_Events.Clear();
                    var visitor = new JsonSceneReader();
                    visitor.SetView(document.AsUnsafe());
                    visitor.SetEvents(k_Events);
                    T value = default;
                    var container = new PropertyWrapper<T>(value);
                    try
                    {
                        using (visitor.Lock()) PropertyContainer.Visit(ref container, visitor);
                    }
                    catch (Exception e)
                    {
                        k_Events.Add(new DeserializationEvent(EventType.Exception, e));
                    }
                    value = container.Value;

                    var result = CreateResult(k_Events);
                    if (!result.DidSucceed())
                    {
                        foreach (var @event in k_Events)
                        {
                            Debug.LogError(@event);
                        }

                        throw new Exception("Failed to deserialize");
                    }

                    return value;
                }
            }
        }

        /// <summary>
        /// Serialize a scene to Json
        /// Scene must be loaded and valid
        /// </summary>
        /// <param name="scene">The scene to serialize</param>
        /// <returns>The serialized scene as a Json string</returns>
        public static string SerializeScene(Scene scene)
        {
            return ToJson(new SceneContainer(scene));
        }

#if !NET_DOTS && !ENABLE_IL2CPP
        /// <summary>
        /// Register a reflected property bag which is compatible with scene serialization for the given type
        /// </summary>
        /// <param name="type">The type which will be represented by the property bag</param>
        public static void RegisterPropertyBag(Type type)
        {
            if (ReflectedPropertyBagUtils.PropertyBagExists(type))
                return;

            if (type.IsGenericTypeDefinition)
                return;

            var propertyBag = ReflectedPropertyBagProvider.Instance.CreatePropertyBag(type);
            propertyBag?.Register();
        }

        /// <summary>
        /// Register a reflected property bag which is compatible with scene serialization for the given type and the
        /// types of its properties, and their properties recursively
        /// </summary>
        /// <param name="type">The type which will used to create the property bags</param>
        public static void RegisterPropertyBagRecursively(Type type)
        {
            if (!RuntimeTypeInfoCache.IsContainerType(type) || type.IsGenericTypeDefinition)
                return;

            var propertyBag = ReflectedPropertyBagProvider.Instance.CreatePropertyBag(type);
            if (propertyBag == null)
                return;

            propertyBag.Register();

            var method = GetRegisterPropertyBagsForPropertiesMethod(type);
            k_RegisterPropertyBagsArguments[0] = propertyBag;
            method?.Invoke(null, k_RegisterPropertyBagsArguments);
        }

        static MethodInfo GetRegisterPropertyBagsForPropertiesMethod(Type type)
        {
            if (k_RegisterPropertyBagsMethods.TryGetValue(type, out var method))
                return method;

            method = k_RegisterPropertyBagsForPropertiesMethod.MakeGenericMethod(type);
            k_RegisterPropertyBagsMethods[type] = method;
            return method;
        }

        static void RegisterPropertyBagsForProperties<TContainer>(IPropertyBag propertyBag)
        {
            if (!(propertyBag is IPropertyBag<TContainer> typedPropertyBag))
                return;

            var container = default(TContainer);
            foreach (var property in typedPropertyBag.GetProperties(ref container))
            {
                RegisterPropertyBagRecursively(property.DeclaredValueType());
            }
        }
#endif

        /// <summary>
        /// Used by InvokeGenericMethodWrapper to define the method by which generic method delegates are provided
        /// </summary>
        public interface IGenericMethodFactory
        {
            /// <summary>
            /// Get a method which returns void and has a single argument of type T
            /// </summary>
            /// <typeparam name="T">The specific </typeparam>
            /// <returns>The method, which will be invoked by InvokeGenericMethodWrapper</returns>
            Action<T> GetGenericMethod<T>() where T : UnityObject;
        }

        /// <summary>
        /// Invoke a generic method with a UnityObject as an argument based on its specific type
        /// This is currently implemented for the GameObject type. All Component types in loaded assemblies will be
        /// implemented by appending code via the PrefabOverrideAssemblyPostProcessor
        /// </summary>
        /// <param name="argument">The UnityObject which will be passed as an argument, and whose type will be used to
        /// determine the specific generic implementation to use</param>
        /// <param name="methodFactory">A factory object which can provide a generic method delegate on demand</param>
        public static void InvokeGenericMethodWrapper(UnityObject argument, IGenericMethodFactory methodFactory)
        {
            if (argument is GameObject gameObject)
                CallGenericMethod(gameObject, methodFactory);
        }

        internal static void CallGenericMethod<T>(T obj, IGenericMethodFactory methodFactory) where T : UnityObject
        {
            var method = methodFactory.GetGenericMethod<T>();
            method.Invoke(obj);
        }
    }
}
