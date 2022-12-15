using System;
using UnityEngine;
using UnityEngine.Scripting;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    interface IRuntimePrefabOverride<out TValue>
    {
        TValue Value { get; }
    }

    // NB: Changing the type names in this file will break backwards compatibility as they are written into serialized scenes
    [Serializable]
    class RuntimePrefabOverrideUnityObjectReference : RuntimePrefabPropertyOverride<UnityObject>, IRuntimePrefabOverride<UnityObject>
    {
        public RuntimePrefabOverrideUnityObjectReference() { }
        public RuntimePrefabOverrideUnityObjectReference(string propertyPath, string transformPath, int componentIndex,
            UnityObject value) : base(propertyPath, transformPath, componentIndex, value) { }

        [Preserve]
        void Unused<T>()
        {
            var unused = new RuntimePrefabOverridePropertyVisitor<UnityObject>(default, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<UnityObject>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null);
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
            var unused = new RuntimePrefabOverridePropertyVisitor<long>(default, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<long>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null);
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
            var unused = new RuntimePrefabOverridePropertyVisitor<bool>(default, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<bool>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null);
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
            var unused = new RuntimePrefabOverridePropertyVisitor<float>(default, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<float>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null);
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
            var unused = new RuntimePrefabOverridePropertyVisitor<string>(default, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<string>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null);
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
            var unused = new RuntimePrefabOverridePropertyVisitor<int>(default, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<int>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null);
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
            var unused = new RuntimePrefabOverridePropertyVisitor<char>(default, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<char>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null);
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
            var unused = new RuntimePrefabOverridePropertyVisitor<AnimationCurve>(default, null);
            var unused2 = new RuntimePrefabOverridePropertyVisitor<AnimationCurve>.DefaultValueOverrideVisitor();
            var container = default(T);
            SetProperty(ref container, null);
        }
    }
}
