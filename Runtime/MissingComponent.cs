using UnityEngine;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Stub class used to represent MonoBehaviours which were not compiled with the app
    /// </summary>
    public class MissingComponent : MonoBehaviour
    {
        /// <summary>
        /// Json representation of the MonoBehaviour's properties, to be serialized back into the scene
        /// </summary>
        public string JsonString;
    }
}
