using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using Unity.RuntimeSceneSerialization.Prefabs;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Stores asset metadata (guid and fileId) for assets associated with a JSON-serialized scene
    /// This type is used as a look up table for asset objects, and can be used to build an AssetBundle for loading
    /// scene assets in player builds
    /// </summary>
    public class AssetPack : ScriptableObject, ISerializationCallbackReceiver
    {
        [Serializable]
        internal class Asset : ISerializationCallbackReceiver
        {
            [SerializeField]
            long[] m_FileIds;

            [SerializeField]
            UnityObject[] m_Assets;

            readonly Dictionary<long, UnityObject> m_FileIdToAssetMap = new Dictionary<long, UnityObject>();
            readonly Dictionary<UnityObject, long> m_AssetToFileIdMap = new Dictionary<UnityObject, long>();

            // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
            readonly List<long> k_FileIds = new List<long>();
            readonly List<UnityObject> k_Assets = new List<UnityObject>();

            internal Dictionary<long, UnityObject> Assets => m_FileIdToAssetMap;

#if UNITY_EDITOR
            internal long AddAssetAndGetFileId(UnityObject asset, bool warnIfMissing)
            {
                if (asset == null)
                    return k_InvalidId;

                long fileId;
                if (m_AssetToFileIdMap.TryGetValue(asset, out fileId))
                    return fileId;

                if (!TryGetGUIDAndLocalFileIdentifier(asset, out _, out fileId, warnIfMissing))
                    return k_InvalidId;

                m_AssetToFileIdMap[asset] = fileId;
                m_FileIdToAssetMap[fileId] = asset;
                return fileId;
            }

            public void GetOrAddAssetMetadata(UnityObject obj, out long fileId, bool warnIfMissing)
            {
                if (m_AssetToFileIdMap.TryGetValue(obj, out fileId))
                    return;

                if (!TryGetGUIDAndLocalFileIdentifier(obj, out _, out fileId, warnIfMissing))
                    fileId = k_InvalidId;

                AddAssetMetadata(obj, fileId);
            }
#endif

            internal long GetFileIdOfAsset(UnityObject asset)
            {
                if (asset == null)
                    return k_InvalidId;

                long fileId;
                return m_AssetToFileIdMap.TryGetValue(asset, out fileId) ? fileId : k_InvalidId;
            }

            public UnityObject GetAsset(long fileId)
            {
                UnityObject asset;
                return m_FileIdToAssetMap.TryGetValue(fileId, out asset) ? asset : null;
            }

            public void OnBeforeSerialize()
            {
                k_FileIds.Clear();
                k_Assets.Clear();
                foreach (var kvp in m_FileIdToAssetMap)
                {
                    var fileId = kvp.Key;
                    var asset = kvp.Value;

                    k_FileIds.Add(fileId);
                    k_Assets.Add(asset);
                }

                m_FileIds = k_FileIds.ToArray();
                m_Assets = k_Assets.ToArray();
            }

            public void OnAfterDeserialize()
            {
                if (m_FileIds == null || m_Assets == null)
                    return;

                var length = m_FileIds.Length;
                var assetLength = m_Assets.Length;
                if (assetLength < length)
                    Debug.LogWarning("Problem in Asset Pack. Number of assets is less than number of fileIds");

                m_FileIdToAssetMap.Clear();
                m_AssetToFileIdMap.Clear();
                for (var i = 0; i < length; i++)
                {
                    var fileId = m_FileIds[i];
                    if (i < assetLength)
                    {
                        var asset = m_Assets[i];
                        if (asset != null)
                            m_AssetToFileIdMap[asset] = fileId;

                        m_FileIdToAssetMap[fileId] = asset;
                    }
                    else
                    {
                        m_FileIdToAssetMap[fileId] = null;
                    }
                }
            }

            public bool TryGetFileId(UnityObject obj, out long fileId) { return m_AssetToFileIdMap.TryGetValue(obj, out fileId); }

            public void AddAssetMetadata(UnityObject obj, long fileId)
            {
                m_AssetToFileIdMap[obj] = fileId;
                m_FileIdToAssetMap[fileId] = obj;
            }
        }

        const int k_InvalidId = -1;
        const string k_AssetPackFilter = "t:" + nameof(AssetPack);

#if UNITY_EDITOR
        static readonly Dictionary<SceneAsset, AssetPack> k_SceneToAssetPackCache = new Dictionary<SceneAsset, AssetPack>();
#endif

        readonly HashSet<IPrefabFactory> m_PrefabFactories = new HashSet<IPrefabFactory>();

        [SerializeField]
        UnityObject m_SceneAsset;

        [HideInInspector]
        [SerializeField]
        string[] m_Guids;

        [HideInInspector]
        [SerializeField]
        Asset[] m_Assets;

        [HideInInspector]
        [SerializeField]
        List<string> m_PrefabGuids = new List<string>();

        [HideInInspector]
        [SerializeField]
        List<GameObject> m_Prefabs = new List<GameObject>();

        readonly Dictionary<string, Asset> m_AssetDictionary = new Dictionary<string, Asset>();
        readonly Dictionary<UnityObject, KeyValuePair<string, long>> m_AssetLookupMap = new Dictionary<UnityObject, KeyValuePair<string, long>>();
        readonly Dictionary<UnityObject, string> m_GuidMap = new Dictionary<UnityObject, string>();
        readonly Dictionary<string, GameObject> m_PrefabDictionary = new Dictionary<string, GameObject>();

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        readonly List<string> k_Guids = new List<string>();
        readonly List<Asset> k_Assets = new List<Asset>();

        internal Dictionary<string, Asset> Assets => m_AssetDictionary;
        internal Dictionary<string, GameObject> Prefabs => m_PrefabDictionary;

        /// <summary>
        /// The number of assets in in this AssetPack
        /// </summary>
        public int AssetCount => Assets.Count;

        /// <summary>
        /// The associated SceneAsset for this AssetPack
        /// </summary>
        public UnityObject SceneAsset { set => m_SceneAsset = value; get => m_SceneAsset; }

        /// <summary>
        /// Register an IPrefabFactory to instantiate prefabs which were not saved along with the scene
        /// </summary>
        /// <param name="factory">An IPrefabFactory which can instantiate prefabs by guid</param>
        public void RegisterPrefabFactory(IPrefabFactory factory) { m_PrefabFactories.Add(factory); }

        /// <summary>
        /// Unregister an IPrefabFactory
        /// </summary>
        /// <param name="factory">The IPrefabFactory to be unregistered</param>
        public void UnregisterPrefabFactory(IPrefabFactory factory) { m_PrefabFactories.Remove(factory); }

        /// <summary>
        /// Clear all asset references in this AssetPack
        /// </summary>
        public void Clear()
        {
            m_AssetDictionary.Clear();
            m_AssetLookupMap.Clear();
            m_PrefabDictionary.Clear();
        }

        /// <summary>
        /// Get the guid and sub-asset index for a given asset
        /// Also adds the asset to the asset pack in the editor
        /// </summary>
        /// <param name="obj">The asset object</param>
        /// <param name="guid">The guid for this asset in the AssetDatabase</param>
        /// <param name="fileId">The fileId within the asset for the object</param>
        /// <param name="warnIfMissing">Whether to print warnings if the object could not be found (suppress if this
        /// might be a scene object and metadata doesn't exist)</param>
        public void GetAssetMetadata(UnityObject obj, out string guid, out long fileId, bool warnIfMissing)
        {
            KeyValuePair<string, long> assetData;
            if (m_AssetLookupMap.TryGetValue(obj, out assetData))
            {
                guid = assetData.Key;
                fileId = assetData.Value;
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GetOrAddAssetMetadata(obj, out guid, out fileId, warnIfMissing);
                return;
            }
#endif

            guid = string.Empty;
            fileId = k_InvalidId;

            // Suppress warning for DontSave objects
            if ((obj.hideFlags & HideFlags.DontSave) == HideFlags.None && warnIfMissing)
                Debug.LogWarning($"Could not find asset metadata for {obj}");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Get the guid of a given prefab, storing the result in the given AssetPack, if provided
        /// </summary>
        /// <param name="prefabInstance">The prefab instance whose guid to find</param>
        /// <param name="guid">The guid, if one is found</param>
        /// <param name="assetPack">The AssetPack to store the prefab and guid</param>
        public static void GetPrefabMetadata(GameObject prefabInstance, out string guid, AssetPack assetPack = null)
        {
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstance);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"Could not find prefab path for {prefabInstance.name}");
                guid = null;
                return;
            }

            guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"Could not find guid for {path}");
                return;
            }

            if (assetPack != null)
                assetPack.m_PrefabDictionary[guid] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        void GetOrAddAssetMetadata(UnityObject obj, out string guid, out long fileId, bool warnIfMissing)
        {
            Asset asset;
            if (!m_GuidMap.TryGetValue(obj, out guid))
            {
                if (TryGetGUIDAndLocalFileIdentifier(obj, out guid, out fileId, warnIfMissing))
                {
                    m_GuidMap[obj] = guid;
                    asset = new Asset();
                    asset.AddAssetMetadata(obj, fileId);
                    m_AssetDictionary[guid] = asset;
                    return;
                }

                m_GuidMap[obj] = null;
                return;
            }

            if (string.IsNullOrEmpty(guid))
            {
                fileId = k_InvalidId;
                return;
            }

            if (!m_AssetDictionary.TryGetValue(guid, out asset))
            {
                asset = new Asset();
                m_AssetDictionary[guid] = asset;
            }

            asset.GetOrAddAssetMetadata(obj, out fileId, warnIfMissing);
        }

        static bool TryGetGUIDAndLocalFileIdentifier(UnityObject obj, out string guid, out long fileId, bool warnIfMissing)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out fileId))
            {
                // Check if target object is marked as "DontSave"--that means it is a scene object but won't be found in metadata
                // Otherwise, this is an error, and we cannot find a valid asset path
                // Suppress warning in certain edge cases (i.e. during deserialization before scene object metadata is set up)
                if ((obj.hideFlags & HideFlags.DontSave) == HideFlags.None && warnIfMissing)
                    Debug.LogWarningFormat("Could not find asset path for {0}", obj);

                guid = string.Empty;
                fileId = k_InvalidId;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the AssetPack associated with the given scene
        /// </summary>
        /// <param name="sceneAsset">The SceneAsset which will be used to find the AssetPack</param>
        /// <returns>The associated AssetPack, if one exists</returns>
        public static AssetPack GetAssetPackForScene(SceneAsset sceneAsset)
        {
            if (k_SceneToAssetPackCache.TryGetValue(sceneAsset, out var assetPack))
                return assetPack;

            var allAssetPacks = AssetDatabase.FindAssets(k_AssetPackFilter);
            foreach (var guid in allAssetPacks)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                var loadedAssetPack = AssetDatabase.LoadAssetAtPath<AssetPack>(path);
                if (loadedAssetPack == null)
                    continue;

                var loadedSceneAsset = loadedAssetPack.SceneAsset as SceneAsset;
                if (loadedSceneAsset == null)
                    continue;

                k_SceneToAssetPackCache[loadedSceneAsset] = loadedAssetPack;

                if (loadedSceneAsset == sceneAsset)
                    assetPack = loadedAssetPack;
            }

            return assetPack;
        }

        /// <summary>
        /// Remove a cached asset pack mapping
        /// </summary>
        /// <param name="sceneAsset">The SceneAsset associated to the AssetPack to remove</param>
        public static void RemoveCachedAssetPack(SceneAsset sceneAsset)
        {
            if (sceneAsset != null)
                k_SceneToAssetPackCache.Remove(sceneAsset);
        }

        /// <summary>
        /// Clear the cached map of scenes to asset packs
        /// </summary>
        public static void ClearSceneToAssetPackCache() { k_SceneToAssetPackCache.Clear(); }
