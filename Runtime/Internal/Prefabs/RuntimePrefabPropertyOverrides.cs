using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    interface IRuntimePrefabOverrideUnityObjectReference
    {
        UnityObjectReference Value { get; }
        void ConvertToUnityObjectOverride(List<RuntimePrefabPropertyOverride> list, int i, SerializationMetadata metadata);
    }

    interface IRuntimePrefabOverrideUnityObject
    {
        UnityObject Value { get; }
        void ConvertToUnityObjectReferenceOverride(List<RuntimePrefabPropertyOverride> list, int i, SerializationMetadata metadata);
    }

    interface IRuntimePrefabOverride<out TValue>
    {
        TValue Value { get; }
    }

    // NB: Changing the type names in this file will break backwards compatibility as they are written into serialized scenes
    [Serializable]
    class RuntimePrefabOverrideUnityObjectReference : RuntimePrefabPropertyOverride<UnityObjectReference>,
        IRuntimePrefabOverrideUnityObjectReference
    {
        public RuntimePrefabOverrideUnityObjectReference() { }
        public RuntimePrefabOverrideUnityObjectReference(string propertyPath, string transformPath, int componentIndex,
            UnityObjectReference value) : base(propertyPath, transformPath, componentIndex, value) { }

        public void ConvertToUnityObjectOverride(List<RuntimePrefabPropertyOverride> list, int i, SerializationMetadata metadata)
        {
            var objectReference = m_Value;
            var guid = objectReference.guid;
            if (!string.IsNullOrEmpty(guid))
            {
                list[i] = new RuntimePrefabOverrideUnityObject(PropertyPath, TransformPath, ComponentIndex,
                    UnityObjectReference.GetAsset(guid, objectReference.fileId, metadata.AssetPack));

                return;
            }

            var sceneId = objectReference.sceneID;
            if (sceneId == SerializationMetadata.InvalidID)
                return;

            list[i] = null;

            var index = i;
            metadata.EnqueuePostSerializationAction(() =>
            {
                list[index] = new RuntimePrefabOverrideUnityObject(PropertyPath, TransformPath, ComponentIndex,
                    metadata.GetSceneObject(sceneId));
            });
        }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<T>(default, null, null);
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }

    [Serializable]
    class RuntimePrefabOverrideUnityObject
        : RuntimePrefabPropertyOverride<UnityObject>, IRuntimePrefabOverrideUnityObject
    {
        public RuntimePrefabOverrideUnityObject() { }
        public RuntimePrefabOverrideUnityObject(string propertyPath, string transformPath, int componentIndex,
            UnityObject value) : base(propertyPath, transformPath, componentIndex, value) { }

        public void ConvertToUnityObjectReferenceOverride(List<RuntimePrefabPropertyOverride> list, int i, SerializationMetadata metadata)
        {
            var unityObjectReference = UnityObjectReference.GetReferenceForObject(Value, metadata);
            list[i] = new RuntimePrefabOverrideUnityObjectReference(PropertyPath, TransformPath, ComponentIndex, unityObjectReference);
        }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<UnityObject>(default, null, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<UnityObject>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }

    [Serializable]
    class RuntimePrefabOverrideLong : RuntimePrefabPropertyOverride<long>, IRuntimePrefabOverride<long>
    {
        public RuntimePrefabOverrideLong() { }
        public RuntimePrefabOverrideLong(string propertyPath, string transformPath, int componentIndex, long value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<long>(default, null, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<long>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }

    [Serializable]
    class RuntimePrefabOverrideBool : RuntimePrefabPropertyOverride<bool>, IRuntimePrefabOverride<bool>
    {
        public RuntimePrefabOverrideBool() { }
        public RuntimePrefabOverrideBool(string propertyPath, string transformPath, int componentIndex, bool value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<bool>(default, null, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<bool>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }

    [Serializable]
    class RuntimePrefabOverrideFloat : RuntimePrefabPropertyOverride<float>, IRuntimePrefabOverride<float>
    {
        public RuntimePrefabOverrideFloat() { }
        public RuntimePrefabOverrideFloat(string propertyPath, string transformPath, int componentIndex, float value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<float>(default, null, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<float>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }

    [Serializable]
    class RuntimePrefabOverrideString : RuntimePrefabPropertyOverride<string>, IRuntimePrefabOverride<string>
    {
        public RuntimePrefabOverrideString() { }
        public RuntimePrefabOverrideString(string propertyPath, string transformPath, int componentIndex, string value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<string>(default, null, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<string>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }

    [Serializable]
    class RuntimePrefabOverrideInt : RuntimePrefabPropertyOverride<int>, IRuntimePrefabOverride<int>
    {
        public RuntimePrefabOverrideInt() { }
        public RuntimePrefabOverrideInt(string propertyPath, string transformPath, int componentIndex, int value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<int>(default, null, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<int>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }

    [Serializable]
    class RuntimePrefabOverrideChar : RuntimePrefabPropertyOverride<char>, IRuntimePrefabOverride<char>
    {
        public RuntimePrefabOverrideChar() { }
        public RuntimePrefabOverrideChar(string propertyPath, string transformPath, int componentIndex, char value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<char>(default, null, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<char>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }

    [Serializable]
    class RuntimePrefabOverrideAnimationCurve : RuntimePrefabPropertyOverride<AnimationCurve>, IRuntimePrefabOverride<AnimationCurve>
    {
        public RuntimePrefabOverrideAnimationCurve() { }
        public RuntimePrefabOverrideAnimationCurve(string propertyPath, string transformPath, int componentIndex,
            AnimationCurve value) : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<AnimationCurve>(default, null, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<AnimationCurve>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }
}
