using System;
using System.Linq;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Extension methods used for scene object serialization
    /// </summary>
    public static class SerializationExtensions
    {
        /// <summary>
        /// Get the path of a given transform relative to this transform
        /// </summary>
        /// <param name="root">The transform to use as the root of the path</param>
        /// <param name="target">The transform whose path to get</param>
        /// <returns>A path based on GameObject names to used find a transform relative to the root</returns>
        public static string GetTransformPath(this Transform root, Transform target)
        {
            var path = string.Empty;
            while (target != null && root != target)
            {
                var name = target.name;
                if (name.Contains('/'))
                {
                    name = name.Replace('/', '_');
                    Debug.LogWarning("Encountered GameObject with name that contains '/'. This may cause issues when deserializing prefab overrides");
                }

                path = string.IsNullOrEmpty(path) ? name : $"{name}/{path}";

                target = target.parent;
            }

            if (target == null)
                Debug.LogError($"Could not find target transform {target} in {root}");

            return path;
        }

        /// <summary>
        /// Get the transform at the given path relative to this transform
        /// </summary>
        /// <param name="root">The transform to use as the root of the path</param>
        /// <param name="path">A path based on GameObject names to used find a transform relative to the root</param>
        /// <returns>The target transform, if it was found</returns>
        public static Transform GetTransformAtPath(this Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            var names = path.Split('/');
            foreach (var name in names)
            {
                var found = false;
                foreach (Transform child in root)
                {
                    var childName = child.name;
                    if (childName.Contains('/'))
                    {
                        childName = name.Replace('/', '_');
                        Debug.LogWarning("Encountered GameObject with name that contains '/'. This may cause issues when deserializing prefab overrides");
                    }

                    if (childName == name)
                    {
                        root = child;
                        found = true;
                    }
                }

                if (!found)
                {
                    Debug.LogError($"Could not find {name} in {root.name}");
                    return null;
                }
            }

            return root;
        }
    }
}
