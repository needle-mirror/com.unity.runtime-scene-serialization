using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class ReflectedPropertyBagUtils
    {
        static readonly Dictionary<Type, bool> k_ListTypes = new();
        static readonly Dictionary<Type, bool> k_SerializableTypes = new();

        static readonly Dictionary<Type, bool> k_KnownUnityObjectTypes = new();

        static readonly Dictionary<string, (bool, bool, string)> k_SerializableFieldAttributes = new();

        static readonly Type k_GameObjectType = typeof(GameObject);
        static readonly Type k_ObjectType = typeof(object);
        static readonly Type k_UnityObjectType = typeof(UnityObject);
        static readonly Type k_StringType = typeof(string);

        static readonly Dictionary<string, HashSet<string>> k_IncludedProperties = new();
        static readonly Dictionary<string, HashSet<string>> k_IgnoredProperties = new();
        static readonly string k_SerializeFieldTypeName = typeof(SerializeField).FullName;
        const string k_NativePropertyAttributeName = "UnityEngine.Bindings.NativePropertyAttribute";
        const string k_NativeNameTypeName = "UnityEngine.Bindings.NativeNameAttribute";
        const string k_IgnoreTypeName = "UnityEngine.Bindings.IgnoreAttribute";

        internal static void SetIncludedProperties(Type type, HashSet<string> properties)
        {
            var typeName = type.FullName;
            if (string.IsNullOrEmpty(typeName))
            {
                Console.WriteLine("Error: Encountered null or empty type name in SetIncludedProperties");
                return;
            }

            k_IncludedProperties[typeName] = properties;
        }

        internal static void SetIgnoredProperties(Type type, HashSet<string> properties)
        {
            var typeName = type.FullName;
            if (string.IsNullOrEmpty(typeName))
                return;

            k_IgnoredProperties[typeName] = properties;
        }

        internal static HashSet<string> GetIncludedProperties(string typeName)
        {
            k_IncludedProperties.TryGetValue(typeName, out var includedProperties);
            return includedProperties;
        }

        internal static HashSet<string> GetIgnoredProperties(string typeName)
        {
            k_IgnoredProperties.TryGetValue(typeName, out var ignoredProperties);
            return ignoredProperties;
        }

        internal static bool IsListType(Type type)
        {
            if (k_ListTypes.TryGetValue(type, out var isList))
                return isList;

            isList = type.IsGenericType && typeof(IList).IsAssignableFrom(type);
            k_ListTypes[type] = isList;
            return isList;
        }

        static bool IsSerializable(Type type)
        {
            if (k_SerializableTypes.TryGetValue(type, out var isSerializable))
                return isSerializable;

            isSerializable =
                type == k_StringType
                || type.Namespace == "UnityEngine" // Allow an exception for types like Vector3
                || type.IsPrimitive
                || type.IsValueType
                || IsAssignableToUnityObject(type);

            if (!isSerializable)
            {
                isSerializable = type.IsEnum || (type.Attributes & TypeAttributes.Serializable) != 0;
            }

            k_SerializableTypes[type] = isSerializable;
            return isSerializable;
        }

        static bool IsAssignableToUnityObject(Type type)
        {
            if (type == k_UnityObjectType)
                return true;

            if (k_KnownUnityObjectTypes.TryGetValue(type, out var isAssignable))
                return isAssignable;

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType == k_UnityObjectType)
                    break;

                baseType = baseType.BaseType;
            }

            isAssignable = baseType != null;
            k_KnownUnityObjectTypes[type] = isAssignable;
            return isAssignable;
        }

        static bool TestField(FieldInfo field, Type containerType, HashSet<string> includedProperties, HashSet<string> ignoredProperties, out string nameOverride)
        {
            nameOverride = null;
            if (field.IsStatic || field.IsInitOnly)
                return false;

            var (hasSerializeField, hasIgnore, nativeName) = GetSerializedFieldAttributes(field);
            nameOverride = nativeName;

            if (hasIgnore)
                return false;

            var fieldName = field.Name;
            var includeField = includedProperties != null && includedProperties.Contains(fieldName);
            if (!includeField)
            {
                if (ignoredProperties != null && ignoredProperties.Contains(fieldName))
                    return false;

                if (!field.IsPublic)
                {
                    // Special case internal structs
                    var isBuiltInStruct = containerType.Namespace == "UnityEngine" && containerType.IsValueType;
                    if (!(isBuiltInStruct || hasSerializeField || nativeName != null))
                        return false;
                }
            }

            var fieldType = field.FieldType;
            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();
            else if (IsListType(fieldType))
                fieldType = fieldType.GenericTypeArguments[0];

            return fieldType != null && IsSerializable(fieldType);
        }

        static bool TestProperty(PropertyInfo propertyDefinition, Type containerType, HashSet<string> includedProperties, HashSet<string> ignoredProperties)
        {
            var getMethod = propertyDefinition.GetMethod;
            if (getMethod == null)
                return false;

            if (getMethod.IsStatic)
                return false;

            var setMethod = propertyDefinition.SetMethod;
            if (setMethod == null)
                return false;

            var propertyName = propertyDefinition.Name;
            if (containerType == k_GameObjectType && propertyName == "name")
                return true;

            if (ignoredProperties != null && ignoredProperties.Contains(propertyName))
                return false;

            if (includedProperties != null && includedProperties.Contains(propertyName))
                return true;

            // Extern properties are mostly serialized and can be identified by methods the InternalCall flag
            // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
            if ((propertyDefinition.SetMethod.MethodImplementationFlags & MethodImplAttributes.InternalCall) != 0)
                return true;

            foreach (var attribute in propertyDefinition.CustomAttributes)
            {
                if (attribute.AttributeType.FullName == k_NativePropertyAttributeName)
                    return true;
            }

            return false;
        }

        internal static IEnumerable<(MemberInfo, string)> GetPropertyMembers(Type type)
        {
            var containerType = type;
            for (;;)
            {
                if (type == null)
                    yield break;

                var typeName = type.FullName;
                var includedProperties = GetIncludedProperties(typeName);
                var ignoredProperties = GetIgnoredProperties(typeName);

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (var field in type.GetFields(flags))
                {
                    if (TestField(field, containerType, includedProperties, ignoredProperties, out var nativeName))
                        yield return (field, nativeName);
                }

                foreach (var property in type.GetProperties(flags))
                {
                    if (TestProperty(property, containerType, includedProperties, ignoredProperties))
                        yield return (property, null);
                }

                if (null == type.BaseType || type.BaseType == k_ObjectType)
                {
                    break;
                }

                type = type.BaseType;
            }
        }

        static (bool, bool, string) GetSerializedFieldAttributes(MemberInfo fieldDefinition)
        {
            var declaringType = fieldDefinition.DeclaringType;
            if (declaringType == null)
                return default;

            var fieldName = $"{declaringType.FullName}.{fieldDefinition.Name}";
            if (k_SerializableFieldAttributes.TryGetValue(fieldName, out var attributes))
                return attributes;

            var hasSerializeField = false;
            var hasIgnore = false;
            string nameOverride = null;
            foreach (var attribute in fieldDefinition.CustomAttributes)
            {
                var attributeTypeName = attribute.AttributeType.FullName;
                if (attributeTypeName == k_SerializeFieldTypeName)
                    hasSerializeField = true;

                switch (attributeTypeName)
                {
                    case k_IgnoreTypeName:
                        hasIgnore = true;
                        break;

                    // Override name if [NativeName("m_NewName)] is used
                    case k_NativeNameTypeName:
                        nameOverride = attribute.ConstructorArguments[0].Value as string;
                        break;
                }
            }

            attributes = (hasSerializeField, hasIgnore, nameOverride);
            k_SerializableFieldAttributes[fieldName] = attributes;
            return attributes;
        }
    }
}
