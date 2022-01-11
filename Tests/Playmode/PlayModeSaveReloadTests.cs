#if UNITY_EDITOR
using UnityEditor;
#endif

using NUnit.Framework;
using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Collections.Generic;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Tests
{
    class PlaymodeSaveReloadTest : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup()
        {
#if UNITY_EDITOR
            // Prepare a scene file for each of the Scenarios and make sure it is part of the build

            // Note: For full reference support, this code probably also should serialize each new scene to JSON,
            // in order to populate an AssetPack.  That AssetPack should be saved into the Resources folder, or bundled into an AssetBundle
            // so that it is available when SceneSerialization.ImportScene is called in the actual test.
            // And given that creation of the JSON is needed in the editor in order to populate the AssetPack it could be interesting
            // to only bundle a bootstrap scene and bundle the JSON string instead of the scene file so that all serialization happens via JSON
            // without any unity files.

            foreach ( var scenario in ScenarioEnumerator.GetEnum())
            {
                CreateSceneUtility.CreateEmptySceneAdditively(scenario.Name, () => {
                        scenario.BuildContent();
                });
            }
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            // Clean up the scenario specific scenes that were included in the build
            foreach ( var scenario in ScenarioEnumerator.GetEnum())
                CleanupTestScenes(scenario.Name);

            // Remove scene from the build settings
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
#endif
        }

        static void CleanupTestScenes(string sceneName)
        {
            var testSceneFullPath = Path.Combine(Application.dataPath, $"{sceneName}.unity");
            File.Delete(testSceneFullPath);
            File.Delete(testSceneFullPath + ".meta");
        }

        static IEnumerable<TestCaseData> SceneTestClasses()
        {
            foreach (var scenario in ScenarioEnumerator.GetEnum())
                yield return new TestCaseData(scenario).Returns(null).SetName(scenario.Name);
        }

        [UnityTest, TestCaseSource(nameof(SceneTestClasses))]
        public IEnumerator PerformTest(ISceneSerializeTestScenario scenario)
        {
            var sceneName = scenario.Name;

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;

            // Sanity check that the loaded scene has expected content
            scenario.CompareWithOriginalContent();

            var activeScene = SceneManager.GetActiveScene();
            var renderSettings = SerializedRenderSettings.CreateFromActiveScene();
            var assetPack = ScriptableObject.CreateInstance<AssetPack>();
            var jsonText = SceneSerialization.SerializeScene(activeScene, renderSettings, assetPack);

            // Erase the scene's game objects so only the new objects loaded from JSON are visible
            foreach (var o in UnityObject.FindObjectsOfType<GameObject>())
            {
                UnityObject.Destroy(o);
            }

            yield return null;

            var meta = SceneSerialization.ImportScene(jsonText, assetPack);
            Assert.IsNotNull(meta);

            // Confirm objects have been deserialized with the expected state
            scenario.CompareWithOriginalContent();

            yield return null;
        }
    }
}
