using System;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Prefabs
{
    public interface IPrefabFactory
    {
        GameObject TryInstantiatePrefab(string guid, Transform parent = null);
    }
}
