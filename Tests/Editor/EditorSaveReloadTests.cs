using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Tests
{
    class EditorJsonSaveReloadTests
    {
        static IEnumerable<TestCaseData> SceneTestClasses()
        {
            foreach (var scenario in ScenarioEnumerator.GetEnum())
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
            scenario.CompareWithOriginalContent();

            // Using in memory AssetPack because code runs in editor.  Needed to track external references (e.g. materials)
            var assetPack = ScriptableObject.CreateInstance<AssetPack>();
            var renderSettings = SerializedRenderSettings.CreateFromActiveScene();
            var jsonText = SceneSerialization.SerializeScene(activeScene, renderSettings, assetPack);

            scenario.CheckAssetPack(assetPack);

            // Clear local state - otherwise the ImportScene call would be additive to the existing state
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Reload scene from JSON
            var meta = SceneSerialization.ImportScene(jsonText, assetPack);
            Assert.IsNotNull(meta);

            // Confirm expected content
            scenario.CompareWithOriginalContent();
            scenario.CheckAssetPack(assetPack);
        }
    }
}
