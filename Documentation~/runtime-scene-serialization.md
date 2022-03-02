### About Unity Runtime Scene Serialization
Use Runtime Scene Serialization to save and load scenes in Unity Player builds.

To test this functionality in the Editor, try opening a scene and going to *File > Save JSON Scene...*. Make a new scene, then *Choose File > Open JSON Scene...* choose the scene you just saved, and you should see the same objects as if you had opened the Unity scene.

To use this in your project, simply call `SceneSerialization.SerializeScene` on the currently loaded scene you would like to serialize, and call `SceneSerialization.DeserializeScene` to parse a JSON scene and create the objects within.

## Extra Steps for AOT Builds
This package includes ILPostProcessor implementations which generate code in order to visit properties for any type on IL2CPP without using reflection. This process can add a significant amount of time to IL2CPP builds, and will increase the build size by a small amount. Both of these effects scale up with the number of serializable types in your project.

### Runtime Scene Serialization Settings

You can customize this process by going to *Edit > Project Settings... > Runtime Scene Serialization*. By default, the system will not generate property bags (needed for visitation) for any assemblies in your project. You need to enable each an assembly as needed for serialization to work in IL2CPP builds. Within each assembly, you can disable a namespace or individual type by toggling the checkbox in the right column. Note that disabling a namespace in one assembly will exclude types in another assembly that belong to that namespace.

**You must manually choose which assemblies and types will be processed for code generation. Without this step, you will see errors about missing property bags in IL2CPP builds and serialization/deserialization will fail**

### Stub script

In order to prevent the code in the `Unity.RuntimeSceneSerialization.Generated` assembly from being stripped, we need to reference its code in a scene that will be built into the Player. There is a script called `Stub` in this assembly which must be added to at least one scene that is included in the build.

**Failure to include the `Stub` script will cause APIs in this package to throw `MissingPropertyBagExceptions` on AOT platforms.**

### Using an AssetPack to collect asset references
If your scene has any asset references (almost all scenes do) you will need to use an `AssetPack`. This is a type of `ScriptableObject` defined by this package which is used to store a mapping of Asset Database GUID and FileID values to a serialized reference for that object. The `SerializeScene` and `ImportScene` APIs both include optional arguments for an AssetPack, which must be provided for serializing and deserializing asset references in play mode or Player builds.

Because the Asset Database only exists in the Unity Editor, Player builds have no way to find an asset based on its GUID. To solve this problem, Player builds must include a reference to one or more AssetPacks which contain references to all of the assets needed for runtime scene serialization. These can be included either by referencing them in build scenes, or by building AssetBundles containing one or more AssetPacks.

For an example of how to create, use, and save AssetPacks, refer to `Editor/MenuItems.cs` within this package. The save and load methods associate an AssetPack asset with each json scene. The AssetPack contains a reference to the scene that was used to populate it, but this is only a hint for certain workflows. The `m_SceneAsset` field is not required to be set in order for the AssetPack to function.

The AR Companion Resource Manager (part of `com.unity.ar-companion-core`) is an example of using AssetBundles to export scenes and prefabs from the Editor which can be loaded in builds of the app. Although the resource manager is a rather complex use case, some project-specific code will always be required to support building and loading of AssetBundles for AssetPacks.

#### IPrefabFactory

As an extension for AssetPacks, the `IPrefabFactory` API can be used to write custom code that provides prefab references on deserialization. To use this feature, define a class which implements the `IPrefabFactory` interface, and use the `RegisterPrefabFactory` method on the AssetPack being used to deserialize the scene before calling ImportScene. When deserializing a prefab, first the AssetPack is checked for a prefab with the corresponding GUID, and if it is not found, the prefab factories are queried. Because a HashSet is used to contain the list of prefab factories, they are generally queried in the order they were added, but this is not guaranteed. 

<a name="Installation"></a>

## Installation

To install this package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Manual/upm-ui-install.html).

## Requirements

This version of Runtime Scene Serialization is compatible with the following versions of the Unity Editor:

* 2019.3 and later (recommended)

## Known limitations

Runtime Scene Serialization version 0.3.4-preview includes the following known limitations:

* Some properties like LightMap settings on renderers are not accessible from C# and cannot be serialized with this package
* There may be some interference with DOTS serialization because of the types of property bags generated by this package
