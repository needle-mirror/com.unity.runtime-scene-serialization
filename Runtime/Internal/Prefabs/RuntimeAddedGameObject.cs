using System;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    /// <summary>
    /// Represents a component which was added as a prefab override
    /// </summary>
    [Serializable]
    class RuntimeAddedGameObject
    {
        public string TransformPath;
        public GameObjectContainer GameObject;

        public RuntimeAddedGameObject() { }

        public RuntimeAddedGameObject(string transformPath, GameObject gameObject, SerializationMetadata metadata)
        {
            TransformPath = transformPath;
            GameObject = new GameObjectContainer(gameObject, metadata);
        }
    }
}
