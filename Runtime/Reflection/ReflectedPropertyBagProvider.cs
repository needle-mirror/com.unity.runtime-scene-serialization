#if !NET_DOTS && !ENABLE_IL2CPP
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Properties;
using Unity.Properties.Internal;
using UnityEngine;
using UnityEngine.Scripting;

namespace Unity.RuntimeSceneSerialization.Internal
{
    readonly struct FieldMember : IMemberInfo
    {
        readonly FieldInfo m_FieldInfo;
        readonly string m_Name;

        /// <summary>
        /// Initializes a new <see cref="FieldMember"/> instance.
        /// </summary>
        /// <param name="fieldInfo">The backing <see cref="FieldInfo"/> object.</param>
        /// <param name="name">The name to use for this property</param>
        public FieldMember(FieldInfo fieldInfo, string name)
        {
            m_FieldInfo = fieldInfo;
            m_Name = name;
        }

        /// <inheritdoc/>
        public string Name => m_Name;

        /// <inheritdoc/>
        public bool IsReadOnly => m_FieldInfo.IsInitOnly;

        /// <inheritdoc/>
        public Type ValueType => m_FieldInfo.FieldType;

        /// <inheritdoc/>
        public object GetValue(object obj) => m_FieldInfo.GetValue(obj);

        /// <inheritdoc/>
        public void SetValue(object obj, object value) => m_FieldInfo.SetValue(obj, value);

        /// <inheritdoc/>
        public IEnumerable<Attribute> GetCustomAttributes() => m_FieldInfo.GetCustomAttributes();
    }

    readonly struct PropertyMember : IMemberInfo
    {
        readonly PropertyInfo m_PropertyInfo;
        readonly string m_Name;

        /// <inheritdoc/>
        public string Name => m_Name;

        /// <inheritdoc/>
        public bool IsReadOnly => !m_PropertyInfo.CanWrite;

        /// <inheritdoc/>
        public Type ValueType => m_PropertyInfo.PropertyType;

        /// <summary>
        /// Initializes a new <see cref="PropertyMember"/> instance.
        /// </summary>
        /// <param name="propertyInfo">The backing <see cref="PropertyInfo"/> object.</param>
        /// <param name="name">The name to use for this property</param>
        public PropertyMember(PropertyInfo propertyInfo, string name)
        {
            m_PropertyInfo = propertyInfo;
            m_Name = name;
        }

        /// <inheritdoc/>
        public object GetValue(object obj) => m_PropertyInfo.GetValue(obj);

        /// <inheritdoc/>
        public void SetValue(object obj, object value) => m_PropertyInfo.SetValue(obj, value);

        /// <inheritdoc/>
        public IEnumerable<Attribute> GetCustomAttributes() => m_PropertyInfo.GetCustomAttributes();
    }

    class ReflectedPropertyBagProvider
    {
        internal static readonly ReflectedPropertyBagProvider Instance = new ReflectedPropertyBagProvider();

        readonly MethodInfo m_CreatePropertyBagMethod;
        readonly MethodInfo m_CreatePropertyMethod;
        readonly MethodInfo m_CreateListPropertyBagMethod;
        readonly MethodInfo m_CreateSetPropertyBagMethod;
        readonly MethodInfo m_CreateDictionaryPropertyBagMethod;
        readonly MethodInfo m_CreateKeyValuePairPropertyBagMethod;

