using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.RuntimeSceneSerialization.Internal;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using MethodBody = Mono.Cecil.Cil.MethodBody;
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

        static readonly ConcurrentDictionary<string, bool> k_ListTypes = new();

        static readonly ConcurrentDictionary<string, bool> k_SerializableContainerTypes = new();
        static readonly ConcurrentDictionary<string, bool> k_SerializableTypes = new();

        static readonly ConcurrentDictionary<string, bool> k_KnownUnityObjectTypes = new();
        static readonly ConcurrentDictionary<string, bool> k_KnownComponentTypes = new();

        static readonly ConcurrentDictionary<string, (bool, bool)> k_SerializableTypeAttributes = new();
        static readonly ConcurrentDictionary<string, (bool, bool, string)> k_SerializableFieldAttributes = new();

        static readonly string k_ListTypeName = typeof(List<>).FullName;
        static readonly string k_StringTypeName = typeof(string).FullName;
        static readonly string k_ObjectTypeName = typeof(object).FullName;
        static readonly string k_UnityObjectTypeName = typeof(UnityObject).FullName;
        static readonly string k_ComponentTypeName = typeof(Component).FullName;
        static readonly string k_GameObjectTypeName = typeof(GameObject).FullName;
        static readonly string k_CompilerGeneratedAttributeName = typeof(CompilerGeneratedAttribute).FullName;

        static readonly string k_SerializeFieldTypeName = typeof(SerializeField).FullName;
        const string k_NativePropertyAttributeName = "UnityEngine.Bindings.NativePropertyAttribute";
        const string k_NativeNameTypeName = "UnityEngine.Bindings.NativeNameAttribute";
        const string k_IgnoreTypeName = "UnityEngine.Bindings.IgnoreAttribute";
        const string k_EnableIl2CppDefine = "ENABLE_IL2CPP";
        const string k_NetDotsDefine = "NET_DOTS";

        static bool s_Initialized;

        internal static bool RequiresCodeGen(this ICompiledAssembly compiledAssembly)
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
                else
                    Console.WriteLine($"Error: {type} in {type.Module.Assembly.Name} cannot be resolved");
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
                && ((type.Attributes & TypeAttributes.Serializable) != 0
                || type.IsValueType && type.Namespace == "UnityEngine");

            k_SerializableContainerTypes[typeName] = isSerializableContainer;

            if (isSerializableContainer)
                k_SerializableTypes[typeName] = true;

            return isSerializableContainer;
        }

        internal static bool TypeIsPrimitive(TypeDefinition type, string typeName)
        {
            return type.IsPrimitive || type.IsEnum || typeName == k_StringTypeName;
        }

        internal static bool IsAssignableToUnityObject(TypeReference type)
        {
            var typeName = type.FullName;
            if (typeName == k_UnityObjectTypeName)
                return true;

            if (k_KnownUnityObjectTypes.TryGetValue(typeName, out var isAssignable))
                return isAssignable;

            var resoledType = type.Resolve();
            if (resoledType == null)
            {
                Console.WriteLine($"Error: {type} in {type.Module.Assembly.Name} cannot be resolved");
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

        internal static bool IsAssignableToComponent(Type type)
        {
            var typeName = type.FullName;
            if (typeName == k_ComponentTypeName)
                return true;

            if (typeName == null)
            {
                Console.WriteLine($"Error: FullName of {type} is null");
                return false;
            }

            if (k_KnownComponentTypes.TryGetValue(typeName, out var isAssignable))
                return isAssignable;

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.FullName == k_ComponentTypeName)
                    break;

                baseType = baseType.BaseType;
            }

            isAssignable = baseType != null;
            k_KnownComponentTypes[typeName] = isAssignable;
            if (isAssignable)
                k_KnownUnityObjectTypes[typeName] = true;

            return isAssignable;
        }

        internal static bool IsAssignableToComponent(TypeReference type)
        {
            var typeName = type.FullName;
            if (typeName == k_ComponentTypeName)
                return true;

            if (k_KnownComponentTypes.TryGetValue(typeName, out var isAssignable))
                return isAssignable;

            var resoledType = type.Resolve();
            if (resoledType == null)
            {
                Console.WriteLine($"Error: {type} in {type.Module.Assembly.Name} cannot be resolved");
                return false;
            }

            var baseType = resoledType.BaseType;
            while (baseType != null)
            {
                if (baseType.FullName == k_ComponentTypeName)
                    break;

                var resolved = baseType.Resolve();
                if (resolved == null)
                {
                    Console.WriteLine($"Error: {baseType} in {baseType.Module.Assembly.Name} cannot be resolved");
                    baseType = null;
                    break;
                }

                baseType = resolved.BaseType;
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

            if (!IsSerializable(resolvedFieldType))
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
                {
                    var nativeName = attribute.ConstructorArguments?[0].Value as string;
                    if (string.IsNullOrEmpty(nativeName))
                        continue;

                    // Fix issue with NativeName in Matrix4x4 (NativeName is "m_Data[0]")
                    if (nativeName.Contains('['))
                        continue;

                    nameOverride = nativeName;
                }
            }

            attributes = (hasSerializeField, hasIgnore, nameOverride);
            k_SerializableFieldAttributes[fieldName] = attributes;
            return attributes;
        }

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

        public static void PostProcessPropertyBag(Context context, MethodBody ctorMethodBody, Instruction baseCtorCall)
        {
            var il = ctorMethodBody.GetILProcessor();
            var nop = il.Create(OpCodes.Nop);
            var ret = il.Create(OpCodes.Ret);
            var leave = il.Create(OpCodes.Leave, ret);

            il.InsertAfter(ctorMethodBody.Instructions.Last(), nop);
            il.InsertAfter(nop, leave);
            il.InsertAfter(leave, ret);

            var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = baseCtorCall.Next,
                TryEnd = nop,
                HandlerStart = nop,
                HandlerEnd = ret,
                CatchType = context.Module.ImportReference(typeof(Exception))
            };

            ctorMethodBody.ExceptionHandlers.Add(handler);
        }

        public static void WriteAssemblyList(string playerAssembliesPath, HashSet<string> hashSet, AssembliesType type)
        {
            try
            {
                hashSet.Clear();
                var assemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(type);
                foreach (var assembly in assemblies)
                {
                    hashSet.Add(Path.GetFullPath(assembly.outputPath));
                }

                File.WriteAllText(playerAssembliesPath, string.Join(",", hashSet));
            }
            catch
            {
                // ignored
            }
        }

        public static void WriteAssemblyList(string playerAssembliesPath, HashSet<string> hashSet, string[] assemblies)
        {
            try
            {
                hashSet.Clear();
                foreach (var assembly in assemblies)
                {
                    hashSet.Add(Path.GetFullPath(assembly));
                }

                File.WriteAllText(playerAssembliesPath, string.Join(",", hashSet));
            }
            catch
            {
                // ignored
            }
        }

        public static void ReadAssemblyList(string path, HashSet<string> hashSet)
        {
            try
            {
                if (File.Exists(path))
                {
                    var assemblies = File.ReadAllText(path);
                    var split = assemblies.Split(',');
                    foreach (var assembly in split)
                    {
                        hashSet.Add(assembly);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
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
