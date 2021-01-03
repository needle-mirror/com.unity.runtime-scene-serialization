using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.RuntimeSceneSerialization.EditorInternal
{
    static class MenuItems
    {
        const string k_Extension = "json";

        [MenuItem("File/Save JSON Scene...")]
        static void SaveJsonScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            var path = EditorUtility.SaveFilePanel(
                "Save scene as JSON",
                Application.dataPath,
                activeScene.name,
                k_Extension);

            if (string.IsNullOrEmpty(path))
                return;

            var assetPackPath = Path.ChangeExtension(path, ".asset");
            assetPackPath = assetPackPath.Replace(Application.dataPath, "Assets");

            var assetPack = AssetDatabase.LoadAssetAtPath<AssetPack>(assetPackPath);
            var created = false;
            if (assetPack == null)
            {
                created = true;
                assetPack = ScriptableObject.CreateInstance<AssetPack>();
            }
            else
            {
                assetPack.Clear();
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(activeScene.path);
            if (sceneAsset != null)
                assetPack.SceneAsset = sceneAsset;

            File.WriteAllText(path, SceneSerialization.SerializeScene(activeScene, assetPack));

            if (created)
            {
                if (assetPack.AssetCount > 0)
                    AssetDatabase.CreateAsset(assetPack, assetPackPath);
            }
            else
            {
                if (assetPack.AssetCount > 0)
                    EditorUtility.SetDirty(assetPack);
                else if (AssetDatabase.LoadAssetAtPath<AssetPack>(assetPackPath) != null)
                    AssetDatabase.DeleteAsset(assetPackPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("File/Open JSON Scene...")]
        static void OpenJsonScene()
        {
            var path = EditorUtility.OpenFilePanel("Open JSON scene", Application.dataPath, k_Extension);
            if (!string.IsNullOrEmpty(path))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                var assetPackPath = Path.ChangeExtension(path, ".asset");
                assetPackPath = assetPackPath.Replace(Application.dataPath, "Assets");
                var assetPack = AssetDatabase.LoadAssetAtPath<AssetPack>(assetPackPath);
                var jsonText = File.ReadAllText(path);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                SceneSerialization.ImportScene(jsonText, assetPack);
                scene.name = Path.GetFileNameWithoutExtension(path);
            }
        }
    }
}
