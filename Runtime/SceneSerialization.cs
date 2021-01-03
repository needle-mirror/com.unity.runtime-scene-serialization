using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
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
    /// <summary>
    /// Used to save and load scene objects in a JSON format
    /// </summary>
    public static class SceneSerialization
    {
#if !NET_DOTS && !ENABLE_IL2CPP
        static readonly object[] k_RegisterPropertyBagsArguments = new object[1];
#endif

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<GameObject> k_Roots = new List<GameObject>();
        static readonly List<DeserializationEvent> k_Events = new List<DeserializationEvent>();

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
            var metadata = new SerializationMetadata(assetPack);
            var container = new SceneContainer(SceneManager.GetActiveScene(), metadata, false);
            var sceneRoot = new GameObject();
            var sceneRootTransform = sceneRoot.transform;
            container.SceneRootTransform = sceneRootTransform;

            // Set root inactive so that we can activate everything at once
            sceneRoot.SetActive(false);
            try
            {
                SerializationUtils.DeserializeScene(json, metadata, ref container);
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

                metadata.SetupSceneObjectMetadata(k_Roots);
                metadata.DoPostSerializationActions();

                onAfterDeserialize?.Invoke(k_Roots);

                foreach (var root in k_Roots)
                {
                    // Moving this root object out of its parent will activate all newly created GameObjects
                    root.transform.SetParent(null, false);
                }
            }

            UnityObjectUtils.Destroy(sceneRoot);
            return metadata;
        }

        /// <summary>
        /// Alternative method to JsonSerialization.ToJson which uses JsonSceneWriter
        /// Use this if you need to support `ISerializationCallbacks`
        /// </summary>
        /// <param name="value">The value to serialize</param>
        /// <param name="metadata">SerializationMetadata for this call</param>
        /// <typeparam name="T">The type of the value being serialized</typeparam>
        /// <returns>A string containing the Json serialized representation of `value`</returns>
        public static string ToJson<T>(T value, SerializationMetadata metadata = null)
        {
            var parameters = new JsonSerializationParameters
            {
                DisableRootAdapters = true,
                DisableSerializedReferences = true
            };

            using (var writer = new JsonStringBuffer(parameters.InitialCapacity, Allocator.Temp))
            {
                var container = new PropertyWrapper<T>(value);

                var visitor = new JsonSceneWriter(metadata);

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
                    var visitor = new JsonSceneReader(new SerializationMetadata());
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

                    var result = SerializationUtils.CreateResult(k_Events);
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
        /// <param name="assetPack">The asset pack used to store and retrieve assets</param>
        /// <returns>The serialized scene as a Json string</returns>
        public static string SerializeScene(Scene scene, AssetPack assetPack = null)
        {
            var metadata = new SerializationMetadata(assetPack);
            return ToJson(new SceneContainer(scene, metadata), metadata);
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

            if (type.IsGenericTypeDefinition || type.IsAbstract || type.IsInterface)
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
            if (!RuntimeTypeInfoCache.IsContainerType(type) || type.IsGenericTypeDefinition || type.IsAbstract || type.IsInterface)
                return;

            var propertyBag = ReflectedPropertyBagProvider.Instance.CreatePropertyBag(type);
            if (propertyBag == null)
                return;

            propertyBag.Register();

            var method = SerializationUtils.GetRegisterPropertyBagsForPropertiesMethod(type);
            k_RegisterPropertyBagsArguments[0] = propertyBag;
            method?.Invoke(null, k_RegisterPropertyBagsArguments);
        }
#endif
    }
}
