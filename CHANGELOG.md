# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
