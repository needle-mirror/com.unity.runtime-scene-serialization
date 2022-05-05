using Unity.Properties;
using UnityEngine;
using UnityEngine.Scripting;

namespace Unity.RuntimeSceneSerialization.Generated
{
    /// <summary>
    /// Stub class to provide a minimal assembly into which we will generate property bags for built-in types
    /// </summary>
    [Preserve]
    public class Stub : MonoBehaviour
    {

    }

    [Preserve]
    class StubPropertyBag : ContainerPropertyBag<Stub>
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            PropertyBag.Register(new StubPropertyBag());
        }
    }
}
