#if !NET_DOTS && !ENABLE_IL2CPP
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Properties;
using UnityEngine.Scripting;

namespace Unity.RuntimeSceneSerialization.Internal
{
    class ReflectedPropertyBagProvider
    {
#if UNITY_2022_2_OR_NEWER
        const string k_PropertiesProviderTypeName = "Unity.Properties.Internal.ReflectedPropertyBagProvider, UnityEngine.PropertiesModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
#else
        const string k_PropertiesProviderTypeName = "Unity.Properties.Internal.ReflectedPropertyBagProvider, Unity.Properties, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
#endif
        internal static readonly ReflectedPropertyBagProvider Instance = new();

        readonly object m_PropertiesProviderInstance;

        readonly MethodInfo m_CreatePropertyMethod;
        readonly MethodInfo m_CreatePropertyBagMethod;
        readonly MethodInfo m_CreateIndexedCollectionPropertyBagMethod;
        readonly MethodInfo m_CreateSetPropertyBagMethod;
        readonly MethodInfo m_CreateKeyValueCollectionPropertyBagMethod;
        readonly MethodInfo m_CreateKeyValuePairPropertyBagMethod;
        readonly MethodInfo m_CreateArrayPropertyBagMethod;
        readonly MethodInfo m_CreateListPropertyBagMethod;
        readonly MethodInfo m_CreateHashSetPropertyBagMethod;
        readonly MethodInfo m_CreateDictionaryPropertyBagMethod;

        public ReflectedPropertyBagProvider()
        {
            m_CreatePropertyBagMethod = typeof(ReflectedPropertyBagProvider).GetMethods(BindingFlags.Instance | BindingFlags.Public).First(x => x.Name == nameof(CreatePropertyBag) && x.IsGenericMethod);
            m_CreatePropertyMethod = typeof(ReflectedPropertyBagProvider).GetMethod(nameof(CreateProperty), BindingFlags.Instance | BindingFlags.NonPublic);

            var propertiesProviderType = Type.GetType(k_PropertiesProviderTypeName);
            if (propertiesProviderType == null)
                return; // Suppress exceptions

            m_PropertiesProviderInstance = Activator.CreateInstance(propertiesProviderType);

            // // Generic interface property bag types (e.g. IList<T>, ISet<T>, IDictionary<K, V>)
            m_CreateIndexedCollectionPropertyBagMethod = propertiesProviderType.GetMethod("CreateIndexedCollectionPropertyBag", BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateSetPropertyBagMethod = propertiesProviderType.GetMethod("CreateSetPropertyBag", BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateKeyValueCollectionPropertyBagMethod = propertiesProviderType.GetMethod("CreateKeyValueCollectionPropertyBag", BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateKeyValuePairPropertyBagMethod = propertiesProviderType.GetMethod("CreateKeyValuePairPropertyBag", BindingFlags.Instance | BindingFlags.NonPublic);

            // Concrete collection property bag types (e.g. List<T>, HashSet<T>, Dictionary<K, V>
            m_CreateArrayPropertyBagMethod = propertiesProviderType.GetMethod("CreateArrayPropertyBag", BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateListPropertyBagMethod = propertiesProviderType.GetMethod("CreateListPropertyBag", BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateHashSetPropertyBagMethod = propertiesProviderType.GetMethod("CreateHashSetPropertyBag", BindingFlags.Instance | BindingFlags.NonPublic);
            m_CreateDictionaryPropertyBagMethod = propertiesProviderType.GetMethod("CreateDictionaryPropertyBag", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public IPropertyBag CreatePropertyBag(Type type)
        {
            if (type.IsGenericTypeDefinition) return null;
            return (IPropertyBag) m_CreatePropertyBagMethod.MakeGenericMethod(type).Invoke(this, null);
        }

        public IPropertyBag<TContainer> CreatePropertyBag<TContainer>()
        {
            if (!TypeTraits<TContainer>.IsContainer || TypeTraits<TContainer>.IsObject)
            {
                throw new InvalidOperationException("Invalid container type.");
            }

            if (typeof(TContainer).IsArray)
            {
                if (typeof(TContainer).GetArrayRank() != 1)
                {
                    throw new InvalidOperationException("Properties does not support multidimensional arrays.");
                }

                return (IPropertyBag<TContainer>) m_CreateArrayPropertyBagMethod.MakeGenericMethod(typeof(TContainer).GetElementType()).Invoke(m_PropertiesProviderInstance, new object[0]);
            }

            if (typeof(TContainer).IsGenericType && typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>)))
                return (IPropertyBag<TContainer>) m_CreateListPropertyBagMethod.MakeGenericMethod(typeof(TContainer).GetGenericArguments().First()).Invoke(m_PropertiesProviderInstance, new object[0]);

            if (typeof(TContainer).IsGenericType && typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(HashSet<>)))
                return (IPropertyBag<TContainer>) m_CreateHashSetPropertyBagMethod.MakeGenericMethod(typeof(TContainer).GetGenericArguments().First()).Invoke(m_PropertiesProviderInstance, new object[0]);

            if (typeof(TContainer).IsGenericType && typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>)))
                return (IPropertyBag<TContainer>) m_CreateDictionaryPropertyBagMethod.MakeGenericMethod(typeof(TContainer).GetGenericArguments().First(), typeof(TContainer).GetGenericArguments().ElementAt(1)).Invoke(m_PropertiesProviderInstance, new object[0]);

            if (typeof(TContainer).IsGenericType && typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(IList<>)))
                return (IPropertyBag<TContainer>) m_CreateIndexedCollectionPropertyBagMethod.MakeGenericMethod(typeof(TContainer), typeof(TContainer).GetGenericArguments().First()).Invoke(m_PropertiesProviderInstance, new object[0]);

            if (typeof(TContainer).IsGenericType && typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(ISet<>)))
                return (IPropertyBag<TContainer>) m_CreateSetPropertyBagMethod.MakeGenericMethod(typeof(TContainer), typeof(TContainer).GetGenericArguments().First()).Invoke(m_PropertiesProviderInstance, new object[0]);

            if (typeof(TContainer).IsGenericType && typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(IDictionary<,>)))
                return (IPropertyBag<TContainer>) m_CreateKeyValueCollectionPropertyBagMethod.MakeGenericMethod(typeof(TContainer),  typeof(TContainer).GetGenericArguments().First(), typeof(TContainer).GetGenericArguments().ElementAt(1)).Invoke(m_PropertiesProviderInstance, new object[0]);

            if (typeof(TContainer).IsGenericType && typeof(TContainer).GetGenericTypeDefinition().IsAssignableFrom(typeof(KeyValuePair<,>)))
            {
                var types = typeof(TContainer).GetGenericArguments().ToArray();
                return (IPropertyBag<TContainer>) m_CreateKeyValuePairPropertyBagMethod.MakeGenericMethod(types[0], types[1]).Invoke(m_PropertiesProviderInstance, new object[0]);
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

            propertyBag.AddProperty(new ReflectedMemberProperty<TContainer, TValue>(member, member.Name));
        }
    }
}
#endif
