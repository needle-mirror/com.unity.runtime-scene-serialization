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
            yield return new LoadLegacyScene();

#if UNITY_EDITOR
            yield return new SerializedReferenceScenario();
#endif
        }
    }

    class BasicGameObject : ISceneSerializeTestScenario
    {
        string ISceneSerializeTestScenario.Name => "BasicGameObject";

        const string k_GameObjectName = "MyGo";
        static readonly Vector3 k_ExpectedPosition = new Vector3(1.0f, 1.1f, 1.2f);
        const int k_ExpectedLayer = 3;
        const string k_ExpectedTag = "MainCamera";

#if UNITY_EDITOR
        const HideFlags k_ExpectedHideFlags = HideFlags.NotEditable;
#endif

        void ISceneSerializeTestScenario.BuildContent()
        {
            var go = new GameObject(k_GameObjectName);
            go.layer = k_ExpectedLayer;
            go.tag = k_ExpectedTag;
            go.transform.position = k_ExpectedPosition;

#if UNITY_EDITOR
            go.hideFlags = k_ExpectedHideFlags;
#endif
        }

        void ISceneSerializeTestScenario.CompareWithOriginalContent(ScenarioTestPhase phase)
        {
            var go = GameObject.Find(k_GameObjectName);
            Assert.IsNotNull(go);
            Assert.AreEqual(k_ExpectedLayer, go.layer);
            Assert.AreEqual(k_ExpectedTag, go.tag);
            Assert.AreEqual(k_ExpectedPosition, go.transform.position);

#if UNITY_EDITOR
            Assert.AreEqual(k_ExpectedHideFlags, go.hideFlags);
#endif

            var emptyName = GameObject.Find(string.Empty);
            Assert.IsNull(emptyName);
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

    class LoadLegacyScene : ISceneSerializeTestScenario
    {
        const string k_LegacyScenePath = "RuntimeSceneSerializationTests/LegacyScene";
        const string k_LegacySceneAssetPackPath = "RuntimeSceneSerializationTests/LegacySceneAssetPack";
        const string k_CapsuleName = "Capsule";
        const string k_CubeName = "Cube";

        string ISceneSerializeTestScenario.Name => "LoadLegacyScene";

        void ISceneSerializeTestScenario.BuildContent()
        {
            var json = Resources.Load<TextAsset>(k_LegacyScenePath).text;
            var assetPack = Resources.Load<AssetPack>(k_LegacySceneAssetPackPath);
            SceneSerialization.ImportScene(json, assetPack);
        }

        void ISceneSerializeTestScenario.CompareWithOriginalContent(ScenarioTestPhase phase)
        {
            var capsule = GameObject.Find(k_CapsuleName);
            Assert.IsNotNull(capsule);

            var cube = GameObject.Find(k_CubeName);
            Assert.IsNotNull(cube);

            var meshFilter = cube.GetComponent<MeshFilter>();
            Assert.IsNotNull(meshFilter);
            Assert.IsNotNull(meshFilter.sharedMesh);
        }

#if UNITY_EDITOR
        void ISceneSerializeTestScenario.CheckAssetPack(AssetPack assetPack)
        {
            // Expect Capsule prefab, cube mesh, and default material
            Assert.AreEqual(3, assetPack.AssetCount);
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
