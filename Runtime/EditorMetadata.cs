using System;
using System.Collections.Generic;
using Unity.RuntimeSceneSerialization.Prefabs;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Runtime API for object metadata
    /// </summary>
    public static class EditorMetadata
    {
        public const int InvalidID = -1;
        public static bool SettingSceneReferences;
        public static readonly Queue<Action> SceneReferenceActions = new Queue<Action>();

        static int s_SceneObjectCount;
        static List<Tuple<int, UnityObject>> s_SortedObjectList;

        static readonly Dictionary<int, UnityObject> k_SceneObjects = new Dictionary<int, UnityObject>();
        static readonly Dictionary<UnityObject, int> k_SceneObjectLookupMap = new Dictionary<UnityObject, int>();

        static readonly List<Component> k_Components = new List<Component>();
        static readonly List<KeyValuePair<Component, bool>> k_SortedComponents = new List<KeyValuePair<Component, bool>>();

        static bool s_IsSetup;

        public static bool IsSetup => s_IsSetup;

        internal static List<Tuple<int, UnityObject>> SortedObjectList
        {
            get
            {
                if (s_SortedObjectList == null || s_SortedObjectList.Count != k_SceneObjects.Count)
                {
                    s_SortedObjectList = new List<Tuple<int, UnityObject>>(k_SceneObjects.Count);
                    foreach (var kvp in k_SceneObjects)
                    {
                        s_SortedObjectList.Add(new Tuple<int, UnityObject>(kvp.Key, kvp.Value));
                    }

                    s_SortedObjectList.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                }

                return s_SortedObjectList;
            }
        }

        public static void Reset()
        {
            s_SortedObjectList = null;
            s_IsSetup = false;
            k_SceneObjects.Clear();
            k_SceneObjectLookupMap.Clear();
            s_SceneObjectCount = 0;
        }

        /// <summary>
        /// Set up EditorMetadata for the first time on a list of scene roots
        /// This is the only thing that can cause IsSetup to be true. Some warnings are suppressed while IsSetup is false
        /// </summary>
        /// <param name="roots">List of scene roots for which to add metadata</param>
        public static void SetupEditorMetadata(List<GameObject> roots)
        {
            s_IsSetup = true;
            foreach (var gameObject in roots)
            {
                AddMetadataRecursively(gameObject);
            }
        }

        public static void AddMetadataRecursively(GameObject gameObject)
        {
            const HideFlags dontSave = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            if ((gameObject.hideFlags & dontSave) != 0)
                return;

            AddMetadata(gameObject);
            foreach (Transform child in gameObject.transform)
            {
                AddMetadataRecursively(child.gameObject);
            }
        }

        static void AddMetadata(GameObject gameObject)
        {
            if (gameObject == null)
            {
                Debug.LogError("Tried to add metadata for null object");
                return;
            }

            if (k_SceneObjectLookupMap.ContainsKey(gameObject))
            {
                Debug.LogError($"Tried to add metadata {gameObject} twice");
                return;
            }

            gameObject.GetComponents(k_Components);
            SerializationUtils.SortComponentList(k_Components, k_SortedComponents);

            var gameObjectSceneID = s_SceneObjectCount++;
            k_SceneObjects[gameObjectSceneID] = gameObject;
            k_SceneObjectLookupMap[gameObject] = gameObjectSceneID;

            var componentCount = k_SortedComponents.Count;
            var componentIDs = new int[componentCount];
            for (var i = 0; i < componentCount; i++)
            {
                var component = k_SortedComponents[i].Key;
                if (component is PrefabMetadata)
                    continue;

                var componentSceneID = s_SceneObjectCount++;
                componentIDs[i] = componentSceneID;
                k_SceneObjects[componentSceneID] = component;
                k_SceneObjectLookupMap[component] = componentSceneID;
            }
        }

        public static int GetSceneID(UnityObject obj)
        {
            return k_SceneObjectLookupMap.TryGetValue(obj, out var id) ? id : InvalidID;
        }

        public static UnityObject GetSceneObject(int sceneID)
        {
            return k_SceneObjects.TryGetValue(sceneID, out var sceneObject) ? sceneObject : null;
        }

        public static string PrintStatus()
        {
            var output = string.Empty;
            foreach (var tuple in SortedObjectList)
            {
                output = $"{output}\n{tuple.Item1} - {tuple.Item2}";
            }

            return output;
        }
    }
}
