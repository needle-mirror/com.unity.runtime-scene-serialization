using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization
{
    [Serializable]
    public class SceneContainer : IFormatVersion
    {
        const int k_FormatVersion = 2;

        [SerializeField]
        int m_FormatVersion = k_FormatVersion;

        [SerializeField]
        List<GameObjectContainer> m_RootGameObjects;

        public Transform SceneRootTransform { get; set; }

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<GameObject> k_Roots = new List<GameObject>();
        static readonly List<GameObject> k_SavedRoots = new List<GameObject>();

        // Needed for activation during deserialization
        // ReSharper disable once UnusedMember.Global
        public SceneContainer() { }

        public void CheckFormatVersion()
        {
            if (m_FormatVersion != k_FormatVersion)
                throw new FormatException($"Serialization format mismatch. Expected {k_FormatVersion} but was {m_FormatVersion}.");
        }

        public SceneContainer(Scene scene, bool collectGameObjects = true)
        {
            EditorMetadata.Reset();

            m_RootGameObjects = new List<GameObjectContainer>();

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

                EditorMetadata.SetupEditorMetadata(k_SavedRoots);
                foreach (var gameObject in k_SavedRoots)
                {
                    m_RootGameObjects.Add(new GameObjectContainer(gameObject));
                }

                k_SavedRoots.Clear();
            }
        }

        public void SetSceneReferences()
        {
            var sceneReferenceActions = EditorMetadata.SceneReferenceActions;
            EditorMetadata.SettingSceneReferences = true;
            while (sceneReferenceActions.Count > 0)
            {
                try
                {
                    sceneReferenceActions.Dequeue()();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            EditorMetadata.SettingSceneReferences = false;
        }
    }
}
