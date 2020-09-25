using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Prefabs
{
    public class PrefabMetadata : MonoBehaviour
    {
        public string Guid;
        public List<RuntimePrefabPropertyOverride> PropertyOverrides;
        public List<RuntimeAddedGameObject> AddedGameObjects;
        public List<RuntimeAddedComponent> AddedComponents;
        public List<RuntimeRemovedComponent> RemovedComponents;

        internal void Setup(PrefabMetadataContainer metadataContainer)
        {
            Guid = metadataContainer.Guid;
            AddedGameObjects = metadataContainer.AddedGameObjects;
            AddedComponents = metadataContainer.AddedComponents;
            RemovedComponents = metadataContainer.RemovedComponents;
            PropertyOverrides = metadataContainer.PropertyOverrides;
        }

        internal static void PostProcessOverrideList(List<RuntimePrefabPropertyOverride> list)
        {
            var count = list.Count;
            for (var i = 0; i < count; i++)
            {
                var propertyOverride = list[i];
                if (propertyOverride is IRuntimePrefabOverrideUnityObjectReference objectProperty)
                {
                    objectProperty.ConvertToUnityObjectOverride(list, i);
                }
                else if (propertyOverride is RuntimePrefabPropertyOverrideList listProperty)
                {
                    PostProcessOverrideList(listProperty.List);
                }
            }
        }

        public void SetOverride<TValue>(string propertyPath, string transformPath, int componentIndex, TValue value)
        {
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
    }
}
