using System;
using System.Collections.Generic;
using Unity.RuntimeSceneSerialization.Internal;
using Unity.RuntimeSceneSerialization.Internal.Prefabs;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Prefabs
{
    /// <summary>
    /// Container for prefab overrides
    /// </summary>
    public class PrefabMetadata : MonoBehaviour
    {
        [SerializeField]
        string m_Guid;

        // ReSharper disable once Unity.RedundantSerializeFieldAttribute
        [SerializeField]
        List<RuntimePrefabPropertyOverride> m_PropertyOverrides;

        [SerializeField]
        List<RuntimeAddedGameObject> m_AddedGameObjects;

        [SerializeField]
        List<RuntimeAddedComponent> m_AddedComponents;

        [SerializeField]
        List<RuntimeRemovedComponent> m_RemovedComponents;

        internal List<RuntimePrefabPropertyOverride> PropertyOverrides => m_PropertyOverrides;
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

        internal void Setup(PrefabMetadataContainer metadataContainer)
        {
            Guid = metadataContainer.Guid;
            m_AddedGameObjects = metadataContainer.AddedGameObjects;
            m_AddedComponents = metadataContainer.AddedComponents;
            m_RemovedComponents = metadataContainer.RemovedComponents;
            m_PropertyOverrides = metadataContainer.PropertyOverrides;
        }

        internal static void PostProcessOverrideList(List<RuntimePrefabPropertyOverride> list, SerializationMetadata metadata)
        {
            var count = list.Count;
            for (var i = 0; i < count; i++)
            {
                var propertyOverride = list[i];
                if (propertyOverride is IRuntimePrefabOverrideUnityObjectReference objectProperty)
                {
                    objectProperty.ConvertToUnityObjectOverride(list, i, metadata);
                }
                else if (propertyOverride is RuntimePrefabPropertyOverrideList listProperty)
                {
                    PostProcessOverrideList(listProperty.List, metadata);
                }
            }
        }

        /// <summary>
        /// Add or update a property override to this prefab
        /// </summary>
        /// <param name="propertyPath">The path of the property relative to its component</param>
        /// <param name="transformPath">The transform path of the GameObject relative to the prefab root</param>
        /// <param name="componentIndex">The index of the component on the target transform</param>
        /// <param name="value">The value to be applied as an override</param>
        /// <param name="metadata">SerializationMetadata to use for scene object or asset references</param>
        /// <typeparam name="TValue">The property value type</typeparam>
        public void SetPropertyOverride<TValue>(string propertyPath, string transformPath, int componentIndex,
            TValue value, SerializationMetadata metadata = null)
        {
            if (m_PropertyOverrides == null)
                m_PropertyOverrides = new List<RuntimePrefabPropertyOverride>();

            var hasOverride = false;
            foreach (var existingOverride in m_PropertyOverrides)
            {
                if (existingOverride.PropertyPath != propertyPath || existingOverride.TransformPath != transformPath
                    || existingOverride.ComponentIndex != componentIndex)
                    continue;

                hasOverride = true;
                RuntimePrefabPropertyOverride.Update(existingOverride, value, metadata);
            }

            if (!hasOverride)
                m_PropertyOverrides.Add(RuntimePrefabPropertyOverride.Create(propertyPath, transformPath, componentIndex, value, metadata));
        }

        /// <summary>
        /// Add a GameObject to this prefab as an override
        /// </summary>
        /// <param name="transformPath">The transform path of the GameObject relative to the prefab root</param>
        /// <param name="addedGameObject">The added GameObject to be applied as an override</param>
        /// <param name="metadata">SerializationMetadata to be used when serializing the scene</param>
        public void SetAddedGameObject(string transformPath, GameObject addedGameObject, SerializationMetadata metadata)
        {
            if (m_AddedGameObjects == null)
                m_AddedGameObjects = new List<RuntimeAddedGameObject>();

            var hasOverride = false;
            foreach (var existingOverride in m_AddedGameObjects)
            {
                if (existingOverride.TransformPath != transformPath)
                    continue;

                hasOverride = true;
                existingOverride.GameObject = new GameObjectContainer(addedGameObject, metadata);
            }

            if (!hasOverride)
                m_AddedGameObjects.Add(new RuntimeAddedGameObject(transformPath, gameObject, metadata));
        }

        /// <summary>
        /// Add or update a property override to this prefab
        /// </summary>
        /// <param name="transformPath">The transform path of the GameObject relative to the prefab root</param>
        /// <param name="addedComponent">The added Component to be applied as an override</param>
        public void SetAddedComponent(string transformPath, Component addedComponent)
        {
            if (m_AddedComponents == null)
                m_AddedComponents = new List<RuntimeAddedComponent>();

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
            if (m_RemovedComponents == null)
                m_RemovedComponents = new List<RuntimeRemovedComponent>();

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
