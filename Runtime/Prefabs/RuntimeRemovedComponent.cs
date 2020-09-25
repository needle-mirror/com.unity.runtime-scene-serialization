using System;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Prefabs
{
    [Serializable]
    public struct RuntimeRemovedComponent
    {
        public string TransformPath;
        public int ComponentIndex;
    }
}
