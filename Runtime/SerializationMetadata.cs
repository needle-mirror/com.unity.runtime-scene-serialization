using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Used to store state related to deserialization
    /// </summary>
    public partial class SerializationMetadata
    {
        const string k_PostSerializationMessage = "You must deserialize a scene using this metadata in order to perform post-serialization actions";

        /// <summary>
        /// Most recently used SerializationMetadata, used to display debug info in SerializationMetadata window
        /// </summary>
        internal static SerializationMetadata CurrentSerializationMetadata { get; private set; }

        /// <summary>
        /// The asset pack to use for storing and retrieving assets
        /// </summary>
        readonly AssetPack m_AssetPack;

        /// <summary>
        /// A queue of actions which will be run after serialization completes (e.g. prefab overrides and scene references)
        /// </summary>
        readonly Queue<Action> m_PostSerializationActions = new Queue<Action>();

        /// <summary>
        /// Scene references require all objects to exist before they can be set, but they are stored along with the rest
        /// of the scene. This is set to true after   After deserialization, call DoPostSerializationActions to apply scene references
        /// </summary>
        bool m_QueueSceneReferences = true;

        /// <summary>
        /// The AssetPack used to track asset references
        /// </summary>
        public AssetPack AssetPack => m_AssetPack;

        /// <summary>
        /// Create a new SerializationMetadata object for use in scene object serialization or deserialization
        /// </summary>
        /// <param name="assetPack">The AssetPack to use for asset references</param>
        public SerializationMetadata(AssetPack assetPack = null)
        {
            CurrentSerializationMetadata = this;
            m_AssetPack = assetPack;
        }

        /// <summary>
        /// Dequeue and Invoke all actions in the PostSerializationAction queue
        /// </summary>
        public void DoPostSerializationActions()
        {
            if (m_QueueSceneReferences)
                throw new InvalidOperationException(k_PostSerializationMessage);

            while (m_PostSerializationActions.Count > 0)
            {
                try
                {
                    m_PostSerializationActions.Dequeue()();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// Called at the end of DeserializeScene to switch SetSceneReference to setting values directly, instead of
        /// queueing them
        /// </summary>
        internal void OnDeserializationComplete()
        {
            m_QueueSceneReferences = false;
        }

        /// <summary>
        /// Use this method when setting property values to scene objects. It will queue the action during deserialization,
        /// and invoke the action after serialization is complete
        /// </summary>
        /// <param name="setValue"></param>
        internal void SetSceneReference(Action setValue)
        {
            if (m_QueueSceneReferences)
                m_PostSerializationActions.Enqueue(setValue);
            else
                setValue();
        }

        /// <summary>
        /// Enqueue an action to be performed after serialization is complete (e.g. prefab property overrides)
        /// </summary>
        /// <param name="action">The action to be enqueued</param>
        internal void EnqueuePostSerializationAction(Action action)
        {
            m_PostSerializationActions.Enqueue(action);
        }
    }
}
