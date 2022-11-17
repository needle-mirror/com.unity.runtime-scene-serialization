#if INCLUDE_AUDIO
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class AudioListenerPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(AudioListener), new HashSet<string>
            {
                nameof(AudioListener.velocityUpdateMode)
            });
        }
    }
}
#endif
