using System.Collections.Generic;
using Unity.RuntimeSceneSerialization.Internal.Prefabs;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Prefabs
{
    /// <summary>
    /// Container for prefab overrides
    /// </summary>
    public class PrefabMetadata : MonoBehaviour
    {
        [SerializeField]
        string m_Guid;

        [SerializeField]
        List<RuntimeRemovedComponent> m_RemovedComponents;

        [SerializeField]
        List<RuntimeAddedGameObject> m_AddedGameObjects;

        [SerializeField]
        List<RuntimeAddedComponent> m_AddedComponents;

        internal List<RuntimePrefabPropertyOverride> PropertyOverrides { get; private set; }
        internal List<RuntimeAddedGameObject> AddedGameObjects => m_AddedGameObjects;
        internal List<RuntimeAddedComponent> AddedComponents => m_AddedComponents;
        internal List<RuntimeRemovedComponent> RemovedComponents => m_RemovedComponents;

        /// <summary>
        /// The guid of this prefab
        /// </summary>
        public string Guid
        {
            get => m_Guid;
            set => m_Guid = value;
        }

        internal void Setup(string guid, List<RuntimeAddedGameObject> addedGameObjects, List<RuntimeAddedComponent> addedComponents,
            List<RuntimeRemovedComponent> removedComponents, List<RuntimePrefabPropertyOverride> propertyOverrides)
        {
            Guid = guid;
            m_AddedGameObjects = addedGameObjects;
            m_AddedComponents = addedComponents;
            m_RemovedComponents = removedComponents;
            PropertyOverrides = propertyOverrides;
        }

        /// <summary>
        /// Add or update a property override to this prefab
        /// </summary>
        /// <param name="propertyPath">The path of the property relative to its component</param>
        /// <param name="transformPath">The transform path of the GameObject relative to the prefab root</param>
        /// <param name="componentIndex">The index of the component on the target transform</param>
        /// <param name="value">The value to be applied as an override</param>
        /// <typeparam name="TValue">The property value type</typeparam>
        public void SetPropertyOverride<TValue>(string propertyPath, string transformPath, int componentIndex, TValue value)
        {
            PropertyOverrides ??= new List<RuntimePrefabPropertyOverride>();

            var hasOverride = false;
            foreach (var existingOverride in PropertyOverrides)
            {
                if (existingOverride.PropertyPath != propertyPath || existingOverride.TransformPath != transformPath
                    || existingOverride.ComponentIndex != componentIndex)
                    continue;

                hasOverride = true;
                RuntimePrefabPropertyOverride.Update(existingOverride, value);
            }

            if (!hasOverride)
                PropertyOverrides.Add(RuntimePrefabPropertyOverride.Create(propertyPath, transformPath, componentIndex, value));
        }

        /// <summary>
        /// Add a GameObject to this prefab as an override
        /// </summary>
        /// <param name="transformPath">The transform path of the GameObject relative to the prefab root</param>
        /// <param name="addedGameObject">The added GameObject to be applied as an override</param>
        public void SetAddedGameObject(string transformPath, GameObject addedGameObject)
        {
            m_AddedGameObjects ??= new List<RuntimeAddedGameObject>();

            var hasOverride = false;
            foreach (var existingOverride in m_AddedGameObjects)
            {
                if (existingOverride.TransformPath != transformPath)
                    continue;

                hasOverride = true;
                existingOverride.GameObject = addedGameObject;
            }

            if (!hasOverride)
                m_AddedGameObjects.Add(new RuntimeAddedGameObject(transformPath, gameObject));
        }

        /// <summary>
        /// Add or update a property override to this prefab
        /// </summary>
        /// <param name="transformPath">The transform path of the GameObject relative to the prefab root</param>
        /// <param name="addedComponent">The added Component to be applied as an override</param>
        public void SetAddedComponent(string transformPath, Component addedComponent)
        {
            m_AddedComponents ??= new List<RuntimeAddedComponent>();

            var hasOverride = false;
            foreach (var existingOverride in m_AddedComponents)
            {
                if (existingOverride.TransformPath != transformPath)
                    continue;

                hasOverride = true;
                existingOverride.Component = addedComponent;
            }

            if (!hasOverride)
                m_AddedComponents.Add(new RuntimeAddedComponent(transformPath, addedComponent));
        }

        /// <summary>
        /// Remove a component from this prefab as an override
        /// </summary>
        /// <param name="transformPath">The transform path of the GameObject relative to the prefab root</param>
        /// <param name="componentIndex">The index of the component to be removed as an override</param>
        public void SetRemovedComponents(string transformPath, int componentIndex)
        {
            m_RemovedComponents ??= new List<RuntimeRemovedComponent>();

            var hasOverride = false;
            foreach (var existingOverride in m_RemovedComponents)
            {
                if (existingOverride.TransformPath != transformPath)
                    continue;

                hasOverride = true;
                existingOverride.ComponentIndex = componentIndex;
            }

            if (!hasOverride)
                m_RemovedComponents.Add(new RuntimeRemovedComponent(transformPath, componentIndex));
        }

        /// <summary>
        /// Add a component as an override to its parent prefab, if one exists
        /// </summary>
        /// <param name="component">The component to add</param>
        public static void AddComponentOverride(Component component)
        {
            if (component == null)
                return;

            // Apply as overrides to prefab parent, if any exist
            var prefabMetadata = component.GetComponentInParent<PrefabMetadata>();
            if (prefabMetadata == null)
                return;

            var transformPath = prefabMetadata.transform.GetTransformPath(component.transform);
            prefabMetadata.SetAddedComponent(transformPath, component);
        }
    }
}
