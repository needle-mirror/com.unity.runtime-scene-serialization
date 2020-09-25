using System;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Prefabs
{
    [Serializable]
    public struct RuntimeAddedGameObject
    {
        public string TransformPath;
        public GameObjectContainer GameObject;
    }
}
