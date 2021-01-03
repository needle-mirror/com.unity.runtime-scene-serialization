using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    [Serializable]
    class RuntimePrefabPropertyOverrideList : RuntimePrefabPropertyOverride
    {
        // ReSharper disable once Unity.RedundantSerializeFieldAttribute
        [SerializeField]
        protected List<RuntimePrefabPropertyOverride> m_List;

        public List<RuntimePrefabPropertyOverride> List => m_List;

        public RuntimePrefabPropertyOverrideList() { }

        public RuntimePrefabPropertyOverrideList(string propertyPath, string transformPath, int componentIndex)
            : base(propertyPath, transformPath, componentIndex)
        {
            m_List = new List<RuntimePrefabPropertyOverride>();
        }

        protected internal override void ApplyOverrideToTarget(UnityObject target, SerializationMetadata metadata)
        {
            foreach (var propertyOverride in m_List)
            {
                if (propertyOverride == null)
                {
                    Debug.LogError("Encountered null property override");
                    continue;
                }

                propertyOverride.ApplyOverrideToTarget(target, metadata);
            }
        }
    }
}
