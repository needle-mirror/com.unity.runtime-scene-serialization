#if INCLUDE_UGUI
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class CanvasPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(Canvas), new HashSet<string>
            {
                nameof(Canvas.worldCamera)
            });
        }
    }
}
#endif
