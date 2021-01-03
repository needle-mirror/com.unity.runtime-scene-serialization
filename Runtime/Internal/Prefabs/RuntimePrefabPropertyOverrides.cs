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

            list[i] = null;
            var sceneId = objectReference.sceneID;
            if (sceneId == SerializationMetadata.InvalidID)
                return;

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
            var unused = new SetPropertyVisitor(default, null, null);
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
            var unused = new SetPropertyVisitor(default, null, null);
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
            var unused = new SetPropertyVisitor(default, null, null);
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
            var unused = new SetPropertyVisitor(default, null, null);
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
            var unused = new SetPropertyVisitor(default, null, null);
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
            var unused = new SetPropertyVisitor(default, null, null);
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
            var unused = new SetPropertyVisitor(default, null, null);
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
            var unused = new SetPropertyVisitor(default, null, null);
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
            var unused = new SetPropertyVisitor(default, null, null);
            var container = default(T);
            SetProperty(ref container, null, null);
        }
    }
}
