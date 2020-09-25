using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Prefabs
{
    interface IRuntimePrefabOverrideUnityObjectReference
    {
        UnityObjectReference Value { get; }
        void ConvertToUnityObjectOverride(List<RuntimePrefabPropertyOverride> list, int i);
    }

    interface IRuntimePrefabOverrideUnityObject
    {
        UnityObject Value { get; }
        void ConvertToUnityObjectReferenceOverride(List<RuntimePrefabPropertyOverride> list, int i);
    }

    interface IRuntimePrefabOverride<out TValue>
    {
        TValue Value { get; }
    }

    [Serializable]
    public class RuntimePrefabOverrideUnityObjectReference
        : RuntimePrefabPropertyOverride<UnityObjectReference>, IRuntimePrefabOverrideUnityObjectReference
    {
        public RuntimePrefabOverrideUnityObjectReference() { }
        public RuntimePrefabOverrideUnityObjectReference(string propertyPath, string transformPath, int componentIndex,
            UnityObjectReference value) : base(propertyPath, transformPath, componentIndex, value) { }

        public void ConvertToUnityObjectOverride(List<RuntimePrefabPropertyOverride> list, int i)
        {
            var objectReference = m_Value;
            var guid = objectReference.guid;
            if (!string.IsNullOrEmpty(guid))
            {
                list[i] = new RuntimePrefabOverrideUnityObject(PropertyPath, TransformPath, ComponentIndex,
                    UnityObjectReference.GetAsset(guid, objectReference.fileId, AssetPack.CurrentAssetPack));

                return;
            }

            list[i] = null;
            var sceneId = objectReference.sceneID;
            if (sceneId == UnityObjectReference.InvalidSceneID)
                return;

            var index = i;
            EditorMetadata.SceneReferenceActions.Enqueue(() =>
            {
                list[index] = new RuntimePrefabOverrideUnityObject(PropertyPath, TransformPath, ComponentIndex,
                    UnityObjectReference.GetSceneObject(sceneId));
            });
        }

        [Preserve]
        void Unused<T>()
        {
            var unused = new SetPropertyVisitor(null, null);

            var container = default(T);
            SetProperty(ref container, null);
        }
    }

    [Serializable]
    public class RuntimePrefabOverrideUnityObject
        : RuntimePrefabPropertyOverride<UnityObject>, IRuntimePrefabOverrideUnityObject
    {
        public RuntimePrefabOverrideUnityObject() { }
        public RuntimePrefabOverrideUnityObject(string propertyPath, string transformPath, int componentIndex,
            UnityObject value) : base(propertyPath, transformPath, componentIndex, value) { }

        public void ConvertToUnityObjectReferenceOverride(List<RuntimePrefabPropertyOverride> list, int i)
        {
            var unityObjectReference = UnityObjectReference.GetReferenceForObject(Value);
            list[i] = new RuntimePrefabOverrideUnityObjectReference(PropertyPath, TransformPath, ComponentIndex, unityObjectReference);
        }

        [Preserve]
        void Unused<T>()
        {
            var unused = new SetPropertyVisitor(null, null);

            var container = default(T);
            SetProperty(ref container, null);
        }
    }

    [Serializable]
    public class RuntimePrefabOverrideLong : RuntimePrefabPropertyOverride<long>, IRuntimePrefabOverride<long>
    {
        public RuntimePrefabOverrideLong() { }
        public RuntimePrefabOverrideLong(string propertyPath, string transformPath, int componentIndex, long value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new SetPropertyVisitor(null, null);

            var container = default(T);
            SetProperty(ref container, null);
        }
    }

    [Serializable]
    public class RuntimePrefabOverrideBool : RuntimePrefabPropertyOverride<bool>, IRuntimePrefabOverride<bool>
    {
        public RuntimePrefabOverrideBool() { }
        public RuntimePrefabOverrideBool(string propertyPath, string transformPath, int componentIndex, bool value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new SetPropertyVisitor(null, null);

            var container = default(T);
            SetProperty(ref container, null);
        }
    }

    [Serializable]
    public class RuntimePrefabOverrideFloat : RuntimePrefabPropertyOverride<float>, IRuntimePrefabOverride<float>
    {
        public RuntimePrefabOverrideFloat() { }
        public RuntimePrefabOverrideFloat(string propertyPath, string transformPath, int componentIndex, float value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new SetPropertyVisitor(null, null);

            var container = default(T);
            SetProperty(ref container, null);
        }
    }

    [Serializable]
    public class RuntimePrefabOverrideString : RuntimePrefabPropertyOverride<string>, IRuntimePrefabOverride<string>
    {
        public RuntimePrefabOverrideString() { }
        public RuntimePrefabOverrideString(string propertyPath, string transformPath, int componentIndex, string value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new SetPropertyVisitor(null, null);

            var container = default(T);
            SetProperty(ref container, null);
        }
    }

    [Serializable]
    public class RuntimePrefabOverrideInt : RuntimePrefabPropertyOverride<int>, IRuntimePrefabOverride<int>
    {
        public RuntimePrefabOverrideInt() { }
        public RuntimePrefabOverrideInt(string propertyPath, string transformPath, int componentIndex, int value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new SetPropertyVisitor(null, null);

            var container = default(T);
            SetProperty(ref container, null);
        }
    }

    [Serializable]
    public class RuntimePrefabOverrideChar : RuntimePrefabPropertyOverride<char>, IRuntimePrefabOverride<char>
    {
        public RuntimePrefabOverrideChar() { }
        public RuntimePrefabOverrideChar(string propertyPath, string transformPath, int componentIndex, char value)
            : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new SetPropertyVisitor(null, null);

            var container = default(T);
            SetProperty(ref container, null);
        }
    }

    [Serializable]
    public class RuntimePrefabOverrideAnimationCurve : RuntimePrefabPropertyOverride<AnimationCurve>, IRuntimePrefabOverride<AnimationCurve>
    {
        public RuntimePrefabOverrideAnimationCurve() { }
        public RuntimePrefabOverrideAnimationCurve(string propertyPath, string transformPath, int componentIndex,
            AnimationCurve value) : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new SetPropertyVisitor(null, null);

            var container = default(T);
            SetProperty(ref container, null);
        }
    }
}
