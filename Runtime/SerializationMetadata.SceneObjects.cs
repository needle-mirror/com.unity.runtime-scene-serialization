using System;
using System.Collections.Generic;
using Unity.RuntimeSceneSerialization.Internal;
using Unity.RuntimeSceneSerialization.Prefabs;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// API and state variables for assigning predictable IDs for scene objects
    /// </summary>
    public partial class SerializationMetadata
    {
        /// <summary>
        /// Fixed ID for an invalid object
        /// </summary>
        public const int InvalidID = -1;

        const HideFlags k_DontSaveFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

        bool m_SceneObjectsSetup;
        int m_SceneObjectCount;
        List<Tuple<int, UnityObject>> m_SortedObjectList;

        readonly Dictionary<int, UnityObject> k_SceneObjects = new Dictionary<int, UnityObject>();
        readonly Dictionary<UnityObject, int> k_SceneObjectLookupMap = new Dictionary<UnityObject, int>();

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Component> k_Components = new List<Component>();
        static readonly List<(Component, bool)> k_SortedComponents = new List<(Component, bool)>();

        internal bool SceneObjectsSetup => m_SceneObjectsSetup;

        internal List<Tuple<int, UnityObject>> SortedObjectList
        {
            get
            {
                if (m_SortedObjectList == null || m_SortedObjectList.Count != k_SceneObjects.Count)
                {
                    m_SortedObjectList = new List<Tuple<int, UnityObject>>(k_SceneObjects.Count);
                    foreach (var kvp in k_SceneObjects)
                    {
                        m_SortedObjectList.Add(new Tuple<int, UnityObject>(kvp.Key, kvp.Value));
                    }

                    m_SortedObjectList.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                }

                return m_SortedObjectList;
            }
        }

        /// <summary>
        /// Set up metadata for the first time on a list of scene roots
        /// This is the only thing that can cause IsSetup to be true. Some warnings are suppressed while IsSetup is false
        /// </summary>
        /// <param name="roots">List of scene roots for which to add metadata</param>
        public void SetupSceneObjectMetadata(List<GameObject> roots)
        {
            m_SceneObjectsSetup = true;
            foreach (var gameObject in roots)
            {
                AddToMetadataRecursively(gameObject);
            }
        }

        /// <summary>
        /// Track metadata for this object, its components, and its children and their components
        /// </summary>
        /// <param name="gameObject">The GameObject to be tracked</param>
        public void AddToMetadataRecursively(GameObject gameObject)
        {
            if ((gameObject.hideFlags & k_DontSaveFlags) != 0)
                return;

            AddMetadata(gameObject);
            foreach (Transform child in gameObject.transform)
            {
                AddToMetadataRecursively(child.gameObject);
            }
        }

        void AddMetadata(GameObject gameObject)
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

            // k_Components is cleared by GetComponents
            gameObject.GetComponents(k_Components);
            SerializationUtils.SortComponentList(k_Components, k_SortedComponents);

            var gameObjectSceneID = m_SceneObjectCount++;
            k_SceneObjects[gameObjectSceneID] = gameObject;
            k_SceneObjectLookupMap[gameObject] = gameObjectSceneID;

            var componentCount = k_SortedComponents.Count;
            var componentIDs = new int[componentCount];
            for (var i = 0; i < componentCount; i++)
            {
                var component = k_SortedComponents[i].Item1;
                if (component is PrefabMetadata)
                    continue;

                if ((component.hideFlags & k_DontSaveFlags) != 0)
                    return;

                var componentSceneID = m_SceneObjectCount++;
                componentIDs[i] = componentSceneID;
                k_SceneObjects[componentSceneID] = component;
                k_SceneObjectLookupMap[component] = componentSceneID;
            }
        }

        /// <summary>
        /// Get the metadata id for a scene object
        /// </summary>
        /// <param name="sceneObject">The scene object whose id will be returned</param>
        /// <returns>The id of the given scene object, if it is tracked in this metadata object</returns>
        public int GetSceneID(UnityObject sceneObject)
        {
            return k_SceneObjectLookupMap.TryGetValue(sceneObject, out var id) ? id : InvalidID;
        }

        /// <summary>
        /// Get a scene object by its metadata id (not InstanceID)
        /// </summary>
        /// <param name="sceneID">The metadata id of the desired scene object</param>
        /// <returns>The scene object for the given id, if it exists</returns>
        public UnityObject GetSceneObject(int sceneID)
        {
            return k_SceneObjects.TryGetValue(sceneID, out var sceneObject) ? sceneObject : null;
        }

        /// <summary>
        /// Return a string which summarizes the state of the scene object metadata
        /// </summary>
        /// <returns></returns>
        public string PrintSceneObjectList()
        {
            var output = string.Empty;
            foreach (var (id, sceneObject) in SortedObjectList)
            {
                output = $"{output}\n{id} - {sceneObject}";
            }

            return output;
        }

        /// <summary>
        /// Get the guid and fileId from the AssetPack for a UnityObject if it is an asset
        /// </summary>
        /// <param name="unityObject">The object whose metadata to get</param>
        /// <param name="guid">The guid of the object, if it is an asset tracked by the AssetPack</param>
        /// <param name="fileId">The fileId of the object, if it is an asset tracked by the AssetPack</param>
        public void GetAssetMetadata(UnityObject unityObject, out string guid, out long fileId)
        {
            if (m_AssetPack != null)
            {
                m_AssetPack.GetAssetMetadata(unityObject, out guid, out fileId, SceneObjectsSetup);
                return;
            }

            guid = default;
            fileId = default;
        }
    }
}
