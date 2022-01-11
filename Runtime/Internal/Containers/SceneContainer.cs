using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal
{
    [Serializable]
    class SceneContainer : IFormatVersion
    {
        const int k_FormatVersion = 2;

        // ReSharper disable Unity.RedundantSerializeFieldAttribute
        [SerializeField]
        int m_FormatVersion = k_FormatVersion;

        [SerializeField]
        List<GameObjectContainer> m_RootGameObjects;

        [SerializeField]
        SerializedRenderSettings m_RenderSettings;
        // ReSharper restore Unity.RedundantSerializeFieldAttribute

        public Transform SceneRootTransform { get; set; }

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<GameObject> k_Roots = new List<GameObject>();
        static readonly List<GameObject> k_SavedRoots = new List<GameObject>();

        // Needed for activation during deserialization
        // ReSharper disable once UnusedMember.Global
        public SceneContainer() { }

        /// <summary>
        /// Check the format version of this scene container
        /// </summary>
        /// <exception cref="FormatException">Thrown if the parsed scene's version doesn't match the hard-coded version</exception>
        public void CheckFormatVersion()
        {
            if (m_FormatVersion != k_FormatVersion)
                throw new FormatException($"Serialization format mismatch. Expected {k_FormatVersion} but was {m_FormatVersion}.");
        }

        public SceneContainer(Scene scene, SerializationMetadata metadata,
            SerializedRenderSettings renderSettings = default, bool collectGameObjects = true)
        {
            m_RootGameObjects = new List<GameObjectContainer>();
            m_RenderSettings = renderSettings;

            if (collectGameObjects)
            {
                k_SavedRoots.Clear();
                scene.GetRootGameObjects(k_Roots);
                foreach (var gameObject in k_Roots)
                {
                    if ((gameObject.hideFlags & HideFlags.DontSave) != 0)
                        continue;

                    k_SavedRoots.Add(gameObject);
                }

                metadata.SetupSceneObjectMetadata(k_SavedRoots);
                foreach (var gameObject in k_SavedRoots)
                {
                    m_RootGameObjects.Add(new GameObjectContainer(gameObject, metadata));
                }

                k_SavedRoots.Clear();
            }
        }

        /// <summary>
        /// Apply this scene container's render settings to the current render settings
        /// </summary>
        public void ApplyRenderSettings() { m_RenderSettings.ApplyValuesToRenderSettings(); }
    }
}
