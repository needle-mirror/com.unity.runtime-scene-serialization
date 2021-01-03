using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.EditorInternal
{
    [CustomEditor(typeof(AssetPack))]
    class AssetPackEditor : Editor
    {
        const int k_Indent = 15;
        bool m_AssetsExpanded;
        bool m_PrefabsExpanded;
        readonly Dictionary<string, bool> m_Foldouts = new Dictionary<string, bool>();
        SerializedProperty m_SceneAssetProperty;

        void OnEnable() { m_SceneAssetProperty = serializedObject.FindProperty("m_SceneAsset"); }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(m_SceneAssetProperty);
            using (new EditorGUI.DisabledScope(true))
            {
                var assetPack = (AssetPack)target;
                var assets = assetPack.Assets;
                var label = $"Assets ({assets.Count})";
                m_AssetsExpanded = EditorGUILayout.Foldout(m_AssetsExpanded, label, true);
                if (m_AssetsExpanded)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(k_Indent);
                        using (new EditorGUILayout.VerticalScope())
                        {
                            foreach (var kvp in assets)
                            {
                                DrawAsset(kvp.Key, kvp.Value);
                            }
                        }
                    }
                }

                var prefabs = assetPack.Prefabs;
                label = $"Prefabs ({prefabs.Count})";
                m_PrefabsExpanded = EditorGUILayout.Foldout(m_PrefabsExpanded, label, true);
                if (m_PrefabsExpanded)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var kvp in prefabs)
                        {
                            var prefab = kvp.Value;
                            if (prefab)
                                EditorGUILayout.ObjectField(kvp.Key, prefab, prefab.GetType(), false);
                            else
                                EditorGUILayout.ObjectField(kvp.Key, null, typeof(GameObject), false);
                        }
                    }
                }
            }
        }

        void DrawAsset(string guid, AssetPack.Asset asset)
        {
            bool foldout;
            m_Foldouts.TryGetValue(guid, out foldout);

            var assets = asset.Assets;
            var label = $"{guid} ({assets.Count})";
            foldout = EditorGUILayout.Foldout(foldout, label, true);
            if (foldout)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(k_Indent);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        foreach (var kvp in assets)
                        {
                            var obj = kvp.Value;
                            if (obj)
                                EditorGUILayout.ObjectField(kvp.Key.ToString(), obj, obj.GetType(), false);
                            else
                                EditorGUILayout.ObjectField(kvp.Key.ToString(), null, typeof(UnityObject), false);
                        }
                    }
                }
            }

            m_Foldouts[guid] = foldout;
        }
    }
}
