using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class TransformPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(Transform), new HashSet<string>
            {
                nameof(Transform.localPosition),
                nameof(Transform.localRotation),
                nameof(Transform.localScale)
            });

            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(Transform), new HashSet<string>
            {
                nameof(Transform.hasChanged)
            });

            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(RectTransform), new HashSet<string>
            {
                nameof(RectTransform.pivot),
                nameof(RectTransform.anchorMin),
                nameof(RectTransform.anchorMax),
                nameof(RectTransform.sizeDelta),
                nameof(RectTransform.anchoredPosition)
            });

            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(RectTransform), new HashSet<string>
            {
                "drivenByObject"
            });
        }

        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(Transform t)
        {
            t.localPosition = t.localPosition;
            t.localRotation = t.localRotation;
            t.localScale = t.localScale;
        }

        public static void Unused(RectTransform rect)
        {
            rect.localRotation = rect.localRotation;
            rect.localPosition = rect.localPosition;
            rect.localScale = rect.localScale;
            rect.anchorMin = rect.anchorMin;
            rect.anchorMax = rect.anchorMax;
            rect.anchoredPosition = rect.anchoredPosition;
            rect.sizeDelta = rect.sizeDelta;
            rect.pivot = rect.pivot;
        }
    }
}
