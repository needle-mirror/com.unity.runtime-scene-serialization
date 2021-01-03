using System;
using System.Collections.Generic;
using Unity.Properties;
using Unity.RuntimeSceneSerialization.Internal.Prefabs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting;
using Unity.RuntimeSceneSerialization.Prefabs;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal
{
    class GameObjectContainer
    {
        class GameObjectContainerPropertyBag : ContainerPropertyBag<GameObjectContainer>
        {
            static readonly DelegateProperty<GameObjectContainer, PrefabMetadataContainer> k_PrefabMetadata = new DelegateProperty<GameObjectContainer, PrefabMetadataContainer>(
                PrefabMetadataProperty,
                (ref GameObjectContainer container) => container.PrefabMetadataContainer,
                (ref GameObjectContainer container, PrefabMetadataContainer value) => container.PrefabMetadataContainer = value
            );

            static readonly DelegateProperty<GameObjectContainer, string> k_Name = new DelegateProperty<GameObjectContainer, string>(
                "name",
                (ref GameObjectContainer container) => container.GameObject.name,
                (ref GameObjectContainer container, string value) => { container.GameObject.name = value; }
            );

            static readonly DelegateProperty<GameObjectContainer, HideFlags> k_HideFlags = new DelegateProperty<GameObjectContainer, HideFlags>(
                "hideFlags",
                (ref GameObjectContainer container) => container.GameObject.hideFlags,
                (ref GameObjectContainer container, HideFlags value) => { container.GameObject.hideFlags = value; }
            );

            static readonly DelegateProperty<GameObjectContainer, int> k_Layer = new DelegateProperty<GameObjectContainer, int>(
                "layer",
                (ref GameObjectContainer container) => container.GameObject.layer,
                (ref GameObjectContainer container, int value) => { container.GameObject.layer = value; }
            );

            static readonly DelegateProperty<GameObjectContainer, string> k_Tag = new DelegateProperty<GameObjectContainer, string>(
                "tag",
                (ref GameObjectContainer container) => container.GameObject.tag,
                (ref GameObjectContainer container, string value) => { container.GameObject.tag = value; }
            );

            static readonly DelegateProperty<GameObjectContainer, bool> k_Active = new DelegateProperty<GameObjectContainer, bool>(
                "active",
                (ref GameObjectContainer container) => container.GameObject.activeSelf,
                (ref GameObjectContainer container, bool value) => { container.GameObject.SetActive(value); }
            );

            static readonly DelegateProperty<GameObjectContainer, List<Component>> k_Components = new DelegateProperty<GameObjectContainer, List<Component>>(
                ComponentsProperty,
                (ref GameObjectContainer container) => container.m_Components,
                (ref GameObjectContainer container, List<Component> value) => { container.m_Components = value; }
            );

            static readonly DelegateProperty<GameObjectContainer, List<GameObjectContainer>> k_Children = new DelegateProperty<GameObjectContainer, List<GameObjectContainer>>(
                ChildrenProperty,
                (ref GameObjectContainer container) => container.m_Children,
                (ref GameObjectContainer container, List<GameObjectContainer> value) => { container.m_Children = value; }
            );

            public GameObjectContainerPropertyBag()
            {
                AddProperty(k_PrefabMetadata);
                AddProperty(k_Name);
                AddProperty(k_HideFlags);
                AddProperty(k_Layer);
                AddProperty(k_Tag);
                AddProperty(k_Active);
                AddProperty(k_Components);
                AddProperty(k_Children);
            }
        }

        public const string PrefabMetadataProperty = "prefabMetadata";
        public const string ComponentsProperty = "components";
        public const string ChildrenProperty = "children";

        List<Component> m_Components;
        List<GameObjectContainer> m_Children;
        GameObject m_GameObject;

        internal GameObject GameObject
        {
            get
            {
                if (m_GameObject == null)
                {
                    m_GameObject = new GameObject();
                    m_GameObject.transform.parent = Parent;
                }

                return m_GameObject;
            }
        }

        internal PrefabMetadataContainer PrefabMetadataContainer { get; private set; }

        public Transform Parent { private get; set; }

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Component> k_TempComponents = new List<Component>();
        static readonly List<Component> k_ComponentsToRemove = new List<Component>();

        static GameObjectContainer()
        {
            PropertyBag.Register(new GameObjectContainerPropertyBag());
            PropertyBag.RegisterList<GameObjectContainer, List<GameObjectContainer>, GameObjectContainer>();
            PropertyBag.RegisterList<GameObjectContainer, List<Component>, Component>();
        }

        [Preserve]
        public GameObjectContainer() { }

        public GameObjectContainer(GameObject gameObject, SerializationMetadata metadata)
        {
            m_GameObject = gameObject;
            m_Components = new List<Component>();

#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                PrefabMetadataContainer = new PrefabMetadataContainer(gameObject, metadata);
                return;
            }
#endif

            var prefabMetadata = gameObject.GetComponent<PrefabMetadata>();
            if (prefabMetadata != null)
            {
                PrefabMetadataContainer = new PrefabMetadataContainer(prefabMetadata, metadata);
                return;
            }

            gameObject.GetComponents(k_TempComponents);
            foreach (var component in k_TempComponents)
            {
                //TODO: Insert null component for missing scripts
                if (component == null)
                {
                    Debug.LogWarningFormat("Found missing script on {0} during serialization", gameObject.name);
                    continue;
                }

                if (component.GetType() != typeof(PrefabMetadata) && (component.hideFlags & HideFlags.DontSave) != 0)
                    continue;

                m_Components.Add(component);
            }

            m_Children = new List<GameObjectContainer>();
            foreach (Transform child in gameObject.transform)
            {
                var childGameObject = child.gameObject;
                if ((childGameObject.hideFlags & HideFlags.DontSave) != 0)
                    continue;

                m_Children.Add(new GameObjectContainer(childGameObject, metadata));
            }
        }

        internal void InstantiatePrefab(string prefabGuid, AssetPack assetPack)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning($"Cannot instantiate prefab with guid {prefabGuid}");
                    return;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    Debug.LogWarning($"Failed to load prefab asset at path {path}");
                    return;
                }

                m_GameObject = (GameObject)PrefabUtility.InstantiatePrefab(asset, Parent);
                return;
            }
