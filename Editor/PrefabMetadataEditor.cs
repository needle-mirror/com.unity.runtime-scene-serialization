using System.Collections.Generic;
using Unity.RuntimeSceneSerialization.Internal.Prefabs;
using Unity.RuntimeSceneSerialization.Prefabs;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.EditorInternal
{
    [CustomEditor(typeof(PrefabMetadata))]
    class PrefabMetadataEditor : Editor
    {
        bool m_Expanded;
        readonly Dictionary<RuntimePrefabPropertyOverride, bool> m_ExpandedStates = new Dictionary<RuntimePrefabPropertyOverride, bool>();

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var meta = (PrefabMetadata)target;
            var propertyOverrides = meta.PropertyOverrides;
            m_Expanded = EditorGUILayout.Foldout(m_Expanded, "Property Overrides", true);
            if (m_Expanded && propertyOverrides != null)
            {
                using (new EditorGUI.DisabledScope(true))
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var propertyOverride in propertyOverrides)
                    {
                        DrawPropertyOverride(propertyOverride);
                    }
                }
            }
        }

        void DrawPropertyOverride(RuntimePrefabPropertyOverride propertyOverride)
        {
            m_ExpandedStates.TryGetValue(propertyOverride, out var expandedState);
            var wasExpanded = expandedState;
            expandedState = EditorGUILayout.Foldout(expandedState, propertyOverride.PropertyPath, true);
            if (expandedState != wasExpanded)
                m_ExpandedStates[propertyOverride] = expandedState;

            if (expandedState)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.TextField("Property Path", propertyOverride.PropertyPath);
                    EditorGUILayout.TextField("Transform Path", propertyOverride.TransformPath);
                    EditorGUILayout.IntField("Component Index", propertyOverride.ComponentIndex);

                    switch (propertyOverride)
                    {
                        case IRuntimePrefabOverrideUnityObject unityObjectProperty:
                            EditorGUILayout.ObjectField("Value", unityObjectProperty.Value, typeof(UnityObject), true);
                            break;
                        case IRuntimePrefabOverrideUnityObjectReference objectReferenceProperty:
                            var objectReference = objectReferenceProperty.Value;
                            EditorGUILayout.IntField("Scene ID", objectReference.sceneID);
                            EditorGUILayout.TextField("Guid", objectReference.guid);
                            EditorGUILayout.LongField("File ID", objectReference.fileId);
                            break;
                        case IRuntimePrefabOverride<AnimationCurve> animationCurveProperty:
                            EditorGUILayout.CurveField("Value", animationCurveProperty.Value);
                            break;
                        case RuntimePrefabPropertyOverrideList listProperty:
                            foreach (var element in listProperty.List)
                            {
                                DrawPropertyOverride(element);
                            }

                            break;
                        case IRuntimePrefabOverride<bool> boolProperty:
                            EditorGUILayout.Toggle("Value", boolProperty.Value);
                            break;
                        case IRuntimePrefabOverride<char> charProperty:
                            EditorGUILayout.TextField("Value", charProperty.Value.ToString());
                            break;
                        case IRuntimePrefabOverride<float> floatProperty:
                            EditorGUILayout.FloatField("Value", floatProperty.Value);
                            break;
                        case IRuntimePrefabOverride<int> intProperty:
                            EditorGUILayout.IntField("Value", intProperty.Value);
                            break;
                        case IRuntimePrefabOverride<long> longProperty:
                            EditorGUILayout.LongField("Value", longProperty.Value);
                            break;
                        case IRuntimePrefabOverride<string> stringProperty:
                            EditorGUILayout.TextField("Value", stringProperty.Value);
                            break;
                    }
                }
            }
        }
    }
}
