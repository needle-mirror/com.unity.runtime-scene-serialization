using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Tests
{
    enum ScenarioTestPhase
    {
        AfterSceneLoad,
        AfterSerialize,
        AfterDeserialize,
#if UNITY_EDITOR
        AfterBuildContentInEditor,
        AfterSerializeInEditor
#endif
    }

    // Classes that create scenarios suitable for incorporation into save/reload style serialization tests.
    // Each class implements the same interface and will build content, then check expected values are still present.
    interface ISceneSerializeTestScenario
    {
        // Name of the scenario, used for temporary files and shown as the test name
        string Name { get; }

        // Build scene content with initial state
        void BuildContent();

        // Confirm that the state is the same as the original content
        void CompareWithOriginalContent(ScenarioTestPhase phase);

#if UNITY_EDITOR
        void CheckAssetPack(AssetPack assetPack);
#endif
    }

    static class ScenarioEnumerator
    {
        // Tests that want to repeat a scenario against each class that implements ISerializeRuleMonoBehaviour can use this enumerator
        public static IEnumerable<ISceneSerializeTestScenario> GetEnumerator()
        {
            yield return new BasicGameObject();
            yield return new NestedObjects();
            yield return new MonoBehaviourByValScenario();
            yield return new MonoBehaviourSerializationCallbackReceiverScenario();

#if UNITY_EDITOR
            yield return new SerializedReferenceScenario();
#endif
        }
    }

    class BasicGameObject : ISceneSerializeTestScenario
    {
        string ISceneSerializeTestScenario.Name => "BasicGameObject";

        void ISceneSerializeTestScenario.BuildContent()
        {
            var go = new GameObject("MyGo");
            go.transform.position = new Vector3(1.0f, 1.1f, 1.2f);
        }

        void ISceneSerializeTestScenario.CompareWithOriginalContent(ScenarioTestPhase phase)
        {
            var go = GameObject.Find("MyGo");
            Assert.IsNotNull(go);
            Assert.AreEqual(new Vector3(1.0f, 1.1f, 1.2f), go.transform.position);

            var goRenamed = GameObject.Find("Renamed");
            Assert.IsNull(goRenamed);
        }

#if UNITY_EDITOR
        void ISceneSerializeTestScenario.CheckAssetPack(AssetPack assetPack)
        {
            // Expect default skybox to be included in asset pack
            Assert.AreEqual(1, assetPack.AssetCount);
        }
#endif
    }

    class NestedObjects : ISceneSerializeTestScenario
    {
        const float k_Pos1 = 2.0f;
        const float k_Pos2 = 3.0f;

        string ISceneSerializeTestScenario.Name => "NestedObjects";

        void ISceneSerializeTestScenario.BuildContent()
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

        void ISceneSerializeTestScenario.CompareWithOriginalContent(ScenarioTestPhase phase)
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

#if UNITY_EDITOR
        void ISceneSerializeTestScenario.CheckAssetPack(AssetPack assetPack)
        {
            // Because SharedMesh and SharedMaterial references
            Assert.AreEqual(2, assetPack.AssetCount);
        }
#endif
    }

    class MonoBehaviourByValScenario : ISceneSerializeTestScenario
    {
        string ISceneSerializeTestScenario.Name => "MonoBehaviourByValScenario";

        void ISceneSerializeTestScenario.BuildContent()
        {
            var go = new GameObject("GoWithMonoBehaviour");
            var comp = go.AddComponent<MonoBehaviourByValueFields>();
            comp.SetKnownState();
        }

        void ISceneSerializeTestScenario.CompareWithOriginalContent(ScenarioTestPhase phase)
        {
            var go = GameObject.Find("GoWithMonoBehaviour");
            Assert.IsNotNull(go);

            var comp = go.GetComponent<MonoBehaviourByValueFields>();
            comp.TestKnownState();
        }

#if UNITY_EDITOR
        void ISceneSerializeTestScenario.CheckAssetPack(AssetPack assetPack)
        {
            // Expect default skybox to be included in asset pack
            Assert.AreEqual(1, assetPack.AssetCount);
        }
#endif
    }

    class MonoBehaviourSerializationCallbackReceiverScenario : ISceneSerializeTestScenario
    {

        string ISceneSerializeTestScenario.Name => "MonoBehaviourSerializationCallbackReceiverScenario";

        void ISceneSerializeTestScenario.BuildContent()
        {
            var go = new GameObject("SerializationCallbackReceiver");
            var callbackReceiver = go.AddComponent<MonoBehaviourSerializationCallbackReceiver>();
            Assert.IsFalse(callbackReceiver.BeforeSerialize);
            Assert.IsFalse(callbackReceiver.AfterDeserialize);
        }

        void ISceneSerializeTestScenario.CompareWithOriginalContent(ScenarioTestPhase phase)
        {
            var go = GameObject.Find("SerializationCallbackReceiver");
            Assert.IsNotNull(go);

            var callbackReceiver = go.GetComponent<MonoBehaviourSerializationCallbackReceiver>();
            switch (phase)
            {
#if UNITY_EDITOR
                case ScenarioTestPhase.AfterBuildContentInEditor:
                    Assert.IsFalse(callbackReceiver.BeforeSerialize);
                    Assert.IsFalse(callbackReceiver.AfterDeserialize);
                    break;
                case ScenarioTestPhase.AfterSerializeInEditor:
                    Assert.IsTrue(callbackReceiver.BeforeSerialize);
                    Assert.IsFalse(callbackReceiver.AfterDeserialize);
                    break;
#endif
                case ScenarioTestPhase.AfterSerialize:
                    Assert.IsTrue(callbackReceiver.BeforeSerialize);
                    Assert.IsTrue(callbackReceiver.AfterDeserialize);
                    break;
                case ScenarioTestPhase.AfterSceneLoad:
                case ScenarioTestPhase.AfterDeserialize:
                    Assert.IsFalse(callbackReceiver.BeforeSerialize);
                    Assert.IsTrue(callbackReceiver.AfterDeserialize);
                    break;
            }
        }

#if UNITY_EDITOR
        void ISceneSerializeTestScenario.CheckAssetPack(AssetPack assetPack)
        {
            // Expect default skybox to be included in asset pack
            Assert.AreEqual(1, assetPack.AssetCount);
        }
#endif
    }

#if UNITY_EDITOR
    class SerializedReferenceScenario : ISceneSerializeTestScenario
    {
        string ISceneSerializeTestScenario.Name => "SerializedReferenceScenario";

        void ISceneSerializeTestScenario.BuildContent()
        {
            var go = new GameObject("GoWithSRMonoBehaviour");
            var comp = go.AddComponent<MonoBehaviourSerializedRef>();
            comp.SetKnownState();
        }

        void ISceneSerializeTestScenario.CompareWithOriginalContent(ScenarioTestPhase phase)
        {
            var go = GameObject.Find("GoWithSRMonoBehaviour");
            Assert.IsNotNull(go);

            var comp = go.GetComponent<MonoBehaviourSerializedRef>();
            comp.TestKnownState();
        }

        void ISceneSerializeTestScenario.CheckAssetPack(AssetPack assetPack)
        {
            // Expect default skybox to be included in asset pack
            Assert.AreEqual(1, assetPack.AssetCount);
        }
    }
#endif
}
