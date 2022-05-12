using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization
{
    static class BuildAssetBundles
    {
        [MenuItem("Assets/Serialization/Build AssetBundles", false)]
        static void Build()
        {
            var builds = new List<AssetBundleBuild>();
            foreach (var selected in Selection.objects)
            {
                if (selected is AssetPack assetPack)
                {
                    var path = AssetDatabase.GetAssetPath(assetPack);
                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.LogError($"Could not get asset path for {assetPack}");
                        continue;
                    }

                    var guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid))
                    {
                        Debug.LogError($"Could not get valid guid for asset at path {path}");
                        continue;
                    }

                    // Check existing asset bundle name to avoid overwriting it
                    var assetImporter = AssetImporter.GetAtPath(path);
                    if (!string.IsNullOrEmpty(assetImporter.assetBundleName) && assetImporter.assetBundleName != guid)
                    {
                        Debug.LogError(assetPack.name + " is already part of an AssetBundle, and cannot be built to without overwriting its AssetBundle name. You need to temporarily set its AssetBundle name to None in the inspector in order to build this asset.");
                        continue;
                    }

                    builds.Add(new AssetBundleBuild
                    {
                        assetBundleName = path,
                        assetNames = new[] {path}
                    });
                }
            }

            var outputPath = EditorUtility.SaveFolderPanel("Build AssetBundles",  new DirectoryInfo(Application.dataPath).Parent.FullName, "Bundles");
            if (string.IsNullOrEmpty(outputPath))
                return;

            Debug.Log("Building AssetBundles");
            var manifest = BuildPipeline.BuildAssetBundles(outputPath, builds.ToArray(), BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
            if (manifest == null)
                throw new BuildFailedException("Failed to build AssetBundles");
        }

        [MenuItem("Assets/Serialization/Build AssetBundles", true)]
        static bool ValidateBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            var isValid = false;
            foreach (var selected in Selection.objects)
            {
                if (selected is AssetPack)
                {
                    isValid = true;
                    break;
                }
            }

            return isValid;
        }
    }
}
