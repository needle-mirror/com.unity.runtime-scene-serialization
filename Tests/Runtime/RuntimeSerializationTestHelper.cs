#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.RuntimeSceneSerialization.Tests
{
    // ReSharper disable once UnusedMember.Global
    class RuntimeSerializationTestHelper
    {
        // ReSharper disable once UnusedMember.Local
        static void AddBuildScenes()
        {
            const string scenePath = "Assets/defaultScene.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
            PlayModeSaveReloadTests.AddBuildScenes();
        }
    }
}
#endif
