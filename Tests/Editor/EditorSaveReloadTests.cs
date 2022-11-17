using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

#if INCLUDE_TEST_SCENE_TEST
using System;
using System.Linq;
using UnityEditor;
using UnityObject = UnityEngine.Object;
#endif

namespace Unity.RuntimeSceneSerialization.Tests
{
    class EditorJsonSaveReloadTests
    {
#if INCLUDE_TEST_SCENE_TEST
        const string k_TestScenePath = "Packages/com.unity.runtime-scene-serialization/Tests/Common/Test Scene/Test Scene.unity";
        const string k_TestSceneAssetPackPath = "RuntimeSerializationTests/Test Scene AssetPack";
        const string k_TestSceneJsonPath = "RuntimeSerializationTests/Test Scene Json";
#endif

        static IEnumerable<TestCaseData> SceneTestClasses()
        {
            foreach (var scenario in ScenarioEnumerator.GetEnumerator())
            {
                yield return new TestCaseData(scenario).SetName(scenario.Name);
            }
        }

        [Test, TestCaseSource(nameof(SceneTestClasses))]
        public void RebuildSceneFromJson(ISceneSerializeTestScenario scenario)
        {
            var activeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            scenario.BuildContent();

            // Sanity check that scenario code is correct
            scenario.CompareWithOriginalContent(ScenarioTestPhase.AfterBuildContentInEditor);

            // Using in memory AssetPack because code runs in editor.  Needed to track external references (e.g. materials)
            var assetPack = ScriptableObject.CreateInstance<AssetPack>();
            var renderSettings = SerializedRenderSettings.CreateFromActiveScene();
            var jsonText = SceneSerialization.SerializeScene(activeScene, renderSettings, assetPack);

            // Confirm objects haven't been modified and OnBeforeSerialize has been called)
            scenario.CompareWithOriginalContent(ScenarioTestPhase.AfterSerializeInEditor);
            scenario.CheckAssetPack(assetPack);

            // Clear local state - otherwise the ImportScene call would be additive to the existing state
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Reload scene from JSON
            var meta = SceneSerialization.ImportScene(jsonText, assetPack);
            Assert.IsNotNull(meta);

            // Confirm expected content
            scenario.CompareWithOriginalContent(ScenarioTestPhase.AfterDeserialize);
            scenario.CheckAssetPack(assetPack);
        }

        [Test]
        public void VanillaSerializationCallbackReceiverTest()
        {
            VanillaSerializationCallbackReceiver.SaveLoadTest();
        }

#if INCLUDE_TEST_SCENE_TEST
        [Test]
        public void SaveLoadTestScene()
        {
            // TODO: Copy scene to Assets folder for CI tests
            var cmdArgs = Environment.GetCommandLineArgs();
            var isCI = Application.isBatchMode || cmdArgs.Contains("-runTests");

            // Do a simpler test on CI to work around read-only scene error
            if (isCI)
            {
                var assetPack = Resources.Load<AssetPack>(k_TestSceneAssetPackPath);
                var jsonText = Resources.Load<TextAsset>(k_TestSceneJsonPath).text;
                var meta = SceneSerialization.ImportScene(jsonText, assetPack);
                Assert.IsNotNull(meta);
            }
            else
            {
                var activeScene = EditorSceneManager.OpenScene(k_TestScenePath);
                var renderSettings = SerializedRenderSettings.CreateFromActiveScene();
                var assetPack = ScriptableObject.CreateInstance<AssetPack>();
                var jsonText = SceneSerialization.SerializeScene(activeScene, renderSettings, assetPack);
                var testJson = Resources.Load<TextAsset>(k_TestSceneJsonPath).text;

                Assert.AreEqual(testJson, jsonText);

                const int expectedAssetCount = 5;
                Assert.AreEqual(expectedAssetCount, assetPack.AssetCount);

                // Erase the scene's game objects so only the new objects loaded from JSON are visible
                foreach (var gameObject in activeScene.GetRootGameObjects())
                {
                    UnityObject.DestroyImmediate(gameObject);
                }

                var meta = SceneSerialization.ImportScene(jsonText, assetPack);
                Assert.IsNotNull(meta);

                var testScene = EditorSceneManager.OpenScene(k_TestScenePath, OpenSceneMode.Additive);
                var activeRoots = activeScene.GetRootGameObjects();
                var testRoots = testScene.GetRootGameObjects();
                var activeRootCount = activeRoots.Length;
                var testRootCount = testRoots.Length;
                Assert.AreEqual(testRootCount, activeRootCount);

                for (var i = 0; i < activeRootCount; i++)
                {
                    var activeRoot = activeRoots[i];
                    var testRoot = testRoots[i];
                    CompareGameObjectsRecursively(testRoot, activeRoot);
                }
            }

            // TODO: Compare render settings
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

            for (var i = 0; i < expectedChildCount; i++)
            {
                CompareGameObjectsRecursively(expectedTransform.GetChild(i).gameObject, actualTransform.GetChild(i).gameObject);
            }

            var expectedComponents = expected.GetComponents(typeof(Component));
            var actualComponents = actual.GetComponents(typeof(Component));
            var expectedComponentsCount = expectedComponents.Length;
            var actualComponentsCount = actualComponents.Length;
            Assert.AreEqual(expectedComponentsCount, actualComponentsCount);

            for (var i = 0; i < expectedComponentsCount; i++)
            {
                var expectedSerializedObject = new SerializedObject(expectedComponents[i]);
                var actualSerializedObject = new SerializedObject(actualComponents[i]);
                var expectedIterator = expectedSerializedObject.GetIterator();
                var actualIterator = actualSerializedObject.GetIterator();
                while (expectedIterator.Next(true))
                {
                    actualIterator.Next(true);
                    Assert.IsTrue(SerializedProperty.DataEquals(expectedIterator, actualIterator));
                }
            }
        }
#endif
    }
}
