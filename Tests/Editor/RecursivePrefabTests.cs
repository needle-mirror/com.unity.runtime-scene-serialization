using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Tests.Editor
{
    class RecursivePrefabTests
    {
        const string k_RecursivePrefabPath = "RuntimeSceneSerializationTests/Recursive Prefab";

        [Test]
        public void SaveLoadRecursivePrefab()
        {
            var prefab = Resources.Load<GameObject>(k_RecursivePrefabPath);
            Assert.IsNotNull(prefab);
            var parent = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            const string parentName = "Recursive Prefab Parent";
            parent.name = parentName;
            var child = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            child.name = "Recursive Prefab Child";
            child.transform.parent = parent.transform;

            var assetPack = ScriptableObject.CreateInstance<AssetPack>();
            var activeScene = SceneManager.GetActiveScene();
            var json = SceneSerialization.SerializeScene(activeScene, assetPack: assetPack);
            foreach (var rootGameObject in activeScene.GetRootGameObjects())
            {
                UnityObject.DestroyImmediate(rootGameObject);
            }

            var meta = SceneSerialization.ImportScene(json, assetPack);
            Assert.IsNotNull(meta);

            parent = GameObject.Find(parentName);
            Assert.IsNotNull(parent);
            Assert.AreEqual(1, parent.transform.childCount);
        }
    }
}
