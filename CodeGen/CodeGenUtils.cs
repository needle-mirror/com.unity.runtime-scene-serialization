//#define PROPERTY_GENERATION_LOG

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Properties.CodeGen;
using Unity.RuntimeSceneSerialization.Internal;
using UnityEditor;
using UnityEngine;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    static class CodeGenUtils
    {
        internal const string RuntimeSerializationAssemblyName = "Unity.RuntimeSceneSerialization";
        internal const string RuntimeSerializationCodeGenAssemblyName = "Unity.RuntimeSceneSerialization.CodeGen";
        internal const string RuntimeSerializationEditorAssemblyName = "Unity.RuntimeSceneSerialization.Editor";
        internal const string ExternalPropertyBagAssemblyName = "Unity.RuntimeSceneSerialization.Generated";
        static readonly string k_SkipGenerationAttributeName = typeof(SkipGeneration).FullName;

        static readonly ConcurrentDictionary<string, bool> k_ListTypes = new ConcurrentDictionary<string, bool>();

        static readonly ConcurrentDictionary<string, bool> k_SerializableContainerTypes = new ConcurrentDictionary<string, bool>();
        static readonly ConcurrentDictionary<string, bool> k_SerializableTypes = new ConcurrentDictionary<string, bool>();

        static readonly ConcurrentDictionary<string, bool> k_KnownUnityObjectTypes = new ConcurrentDictionary<string, bool>();
        static readonly ConcurrentDictionary<string, bool> k_KnownComponentTypes = new ConcurrentDictionary<string, bool>();

        static readonly ConcurrentDictionary<string, (bool, bool)> k_SerializableTypeAttributes = new ConcurrentDictionary<string, (bool, bool)>();
        static readonly ConcurrentDictionary<string, (bool, bool, string)> k_SerializableFieldAttributes = new ConcurrentDictionary<string, (bool, bool, string)>();

        static readonly string k_ListTypeName = typeof(List<>).FullName;
        static readonly string k_StringTypeName = typeof(string).FullName;
        static readonly string k_ObjectTypeName = typeof(object).FullName;
        static readonly string k_UnityObjectTypeName = typeof(UnityObject).FullName;
        static readonly string k_ComponentTypeName = typeof(Component).FullName;
        static readonly string k_GameObjectTypeName = typeof(GameObject).FullName;
        static readonly string k_CompilerGeneratedAttributeName = typeof(CompilerGeneratedAttribute).FullName;
        static readonly string k_GameObjectContainerTypeName = typeof(GameObjectContainer).FullName;

        static readonly string k_SerializeFieldTypeName = typeof(SerializeField).FullName;
        const string k_NativePropertyAttributeName = "UnityEngine.Bindings.NativePropertyAttribute";
        const string k_NativeNameTypeName = "UnityEngine.Bindings.NativeNameAttribute";
        const string k_IgnoreTypeName = "UnityEngine.Bindings.IgnoreAttribute";
        const string k_EnableIl2CppDefine = "ENABLE_IL2CPP";
        const string k_NetDotsDefine = "NET_DOTS";
        const string k_NUnitFrameworkAssemblyName = "nunit.framework";

#if UNITY_2020_1_OR_NEWER
        static bool s_Initialized;
#endif

        internal static bool RequiresCodegen(this ICompiledAssembly compiledAssembly)
        {
            var containsIl2Cpp = compiledAssembly.Defines.Contains(k_EnableIl2CppDefine);
            return containsIl2Cpp || compiledAssembly.Defines.Contains(k_NetDotsDefine);
        }

        internal static bool IsListType(TypeReference type, out IGenericInstance genericInstance)
        {
            var typeName = type.FullName;
            genericInstance = null;
            if (string.IsNullOrEmpty(typeName))
                return false;

            if (k_ListTypes.TryGetValue(typeName, out var isList))
            {
                genericInstance = type as IGenericInstance;
                return isList;
            }

            genericInstance = type as IGenericInstance;
            isList = genericInstance != null && type.FullName.StartsWith(k_ListTypeName);
            k_ListTypes[typeName] = isList;
            return isList;
        }

        internal static bool IsSerializableContainer(Type type)
        {
            var typeName = type.FullName;
            if (string.IsNullOrEmpty(typeName))
                return false;

            if (k_SerializableContainerTypes.TryGetValue(typeName, out var isSerializableContainer))
                return isSerializableContainer;

            var isPrimitive = type.IsPrimitive || type.IsEnum || type == typeof(string);
            var isAbstractOrInterface = type.IsAbstract || type.IsInterface;
            isSerializableContainer = !(isPrimitive || isAbstractOrInterface)
                && (type.GetCustomAttribute<SerializableAttribute>() != null
                || type.IsValueType && type.Namespace == "UnityEngine");

            k_SerializableContainerTypes[typeName] = isSerializableContainer;
            if (isSerializableContainer)
                k_SerializableTypes[typeName] = true;

            return isSerializableContainer;
        }

        static bool IsSerializable(TypeReference type)
        {
            var typeName = type.FullName;
            if (string.IsNullOrEmpty(typeName))
                return false;

            if (k_SerializableTypes.TryGetValue(typeName, out var isSerializable))
                return isSerializable;

            isSerializable =
                typeName == k_StringTypeName
                || type.Namespace == "UnityEngine" // Allow an exception for types like Vector3
                || type.IsPrimitive
                || type.IsValueType
                || IsAssignableToUnityObject(type);

            if (!isSerializable)
            {
                var resolvedType = type.Resolve();
                if (resolvedType != null)
                    isSerializable = resolvedType.IsEnum || (resolvedType.Attributes & TypeAttributes.Serializable) != 0;
#if PROPERTY_GENERATION_LOG
                else
                    Console.Error.WriteLine($"{type} in {type.Module.Assembly.Name} cannot be resolved");
#endif
            }

            k_SerializableTypes[typeName] = isSerializable;
            return isSerializable;
        }

        internal static bool IsSerializableContainer(TypeDefinition type)
        {
            var typeName = type.FullName;
            if (string.IsNullOrEmpty(typeName))
                return false;

            if (k_SerializableContainerTypes.TryGetValue(typeName, out var isSerializableContainer))
                return isSerializableContainer;

            var isOpenGeneric = type.HasGenericParameters && !type.IsGenericInstance;
            var isPrimitive = TypeIsPrimitive(type, typeName);
            var isAbstractOrInterface = type.IsAbstract || type.IsInterface;
            isSerializableContainer = !(isPrimitive || isAbstractOrInterface || isOpenGeneric)
                && ((type.Attributes & TypeAttributes.Serializable) != 0 || type.Namespace == "UnityEngine");

            k_SerializableContainerTypes[typeName] = isSerializableContainer;

            if (isSerializableContainer)
                k_SerializableTypes[typeName] = true;

            return isSerializableContainer;
        }

        internal static bool TypeIsPrimitive(TypeDefinition type, string typeName)
        {
            return type.IsPrimitive || type.IsEnum || typeName == k_StringTypeName;
        }

        internal static bool IsAssignableToUnityObject(TypeReference memberType)
        {
            var typeName = memberType.FullName;
            if (typeName == k_UnityObjectTypeName)
                return true;

            if (k_KnownUnityObjectTypes.TryGetValue(typeName, out var isAssignable))
                return isAssignable;

            var resoledType = memberType.Resolve();
            if (resoledType == null)
            {
#if PROPERTY_GENERATION_LOG
                Console.Error.WriteLine($"{memberType} in {memberType.Module.Assembly.Name} cannot be resolved");
#endif

                return false;
            }

            var baseType = resoledType.BaseType;
            while (baseType != null)
            {
                if (baseType.FullName == k_UnityObjectTypeName)
                    break;

                baseType = baseType.Resolve().BaseType;
            }

            isAssignable = baseType != null;
            k_KnownUnityObjectTypes[typeName] = isAssignable;
            return isAssignable;
        }

        static bool IsAssignableToComponent(TypeReference memberType)
        {
            var typeName = memberType.FullName;
            if (typeName == k_ComponentTypeName)
                return true;

            if (k_KnownComponentTypes.TryGetValue(typeName, out var isAssignable))
                return isAssignable;

            var resoledType = memberType.Resolve();
            if (resoledType == null)
            {
#if PROPERTY_GENERATION_LOG
                Console.Error.WriteLine($"{memberType} in {memberType.Module.Assembly.Name} cannot be resolved");
#endif

                return false;
            }

            var baseType = resoledType.BaseType;
            while (baseType != null)
            {
                if (baseType.FullName == k_ComponentTypeName)
                    break;

                baseType = baseType.Resolve().BaseType;
            }

            isAssignable = baseType != null;
            k_KnownComponentTypes[typeName] = isAssignable;
            if (isAssignable)
                k_KnownUnityObjectTypes[typeName] = true;

            return isAssignable;
        }

        static bool TestField(FieldDefinition fieldDefinition, TypeDefinition containerType, HashSet<string> includedProperties, HashSet<string> ignoredProperties, out string nameOverride)
        {
            nameOverride = null;
            if (fieldDefinition.IsStatic || fieldDefinition.IsInitOnly)
                return false;

            var (hasSerializeField, hasIgnore, nativeName) = GetSerializedFieldAttributes(fieldDefinition);
            nameOverride = nativeName;

            if (hasIgnore)
                return false;

            var fieldName = fieldDefinition.Name;
            var includeField = includedProperties != null && includedProperties.Contains(fieldName);
            if (!includeField)
            {
                if (ignoredProperties != null && ignoredProperties.Contains(fieldName))
                    return false;


                if (!fieldDefinition.IsPublic)
                {
                    // Special case internal structs
                    var isBuiltInStruct = containerType.Namespace == "UnityEngine" && containerType.IsValueType;
                    if (!(isBuiltInStruct || hasSerializeField || nativeName != null))
                        return false;
                }
            }

            var fieldType = fieldDefinition.FieldType;
            if (fieldType.IsGenericParameter && containerType.HasGenericParameters)
                return false;

            var resolvedFieldType = fieldType.ResolveGenericParameter(containerType);
            if (resolvedFieldType.IsArray)
                resolvedFieldType = resolvedFieldType.GetElementType();
            else if (IsListType(resolvedFieldType, out var genericInstance))
                resolvedFieldType = genericInstance.GenericArguments[0];

            if (!(resolvedFieldType.FullName == k_GameObjectContainerTypeName || IsSerializable(resolvedFieldType)))
                return false;

            return true;
        }

        static bool TestProperty(PropertyDefinition propertyDefinition, TypeDefinition containerType, HashSet<string> includedProperties, HashSet<string> ignoredProperties)
        {
            if (!propertyDefinition.HasThis)
                return false;

            var getMethod = propertyDefinition.GetMethod;
            if (getMethod == null)
                return false;

            var setMethod = propertyDefinition.SetMethod;
            if (setMethod == null)
                return false;

            var propertyName = propertyDefinition.Name;
            if (containerType.FullName == k_GameObjectTypeName && propertyName == "name")
                return true;

            if (ignoredProperties != null && ignoredProperties.Contains(propertyName))
                return false;

            if (includedProperties != null && includedProperties.Contains(propertyName))
                return true;

            // Extern properties are mostly serialized and can be identified by methods with no bodies
            if (!getMethod.HasBody && !setMethod.HasBody)
                return true;

            foreach (var attribute in propertyDefinition.CustomAttributes)
            {
                if (attribute.AttributeType.FullName == k_NativePropertyAttributeName)
                    return true;
            }

            var propertyType = propertyDefinition.PropertyType;
            if (propertyType.IsGenericParameter && containerType.HasGenericParameters)
                return false;

            var resolvedPropertyType = propertyType.ResolveGenericParameter(containerType);
            if (resolvedPropertyType.IsArray)
                resolvedPropertyType = resolvedPropertyType.GetElementType();
            else if (IsListType(resolvedPropertyType, out var genericInstance))
                resolvedPropertyType = genericInstance.GenericArguments[0];

            if (!IsSerializable(resolvedPropertyType))
                return false;

            return false;
        }

        internal static IEnumerable<(IMemberDefinition, string)> GetPropertyMembers(TypeDefinition type)
        {
            var containerType = type;
            for (;;)
            {
                if (type == null)
                    yield break;

                var typeName = type.FullName;
                var includedProperties = ReflectedPropertyBagUtils.GetIncludedProperties(typeName);
                var ignoredProperties = ReflectedPropertyBagUtils.GetIgnoredProperties(typeName);

                foreach (var field in type.Fields)
                {
                    if (TestField(field, containerType, includedProperties, ignoredProperties, out var nativeName))
                        yield return (field, nativeName);
                }

                foreach (var property in type.Properties)
                {
                    if (TestProperty(property, containerType, includedProperties, ignoredProperties))
                        yield return (property, null);
                }

                if (null == type.BaseType || type.BaseType.FullName == k_ObjectTypeName)
                {
                    break;
                }

                type = type.BaseType.Resolve();
            }
        }

        // TODO: Name override for [NativeName] if needed
        internal static bool TryGenerateUnityObjectProperty(Context context, TypeReference containerType, TypeDefinition externalContainer, IMemberDefinition member,
            ILProcessor il, MethodReference addPropertyMethod, MethodReference createValueMethod, MethodReference createArrayMethod,
            MethodReference createListMethod, TypeReference unityObjectReference, TypeReference unityObjectListReference)
        {
            if (null == member)
                throw new ArgumentException(nameof(member));

            var memberType = Utility.GetMemberType(member);
            if (memberType.IsGenericParameter)
                return false;

            if (TryGenerateUnityObjectListProperty(context, containerType, externalContainer, member, il, addPropertyMethod, memberType, createListMethod, unityObjectReference, unityObjectListReference))
                return true;

            if (!IsAssignableToUnityObject(memberType))
                return false;

            il.Emit(OpCodes.Ldarg_0);

            // First argument is member name
            il.Emit(OpCodes.Ldstr, member.Name);

            // Second argument is true if member is a property
            il.Emit(member is PropertyDefinition ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);

            // Third argument is external type container name or null if not external type
            if (externalContainer != null)
                il.Emit(OpCodes.Ldstr, containerType.GetAssemblyQualifiedName());
            else
                il.Emit(OpCodes.Ldnull);

            var effectiveContainerType = externalContainer ?? containerType;
            var module = context.Module;
            if (memberType.IsArray)
            {
                var elementType = context.ImportReference(memberType.GetElementType());
                var createPropertyInstanceMethod = createArrayMethod.MakeGenericInstanceMethod(effectiveContainerType, elementType);
                il.Emit(OpCodes.Call, module.ImportReference(createPropertyInstanceMethod));
                il.Emit(OpCodes.Call, module.ImportReference(addPropertyMethod.MakeGenericInstanceMethod(unityObjectListReference)));

                var method = context.Module.ImportReference(context.PropertyBagRegisterListGenericMethodReference.Value.
                    MakeGenericInstanceMethod(effectiveContainerType, unityObjectListReference, unityObjectReference));

                il.Emit(OpCodes.Call, method);
            }
            else
            {
                il.Emit(OpCodes.Call, module.ImportReference(createValueMethod.MakeGenericInstanceMethod(effectiveContainerType)));
                il.Emit(OpCodes.Call, module.ImportReference(addPropertyMethod.MakeGenericInstanceMethod(unityObjectReference)));
            }

            return true;
        }

        static bool TryGenerateUnityObjectListProperty(Context context, TypeReference containerType, TypeDefinition externalContainer, IMemberDefinition member,
            ILProcessor il, MethodReference addPropertyMethod, TypeReference memberType, MethodReference createListProperty,
            TypeReference unityObjectReference, TypeReference unityObjectListReference)
        {
            if (!(memberType is GenericInstanceType genericInstanceType))
                return false;

            if (!memberType.FullName.StartsWith(k_ListTypeName))
                return false;

            var elementType = genericInstanceType.GenericArguments[0];
            if (!IsAssignableToUnityObject(elementType))
                return false;

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, member.Name);

            // Second argument is true if member is a property
            il.Emit(member is PropertyDefinition ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);

            // Third argument is external type container name or null if not external type
            if (externalContainer != null)
                il.Emit(OpCodes.Ldstr, containerType.GetAssemblyQualifiedName());
            else
                il.Emit(OpCodes.Ldnull);

            var module = context.Module;
            elementType = context.ImportReference(elementType);

            var effectiveContainerType = externalContainer ?? containerType;
            var createPropertyInstanceMethod = createListProperty.MakeGenericInstanceMethod(effectiveContainerType, elementType);
            il.Emit(OpCodes.Call, module.ImportReference(createPropertyInstanceMethod));

            il.Emit(OpCodes.Call, module.ImportReference(addPropertyMethod.MakeGenericInstanceMethod(unityObjectListReference)));

            var method = context.Module.ImportReference(context.PropertyBagRegisterListGenericMethodReference.Value.MakeGenericInstanceMethod(effectiveContainerType, unityObjectListReference, unityObjectReference));
            il.Emit(OpCodes.Call, method);
            return true;
        }

        internal static IEnumerable<TypeDefinition> PostProcessAssembly(HashSet<string> namespaceExceptions,
            HashSet<string> typeExceptions, AssemblyDefinition assembly, bool includeAllContainers = false)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    try
                    {
                        if (type.IsAbstract || type.IsInterface)
                            continue;

                        if (type.HasGenericParameters && !type.IsGenericInstance)
                            continue;

                        var (hasSkipGenerationAttribute, hasCompilerGeneratedAttribute) = GetSerializationAttributes(type);
                        if (hasSkipGenerationAttribute || hasCompilerGeneratedAttribute)
                            continue;

                        if (!IsAssignableToComponent(type))
                        {
                            if (!includeAllContainers || !IsSerializableContainer(type))
                                continue;
                        }

                        var typeName = type.FullName;
                        if (string.IsNullOrEmpty(typeName))
                            continue;

                        if (typeExceptions.Contains(typeName))
                            continue;

                        var partOfNamespaceException = false;
                        var typeNamespace = type.Namespace;
                        if (!string.IsNullOrEmpty(typeNamespace))
                        {
                            foreach (var exception in namespaceExceptions)
                            {
                                if (typeNamespace.Contains(exception))
                                {
                                    partOfNamespaceException = true;
                                    break;
                                }
                            }
                        }

                        if (partOfNamespaceException)
                            continue;
                    }
                    catch
                    {
                        // Ignored
                    }

                    yield return type;
                }
            }
        }

        internal static string GetAssemblyQualifiedName(this TypeReference typeReference)
        {
            var resolvedType = typeReference.Resolve();
            var name = resolvedType.FullName;
            if (typeReference is GenericInstanceType genericInstanceType)
            {
                var arguments = new List<string>();
                foreach (var argument in genericInstanceType.GenericArguments)
                {
                    arguments.Add($"[{argument.GetAssemblyQualifiedName()}]");
                }

                name = $"{name}[{string.Join(", ", arguments)}]";
            }

            return $"{name}, {resolvedType.Module.Assembly.Name}";
        }

        internal static (bool skipGeneration, bool compilerGenerated) GetSerializationAttributes(TypeDefinition type)
        {
            var typeName = type.FullName;
            if (k_SerializableTypeAttributes.TryGetValue(typeName, out var attributes))
                return attributes;

            var hasSkipGenerationAttribute = false;
            var hasCompilerGeneratedAttribute = false;
            foreach (var attribute in type.CustomAttributes)
            {
                var attributeTypeName = attribute.AttributeType.FullName;
                if (attributeTypeName == k_SkipGenerationAttributeName)
                    hasSkipGenerationAttribute = true;

                if (attributeTypeName == k_CompilerGeneratedAttributeName)
                    hasCompilerGeneratedAttribute = true;
            }


            attributes = (hasSkipGenerationAttribute, hasCompilerGeneratedAttribute);
            k_SerializableTypeAttributes[typeName] = attributes;
            return attributes;
        }

        static (bool, bool, string) GetSerializedFieldAttributes(FieldDefinition fieldDefinition)
        {
            var fieldName = fieldDefinition.FullName;
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

                if (attributeTypeName == k_IgnoreTypeName)
                    hasIgnore = true;

                // Override name if [NativeName("m_NewName)] is used
                if (attributeTypeName == k_NativeNameTypeName)
                    nameOverride = attribute.ConstructorArguments?[0].Value as string;
            }

            attributes = (hasSerializeField, hasIgnore, nameOverride);
            k_SerializableFieldAttributes[fieldName] = attributes;
            return attributes;
        }

        public static void ForEachBuiltInAssembly(Action<Assembly> callback)
        {
            var editorAssemblyPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(EditorApplication)).Location);
            if (string.IsNullOrEmpty(editorAssemblyPath))
            {
                Console.WriteLine("Error: Could not find editor assemblies");
                return;
            }

            foreach (var path in Directory.GetFiles(editorAssemblyPath, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(path);
                    callback(assembly);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public static bool IsTestAssembly(Assembly assembly)
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.FullName.Contains(k_NUnitFrameworkAssemblyName))
                    return true;
            }

            return false;
        }

#if UNITY_2020_1_OR_NEWER
        [InitializeOnLoadMethod]
        static void InitializeOnLoad()
        {
            s_Initialized = true;
        }

        public static void CallInitializeOnLoadMethodsIfNeeded()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;

            var serializationAssembly = typeof(TransformPropertyBagDefinition).Assembly;
            foreach (var type in serializationAssembly.GetTypes())
            {
                CallInitializersForType(type);
            }
        }

        static void CallInitializersForType(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.GetCustomAttribute<InitializeOnLoadMethodAttribute>() != null
                    || method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>() != null)
                {
                    try
                    {
                        method.Invoke(null, null);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
#endif
    }

    static class MetadataTokenProviderExtensions
    {
        public static bool IsPublicOrAssembly(this IMetadataTokenProvider member)
        {
            if (member is FieldDefinition field)
                return field.IsPublic || field.IsAssembly;

            if (member is PropertyDefinition property)
            {
                var getMethod = property.GetMethod;
                if (getMethod == null || !(getMethod.IsPublic || getMethod.IsAssembly))
                    return false;

                var setMethod = property.SetMethod;
                if (setMethod == null || !(getMethod.IsPublic || getMethod.IsAssembly))
                    return false;

                return true;
            }

            return false;
        }
    }
}
