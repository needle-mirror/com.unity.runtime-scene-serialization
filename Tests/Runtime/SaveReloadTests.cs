using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.RuntimeSceneSerialization.Generated;
using Unity.Serialization.Json;
using System.IO;
#endif

namespace Unity.RuntimeSceneSerialization.Tests
{
    class SaveReloadTests : IPrebuildSetup, IPostBuildCleanup
    {
#if UNITY_EDITOR
        [Serializable]
        class SerializableBuildScene
        {
            [SerializeField]
            bool m_Enabled;

            [SerializeField]
            string m_Path;

            [SerializeField]
            GUID m_Guid;

            public SerializableBuildScene() { }

            public SerializableBuildScene(EditorBuildSettingsScene scene)
            {
                m_Enabled = scene.enabled;
                m_Path = scene.path;
                m_Guid = scene.guid;
            }

            public EditorBuildSettingsScene ToEditorBuildSettingsScene()
            {
                return new EditorBuildSettingsScene
                {
                    enabled = m_Enabled,
                    path = m_Path,
                    guid = m_Guid
                };
            }

            public static SerializableBuildScene[] FromBuildSettings()
            {
                var scenes = EditorBuildSettings.scenes;
                if (scenes == null || scenes.Length == 0)
                    return Array.Empty<SerializableBuildScene>();

                var length = scenes.Length;
                var result = new SerializableBuildScene[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = new SerializableBuildScene(scenes[i]);
                }

                return result;
            }

            public static void RestoreBuildSettings(SerializableBuildScene[] scenes)
            {
                if (scenes == null || scenes.Length == 0)
                {
                    EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
                    return;
                }

                var length = scenes.Length;
                var buildScenes = new EditorBuildSettingsScene[length];
                for (var i = 0; i < length; i++)
                {
                    buildScenes[i] = scenes[i].ToEditorBuildSettingsScene();
                }

                EditorBuildSettings.scenes = buildScenes;
            }
        }
#endif

#if UNITY_EDITOR
        static readonly string k_BuildScenesPath = Path.Combine(Application.temporaryCachePath, "BuildScenes.json");
#endif

        const string k_TestAssetPackPath = "RuntimeSceneSerializationTests/Test AssetPack";

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

            try
            {
                var json = JsonSerialization.ToJson(SerializableBuildScene.FromBuildSettings());
                File.WriteAllText(k_BuildScenesPath, json);
            }
            catch
            {
                // ignored
            }

            AddBuildScenes();
#endif
        }

#if UNITY_EDITOR
        public static void AddBuildScenes()
        {
            var existingScenes = EditorBuildSettings.scenes.ToList();
            foreach (var scenario in ScenarioEnumerator.GetEnumerator())
            {
                CreateEmptySceneAdditively(existingScenes, scenario.Name, () => {
                    scenario.BuildContent();
                });
            }

            EditorBuildSettings.scenes = existingScenes.ToArray();
        }
#endif

        public void Cleanup()
        {
#if UNITY_EDITOR
            // Clean up the scenario specific scenes that were included in the build
            foreach ( var scenario in ScenarioEnumerator.GetEnumerator())
                CleanupTestScenes(scenario.Name);

            try
            {
                var json = File.ReadAllText(k_BuildScenesPath);
                var buildScenes = JsonSerialization.FromJson<SerializableBuildScene[]>(json);
                SerializableBuildScene.RestoreBuildSettings(buildScenes);
            }
            catch
            {
                EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
            }

            EditorApplication.delayCall += AssetDatabase.SaveAssets;
#endif
        }

#if UNITY_EDITOR
        static void CreateEmptySceneAdditively(List<EditorBuildSettingsScene> newScenes, string sceneName, Action codeToExecute)
        {
            var initScene = SceneManager.GetActiveScene();
            var scenePath = $"Assets/{sceneName}.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var _ = new GameObject("Stub", typeof(Stub));
            codeToExecute();
            EditorSceneManager.SaveScene(scene, scenePath);
            newScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            SceneManager.SetActiveScene(initScene);
            EditorSceneManager.CloseScene(scene, false);
        }

        static void CleanupTestScenes(string sceneName)
        {
            var testSceneFullPath = Path.Combine(Application.dataPath, $"{sceneName}.unity");
            File.Delete(testSceneFullPath);
            File.Delete(testSceneFullPath + ".meta");
        }
#endif

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            PropertyBagOverrides.InitializeOverrides();
        }

        static IEnumerable<TestCaseData> SceneTestClasses()
        {
            foreach (var scenario in ScenarioEnumerator.GetEnumerator())
            {
                yield return new TestCaseData(scenario).Returns(null).SetName(scenario.Name);
            }
        }

        [UnityTest, TestCaseSource(nameof(SceneTestClasses))]
        public IEnumerator PerformTest(ISceneSerializeTestScenario scenario)
        {
            var sceneName = scenario.Name;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;

            // Sanity check that the loaded scene has expected content
            scenario.CompareWithOriginalContent(ScenarioTestPhase.AfterSceneLoad);

            var activeScene = SceneManager.GetActiveScene();
            var renderSettings = SerializedRenderSettings.CreateFromActiveScene();
            var assetPack = Resources.Load<AssetPack>(k_TestAssetPackPath);
            var jsonText = SceneSerialization.SerializeScene(activeScene, renderSettings, assetPack);

            // Confirm objects haven't been modified and OnBeforeSerialize has been called)
            scenario.CompareWithOriginalContent(ScenarioTestPhase.AfterSerialize);

            // Erase the scene's game objects so only the new objects loaded from JSON are visible
            foreach (var gameObject in activeScene.GetRootGameObjects())
            {
                UnityObject.Destroy(gameObject);
            }

            yield return null;

            var meta = SceneSerialization.ImportScene(jsonText, assetPack);
            Assert.IsNotNull(meta);

            // Confirm objects have been deserialized with the expected state
            scenario.CompareWithOriginalContent(ScenarioTestPhase.AfterDeserialize);

            yield return null;
        }

        [Test]
        public void VanillaSerializationCallbackReceiverTest()
        {
            VanillaSerializationCallbackReceiver.SaveLoadTest();
        }

        [Test]
        public void BasicTest()
        {
            Assert.IsTrue(true);
        }
    }
}
