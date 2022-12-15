using System;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Used to store state related to deserialization
    /// </summary>
    public partial class SerializationMetadata
    {
        /// <summary>
        /// Most recently used SerializationMetadata, used to display debug info in SerializationMetadata window
        /// </summary>
        internal static SerializationMetadata CurrentSerializationMetadata { get; private set; }

        /// <summary>
        /// The AssetPack used to track asset references
        /// </summary>
        public AssetPack AssetPack { get; }

        /// <summary>
        /// Create a new SerializationMetadata object for use in scene object serialization or deserialization
        /// </summary>
        /// <param name="assetPack">The AssetPack to use for asset references</param>
        public SerializationMetadata(AssetPack assetPack = null)
        {
            CurrentSerializationMetadata = this;
            AssetPack = assetPack;
        }
    }
}
