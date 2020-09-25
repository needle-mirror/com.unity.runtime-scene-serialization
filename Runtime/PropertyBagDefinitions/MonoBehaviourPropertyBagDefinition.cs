using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class MonoBehaviourPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(MonoBehaviour), new HashSet<string>
            {
                nameof(MonoBehaviour.enabled)
            });

            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(MonoBehaviour), new HashSet<string>
            {
                nameof(MonoBehaviour.useGUILayout),
#if UNITY_EDITOR
                nameof(MonoBehaviour.runInEditMode)
#endif
            });
        }
    }
}