#endif

            m_GameObject = assetPack.TryInstantiatePrefab(prefabGuid, Parent);
            if (m_GameObject == null)
            {
                Debug.LogWarning($"Failed to instantiate prefab with guid {prefabGuid}");
                m_GameObject = new GameObject();
                m_GameObject.transform.parent = Parent;
                m_GameObject.name = $"Prefab Placeholder {prefabGuid}";
            }
        }

        internal void FinalizePrefab(PrefabMetadataContainer metadataContainer, SerializationMetadata metadata)
        {
            var removedComponents = metadataContainer.RemovedComponents;
            if (removedComponents != null)
                HandleRemovedComponents(removedComponents);

            if (Application.isPlaying)
                m_GameObject.AddComponent<PrefabMetadata>().Setup(metadataContainer);

            var overrides = metadataContainer.PropertyOverrides;
            if (overrides == null)
                return;

            metadata.EnqueuePostSerializationAction(() => { HandlePropertyOverrides(overrides, metadata); });
        }

        void HandleRemovedComponents(List<RuntimeRemovedComponent> removedComponents)
        {
            // TODO: optimize component removal
            k_ComponentsToRemove.Clear();
            var root = m_GameObject.transform;
            foreach (var component in removedComponents)
            {
                var target = root.GetTransformAtPath(component.TransformPath);
                target.GetComponents(k_TempComponents);
                var index = component.ComponentIndex;
                if (index < 0)
                {
                    Debug.LogError("Invalid index while trying to remove component during deserialization");
                    continue;
                }

                if (index >= k_TempComponents.Count)
                {
                    Debug.LogWarning($"Component {index} not found on {target.name}");
                    continue;
                }

                var targetComponent = k_TempComponents[index];
                k_ComponentsToRemove.Add(targetComponent);
            }

            foreach (var component in k_ComponentsToRemove)
            {
                UnityObject.DestroyImmediate(component);
            }
        }

        void HandlePropertyOverrides(List<RuntimePrefabPropertyOverride> overrides, SerializationMetadata metadata)
        {
            var root = m_GameObject.transform;
            foreach (var propertyOverride in overrides)
            {
                if (propertyOverride == null)
                {
                    Debug.LogError("Encountered null property override");
                    continue;
                }

                propertyOverride.ApplyOverride(root, metadata);
            }

            // Convert UnityObjectReference properties to UnityObjects because SceneId's might change
            PrefabMetadata.PostProcessOverrideList(overrides, metadata);
        }
    }
}
