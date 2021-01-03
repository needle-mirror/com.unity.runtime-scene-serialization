using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

#if !ENABLE_IL2CPP
using System.Reflection;
#endif

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    [Serializable]
    abstract class RuntimePrefabPropertyOverride
    {
#if UNITY_EDITOR
        static readonly MethodInfo k_GetOverridesMethod = typeof(RuntimePrefabPropertyOverride).GetMethods().First(x => x.IsGenericMethod && x.Name == nameof(GetOverrides));
        static readonly Dictionary<Type, MethodInfo> k_GetOverridesMethods = new Dictionary<Type, MethodInfo>();
        static readonly object[] k_GetOverridesArguments = new object[5];
#endif

        [SerializeField]
        protected string m_PropertyPath;

        [SerializeField]
        protected string m_TransformPath;

        [SerializeField]
        protected int m_ComponentIndex = -1;

        public string PropertyPath => m_PropertyPath;
        public string TransformPath => m_TransformPath;
        public int ComponentIndex => m_ComponentIndex;

        protected RuntimePrefabPropertyOverride() { }

        protected RuntimePrefabPropertyOverride(string propertyPath, string transformPath, int componentIndex = -1)
        {
            m_PropertyPath = propertyPath;
            m_TransformPath = transformPath;
            m_ComponentIndex = componentIndex;
        }

#if UNITY_EDITOR
        static RuntimePrefabPropertyOverride CreateMultiple<TContainer>(SerializedProperty property, string rootPropertyPath,
            string transformPath, int componentIndex, ref bool hasNext, ref bool shouldIterate, SerializationMetadata metadata)
            where TContainer : UnityObject
        {
            if (!hasNext)
            {
                Debug.LogError($"Tried to create {nameof(RuntimePrefabPropertyOverride)} with invalid iterator");
                return null;
            }

            var listOverride = new RuntimePrefabPropertyOverrideList(rootPropertyPath, transformPath, componentIndex);
            var list = listOverride.List;
            var startDepth = property.depth;
            do
            {
                if (shouldIterate)
                {
                    hasNext = property.Next(true);
                    if (!hasNext)
                        break;
                }

                shouldIterate = true;

                var propertyPath = property.propertyPath;
                var depth = property.depth;

                // Skip the .Array property to reduce nesting and avoid early-out based on depth check below
                if (depth == startDepth && propertyPath.EndsWith(".Array"))
                {
                    hasNext = property.Next(true);
                    if (!hasNext)
                        break;

                    depth = property.depth;
                    propertyPath = property.propertyPath;
                }

                if (depth <= startDepth)
                    break;

                if (!property.prefabOverride)
                    continue;

                var propertyOverride = Create<TContainer>(property, propertyPath, transformPath, componentIndex,
                    ref hasNext, out shouldIterate, metadata);

                if (propertyOverride != null)
                    list.Add(propertyOverride);

                if (!hasNext)
                    break;
            }
            while (property.depth > startDepth);

            return listOverride;
        }

        static RuntimePrefabPropertyOverride Create<TContainer>(SerializedProperty property, string propertyPath,
            string transformPath, int componentIndex, ref bool hasNext, out bool shouldIterate, SerializationMetadata metadata)
            where TContainer : UnityObject
        {
            shouldIterate = true;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Bounds:
                case SerializedPropertyType.Quaternion:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.Gradient:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                case SerializedPropertyType.RectInt:
                case SerializedPropertyType.BoundsInt:
                case SerializedPropertyType.Color:
                    var listOverride = CreateMultiple<TContainer>(property, propertyPath, transformPath,
                        componentIndex, ref hasNext, ref shouldIterate, metadata);

                    shouldIterate = false;
                    return listOverride;

                case SerializedPropertyType.Integer:
                    return new RuntimePrefabOverrideLong(propertyPath, transformPath, componentIndex, property.longValue);
                case SerializedPropertyType.Boolean:
                    return new RuntimePrefabOverrideBool(propertyPath, transformPath, componentIndex, property.boolValue);
                case SerializedPropertyType.Float:
                    // TODO: detect double precision
                    return new RuntimePrefabOverrideFloat(propertyPath, transformPath, componentIndex, property.floatValue);
                case SerializedPropertyType.String:
                    return new RuntimePrefabOverrideString(propertyPath, transformPath, componentIndex, property.stringValue);
                case SerializedPropertyType.ObjectReference:
                    var objectReference = UnityObjectReference.GetReferenceForObject(property.objectReferenceValue, metadata);
                    return new RuntimePrefabOverrideUnityObjectReference(propertyPath, transformPath, componentIndex, objectReference);
                case SerializedPropertyType.LayerMask:
                    return new RuntimePrefabOverrideInt(propertyPath, transformPath, componentIndex, property.intValue);
                case SerializedPropertyType.Enum:
                    return new RuntimePrefabOverrideLong(propertyPath, transformPath, componentIndex, property.enumValueIndex);
                case SerializedPropertyType.ArraySize:
                    return new RuntimePrefabOverrideInt(propertyPath, transformPath, componentIndex, property.intValue);
                case SerializedPropertyType.Character:
                    return new RuntimePrefabOverrideChar(propertyPath, transformPath, componentIndex, (char)property.intValue);
                case SerializedPropertyType.AnimationCurve:
                    return new RuntimePrefabOverrideAnimationCurve(propertyPath, transformPath, componentIndex, property.animationCurveValue);
                case SerializedPropertyType.ExposedReference:
                    var exposedReference = UnityObjectReference.GetReferenceForObject(property.exposedReferenceValue, metadata);
                    return new RuntimePrefabOverrideUnityObjectReference(propertyPath, transformPath, componentIndex, exposedReference);
                case SerializedPropertyType.FixedBufferSize:
                    return new RuntimePrefabOverrideInt(propertyPath, transformPath, componentIndex, property.fixedBufferSize);
                case SerializedPropertyType.ManagedReference:
                    Debug.LogWarning($"Encountered managed reference property override override for {propertyPath}");
                    return null;

                default:
                    Debug.LogWarning($"Unknown property type in prefab override for {propertyPath}");
                    return null;
            }
        }

        public static void GetOverrides(UnityObject instanceObject,
            List<RuntimePrefabPropertyOverride> overrides, string transformPath, int componentIndex, SerializationMetadata metadata)
        {
            var type = instanceObject.GetType();
            if (!k_GetOverridesMethods.TryGetValue(type, out var method))
            {
                method = k_GetOverridesMethod.MakeGenericMethod(type);
            }

            k_GetOverridesArguments[0] = instanceObject;
            k_GetOverridesArguments[1] = overrides;
            k_GetOverridesArguments[2] = transformPath;
            k_GetOverridesArguments[3] = componentIndex;
            k_GetOverridesArguments[4] = metadata;
            method.Invoke(null, k_GetOverridesArguments);
        }

        public static void GetOverrides<TContainer>(TContainer instanceObject,
            List<RuntimePrefabPropertyOverride> overrides, string transformPath, int componentIndex,
            SerializationMetadata metadata) where TContainer : UnityObject
        {
            var serializedObject = new SerializedObject(instanceObject);
            var property = serializedObject.GetIterator();
            var hasNext = property.Next(true);

            do
            {
                var shouldIterate = true;
                if (property.prefabOverride)
                {
                    var propertyOverride = Create<TContainer>(property, property.propertyPath, transformPath,
                        componentIndex, ref hasNext, out shouldIterate, metadata);

                    if (propertyOverride != null)
                        overrides.Add(propertyOverride);
                }

                if (hasNext && shouldIterate)
                    hasNext = property.Next(true);
            }
            while (hasNext);
        }
