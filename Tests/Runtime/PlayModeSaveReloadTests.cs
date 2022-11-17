using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;
using Unity.RuntimeSceneSerialization.Internal;
using UnityEngine.Scripting;

#if UNITY_EDITOR || INCLUDE_TEST_SCENE_TEST && !UNITY_2021_3_OR_NEWER && !UNITY_EDITOR_LINUX
using System;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.RuntimeSceneSerialization.Generated;
using Unity.Serialization.Json;
using System.IO;
#endif

#if INCLUDE_TEST_SCENE_TEST && !UNITY_2021_3_OR_NEWER && !UNITY_EDITOR_LINUX
using System.Linq;
using Unity.Properties;
using Unity.RuntimeSceneSerialization.Prefabs;
#endif

namespace Unity.RuntimeSceneSerialization.Tests
{
    class PlayModeSaveReloadTests : IPrebuildSetup, IPostBuildCleanup
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

#if INCLUDE_TEST_SCENE_TEST && !UNITY_2021_3_OR_NEWER && !UNITY_EDITOR_LINUX
        class CompareVisitor : PropertyVisitor
        {
            public object expected;

            protected override void VisitProperty<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value)
            {
                // Don't compare UnityObject types--they will not be reference equal, and we don't want to traverse them
                if (value is UnityObject)
                    return;

                if (expected is TContainer expectedContainer)
                {
                    var expectedValue = property.GetValue(ref expectedContainer);
                    if (SerializationUtils.IsContainerType<TValue>() && value != null)
                    {
#if !NET_DOTS && !ENABLE_IL2CPP
                        SceneSerialization.RegisterPropertyBagRecursively(typeof(TContainer));
#endif

                        var visitor = new CompareVisitor();
                        visitor.expected = expectedValue;
                        PropertyContainer.Visit(ref value, visitor);
                    }
                    else if (expectedValue is float expectedFloat && value is float floatValue)
                    {
                        // Instantiated prefab children can have slightly different values in rotation quaternions
                        Assert.IsTrue(Mathf.Approximately(expectedFloat, floatValue));
                    }
                    else
                    {
                        Assert.AreEqual(expectedValue, value);
                    }
                }
            }
        }

        const string k_TestSceneName = "Test Scene";
        const string k_TestSceneAssetPackPath = "RuntimeSerializationTests/Test Scene AssetPack";

#if UNITY_EDITOR
        const string k_TestScenePath = "Packages/com.unity.runtime-scene-serialization/Tests/Common/Test Scene/Test Scene.unity";
        const string k_TestSceneJsonPath = "RuntimeSerializationTests/Test Scene Json";
#else
        const string k_TestSceneJsonPath = "RuntimeSerializationTests/Test Scene Json Player";
#endif

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Component> k_Components = new List<Component>();
#endif

#if UNITY_EDITOR
        static readonly string k_BuildScenesPath = Path.Combine(Application.temporaryCachePath, "BuildScenes.json");
#endif

        const string k_TestAssetPackPath = "RuntimeSerializationTests/Test AssetPack";

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
            var newScenes = new List<EditorBuildSettingsScene>();
            foreach (var scenario in ScenarioEnumerator.GetEnumerator())
            {
                CreateEmptySceneAdditively(newScenes, scenario.Name, () => {
                    scenario.BuildContent();
                });
            }

#if INCLUDE_TEST_SCENE_TEST && !UNITY_2021_3_OR_NEWER && !UNITY_EDITOR_LINUX
            // Add TestScene to build
            newScenes.Add(new EditorBuildSettingsScene(k_TestScenePath, true));
#endif

            EditorBuildSettings.scenes = newScenes.ToArray();
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
        public static void CreateEmptySceneAdditively(List<EditorBuildSettingsScene> newScenes, string sceneName, Action codeToExecute)
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

        [Preserve]
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

