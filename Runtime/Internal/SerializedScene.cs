using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.RuntimeSceneSerialization.Internal
{
    [Serializable]
    class SerializedScene : IFormatVersion
    {
        const int k_FormatVersion = 2;

        [SerializeField]
        int m_FormatVersion = k_FormatVersion;

        [SerializeField]
        List<GameObject> m_RootGameObjects;

        [SerializeField]
        SerializedRenderSettings m_RenderSettings;

        public List<GameObject> RootGameObjects
        {
            get => m_RootGameObjects;
            set => m_RootGameObjects = value;
        }

        public SerializedRenderSettings RenderSettings
        {
            get => m_RenderSettings;
            set => m_RenderSettings = value;
        }

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<GameObject> k_Roots = new();

        public SerializedScene() { }

        public SerializedScene(List<GameObject> roots, SerializedRenderSettings renderSettings)
        {
            m_RootGameObjects = roots;
            m_RenderSettings = renderSettings;
        }

        public static void GetSavedRoots(Scene scene, List<GameObject> savedRoots)
        {
            // GetRootGameObjects clears the collection
            scene.GetRootGameObjects(k_Roots);
            foreach (var gameObject in k_Roots)
            {
                if ((gameObject.hideFlags & HideFlags.DontSave) != 0)
                    continue;

                savedRoots.Add(gameObject);
            }
        }

        void IFormatVersion.CheckFormatVersion()
        {
            if (m_FormatVersion != k_FormatVersion)
                throw new FormatException($"Scene formats do not match. Expected {k_FormatVersion} but got {m_FormatVersion}");
        }
    }
}
