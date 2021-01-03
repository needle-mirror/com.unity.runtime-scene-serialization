using System;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.EditorInternal
{
    class SerializationMetadataWindow : EditorWindow
    {
        Vector2 m_ScrollPosition;

        [MenuItem("Window/SerializationMetadata")]
        public static void Init() { GetWindow<SerializationMetadataWindow>().Show(); }

        void OnGUI()
        {
            var metadata = SerializationMetadata.CurrentSerializationMetadata;
            using (new EditorGUI.DisabledScope(metadata == null))
            {
                if (GUILayout.Button("Print"))
                    Debug.Log(metadata?.PrintSceneObjectList());

                EditorGUILayout.ObjectField("Asset Pack", metadata?.AssetPack, typeof(AssetPack), false);

                if (metadata != null)
                {
                    using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
                    {
                        m_ScrollPosition = scrollView.scrollPosition;
                        using (new EditorGUI.DisabledScope(true))
                        {
                            foreach (var tuple in metadata.SortedObjectList)
                            {
                                EditorGUILayout.ObjectField(tuple.Item1.ToString(), tuple.Item2, typeof(UnityObject), true);
                            }
                        }
                    }
                }
            }
        }
    }
}
