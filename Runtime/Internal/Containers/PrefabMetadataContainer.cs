using System;
using System.Collections.Generic;
using Unity.RuntimeSceneSerialization.Internal.Prefabs;
using Unity.RuntimeSceneSerialization.Prefabs;
using UnityEditor;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    [Serializable]
    class PrefabMetadataContainer
    {
        [SerializeField]
        string m_Guid;

        public string Guid => m_Guid;
        public List<RuntimeRemovedComponent> RemovedComponents;
        public List<RuntimeAddedGameObject> AddedGameObjects;
        public List<RuntimeAddedComponent> AddedComponents;
        public List<RuntimePrefabPropertyOverride> PropertyOverrides;

        internal static string GuidFieldName => nameof(m_Guid);

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Component> k_Components = new List<Component>();

        public PrefabMetadataContainer() { }

        public PrefabMetadataContainer(PrefabMetadata prefabMetadata, SerializationMetadata metadata)
        {
            m_Guid = prefabMetadata.Guid;
            AddedGameObjects = prefabMetadata.AddedGameObjects;
            AddedComponents = prefabMetadata.AddedComponents;
            RemovedComponents = prefabMetadata.RemovedComponents;
            PropertyOverrides = prefabMetadata.PropertyOverrides;

            // Convert any UnityObject properties back to UnityObjectReference for serialization
            if (PropertyOverrides == null)
                return;

            PostProcessOverrideList(PropertyOverrides, metadata);
        }

        static void PostProcessOverrideList(List<RuntimePrefabPropertyOverride> list, SerializationMetadata metadata)
        {
            var count = list.Count;
            for (var i = 0; i < count; i++)
            {
                var propertyOverride = list[i];
                switch (propertyOverride)
                {
                    case IRuntimePrefabOverrideUnityObject objectProperty:
                        objectProperty.ConvertToUnityObjectReferenceOverride(list, i, metadata);
                        break;

                    case RuntimePrefabPropertyOverrideList listProperty:
                        PostProcessOverrideList(listProperty.List, metadata);
                        break;
                }
            }
        }

#if UNITY_EDITOR
        public PrefabMetadataContainer(GameObject gameObject, SerializationMetadata metadata)
        {
            PropertyOverrides = new List<RuntimePrefabPropertyOverride>();
            AssetPack.GetPrefabMetadata(gameObject, out var guid, metadata.AssetPack);
            m_Guid = guid;

            var overrides = PrefabUtility.GetObjectOverrides(gameObject, true);
            foreach (var over in overrides)
            {
                var instanceObject = over.instanceObject;
                if (instanceObject == null)
                    continue;

                SerializationUtils.GetTransformPathAndComponentIndex(gameObject.transform, instanceObject,
                    out var transformPath, out var componentIndex);

                RuntimePrefabPropertyOverride.GetOverrides(instanceObject, PropertyOverrides, transformPath, componentIndex, metadata);
            }

            RemovedComponents = new List<RuntimeRemovedComponent>();
            var removedComponents = PrefabUtility.GetRemovedComponents(gameObject);
            foreach (var component in removedComponents)
            {
                // TODO: optimize get component index
                var assetComponent = component.assetComponent;

                // Stale overrides can have a null reference on assetComponent
                if (assetComponent == null)
                    continue;

                assetComponent.gameObject.GetComponents(k_Components);
                var index = k_Components.IndexOf(assetComponent);
                if (index < 0)
                {
                    Debug.LogWarning("Could not find removed component " + assetComponent);
                    continue;
                }

                RemovedComponents.Add(new RuntimeRemovedComponent
                {
                    TransformPath = gameObject.transform.GetTransformPath(component.containingInstanceGameObject.transform),
                    ComponentIndex = index
                });
            }

            AddedGameObjects = new List<RuntimeAddedGameObject>();
            AddedComponents = new List<RuntimeAddedComponent>();
            GetMetadataRecursively(gameObject, metadata);
        }

        void GetMetadataRecursively(GameObject gameObject, SerializationMetadata metadata, string parentTransformPath = "", string transformPath = "")
        {
            var transform = gameObject.transform;
            if (PrefabUtility.IsAddedGameObjectOverride(gameObject))
            {
                AddedGameObjects.Add(new RuntimeAddedGameObject
                {
                    TransformPath = parentTransformPath,
                    GameObject = new GameObjectContainer(gameObject, metadata)
                });
            }

            gameObject.GetComponents(k_Components);
            foreach (var component in k_Components)
            {
                if (PrefabUtility.IsAddedComponentOverride(component))
                {
                    AddedComponents.Add(new RuntimeAddedComponent
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

                GetMetadataRecursively(child.gameObject, metadata, transformPath, childTransformPath);
            }
        }
#endif
    }
}