#endif

        /// <summary>
        /// Get an asset based on its guid and fileId
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="fileId"></param>
        /// <returns></returns>
        public UnityObject GetAsset(string guid, long fileId)
        {
            if (fileId < 0)
            {
                Debug.LogErrorFormat("Invalid index {0}", fileId);
                return null;
            }

            if (!m_AssetDictionary.TryGetValue(guid, out var asset))
            {
#if UNITY_EDITOR
                Debug.LogWarningFormat("Asset pack {0} does not contain an asset for guid {1}. Falling back to AssetDatabase.", name, guid);
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarningFormat("Could not find asset path for {0}", guid);
                    return null;
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null)
                {
                    Debug.LogWarningFormat("Could not load asset with guid {0}", guid);
                    return null;
                }

                foreach (var subAsset in assets)
                {
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(subAsset, out _, out long assetFileId))
                    {
                        if (assetFileId == fileId)
                            return subAsset;
                    }
                }

                Debug.LogWarningFormat("Invalid index (too large): {0} for asset at path {1}", fileId, path);
                return null;
#else
                Debug.LogWarningFormat("Could not load asset with guid {0}", guid);
                return null;
#endif
            }

            return asset.GetAsset(fileId);
        }

        /// <summary>
        /// Called before serialization to set up lists from dictionaries
        /// </summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            k_Guids.Clear();
            k_Assets.Clear();
            foreach (var kvp in m_AssetDictionary)
            {
                var guid = kvp.Key;
                if (string.IsNullOrEmpty(guid))
                    continue;

                k_Guids.Add(guid);
                k_Assets.Add(kvp.Value);
            }

            m_Guids = k_Guids.ToArray();
            m_Assets = k_Assets.ToArray();

            m_PrefabGuids.Clear();
            m_Prefabs.Clear();
            foreach (var kvp in m_PrefabDictionary)
            {
                var guid = kvp.Key;
                if (string.IsNullOrEmpty(guid))
                    continue;

                m_PrefabGuids.Add(guid);
                m_Prefabs.Add(kvp.Value);
            }
        }

        /// <summary>
        /// Called after serialization to set up dictionaries from lists
        /// </summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_AssetDictionary.Clear();
            var count = m_Guids.Length;
            for (var i = 0; i < count; i++)
            {
                var asset = m_Assets[i];
                var guid = m_Guids[i];

                if (string.IsNullOrEmpty(guid))
                    continue;

                m_AssetDictionary[guid] = asset;

                foreach (var kvp in asset.Assets)
                {
                    var assetRef = kvp.Value;
                    if (assetRef)
                        m_AssetLookupMap[assetRef] = new KeyValuePair<string, long>(guid, kvp.Key);
                }
            }

            count = m_PrefabGuids.Count;
            for (var i = 0; i < count; i++)
            {
                var prefab = m_Prefabs[i];
                var guid = m_PrefabGuids[i];
                if (string.IsNullOrEmpty(guid))
                    continue;

                m_PrefabDictionary[guid] = prefab;
            }
        }

        /// <summary>
        /// Instantiate the prefab with the given guid, if it is in the asset pack or can be created by a registered factory
        /// </summary>
        /// <param name="prefabGuid">The guid of the prefab to be instantiated</param>
        /// <param name="parent">The parent object to be used when calling Instantiate</param>
        /// <returns>The instantiated prefab, or null if one was not instantiated</returns>
        public GameObject TryInstantiatePrefab(string prefabGuid, Transform parent)
        {
            if (m_PrefabDictionary.TryGetValue(prefabGuid, out var prefab))
                return Instantiate(prefab, parent);

            foreach (var factory in m_PrefabFactories)
            {
                try
                {
                    prefab = factory.TryInstantiatePrefab(prefabGuid, parent);
                    if (prefab != null)
                        return prefab;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            return null;
        }
    }
}
