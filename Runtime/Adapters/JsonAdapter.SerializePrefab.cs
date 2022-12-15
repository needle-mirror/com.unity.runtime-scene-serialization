using System.Collections.Generic;
using Unity.RuntimeSceneSerialization.Internal;
using Unity.RuntimeSceneSerialization.Internal.Prefabs;
using Unity.RuntimeSceneSerialization.Prefabs;
using Unity.RuntimeSceneSerialization.PropertyBags;
using Unity.Serialization.Json;
using UnityEditor;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    partial class JsonAdapter
    {
#if UNITY_EDITOR
        void SerializePrefab(IJsonSerializationContext context, GameObject gameObject)
        {
            AssetPack.GetPrefabMetadata(gameObject, out var guid, m_Metadata.AssetPack);
            var runtimeRemovedComponents = new List<RuntimeRemovedComponent>();
            var removedComponents = PrefabUtility.GetRemovedComponents(gameObject);
            foreach (var component in removedComponents)
            {
                // TODO: optimize get component index
                var assetComponent = component.assetComponent;

                // Stale overrides can have a null reference on assetComponent
                if (assetComponent == null)
                    continue;

                assetComponent.gameObject.GetComponents(k_TempComponents);
                var index = k_TempComponents.IndexOf(assetComponent);
                if (index < 0)
                {
                    Debug.LogWarning("Could not find removed component " + assetComponent);
                    continue;
                }

                runtimeRemovedComponents.Add(new RuntimeRemovedComponent
                {
                    TransformPath = gameObject.transform.GetTransformPath(component.containingInstanceGameObject.transform),
                    ComponentIndex = index
                });
            }

            var addedGameObjects = new List<RuntimeAddedGameObject>();
            var addedComponents = new List<RuntimeAddedComponent>();
            GetMetadataRecursively(gameObject, true, addedGameObjects, addedComponents);

            var propertyOverrides = new List<RuntimePrefabPropertyOverride>();
            var overrides = PrefabUtility.GetObjectOverrides(gameObject, true);
            foreach (var over in overrides)
            {
                var instanceObject = over.instanceObject;
                if (instanceObject == null)
                    continue;

                SerializationUtils.GetTransformPathAndComponentIndex(gameObject.transform, instanceObject,
                    out var transformPath, out var componentIndex);

                RuntimePrefabPropertyOverride.GetOverrides(instanceObject, propertyOverrides, transformPath, componentIndex, m_Metadata);
            }

            SerializePrefab(context, guid, runtimeRemovedComponents, addedGameObjects, addedComponents, propertyOverrides);
        }

        static void GetMetadataRecursively(GameObject gameObject, bool isRoot, List<RuntimeAddedGameObject> addedGameObjects,
            List<RuntimeAddedComponent> addedComponents, string parentTransformPath = "", string transformPath = "")
        {
            var transform = gameObject.transform;
            if (!isRoot && PrefabUtility.IsAddedGameObjectOverride(gameObject))
            {
                addedGameObjects.Add(new RuntimeAddedGameObject
                {
                    TransformPath = parentTransformPath,
                    GameObject = gameObject
                });
            }

            gameObject.GetComponents(k_TempComponents);
            foreach (var component in k_TempComponents)
            {
                if (PrefabUtility.IsAddedComponentOverride(component))
                {
                    addedComponents.Add(new RuntimeAddedComponent
                    {
                        TransformPath = transformPath,
                        Component = component
                    });
                }
            }

            foreach (Transform child in transform)
            {
                var childTransformPath = child.name;
                if (!string.IsNullOrEmpty(transformPath))
                    childTransformPath = $"{transformPath}/{child.name}";

                GetMetadataRecursively(child.gameObject, false, addedGameObjects, addedComponents,
                    transformPath, childTransformPath);
            }
        }
#endif
        void SerializePrefab(IJsonSerializationContext context, PrefabMetadata prefabMetadata)
        {
            SerializePrefab(context, prefabMetadata.Guid, prefabMetadata.RemovedComponents,
                prefabMetadata.AddedGameObjects, prefabMetadata.AddedComponents, prefabMetadata.PropertyOverrides);
        }

        void SerializePrefab(IJsonSerializationContext context, string guid,
            List<RuntimeRemovedComponent> removedComponents, List<RuntimeAddedGameObject> addedGameObjects,
            List<RuntimeAddedComponent> addedComponents, List<RuntimePrefabPropertyOverride> propertyOverrides)
        {
            var writer = context.Writer;
            using (writer.WriteObjectScope())
            {
                writer.WriteKey(GameObjectPropertyBag.PrefabMetadataPropertyName);

                using (writer.WriteObjectScope())
                {
                    writer.WriteKeyValue(k_PrefabMetadataGuidPropertyName,guid);
                    writer.WriteKey(nameof(PrefabMetadata.RemovedComponents));
                    JsonSerialization.ToJson(writer, removedComponents, m_Parameters);

                    writer.WriteKey(nameof(PrefabMetadata.AddedGameObjects));
                    JsonSerialization.ToJson(writer, addedGameObjects, m_Parameters);
                    writer.WriteKey(nameof(PrefabMetadata.AddedComponents));
                    JsonSerialization.ToJson(writer, addedComponents, m_Parameters);

                    using (new SerializeAsReferenceScope(this))
                    {
                        writer.WriteKey(nameof(PrefabMetadata.PropertyOverrides));
                        JsonSerialization.ToJson(writer, propertyOverrides, m_Parameters);
                    }
                }

                // Write empty components property for forward compatibility (triggers FinalizePrefab in older versions)
                writer.WriteKeyValue(GameObjectPropertyBag.ComponentsPropertyName, null);
            }
        }
    }
}
