using System;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Prefabs
{
    /// <summary>
    /// Prefab factories can be registered to AssetPacks to supplement their ability to instantiate prefabs
    /// </summary>
    public interface IPrefabFactory
    {
        /// <summary>
        /// Try to instantiate a prefab with the given guid
        /// </summary>
        /// <param name="guid">The guid of the prefab that should be instantiated</param>
        /// <param name="parent">The parent object to use when calling Instantiate</param>
        /// <returns>The prefab instance, if one was created</returns>
        GameObject TryInstantiatePrefab(string guid, Transform parent = null);
    }
}
