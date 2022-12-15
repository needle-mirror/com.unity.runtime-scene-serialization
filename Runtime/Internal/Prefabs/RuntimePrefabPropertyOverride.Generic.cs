using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityObject = UnityEngine.Object;

#if NET_DOTS || ENABLE_IL2CPP
using Unity.RuntimeSceneSerialization.Internal;
#else
using System.Reflection;
#endif

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    [Serializable]
    class RuntimePrefabPropertyOverride<TOverrideValue> : RuntimePrefabPropertyOverride
    {
#if NET_DOTS || ENABLE_IL2CPP
        class SetPropertyMethodFactory : SerializationUtils.IGenericMethodFactory
        {
            public RuntimePrefabPropertyOverride<TOverrideValue> Override;
            public string PropertyPath;

            public Action<T> GetGenericMethod<T>() where T : UnityObject
            {
                var factory = this;
                return obj =>
                {
                    factory.Override.SetProperty(ref obj, factory.PropertyPath);
                };
            }
        }

        static readonly SetPropertyMethodFactory k_Factory = new SetPropertyMethodFactory();
#else
        static readonly MethodInfo k_SetPropertyMethod = typeof(RuntimePrefabPropertyOverride<TOverrideValue>).GetMethod(nameof(SetProperty), BindingFlags.Instance | BindingFlags.NonPublic);

        // ReSharper disable StaticMemberInGenericType
        static readonly Dictionary<Type, MethodInfo> k_SetPropertyMethods = new();
        static readonly object[] k_SetPropertyArguments = new object[2];
        // ReSharper restore StaticMemberInGenericType
#endif

        // ReSharper disable once Unity.RedundantSerializeFieldAttribute
        [SerializeField]
        protected TOverrideValue m_Value;

        public TOverrideValue Value { get => m_Value; set => m_Value = value; }

        public RuntimePrefabPropertyOverride() { }

        public RuntimePrefabPropertyOverride(string propertyPath, string transformPath, int componentIndex, TOverrideValue value)
            : base(propertyPath, transformPath, componentIndex)
        {
            m_Value = value;
        }

        protected internal override void ApplyOverrideToTarget(UnityObject target)
        {
#if NET_DOTS || ENABLE_IL2CPP
            var factory = new SetPropertyMethodFactory
            {
                Override = this,
                PropertyPath = PropertyPath
            };

            k_Factory.Override = this;
            k_Factory.PropertyPath = PropertyPath;
            var rootContainer = target;
            SerializationUtils.InvokeGenericMethodWrapper(rootContainer, factory);
#else
            var type = target.GetType();
            if (!k_SetPropertyMethods.TryGetValue(type, out var method))
            {
                method = k_SetPropertyMethod.MakeGenericMethod(type);
                k_SetPropertyMethods[type] = method;
            }

            k_SetPropertyArguments[0] = target;
            k_SetPropertyArguments[1] = PropertyPath;
            method.Invoke(this, k_SetPropertyArguments);
#endif
        }

        internal void SetProperty<TContainer>(ref TContainer container, string propertyPath)
        {
#if !NET_DOTS && !ENABLE_IL2CPP
            SceneSerialization.RegisterPropertyBagRecursively(typeof(TContainer));
#endif

            // TODO: re-use the same visitor and tokenize path
            var visitor = new RuntimePrefabOverridePropertyVisitor<TOverrideValue>(this, propertyPath);
            PropertyContainer.TryAccept(visitor, ref container);
        }
    }
}
