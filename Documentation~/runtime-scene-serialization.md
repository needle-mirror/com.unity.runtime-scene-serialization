### About Unity Runtime Scene Serialization
Use Runtime Scene Serialization to save and load scenes in Unity Player builds.

To test this functionality in the Editor, try opening a scene and going to *File > Save JSON Scene...*. Make a new scene, then *Choose File > Open JSON Scene...* choose the scene you just saved, and you should see the same objects as if you had opened the Unity scene.

To use this in your project, simply call `SerializationUtils.SerializeScene` on the curently loaded scene you would like to serialize, and call `SerializationUtils.DeserializeScene` to parse a JSON scene and create the objects within.

<a name="Installation"></a>

## Installation

To install this package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Manual/upm-ui-install.html).

## Requirements

This version of Runtime Scene Serialization is compatible with the following versions of the Unity Editor:

* 2019.3 and later (recommended)

## Known limitations

Runtime Scene Serialization version 0.1.0-preview includes the following known limitations:

* Some properties like lightmap settings on renderers are not accessible from C# and cannot be serialized with this package
* There may be some interference with DOTS serialization because of the types of property bags generated by this package