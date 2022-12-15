# About Unity Runtime Scene Serialization

Use Runtime Scene Serialization to save and load scenes in Unity Player builds.

To test this functionality in the Editor, try opening a scene and going to **File > Save JSON Scene**. Make a new scene, then **Choose File > Open JSON Scene** choose the scene you just saved, and you should see the same objects as if you had opened the Unity scene.

To use this in your project, simply call `SceneSerialization.SerializeScene` on the currently loaded scene you would like to serialize, and call `SceneSerialization.DeserializeScene` to parse a JSON scene and create the objects within.

## Extra Steps for AOT Builds
This package includes ILPostProcessor implementations which generate code in order to visit properties for any type on IL2CPP without using reflection. This process can add a significant amount of time to IL2CPP builds, and will increase the build size by a small amount. Both of these effects scale up with the number of serializable types in your project.

### Runtime Scene Serialization Settings

You can customize this process using the **Runtime Scene Serialization** settings for the project (menu: **Edit > Project Settings**). By default, the system does not generate property bags (needed for visitation) for any assemblies in your project. You must enable it for each assembly as needed for serialization to work in IL2CPP builds. Within each assembly, you can disable a namespace or individual type by toggling the checkbox in the right column of your Scene serialization settings. Note that disabling a namespace in one assembly excludes types that belong to that namespace in ALL assemblies.

> [!IMPORTANT]
> You must manually choose which assemblies and types will be processed for code generation. Without this step, you will see errors about missing property bags in IL2CPP builds and serialization/deserialization will fail.

### Stub script

In order to prevent the code in the `Unity.RuntimeSceneSerialization.Generated` assembly from being stripped from your Player build, you must make sure that the code in this assembly is referenced in a scene that is built into the Player. The Runtime Scene Serialization package provides a script called `Stub` that you can add to at least one scene to ensure that the serialization code is not stripped during the build. You must included the Scene in the build. See [Managed code stripping](https://docs.unity3d.com/Manual/ManagedCodeStripping.html) for more information about code stripping.

> [!IMPORTANT]
> Failure to include the `Stub` script in at least one Scene in your build will cause APIs in this package to throw `MissingPropertyBagExceptions` on AOT platforms.

## Using an AssetPack to collect asset references

If your scene has any asset references (almost all scenes do) you will need to use an `AssetPack`. This is a type of `ScriptableObject` defined by this package which is used to store a mapping of Asset Database GUID and FileID values to a serialized reference for that object. The `SerializeScene` and `ImportScene` APIs both include optional arguments for an AssetPack, which must be provided for serializing and deserializing asset references in play mode or Player builds.

Because the Asset Database only exists in the Unity Editor, Player builds have no way to find an asset based on its GUID. To solve this problem, Player builds must include a reference to one or more AssetPacks which contain references to all of the assets needed for runtime scene serialization. These can be included either by referencing them in build scenes, or by building AssetBundles containing one or more AssetPacks.

For an example of how to create, use, and save AssetPacks, refer to `Editor/MenuItems.cs` within this package. The save and load methods associate an AssetPack asset with each json scene. The AssetPack contains a reference to the scene that was used to populate it, but this is only a hint for certain workflows. The `m_SceneAsset` field is not required to be set in order for the AssetPack to function.

## Asset Bundles

In order to load AssetPacks at runtime, you must build AssetBundles for the AssetPacks.  

The **Basic Asset Bundles** example shows how to build and load AssetBundles for AssetPacks. The `BuildAssetBundles` script adds a context menu item for AssetPacks that you can use to build any selected AssetPacks into AssetBundles. The script places the AssetBundles inside a folder you choose. (The script saves the AssetBundles within this folder at the same relative paths as the AssetPack asset in your Project Asset folder in order to avoid collisions.) Use the `LoadSceneWithAssetBundle` script to provide asset references for importing the associated scene. 

To import the example code into your project:

1. Open the **Package Manager** (menu: **Window > Package Manager**).
2. Click the **Samples** heading to expand the section, if necessary.
3. Click the **Import** button next to the **Basic Asset Bundles** sample. 

To test the example code:

1. Save the desired scene as a JSON file (**File > Save JSON Scene**) somewhere in the Assets folder.
2. Right-click the associated AssetPack asset (a ScriptableObject with the same name as the JSON scene) and choose **Serialization > Build AssetBundles**.
3. Open the `BasicAssetBundles` scene included in the sample folder and set the fields of the `LoadSceneWithAssetBundle` script.
   - Set **Scene Path** to the path of the JSON scene file (for example, `/Users/user/project/Assets/test.json`).
   - Set **Asset Bundle Path** to the path of the AssetBundle file (for example, `/Users/user/project/Bundles/assets/test.asset`).
4. Set `BasicAssetBundles` as the first active build scene in the **Build Settings** window, build the project, and run the built executable.

## IPrefabFactory

As an extension for AssetPacks, the `IPrefabFactory` API can be used to write custom code that provides prefab references on deserialization. To use this feature, define a class which implements the `IPrefabFactory` interface, and use the `RegisterPrefabFactory` method on the AssetPack being used to deserialize the scene before calling ImportScene. When deserializing a prefab, first the AssetPack is checked for a prefab with the corresponding GUID, and if it is not found, the prefab factories are queried. Because a HashSet is used to contain the list of prefab factories, they are generally queried in the order they were added, but this is not guaranteed. 

<a name="Installation"></a>

## Installation

To install this package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Manual/upm-ui-install.html).

## Requirements

This version of Runtime Scene Serialization is compatible with the following versions of the Unity Editor:

* 2021.3 and later (recommended)

## Known limitations

Runtime Scene Serialization version 1.0.0-exp.1 includes the following known limitations:

* Some properties like LightMap settings on renderers are not accessible from C# and cannot be serialized with this package
* There may be some interference with DOTS serialization because of the types of property bags generated by this package
