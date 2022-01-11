using UnityEngine;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Unity.RuntimeSceneSerialization.Tests
{
    // Classes that create scenarios suitable for incorporation into save/reload style serialization tests.
    // Each class implements the same interface and will build content, then check expected values are still present.
    interface ISceneSerializeTestScenario
    {
        // Name of the scenario, used for temporary files and shown as the test name
        string Name { get; }

        // Build scene content with initial state
        void BuildContent();

        // Confirm that the state is the same as the original content
        void CompareWithOriginalContent();

        void CheckAssetPack(AssetPack assetPack);
    }

    static class ScenarioEnumerator
    {
        // Tests that want to repeat a scenario against each class that implements ISerializeRuleMonoBehaviour can use this enumerator
        public static IEnumerable<ISceneSerializeTestScenario> GetEnum()
        {
            yield return new BasicGameObject();
            yield return new NestedObjects();
            yield return new MonoBehaviourByValScenario();
            yield return new SerializedReferenceScenario();
        }
    }

    class BasicGameObject : ISceneSerializeTestScenario
    {
        public string Name => "BasicGameObject";

        public void BuildContent()
        {
            var go = new GameObject("MyGo");
            go.transform.position = new Vector3(1.0f, 1.1f, 1.2f);
        }

        public void CompareWithOriginalContent()
        {
            var go = GameObject.Find("MyGo");
            Assert.IsNotNull(go);
            Assert.AreEqual(new Vector3(1.0f, 1.1f, 1.2f), go.transform.position);

            var goRenamed = GameObject.Find("Renamed");
            Assert.IsNull(goRenamed);
        }

        public void CheckAssetPack(AssetPack assetPack)
        {
            // Expect default skybox to be included in asset pack
            Assert.AreEqual(1, assetPack.AssetCount);
        }
    }

    class NestedObjects : ISceneSerializeTestScenario
    {
        public string Name => "NestedObjects";

        const float k_Pos1 = 2.0f;
        const float k_Pos2 = 3.0f;

        public void BuildContent()
        {
            var go = new GameObject("MyGo");

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "MySphere";
            var sphereTransform = sphere.transform;
            sphereTransform.parent = go.transform;
            sphereTransform.position = new Vector3(k_Pos1, k_Pos1, k_Pos1);

            var sphere2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere2.name = "Level2Sphere";
            sphereTransform = sphere2.transform;
            sphereTransform.parent = sphereTransform;
            sphereTransform.position = new Vector3(k_Pos2, k_Pos2, k_Pos2);
        }

        public void CompareWithOriginalContent()
        {
            var go = GameObject.Find("MyGo");
            Assert.IsNotNull(go);

            CheckSphere("MySphere", k_Pos1);
            CheckSphere("Level2Sphere", k_Pos2);
        }

        static void CheckSphere(string name, float expectedCenter)
        {
            var sphere = GameObject.Find(name);
            Assert.IsNotNull(sphere);

            Assert.AreEqual(new Vector3(expectedCenter, expectedCenter, expectedCenter), sphere.transform.position);
        }

        public void CheckAssetPack(AssetPack assetPack)
        {
            // Because SharedMesh and SharedMaterial references
            Assert.AreEqual(2, assetPack.AssetCount);
        }
    }

    class MonoBehaviourByValScenario : ISceneSerializeTestScenario
    {
        public string Name => "MonoBehaviourByValScenario";

        public void BuildContent()
        {
            var go = new GameObject("GoWithMonoBehaviour");
            var comp = go.AddComponent<MonoBehaviourByValueFields>();
            comp.SetKnownState();
        }

        public void CompareWithOriginalContent()
        {
            var go = GameObject.Find("GoWithMonoBehaviour");
            Assert.IsNotNull(go);

            var comp = go.GetComponent<MonoBehaviourByValueFields>();
            comp.TestKnownState();
        }

        public void CheckAssetPack(AssetPack assetPack)
        {
            // Expect default skybox to be included in asset pack
            Assert.AreEqual(1, assetPack.AssetCount);
        }
    }

    class SerializedReferenceScenario : ISceneSerializeTestScenario
    {
        public string Name => "SerializedReferenceScenario";

        public void BuildContent()
        {
            var go = new GameObject("GoWithSRMonoBehaviour");
            var comp = go.AddComponent<MonoBehaviourSerializedRef>();
            comp.SetKnownState();
        }

        public void CompareWithOriginalContent()
        {
            var go = GameObject.Find("GoWithSRMonoBehaviour");
            Assert.IsNotNull(go);

            var comp = go.GetComponent<MonoBehaviourSerializedRef>();
            comp.TestKnownState();
        }

        public void CheckAssetPack(AssetPack assetPack)
        {
            // Expect default skybox to be included in asset pack
            Assert.AreEqual(1, assetPack.AssetCount);
        }
    }
}
