using System;
using Unity.RuntimeSceneSerialization;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

public class EditorMetadataWindow : EditorWindow
{
    Vector2 m_ScrollPosition;

    [MenuItem("Window/EditorMetadata")]
    public static void Init()
    {
        GetWindow<EditorMetadataWindow>().Show();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Print"))
            Debug.Log(EditorMetadata.PrintStatus());

        using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
        {
            m_ScrollPosition = scrollView.scrollPosition;
            using (new EditorGUI.DisabledScope(true))
            {
                foreach (var tuple in EditorMetadata.SortedObjectList)
                {
                    EditorGUILayout.ObjectField(tuple.Item1.ToString(), tuple.Item2, typeof(UnityObject), true);
                }
            }
        }
    }
}
