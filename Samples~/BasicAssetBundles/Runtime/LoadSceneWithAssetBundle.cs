using System.IO;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Test
{
    class LoadSceneWithAssetBundle : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        string m_ScenePath;

        [SerializeField]
        string m_AssetBundlePath;
#pragma warning restore 649

        void Awake()
        {
            if (string.IsNullOrEmpty(m_ScenePath) || string.IsNullOrEmpty(m_AssetBundlePath))
            {
                Debug.LogError("You must set up the scene and asset bundle path");
                return;
            }

            PropertyBagOverrides.InitializeOverrides();
            var sceneJson = File.ReadAllText(m_ScenePath);
            var assetBundle = AssetBundle.LoadFromFile(m_AssetBundlePath);
            var assetPack = (AssetPack) assetBundle.LoadAllAssets()[0];
            SceneSerialization.ImportScene(sceneJson, assetPack);
        }
    }
}