#if INCLUDE_TEST_SCENE_TEST && !UNITY_2021_3_OR_NEWER && !UNITY_EDITOR_LINUX
        [UnityTest]
        public IEnumerator SaveLoadTestScene()
        {
            var activeScene = SceneManager.CreateScene("Serialization Test Scene");
            SceneManager.SetActiveScene(activeScene);

            var assetPack = Resources.Load<AssetPack>(k_TestSceneAssetPackPath);
            var testSceneJson = Resources.Load<TextAsset>(k_TestSceneJsonPath).text;
            SceneSerialization.ImportScene(testSceneJson, assetPack);

            // Wait for temp scene root to be destroyed
            yield return null;

            var renderSettings = SerializedRenderSettings.CreateFromActiveScene();
            var jsonText = SceneSerialization.SerializeScene(activeScene, renderSettings, assetPack);
            Assert.AreEqual(testSceneJson, jsonText);

            // Erase the scene's game objects so only the new objects loaded from JSON are visible
            foreach (var gameObject in activeScene.GetRootGameObjects())
            {
                UnityObject.Destroy(gameObject);
            }

            yield return null;

            var meta = SceneSerialization.ImportScene(jsonText, assetPack);
            Assert.IsNotNull(meta);

            // TODO: Copy scene to Assets folder for CI tests
            // Early-out on CI runs
            var cmdArgs = Environment.GetCommandLineArgs();
            if (Application.isBatchMode || cmdArgs.Contains("-runTests"))
                yield break;

            SceneManager.LoadScene(k_TestSceneName, LoadSceneMode.Additive);
            yield return null;

            var testScene = SceneManager.GetSceneByName(k_TestSceneName);
            var activeRoots = activeScene.GetRootGameObjects();
            var testRoots = testScene.GetRootGameObjects();
            var activeRootCount = activeRoots.Length;
            var testRootCount = testRoots.Length;
            Assert.AreEqual(testRootCount, activeRootCount);

            // HACK: Re-sort imported scene to work around sibling index 0 bug
            for (var i = 0; i < activeRootCount; i++)
            {
                var activeRootName = activeRoots[i].name;
                if (testRoots[i].name != activeRootName)
                {
                    for (var j = 0; j < testRootCount; j++)
                    {
                        if (testRoots[j].name == activeRootName)
                        {
                            (testRoots[i], testRoots[j]) = (testRoots[j], testRoots[i]);
                            break;
                        }
                    }
                }
            }

            for (var i = 0; i < activeRootCount; i++)
            {
                var activeRoot = activeRoots[i];
                var testRoot = testRoots[i];
                CompareGameObjectsRecursively(testRoot, activeRoot);
            }

            // TODO: Compare render settings

            SceneManager.UnloadSceneAsync(testScene);
            SceneManager.UnloadSceneAsync(activeScene);
            yield return null;
        }

        static void CompareGameObjectsRecursively(GameObject expected, GameObject actual)
        {
            Assert.AreEqual(expected.name, actual.name);
            Assert.AreEqual(expected.hideFlags, actual.hideFlags);
            Assert.AreEqual(expected.layer, actual.layer);
            Assert.AreEqual(expected.tag, actual.tag);
            Assert.AreEqual(expected.activeSelf, actual.activeSelf);

            var expectedTransform = expected.transform;
            var actualTransform = actual.transform;
            var expectedChildCount = expectedTransform.childCount;
            var actualChildCount = actualTransform.childCount;
            Assert.AreEqual(expectedChildCount, actualChildCount);

            var expectedComponents = expected.GetComponents(typeof(Component));

            // GetComponents clears the list
            actual.GetComponents(k_Components);

            // Filter out PrefabMetadata
            k_Components.RemoveAll(component => component is PrefabMetadata);

            var expectedComponentsCount = expectedComponents.Length;
            var actualComponentsCount = k_Components.Count;
            Assert.AreEqual(expectedComponentsCount, actualComponentsCount);

            var visitor = new CompareVisitor();
            for (var i = 0; i < expectedComponentsCount; i++)
            {
                visitor.expected = expectedComponents[i];
                var actualComponent = k_Components[i];

#if !NET_DOTS && !ENABLE_IL2CPP
                SceneSerialization.RegisterPropertyBagRecursively(actualComponent.GetType());
#endif

                PropertyContainer.Visit(ref actualComponent, visitor);
            }

            for (var i = 0; i < expectedChildCount; i++)
            {
                CompareGameObjectsRecursively(expectedTransform.GetChild(i).gameObject, actualTransform.GetChild(i).gameObject);
            }
        }
#endif
    }
}
