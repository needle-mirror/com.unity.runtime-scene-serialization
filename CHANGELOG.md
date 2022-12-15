# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.0.0-exp.3] - 2022-12-15
### Fixed
- Fix an issue when deserializing missing scripts with empty arrays

## [1.0.0-exp.2] - 2022-12-13
### Fixed
- Protect against token mismatch (null arrays) in prefab deserialization.
- Use TryGetValue when accessing fields to fix MissingFieldException when loading some legacy JSON scenes

## [1.0.0-exp.1] - 2022-11-17
### Fixed
- Removed check for negative fileID; it was blocking valid asset references from being serialized.

### Changed
- Minimum Editor version to 2021.3.0f1
- Properties and Serialization dependency version to 2.0.0-exp.13
- Use JsonSerializationAdapters to provide customized serialization features instead of custom visitors

## [0.6.2-preview] - 2022-11-17
### Fixed
- Removed check for negative fileID; it was blocking valid asset references from being serialized.

## [0.6.1-preview] - 2022-07-31
### Fixed
- Issues with MonoBehaviours from pre-compiled DLLs in AOT builds.
- StackOverflowExceptions when serializing prefabs with added child objects which are also prefabs.

## [0.6.0-preview] - 2022-05-12
### Changed
- Properties and Serialization dependency version to 1.8.2-preview

## [0.5.0-preview] - 2022-05-12
### Changed
- Properties and Serialization dependency version to 1.7.0-preview

## [0.4.0-preview] - 2022-05-12
### Changed
- Minimum Editor version to 2020.1.0f1
- Properties and Serialization dependency version to 1.6.0-preview

## [0.3.7-preview] - 2022-11-17
### Fixed
- Removed check for negative fileID; it was blocking valid asset references from being serialized.

## [0.3.6-preview] - 2022-07-31
### Fixed
- Issues with MonoBehaviours from pre-compiled DLLs in AOT builds.
- StackOverflowExceptions when serializing prefabs with added child objects which are also prefabs.

## [0.3.5-preview] - 2022-05-05
### Added
- SceneSerialization.FromJsonOverride method for deserializing to existing containers
- Basic AssetBundles example

### Fixed
- Issues serializing very large or very small `ulong`, `decimal`, `double`, and `float` values
- Issue where generated property bags for built-in types would be empty if those types had `UnityEngine.Object` fields or properties stripped.
- Issues with importing scenes immediately on app start in Awake

## [0.3.4-preview] - 2022-03-02
### Added
- Improvements to documentation explaining the use of AssetPacks

### Changed
- Use UnsafeBuffer instead of MemoryStream in deserialization methods for better performance

## [0.3.3-preview] - 2022-02-15
### Added
- Improvements to documentation for AOT build steps

### Fixed
- Issue where assets were missing from Asset Packs on first save
- Compile errors in player test builds
- Issues with invalid field names in `NativeName` attribute (fields in Matrix4x4)

## [0.3.2-preview] - 2022-01-11
### Fixed
- Warnings about inconsistent line endings

## [0.3.1-preview] - 2021-12-09
### Fixed
- Issue where scenes with only prefabs would not create an asset pack on save

## [0.3.0-preview] - 2021-12-06
### Fixed
- NullReferenceException when deserializing certain scenes
- Compile errors in 2022.1

## [0.2.4-preview] - 2021-10-05
### Added
- SerializedRenderSettings struct and m_RenderSettings field in SceneContainer

## [0.2.3-preview] - 2021-07-29
### Fixed
- Issues with settings provider

## [0.2.2-preview] - 2021-07-29
### Added
- RuntimeSerializationLogs.txt to collect statistics on codegen

### Changed
- All assemblies are now excluded from codegen by default, and must be explicitly included in Runtime Serialization Settings

### Fixed
- Issues in ILPostProcessor code caused by changes introduced in Unity 2020
- Issue where assembly exceptions were not preventing codegen on user assemblies

## [0.2.1-preview] - 2021-01-03
### Added
- Runtime Scene Serialization section in Project Settings for choosing AOT code generation exceptions

### Changed
- Gracefully fail for component types which do not have generated PropertyBags
- Rename MissingMonoBehaviour to MissingComponent

## [0.2.0-preview] - 2020-12-14
### Updated
- Introduce `SerializationMetadata` to replace static fields like `AssetPack.CurrentAssetPack`
- Reduce public API surface
- Add XML Documents where necessary
- Re-organize folder structure

## [0.1.5-preview] - 2020-10-22
### Fixed
- Compile errors in 2020.2

## [0.1.4-preview] - 2020-09-25
### Added
- RegisterPropertyBag and RegisterPropertyBagRecursively methods

### Fixed
- Null reference exceptions in prefab serialization and deserialization

## [0.1.3-preview] - 2020-09-17
### Added
- IFormatVersion interface to allow serialized type check for changes in format and handle migration or throw an exception
- Public getters and/or setters for properties of RuntimePrefabPropertyOverride and related types
- Public access to all RuntimePrefabPropertyOverride types
- PrefabMetadata.SetOverride and public Create and Update methods for RuntimePrefabPropertyOverride
- Support for missing scripts during serialization and deserialization
  - Missing scripts no longer break deserialization
  - MissingMonoBehaviour component stores JsonString version of missing script to prevent it from being stripped

### Changed
- Scene container property names--this will break scenes with the older format
- Color prefab overrides are now correctly treated as lists

### Fixed
- Compile error in IL2CPP builds

## [0.1.2-preview] - 2020-09-15
### Changed
- Use file name to set scene name when opening Json scenes from the file menu

### Removed
- SceneContainer.name property which was responsible for including scene names in serialized scenes

## [0.1.1-preview] - 2020-09-14
### Added
- ReflectedPropertyBagProvider and related infrastructure as an alternative to generated property bags; eliminates extra codegen-related compile time

### Fixed
- Issues with external container types on IL2CPP

## [0.1.0-preview] - 2020-08-27

### This is the first release of *Unity Runtime Scene Serialization*.

This package contains a basic API for saving and loading Unity scenes to and from JSON in Player builds
