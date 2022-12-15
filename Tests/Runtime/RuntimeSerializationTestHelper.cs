#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.RuntimeSceneSerialization.Tests
{
    // Used by CI to set up build scenes for player tests
    // ReSharper disable once UnusedType.Global
    class RuntimeSerializationTestHelper
    {
        // ReSharper disable once UnusedMember.Local
        static void AddBuildScenes()
        {
            const string scenePath = "Assets/defaultScene.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
            SaveReloadTests.AddBuildScenes();
        }
    }
}
#endif
