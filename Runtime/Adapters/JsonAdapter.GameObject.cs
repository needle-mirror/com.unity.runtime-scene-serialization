using System;
using System.Collections.Generic;
using Unity.RuntimeSceneSerialization.Prefabs;
using Unity.RuntimeSceneSerialization.PropertyBags;
using Unity.Serialization.Json;
using UnityEditor;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    partial class JsonAdapter : IJsonAdapter<GameObject>
    {
        readonly struct SerializeAsReferenceScope : IDisposable
        {
            readonly JsonAdapter m_Adapter;
            readonly bool m_PreviousValue;

            public SerializeAsReferenceScope(JsonAdapter adapter)
            {
                m_Adapter = adapter;
                m_PreviousValue = m_Adapter.m_SerializeAsReference;
                m_Adapter.m_SerializeAsReference = true;
            }

            public void Dispose()
            {
                m_Adapter.m_SerializeAsReference = m_PreviousValue;
            }
        }

        const string k_PrefabMetadataGuidPropertyName = "m_Guid";
        const string k_TypePropertyName = "$type";

        readonly SerializationMetadata m_Metadata;
        readonly JsonSerializationParameters m_Parameters;
        readonly Transform m_SceneRoot;

        bool m_SerializeAsReference;

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Component> k_TempComponents = new();
        static readonly List<Component> k_ComponentsToRemove = new();

        public JsonAdapter(SerializationMetadata metadata, JsonSerializationParameters parameters, Transform sceneRoot = null)
        {
            m_Metadata = metadata;
            m_Parameters = parameters;
            m_SceneRoot = sceneRoot;
        }

        void IJsonAdapter<GameObject>.Serialize(in JsonSerializationContext<GameObject> context, GameObject value)
        {
            var writer = context.Writer;
            if (m_SerializeAsReference)
            {
                WriteUnityObjectReference(context, value);
                return;
            }

#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabInstance(value))
            {
                SerializePrefab(context, value);
                return;
            }
#endif

            var prefabMetadata = value.GetComponent<PrefabMetadata>();
            if (prefabMetadata != null)
            {
                SerializePrefab(context, prefabMetadata);
                return;
            }

            using (writer.WriteObjectScope())
            {
                writer.WriteKeyValue(nameof(GameObject.name), value.name);
                writer.WriteKeyValue(nameof(GameObject.hideFlags), (int)value.hideFlags);
                writer.WriteKeyValue(nameof(GameObject.layer), value.layer);
                writer.WriteKeyValue(nameof(GameObject.tag), value.tag);
                writer.WriteKeyValue(GameObjectPropertyBag.ActivePropertyName, value.activeSelf);

                writer.WriteKey(GameObjectPropertyBag.ComponentsPropertyName);
                using (writer.WriteArrayScope())
                {
                    value.GetComponents(k_TempComponents);
                    foreach (var component in k_TempComponents)
                    {
                        if (component == null)
                        {
                            Debug.LogWarningFormat("Found missing script on {0} during serialization", value.name);
                            continue;
                        }

                        JsonSerialization.ToJson(writer, component, m_Parameters);
                    }
                }

                writer.WriteKey(GameObjectPropertyBag.ChildrenPropertyName);
                using (writer.WriteArrayScope())
                {
                    foreach (Transform child in value.transform)
                    {
                        JsonSerialization.ToJson(writer, child.gameObject, m_Parameters);
                    }
                }
            }
        }

        GameObject IJsonAdapter<GameObject>.Deserialize(in JsonDeserializationContext<GameObject> context)
        {
            if (m_SerializeAsReference)
                return (GameObject)ReadUnityObjectReference(context);

            var value = context.SerializedValue.AsObjectView();
            return Deserialize(value, m_SceneRoot);
        }

        GameObject Deserialize(SerializedObjectView value, Transform parent, GameObject gameObject = null)
        {
            if (value.TryGetValue(GameObjectPropertyBag.PrefabMetadataPropertyName, out var prefabMetadata)
                && prefabMetadata.Type == TokenType.Object) // Make sure the view is an Object for backwards compatibility
            {
                return DeserializePrefab(prefabMetadata.AsObjectView(), m_Metadata.AssetPack, parent, gameObject);
            }

            if (gameObject == null)
            {
                gameObject = new GameObject();
                gameObject.transform.SetParent(parent);
            }

            if (value.TryGetValue(nameof(GameObject.name), out var name))
                gameObject.name = name.AsStringView().ToString();

            if (value.TryGetValue(nameof(GameObject.hideFlags), out var hideFlags))
                gameObject.hideFlags = (HideFlags)hideFlags.AsInt32();

            if (value.TryGetValue(nameof(GameObject.layer), out var layer))
                gameObject.layer = layer.AsInt32();

            if (value.TryGetValue(nameof(GameObject.tag), out var tag))
                gameObject.tag = tag.AsStringView().ToString();

            if (value.TryGetValue(nameof(GameObjectPropertyBag.ActivePropertyName), out var active))
                gameObject.SetActive(active.AsBoolean());

            // TODO: Static flags
            // TODO: NavMesh layer
            // TODO: Icon

            if (value.TryGetValue(GameObjectPropertyBag.ComponentsPropertyName, out var components))
            {
                var count = 0;
                gameObject.GetComponents(k_TempComponents);
                var existingCount = k_TempComponents.Count;
                foreach (var componentView in components.AsArrayView())
                {
                    Component existingComponent = null;
                    if (m_FirstPassCompleted && count < existingCount)
                        existingComponent = k_TempComponents[count++];

                    DeserializeComponent(componentView, gameObject, existingComponent);
                }
            }

            if (value.TryGetValue(GameObjectPropertyBag.ChildrenPropertyName, out var children))
            {
                var count = 0;
                var transform = gameObject.transform;
                var existingCount = transform.childCount;
                foreach (var childView in children.AsArrayView())
                {
                    GameObject existingGameObject = null;
                    if (count < existingCount)
                        existingGameObject = transform.GetChild(count++).gameObject;

                    Deserialize(childView.AsObjectView(), transform, existingGameObject);
                }
            }

            return gameObject;
        }

        Component DeserializeComponent(SerializedValueView componentView, GameObject gameObject, Component component = null)
        {
            var assemblyQualifiedTypeName = componentView[k_TypePropertyName].AsStringView().ToString();
            var type = Type.GetType(assemblyQualifiedTypeName);
            if (type == null)
            {
                if (component != null)
                    return component; // Missing script has already been handled; this is the second pass

                var missingComponent = gameObject.AddComponent<MissingComponent>();

                // TODO: Go back to directly deserializing the SerializedValueView--JSONObject conversion is done to work around an exception with empty arrays
                var componentObject = JsonSerialization.FromJson<JsonObject>(componentView, new JsonSerializationParameters {SerializedType = typeof(JsonObject) });

                missingComponent.JsonString = JsonSerialization.ToJson(componentObject);
                return missingComponent;
            }

#if !NET_DOTS && !ENABLE_IL2CPP
            SceneSerialization.RegisterPropertyBagRecursively(type);
#endif

            if (component == null)
                component = type == typeof(Transform) ? gameObject.GetComponent<Transform>() : gameObject.AddComponent(type);

            using (new SerializeAsReferenceScope(this))
            {
                var parameters = m_Parameters;
                parameters.DisableRootAdapters = true;
                JsonSerialization.FromJsonOverride(componentView, ref component, parameters);
            }

            if (component is IFormatVersion formatVersion)
                formatVersion.CheckFormatVersion();

            if (m_FirstPassCompleted && component is ISerializationCallbackReceiver callbackReceiver)
            {
                try
                {
                    callbackReceiver.OnAfterDeserialize();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            return component;
        }
    }
}
