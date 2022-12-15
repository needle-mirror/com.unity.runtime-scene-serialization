using System;
using Unity.Serialization.Json;
using UnityEngine;
using UnityObject = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    /// <summary>
    /// Serialization adapter for all UnityEngine.Object properties
    /// Provides the following functionality:
    ///   Serialize prefab metadata
    ///   Handle deserialization of GameObjects and Components
    ///   Deserialize prefab metadata and apply added/removed Components and added GameObjects
    ///   Enqueue scene references and prefab overrides to be set after deserialization completes
    ///   Call `OnAfterDeserialize` on all Components
    ///   For Mono/Editor functionality, load property bags for Components on-demand
    ///   Serialize assets using guid and fileId
    ///   Serialize scene references by sceneID
    ///   Suppress the global UnityEngine.Object adapter
    /// </summary>
    partial class JsonAdapter : IContravariantJsonAdapter<UnityObject>
    {
        const string k_SceneIdPropertyName = "sceneID";
        const string k_GuidPropertyName = "guid";
        const string k_FileIdPropertyName = "fileId";

        void IContravariantJsonAdapter<UnityObject>.Serialize(IJsonSerializationContext context, UnityObject value)
        {
            if (value is Component && !m_SerializeAsReference)
            {
#if !NET_DOTS && !ENABLE_IL2CPP
                SceneSerialization.RegisterPropertyBagRecursively(value.GetType());
#endif

                context.ContinueVisitationWithoutAdapters();
            }
            else
            {
                WriteUnityObjectReference(context, value);
            }
        }

        void WriteUnityObjectReference(IJsonSerializationContext context, UnityObject value)
        {
            var writer = context.Writer;
            if (value == null)
            {
                WriteNullUnityObjectReference(writer);
                return;
            }

            if (m_Metadata == null)
                throw new InvalidOperationException("Serialization metadata is null");

            if (m_Metadata.GetSceneID(value, out var sceneID))
            {
                using (writer.WriteObjectScope())
                {
                    writer.WriteKeyValue(k_SceneIdPropertyName, sceneID);
                }

                return;
            }

            m_Metadata.GetAssetMetadata(value, out var guid, out var fileId);
            if (string.IsNullOrEmpty(guid))
            {
                // ReSharper disable once CommentTypo
                // Check if target object is marked as "DontSave"--that means it is a scene object but won't be found in metadata
                if ((value.hideFlags & HideFlags.DontSave) != HideFlags.None)
                {
                    WriteNullUnityObjectReference(writer);
                    return;
                }

                // Suppress warning if scene object metadata is not setup (i.e. during deserialization)
                if (m_Metadata.SceneObjectsSetup)
                    Debug.LogWarningFormat("Could not find GUID for {0}", value);

                WriteNullUnityObjectReference(writer);
                return;
            }

            using (writer.WriteObjectScope())
            {
                writer.WriteKeyValue(k_GuidPropertyName, guid);
                writer.WriteKeyValue(k_FileIdPropertyName, fileId);
            }
        }

        static void WriteNullUnityObjectReference(JsonWriter writer)
        {
            // Write sceneId: -1 for forwards compatibility (deserialization relies on this in 0.x versions)
            using (writer.WriteObjectScope())
            {
                const int invalidSceneId = -1;
                writer.WriteKeyValue(k_SceneIdPropertyName, invalidSceneId);
            }
        }

        object IContravariantJsonAdapter<UnityObject>.Deserialize(IJsonDeserializationContext context)
        {
            return ReadUnityObjectReference(context);
        }

        UnityObject ReadUnityObjectReference(IJsonDeserializationContext context)
        {
            var serializedValue = context.SerializedValue;
            if (serializedValue.IsNull())
                return default;

            var objectView = serializedValue.AsObjectView();
            if (objectView.TryGetValueAsString(k_GuidPropertyName, out var guid))
            {
                var fileId = objectView[k_FileIdPropertyName].AsInt64();
                return GetAsset(guid, fileId, m_Metadata.AssetPack);
            }

            if (objectView.TryGetValueAsInt64(k_SceneIdPropertyName, out var sceneId))
                return m_FirstPassCompleted ? m_Metadata.GetSceneObject((int)sceneId) : null;

            throw new InvalidOperationException("Could not resolve Unity Object reference");
        }

#if UNITY_EDITOR
        static UnityObject GetAsset(string guid, long fileId)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null)
            {
                Debug.LogWarningFormat("Could not load asset with guid {0} at path {1}", guid, path);
                return null;
            }

            foreach (var asset in assets)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long assetFileId))
                {
                    if (assetFileId == fileId)
                        return asset;
                }
            }

            Debug.LogWarningFormat("FileId {0} is invalid for asset {1} at path {2}", fileId, guid, path);
            return null;
        }
#endif

        static UnityObject GetAsset(string guid, long fileId, AssetPack assetPack)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var asset = GetAsset(guid, fileId);
                if (asset != null)
                    return asset;

                Debug.LogWarning("Cannot find a local asset with " + guid + ". Falling back to asset pack.");
            }
#endif

            if (assetPack == null)
            {
                Debug.LogWarning("Cannot import asset with guid: " + guid + " and no asset pack");
                return null;
            }

            return assetPack.GetAsset(guid, fileId);
        }
    }
}
