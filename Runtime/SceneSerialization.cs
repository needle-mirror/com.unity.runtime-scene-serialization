using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Properties;
using Unity.RuntimeSceneSerialization.Internal;
using Unity.RuntimeSceneSerialization.Json.Adapters;
using Unity.Serialization.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using JsonAdapter = Unity.RuntimeSceneSerialization.Json.Adapters.JsonAdapter;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Used to save and load scene objects in a JSON format
    /// </summary>
    public static class SceneSerialization
    {
        class CollectionInitializationVisitor : PropertyVisitor
        {
            protected override void VisitProperty<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value)
            {
                if (value != null)
                {
                    if (!typeof(UnityObject).IsAssignableFrom(typeof(TValue)) && TypeTraits<TValue>.IsContainer)
                        PropertyContainer.Accept(this, ref value);

                    return;
                }

                var valueType = typeof(TValue);
                if (valueType.IsArray)
                {
                    var elementType = valueType.GetElementType();
                    if (elementType == null)
                        return;

                    value = (TValue)(object)Array.CreateInstance(elementType, 0);
                    return;
                }

                if (valueType == typeof(string))
                {
                    value = (TValue) (object) string.Empty;
                    return;
                }

                if (!typeof(IList).IsAssignableFrom(valueType))
                    return;

                value = Activator.CreateInstance<TValue>();
            }
        }


#if !NET_DOTS && !ENABLE_IL2CPP
        static readonly object[] k_RegisterPropertyBagsArguments = new object[1];
        static readonly Dictionary<Type, IPropertyBag> k_PropertyBags = new();
