#if INCLUDE_TEXT_MESH_PRO
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class TextMeshProPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(TMP_TextInfo), new HashSet<string>
            {
                nameof(TMP_TextInfo.wordInfo),
                nameof(TMP_TextInfo.characterInfo),
                nameof(TMP_TextInfo.linkInfo),
                nameof(TMP_TextInfo.lineInfo),
                nameof(TMP_TextInfo.pageInfo),
                nameof(TMP_TextInfo.meshInfo),
                "m_CachedMeshInfo"
            });

            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(TMP_CharacterInfo), new HashSet<string>
            {
                nameof(TMP_CharacterInfo.textElement)
            });
        }
    }
}
#endif
