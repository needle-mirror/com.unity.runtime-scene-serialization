using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class MeshFilterPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(MeshFilter), new HashSet<string>
            {
                nameof(MeshFilter.mesh)
            });

            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(MeshFilter), new HashSet<string>
            {
                nameof(MeshFilter.sharedMesh)
            });
        }

        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(MeshFilter filter)
        {
            filter.sharedMesh = filter.sharedMesh;
        }
    }
}