#endif
        public void ApplyOverride(Transform root, SerializationMetadata metadata)
        {
            var targetTransform = root.GetTransformAtPath(m_TransformPath);
            if (targetTransform == null)
            {
                Debug.LogError($"Failed to apply override {m_PropertyPath}. Could not find {m_TransformPath} in {root.name}");
                return;
            }

            var targetGameObject = targetTransform.gameObject;
            var target = targetGameObject.GetTargetObjectWithComponentIndex(m_ComponentIndex);
            if (target == null)
                return;

            var targetType = target.GetType();
            if (targetType == typeof(Transform))
            {
                // Skip root order property as it is handled by scene hierarchy
                if (PropertyPath.Contains("m_RootOrder"))
                    return;

                // TODO: Support Euler hint
                if (PropertyPath.Contains("m_LocalEulerAnglesHint"))
                    return;
            }

            ApplyOverrideToTarget(target, metadata);
        }

        protected internal abstract void ApplyOverrideToTarget(UnityObject target, SerializationMetadata metadata);

        public static RuntimePrefabPropertyOverride Create<TValue>(string propertyPath, string transformPath,
            int componentIndex, TValue value, SerializationMetadata metadata = null)
        {
            switch (value)
            {
                case Vector2 vector2:
                {
                    var @override = new RuntimePrefabPropertyOverrideList(propertyPath, transformPath, componentIndex);
                    var list = @override.List;
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.x", transformPath, componentIndex, vector2.x));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.y", transformPath, componentIndex, vector2.y));
                    return @override;
                }
                case Vector3 vector3:
                {
                    var @override = new RuntimePrefabPropertyOverrideList(propertyPath, transformPath, componentIndex);
                    var list = @override.List;
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.x", transformPath, componentIndex, vector3.x));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.y", transformPath, componentIndex, vector3.y));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.z", transformPath, componentIndex, vector3.z));
                    return @override;
                }
                case Vector4 vector4:
                {
                    var @override = new RuntimePrefabPropertyOverrideList(propertyPath, transformPath, componentIndex);
                    var list = @override.List;
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.x", transformPath, componentIndex, vector4.x));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.y", transformPath, componentIndex, vector4.y));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.z", transformPath, componentIndex, vector4.z));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.w", transformPath, componentIndex, vector4.w));
                    return @override;
                }
                case Quaternion quaternion:
                {
                    var @override = new RuntimePrefabPropertyOverrideList(propertyPath, transformPath, componentIndex);
                    var list = @override.List;
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.x", transformPath, componentIndex, quaternion.x));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.y", transformPath, componentIndex, quaternion.y));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.z", transformPath, componentIndex, quaternion.z));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.w", transformPath, componentIndex, quaternion.w));
                    return @override;
                }
                case Color color:
                {
                    var @override = new RuntimePrefabPropertyOverrideList(propertyPath, transformPath, componentIndex);
                    var list = @override.List;
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.r", transformPath, componentIndex, color.r));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.g", transformPath, componentIndex, color.g));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.b", transformPath, componentIndex, color.b));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.a", transformPath, componentIndex, color.a));
                    return @override;
                }
                case Color32 color:
                {
                    var @override = new RuntimePrefabPropertyOverrideList(propertyPath, transformPath, componentIndex);
                    var list = @override.List;
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.r", transformPath, componentIndex, color.r));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.g", transformPath, componentIndex, color.g));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.b", transformPath, componentIndex, color.b));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.a", transformPath, componentIndex, color.a));
                    return @override;
                }
                case string @string:
                {
                    return new RuntimePrefabOverrideString(propertyPath, transformPath, componentIndex, @string);
                }
                case char @char:
                {
                    return new RuntimePrefabOverrideChar(propertyPath, transformPath, componentIndex, @char);
                }
                case bool @bool:
                {
                    return new RuntimePrefabOverrideBool(propertyPath, transformPath, componentIndex, @bool);
                }
                case int @int:
                {
                    return new RuntimePrefabOverrideInt(propertyPath, transformPath, componentIndex, @int);
                }
                case long @long:
                {
                    return new RuntimePrefabOverrideLong(propertyPath, transformPath, componentIndex, @long);
                }
                case float @float:
                {
                    return new RuntimePrefabOverrideFloat(propertyPath, transformPath, componentIndex, @float);
                }
                case UnityObject unityObject:
                {
                    return new RuntimePrefabOverrideUnityObjectReference(propertyPath, transformPath, componentIndex,
                        UnityObjectReference.GetReferenceForObject(unityObject, metadata));
                }
                default:
                {
                    // TODO: Handle null values better
                    if (typeof(TValue) == typeof(UnityObject) && value == null)
                    {
                        return new RuntimePrefabOverrideUnityObjectReference(propertyPath, transformPath, componentIndex,
                            UnityObjectReference.NullObjectReference);
                    }

                    throw new NotImplementedException();
                }
            }
        }

        public static void Update<TValue>(RuntimePrefabPropertyOverride @override, TValue value, SerializationMetadata metadata = null)
        {
            var propertyPath = @override.m_PropertyPath;
            var transformPath = @override.m_TransformPath;
            var componentIndex = @override.m_ComponentIndex;

            switch (value)
            {
                case Vector2 vector2:
                {
                    var list = ((RuntimePrefabPropertyOverrideList)@override).List;
                    list.Clear();
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.x", transformPath, componentIndex, vector2.x));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.y", transformPath, componentIndex, vector2.y));
                    break;
                }
                case Vector3 vector3:
                {
                    var list = ((RuntimePrefabPropertyOverrideList)@override).List;
                    list.Clear();
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.x", transformPath, componentIndex, vector3.x));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.y", transformPath, componentIndex, vector3.y));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.z", transformPath, componentIndex, vector3.z));
                    break;
                }
                case Vector4 vector4:
                {
                    var list = ((RuntimePrefabPropertyOverrideList)@override).List;
                    list.Clear();
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.x", transformPath, componentIndex, vector4.x));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.y", transformPath, componentIndex, vector4.y));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.z", transformPath, componentIndex, vector4.z));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.w", transformPath, componentIndex, vector4.w));
                    break;
                }
                case Quaternion quaternion:
                {
                    var list = ((RuntimePrefabPropertyOverrideList)@override).List;
                    list.Clear();
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.x", transformPath, componentIndex, quaternion.x));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.y", transformPath, componentIndex, quaternion.y));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.z", transformPath, componentIndex, quaternion.z));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.w", transformPath, componentIndex, quaternion.w));
                    break;
                }
                case Color color:
                {
                    var list = ((RuntimePrefabPropertyOverrideList)@override).List;
                    list.Clear();
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.r", transformPath, componentIndex, color.r));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.g", transformPath, componentIndex, color.g));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.b", transformPath, componentIndex, color.b));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.a", transformPath, componentIndex, color.a));
                    break;
                }
                case Color32 color:
                {
                    var list = ((RuntimePrefabPropertyOverrideList)@override).List;
                    list.Clear();
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.r", transformPath, componentIndex, color.r));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.g", transformPath, componentIndex, color.g));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.b", transformPath, componentIndex, color.b));
                    list.Add(new RuntimePrefabOverrideFloat($"{propertyPath}.a", transformPath, componentIndex, color.a));
                    break;
                }
                case int @int:
                {
                    ((RuntimePrefabOverrideInt)@override).Value = @int;
                    break;
                }
                case long @long:
                {
                    ((RuntimePrefabOverrideLong)@override).Value = @long;
                    break;
                }
                case float @float:
                {
                    ((RuntimePrefabOverrideFloat)@override).Value = @float;
                    break;
                }
                case UnityObject unityObject:
                {
                    ((RuntimePrefabOverrideUnityObjectReference)@override).Value = UnityObjectReference.GetReferenceForObject(unityObject, metadata);
                    break;
                }
                default:
                {
                    // TODO: Handle null values better
                    if (typeof(TValue) == typeof(UnityObject) && value == null)
                    {
                        ((RuntimePrefabOverrideUnityObjectReference)@override).Value = UnityObjectReference.NullObjectReference;
                        break;
                    }

                    ((RuntimePrefabPropertyOverride<TValue>)@override).Value = value;
                    break;
                }
            }
        }
    }
}