        public ReflectedPropertyBagProvider()
        {
            m_CreatePropertyBagMethod = typeof(ReflectedPropertyBagProvider).GetMethods(BindingFlags.Instance | BindingFlags.Public).First(x => x.Name == nameof(CreatePropertyBag) && x.IsGenericMethod);
            m_CreatePropertyMethod = typeof(ReflectedPropertyBagProvider).GetMethod(nameof(CreateProperty), BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateListPropertyBagMethod = typeof(ReflectedPropertyBagProvider).GetMethod(nameof(CreateListPropertyBag), BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateSetPropertyBagMethod = typeof(ReflectedPropertyBagProvider).GetMethod(nameof(CreateSetPropertyBag), BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateDictionaryPropertyBagMethod = typeof(ReflectedPropertyBagProvider).GetMethod(nameof(CreateDictionaryPropertyBag), BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateKeyValuePairPropertyBagMethod = typeof(ReflectedPropertyBagProvider).GetMethod(nameof(CreateKeyValuePairPropertyBag), BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public IPropertyBag CreatePropertyBag(Type type)
        {
            if (type.IsGenericTypeDefinition) return null;
            return (IPropertyBag) m_CreatePropertyBagMethod.MakeGenericMethod(type).Invoke(this, null);
        }

        public IPropertyBag<TContainer> CreatePropertyBag<TContainer>()
        {
            if (!RuntimeTypeInfoCache<TContainer>.IsContainerType || RuntimeTypeInfoCache<TContainer>.IsObjectType)
            {
                throw new InvalidOperationException("Invalid container type.");
            }

            if (typeof(TContainer).IsArray)
            {
                if (typeof(TContainer).GetArrayRank() != 1)
                {
                    throw new InvalidOperationException("Properties does not support multidimensional arrays.");
                }

                return (IPropertyBag<TContainer>) m_CreateListPropertyBagMethod.MakeGenericMethod(typeof(TContainer), typeof(TContainer).GetElementType()).Invoke(this, new object[0]);
            }

            if (typeof(TContainer).IsGenericType && (typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>)) || typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(IList<>))))
            {
                return (IPropertyBag<TContainer>) m_CreateListPropertyBagMethod.MakeGenericMethod(typeof(TContainer), typeof(TContainer).GetGenericArguments().First()).Invoke(this, new object[0]);
            }

            if (typeof(TContainer).IsGenericType && (typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(HashSet<>)) || typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(ISet<>))))
            {
                return (IPropertyBag<TContainer>) m_CreateSetPropertyBagMethod.MakeGenericMethod(typeof(TContainer), typeof(TContainer).GetGenericArguments().First()).Invoke(this, new object[0]);
            }

            if (typeof(TContainer).IsGenericType && (typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>)) || typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(IDictionary<,>))))
            {
                var types = typeof(TContainer).GetGenericArguments().ToArray();
                return (IPropertyBag<TContainer>) m_CreateDictionaryPropertyBagMethod.MakeGenericMethod(typeof(TContainer), types[0], types[1]).Invoke(this, new object[0]);
            }

            if (typeof(TContainer).IsGenericType && typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(KeyValuePair<,>)))
            {
                var types = typeof(TContainer).GetGenericArguments().ToArray();
                return (IPropertyBag<TContainer>) m_CreateKeyValuePairPropertyBagMethod.MakeGenericMethod(types[0], types[1]).Invoke(this, new object[0]);
            }

            var propertyBag = new ReflectedPropertyBag<TContainer>();
            foreach (var (member, nameOverride) in ReflectedPropertyBagUtils.GetPropertyMembers(typeof(TContainer)))
            {
                IMemberInfo info;
                var memberName = string.IsNullOrEmpty(nameOverride) ? member.Name : nameOverride;

                switch (member)
                {
                    case FieldInfo field:
                        info = new FieldMember(field, memberName);
                        break;
                    case PropertyInfo property:
                        info = new PropertyMember(property, memberName);
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                m_CreatePropertyMethod.MakeGenericMethod(typeof(TContainer), info.ValueType).Invoke(this, new object[]
                {
                    info,
                    propertyBag
                });
            }

            return propertyBag;
        }

        [Preserve]
        void CreateProperty<TContainer, TValue>(IMemberInfo member, ReflectedPropertyBag<TContainer> propertyBag)
        {
            if (typeof(TValue).IsPointer)
            {
                return;
            }

            var arrayProperty = ReflectedPropertyBagUtils.TryCreateUnityObjectArrayProperty<TContainer, TValue>(member);
            if (arrayProperty != null)
            {
                propertyBag.AddProperty(arrayProperty);
                return;
            }

            var listProperty = ReflectedPropertyBagUtils.TryCreateUnityObjectListProperty<TContainer, TValue>(member);
            if (listProperty != null)
            {
                propertyBag.AddProperty(listProperty);
                return;
            }

            var property = ReflectedPropertyBagUtils.TryCreateUnityObjectProperty<TContainer>(member);
            if (property != null)
            {
                propertyBag.AddProperty(property);
                return;
            }

            propertyBag.AddProperty(new ReflectedMemberProperty<TContainer, TValue>(member, member.Name));
        }

        [Preserve]
        IPropertyBag<TSet> CreateSetPropertyBag<TSet, TValue>()
            where TSet : ISet<TValue>
        {
            return new SetPropertyBag<TSet, TValue>();
        }

        [Preserve]
        IPropertyBag<TList> CreateListPropertyBag<TList, TElement>()
            where TList : IList<TElement>
        {
            return new ListPropertyBag<TList, TElement>();
        }

        [Preserve]
        IPropertyBag<TDictionary> CreateDictionaryPropertyBag<TDictionary, TKey, TValue>()
            where TDictionary : IDictionary<TKey, TValue>
        {
            return new DictionaryPropertyBag<TDictionary, TKey, TValue>();
        }

        [Preserve]
        IPropertyBag<KeyValuePair<TKey, TValue>> CreateKeyValuePairPropertyBag<TKey, TValue>()
        {
            return new KeyValuePairPropertyBag<TKey, TValue>();
        }
    }
}
#endif