#endif

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<GameObject> k_Roots = new();
        static readonly List<GameObject> k_SavedRoots = new();
        static readonly List<Component> k_Components = new();

        /// <summary>
        /// Load a scene from JSON into the active scene
        /// </summary>
        /// <param name="json">JSON representation of this scene</param>
        /// <param name="assetPack">The AssetPack to be used for asset references</param>
        /// <param name="onAfterDeserialize">An action which will be invoked after deserialization before root objects are activated</param>
        /// <returns>The SerializationMetadata used to import this scene</returns>
        public static SerializationMetadata ImportScene(string json, AssetPack assetPack = null,
            Action<List<GameObject>> onAfterDeserialize = null)
        {
            var sceneRoot = new GameObject("Temp scene root");
            var tempSceneRoot = sceneRoot.transform;

            // Set root inactive so that we can activate everything at once
            sceneRoot.SetActive(false);

            var metadata = new SerializationMetadata(assetPack);
            var adapters = new List<IJsonAdapter>();
            var parameters = new JsonSerializationParameters
            {
                DisableSerializedReferences = true,
                UserDefinedAdapters = adapters
            };

            var adapter = new JsonAdapter(metadata, parameters, tempSceneRoot);
            adapters.Add(adapter);

            try
            {
                JsonSerialization.FromJson<SerializedScene>(json, parameters);

                k_Roots.Clear();
                foreach (Transform child in tempSceneRoot)
                {
                    k_Roots.Add(child.gameObject);
                }

                metadata.SetupSceneObjectMetadata(k_Roots);

                // Deserialize a second time to set scene references
                var activeScene = SceneManager.GetActiveScene();
                adapters.Add(new JsonSerializationCallbackReceiverAdapter());
                JsonSerialization.FromJsonOverride(json, ref activeScene, parameters);

                onAfterDeserialize?.Invoke(k_Roots);

                foreach (var root in k_Roots)
                {
                    var visitor = new CollectionInitializationVisitor();
                    root.GetComponentsInChildren(true, k_Components);
                    var componentCount = k_Components.Count;
                    for (var i = 0; i < componentCount; i++)
                    {
                        var component = k_Components[i];
                        if (component == null)
                            continue;

#if !NET_DOTS && !ENABLE_IL2CPP
                        // Register a runtime scene serialization property bag in case we encounter new component types in prefabs
                        RegisterPropertyBagRecursively(component.GetType());
#endif

                        PropertyContainer.Accept(visitor, ref component);
                    }

                    // Moving this root object out of its parent will activate all newly created GameObjects
                    root.transform.SetParent(null, false);
                }

                adapter.RenderSettings?.ApplyValuesToRenderSettings();
                DynamicGI.UpdateEnvironment();

                k_Roots.Clear();
                SafeDestroy(sceneRoot);
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return default;
            }

            return metadata;
        }

        /// <summary>
        /// Serialize a scene to Json
        /// Scene must be loaded and valid
        /// </summary>
        /// <param name="scene">The scene to serialize</param>
        /// <param name="renderSettings">The scene's render settings</param>
        /// <param name="assetPack">The asset pack used to store and retrieve assets</param>
        /// <returns>The serialized scene as a Json string</returns>
        public static string SerializeScene(Scene scene, SerializedRenderSettings renderSettings = default, AssetPack assetPack = null)
        {
            var metadata = new SerializationMetadata(assetPack);
            k_SavedRoots.Clear();
            SerializedScene.GetSavedRoots(scene, k_SavedRoots);
            metadata.SetupSceneObjectMetadata(k_SavedRoots);
            var jsonText = ToJson(new SerializedScene(k_SavedRoots, renderSettings), metadata);
            k_SavedRoots.Clear();
            return jsonText;
        }

        /// <summary>
        /// Alternative version of JsonSerialization.FromJson which uses custom Json Adapters
        /// Use this if you need to deserialize GameObjects, Components,
        /// SerializedObjects, or types that implement `ISerializationCallbacks`
        /// </summary>
        /// <param name="jsonString">The Json string to be deserialized</param>
        /// <param name="metadata">SerializationMetadata for this call</param>
        /// <typeparam name="T">The type of value represented by the Json string</typeparam>
        /// <returns>The deserialized value</returns>
        /// <exception cref="Exception">Thrown if serialization failed</exception>
        public static T FromJson<T>(string jsonString, SerializationMetadata metadata = null)
        {
            T value = default;
            FromJsonOverride(jsonString, ref value);
            return value;
        }

        /// <summary>
        /// Alternative version of JsonSerialization.FromJson which uses custom Json Adapters
        /// Use this if you need to deserialize GameObjects, Components,
        /// SerializedObjects, or types that implement `ISerializationCallbacks`
        /// </summary>
        /// <param name="jsonString">The Json string to be deserialized</param>
        /// /// <param name="value">A reference of type T to use for populating the deserialized value</param>
        /// <param name="metadata">SerializationMetadata for this call</param>
        /// <typeparam name="T">The type of value represented by the Json string</typeparam>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown if serialization failed</exception>
        public static void FromJsonOverride<T>(string jsonString, ref T value, SerializationMetadata metadata = null)
        {
#if !NET_DOTS && !ENABLE_IL2CPP
            RegisterPropertyBagRecursively(typeof(T));
#endif

            metadata ??= new SerializationMetadata();
            var adapters = new List<IJsonAdapter>();
            var parameters = new JsonSerializationParameters
            {
                DisableSerializedReferences = true,
                UserDefinedAdapters = adapters
            };

            adapters.Add(new JsonAdapter(metadata, parameters));
            adapters.Add(new JsonSerializationCallbackReceiverAdapter());
            adapters.Add(new JsonFormatVersionAdapter());
            JsonSerialization.FromJsonOverride(jsonString, ref value, parameters);
        }

        /// <summary>
        /// Alternative method to JsonSerialization.ToJson which uses custom Json adapters
        /// Use this if you need to serialize GameObjects, Components,
        /// SerializedObjects, or types that implement `ISerializationCallbacks`
        /// </summary>
        /// <param name="value">The value to serialize</param>
        /// <param name="metadata">SerializationMetadata for this call</param>
        /// <typeparam name="T">The type of the value being serialized</typeparam>
        /// <returns>A string containing the Json serialized representation of `value`</returns>
        public static string ToJson<T>(T value, SerializationMetadata metadata = null)
        {
#if !NET_DOTS && !ENABLE_IL2CPP
            RegisterPropertyBagRecursively(typeof(T));
#endif

            metadata ??= new SerializationMetadata();
            var adapters = new List<IJsonAdapter>();
            var parameters = new JsonSerializationParameters
            {
                DisableSerializedReferences = true,
                UserDefinedAdapters = adapters
            };

            adapters.Add(new JsonSerializationCallbackReceiverAdapter());
            adapters.Add(new JsonAdapter(metadata, parameters));
            return JsonSerialization.ToJson(value, parameters);
        }

#if !NET_DOTS && !ENABLE_IL2CPP
        /// <summary>
        /// Register a reflected property bag which is compatible with scene serialization for the given type and the
        /// types of its properties, and their properties recursively
        /// </summary>
        /// <param name="type">The type which will used to create the property bags</param>
        public static void RegisterPropertyBagRecursively(Type type)
        {
            if (type == typeof(object))
                return;

            if (!TypeTraits.IsContainer(type) || type.IsGenericTypeDefinition || type.IsAbstract || type.IsInterface)
                return;

            if (PropertyBag.Exists(type))
                return;

            if (k_PropertyBags.ContainsKey(type))
                return;

            var propertyBag = ReflectedPropertyBagProvider.Instance.CreatePropertyBag(type);
            k_PropertyBags.Add(type, propertyBag);

            if (propertyBag == null)
                return;

            var method = SerializationUtils.GetRegisterPropertyBagsForPropertiesMethod(type);
            k_RegisterPropertyBagsArguments[0] = propertyBag;
            method?.Invoke(null, k_RegisterPropertyBagsArguments);
        }
#endif

        static void SafeDestroy(UnityObject obj)
        {
            if (Application.isPlaying)
            {
                UnityObject.Destroy(obj);
            }
#if UNITY_EDITOR
            else
            {
                UnityObject.DestroyImmediate(obj);
            }
#endif
        }
    }
}
