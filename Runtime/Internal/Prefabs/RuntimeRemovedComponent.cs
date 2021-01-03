using System;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    [Serializable]
    class RuntimeRemovedComponent
    {
        public string TransformPath;
        public int ComponentIndex;

        public RuntimeRemovedComponent() { }

        public RuntimeRemovedComponent(string transformPath, int componentIndex)
        {
            TransformPath = transformPath;
            ComponentIndex = componentIndex;
        }
    }
}
