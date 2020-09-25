using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class LayerMaskPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(LayerMask), new HashSet<string>
            {
                nameof(LayerMask.value)
            });
        }

        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(LayerMask layerMask)
        {
            layerMask.value = layerMask.value;
        }
    }
}
