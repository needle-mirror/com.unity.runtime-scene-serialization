// Creating a scene in IPrebuildSetup.Setup is pretty finicky, so this code is borrowed from EditModeAndPlayModeTests\SceneManagement suite.
// It takes care of adding the new scene file to the EditorBuildSettings and putting back the original state.

using System;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#endif

namespace Unity.RuntimeSceneSerialization.Tests
{
    static class CreateSceneUtility
    {
        public static void CreateEmptySceneAdditively(string sceneName, Action codeToExecute)
        {
#if UNITY_EDITOR
            var initScene = SceneManager.GetActiveScene();
            var scenePath = $"Assets/{sceneName}.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            codeToExecute();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettingsScene[] sceneSettings = { new EditorBuildSettingsScene(scenePath, true) };
            var newSceneSettings = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            newSceneSettings.AddRange(sceneSettings);
            EditorBuildSettings.scenes = newSceneSettings.ToArray();
            SceneManager.SetActiveScene(initScene);
            EditorSceneManager.CloseScene(scene, false);
#endif
        }
    }
}
