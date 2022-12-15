using System.Collections.Generic;
using Unity.RuntimeSceneSerialization.Internal.Prefabs;
using Unity.RuntimeSceneSerialization.Prefabs;
using Unity.Serialization.Json;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    partial class JsonAdapter
    {
        struct PrefabMetadataHelper
        {
            public List<RuntimeRemovedComponent> RemovedComponents;
            public List<RuntimeAddedGameObject> AddedGameObjects;
            public List<RuntimeAddedComponent> AddedComponents;
        }

        readonly Dictionary<GameObject, PrefabMetadataHelper> m_PrefabMetadataHelpers = new();

        GameObject DeserializePrefab(SerializedObjectView prefabMetadata, AssetPack assetPack, Transform parent, GameObject gameObject)
        {
            var guid = prefabMetadata[k_PrefabMetadataGuidPropertyName].AsStringView().ToString();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning($"Cannot instantiate prefab with guid {guid}");
                    return null;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    Debug.LogWarning($"Failed to load prefab asset at path {path}");
                    return null;
                }

                if (gameObject == null)
                    gameObject = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);

                HandlePrefabMetadata(prefabMetadata, gameObject, guid);
                return gameObject;
            }
#endif

            if (gameObject == null)
                gameObject = assetPack.TryInstantiatePrefab(guid, parent);

            if (gameObject == null)
            {
                Debug.LogWarning($"Failed to instantiate prefab with guid {guid}");
                gameObject = new GameObject();
                gameObject.transform.parent = parent;
                gameObject.name = $"Prefab Placeholder {guid}";
            }

            HandlePrefabMetadata(prefabMetadata, gameObject, guid);
            return gameObject;
        }

        void HandlePrefabMetadata(SerializedObjectView prefabMetadata, GameObject gameObject, string guid)
        {
            var prefabRoot = gameObject.transform;

            if (m_FirstPassCompleted)
            {
                var helper = m_PrefabMetadataHelpers[gameObject];
                var addedGameObjects = helper.AddedGameObjects;
                var addedComponents = helper.AddedComponents;
                HandleExistingAddedComponents(prefabMetadata, addedComponents);
                HandleExistingAddedGameObjects(prefabMetadata, addedGameObjects);
                HandlePropertyOverrides(prefabMetadata, gameObject, out var overrides);

                if (Application.isPlaying)
                    gameObject.AddComponent<PrefabMetadata>().Setup(guid, addedGameObjects, addedComponents, helper.RemovedComponents, overrides);
            }
            else
            {
                HandleRemovedComponents(prefabRoot, prefabMetadata, out var removedComponents);
                HandleAddedComponents(prefabRoot, prefabMetadata, out var addedComponents);
                HandleAddedGameObjects(prefabRoot, prefabMetadata, out var addedGameObjects);

                m_PrefabMetadataHelpers[gameObject] = new PrefabMetadataHelper
                {
                    RemovedComponents = removedComponents,
                    AddedComponents = addedComponents,
                    AddedGameObjects = addedGameObjects
                };
            }
        }

        void HandleAddedComponents(Transform prefabRoot, SerializedObjectView prefabMetadata,
            out List<RuntimeAddedComponent> addedComponents)
        {
            if (!prefabMetadata.TryGetValue(nameof(PrefabMetadata.AddedComponents), out var addedComponentsView))
            {
                addedComponents = null;
                return;
            }

            if (addedComponentsView.Type != TokenType.Array)
            {
                addedComponents = null;
                return;
            }

            addedComponents = new List<RuntimeAddedComponent>();
            foreach (var element in addedComponentsView.AsArrayView())
            {
                var transformPath = element[RuntimeAddedComponent.TransformPathFieldName].AsStringView().ToString();
                var target = prefabRoot.GetTransformAtPath(transformPath).gameObject;
                addedComponents.Add(new RuntimeAddedComponent
                {
                    TransformPath = transformPath,
                    Component = DeserializeComponent(element[RuntimeAddedComponent.ComponentFieldName], target)
                });
            }
        }

        void HandleAddedGameObjects(Transform prefabRoot, SerializedObjectView prefabMetadata,
            out List<RuntimeAddedGameObject> addedGameObjects)
        {
            if (!prefabMetadata.TryGetValue(nameof(PrefabMetadata.AddedGameObjects), out var addedGameObjectsView))
            {
                addedGameObjects = null;
                return;
            }

            if (addedGameObjectsView.Type != TokenType.Array)
            {
                addedGameObjects = null;
                return;
            }

            addedGameObjects = new List<RuntimeAddedGameObject>();
            foreach (var element in addedGameObjectsView.AsArrayView())
            {
                var transformPath = element[nameof(RuntimeAddedGameObject.TransformPath)].AsStringView().ToString();
                var parent = prefabRoot.GetTransformAtPath(transformPath);
                addedGameObjects.Add(new RuntimeAddedGameObject
                {
                    TransformPath = transformPath,
                    GameObject = Deserialize(element[nameof(RuntimeAddedGameObject.GameObject)].AsObjectView(), parent)
                });
            }
        }

        static void HandleRemovedComponents(Transform prefabRoot,
            SerializedObjectView prefabMetadata, out List<RuntimeRemovedComponent> removedComponents)
        {
            if (!prefabMetadata.TryGetValue(nameof(PrefabMetadata.RemovedComponents), out var removedComponentsView))
            {
                removedComponents = null;
                return;
            }

            if (removedComponentsView.Type != TokenType.Array)
            {
                removedComponents = null;
                return;
            }

            removedComponents = JsonSerialization.FromJson<List<RuntimeRemovedComponent>>(removedComponentsView);
            if (removedComponents == null)
                return;

            // TODO: optimize component removal
            k_ComponentsToRemove.Clear();
            foreach (var component in removedComponents)
            {
                var target = prefabRoot.GetTransformAtPath(component.TransformPath);
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

        void HandlePropertyOverrides(SerializedObjectView prefabMetadata,
            GameObject gameObject, out List<RuntimePrefabPropertyOverride> overrides)
        {
            if (!prefabMetadata.TryGetValue(nameof(PrefabMetadata.PropertyOverrides), out var propertyOverridesView))
            {
                overrides = null;
                return;
            }

            if (propertyOverridesView.Type != TokenType.Array)
            {
                overrides = null;
                return;
            }

            overrides = JsonSerialization.FromJson<List<RuntimePrefabPropertyOverride>>(propertyOverridesView, m_Parameters);
            using (new SerializeAsReferenceScope(this))
            {
                PostProcessOverrides(gameObject, overrides);
            }
        }

        static void PostProcessOverrides(GameObject gameObject, List<RuntimePrefabPropertyOverride> overrides)
        {
            if (overrides == null)
                return;

            var root = gameObject.transform;
            foreach (var propertyOverride in overrides)
            {
                if (propertyOverride == null)
                {
                    Debug.LogError("Encountered null property override");
                    continue;
                }

                propertyOverride.ApplyOverride(root);
            }
        }

        void HandleExistingAddedGameObjects(SerializedObjectView prefabMetadata, List<RuntimeAddedGameObject> addedGameObjects)
        {
            if (addedGameObjects == null)
                return;

            if (!prefabMetadata.TryGetValue(nameof(PrefabMetadata.AddedGameObjects), out var addedGameObjectsView))
                return;

            if (addedGameObjectsView.Type != TokenType.Array)
                return;

            var count = 0;
            foreach (var element in addedGameObjectsView.AsArrayView())
            {
                var addedGameObject = addedGameObjects[count++];
                var gameObject = addedGameObject.GameObject;
                Deserialize(element[nameof(RuntimeAddedGameObject.GameObject)].AsObjectView(), null, gameObject);
            }
        }

        void HandleExistingAddedComponents(SerializedObjectView prefabMetadata, List<RuntimeAddedComponent> addedComponents)
        {
            if (addedComponents == null)
                return;

            if (!prefabMetadata.TryGetValue(nameof(PrefabMetadata.AddedComponents), out var addedComponentsView))
                return;

            if (addedComponentsView.Type != TokenType.Array)
                return;

            var count = 0;
            foreach (var element in addedComponentsView.AsArrayView())
            {
                var addedComponent = addedComponents[count++];
                var component = addedComponent.Component;
                DeserializeComponent(element[RuntimeAddedComponent.ComponentFieldName], null, component);
            }
        }
    }
}
