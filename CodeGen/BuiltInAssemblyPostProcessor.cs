using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Properties.CodeGen;
using Unity.Properties.CodeGen.Blocks;
using Unity.RuntimeSceneSerialization.Internal;
using Unity.XRTools.Utils;
using UnityEditor;
using UnityEngine;
using Assembly = System.Reflection.Assembly;
using MethodImplAttributes = System.Reflection.MethodImplAttributes;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    /// <summary>
    /// ILPostProcessor responsible for generating property bags for types built into Unity
    /// </summary>
    [InitializeOnLoad]
    class BuiltInAssemblyPostProcessor : ILPostProcessor
    {
        static readonly Type k_NativePropertyAttributeType = ReflectionUtils.FindType(type => type.Name == "NativePropertyAttribute");
        static readonly Type k_NativeNameAttributeType = ReflectionUtils.FindType(type => type.Name == "NativeNameAttribute");
        static readonly Type k_IgnoreAttributeType = ReflectionUtils.FindType(type => type.Name == "IgnoreAttribute");

        static string s_EditorAssemblyLocation;

        static readonly HashSet<Type> k_IgnoredTypes = new HashSet<Type>
        {
            typeof(AnimationCurve),
            typeof(Keyframe),
            typeof(Vector2Int),
            typeof(Vector3Int),
            typeof(Rect),
            typeof(RectInt),
            typeof(BoundsInt)
        };

        static readonly HashSet<string> k_PreCompiledAssemblies = new HashSet<string>();

        static BuiltInAssemblyPostProcessor()
        {
            // Hard-code "Temp" because of missing method exception trying to use Application.temporaryCachePath
            var editorLocationPath = Path.Combine("Temp", "EditorAssemblyLocation");
            try
            {
                if (File.Exists(editorLocationPath))
                    s_EditorAssemblyLocation = File.ReadAllText(editorLocationPath);
            }
            catch
            {
                // ignored
            }

            var preCompiledAssembliesPath = Path.Combine("Temp", "PreCompiledAssemblies");
            CodeGenUtils.ReadAssemblyList(preCompiledAssembliesPath, k_PreCompiledAssemblies);

            EditorApplication.delayCall += () =>
            {
                try
                {
                    s_EditorAssemblyLocation = Path.GetDirectoryName(typeof(EditorApplication).Assembly.Location);
                    File.WriteAllText(editorLocationPath, s_EditorAssemblyLocation);
                }
                catch
                {
                    // ignored
                }

                var assemblies = UnityEditor.Compilation.CompilationPipeline.GetPrecompiledAssemblyPaths(UnityEditor.Compilation.CompilationPipeline.PrecompiledAssemblySources.All);
                CodeGenUtils.WriteAssemblyList(preCompiledAssembliesPath, k_PreCompiledAssemblies, assemblies);
            };
        }

        public override ILPostProcessor GetInstance()
        {
            return this;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return ShouldProcess(compiledAssembly);
        }

        static bool ShouldProcess(ICompiledAssembly compiledAssembly)
        {
            if (!compiledAssembly.RequiresCodeGen())
                return false;

            return compiledAssembly.Name == CodeGenUtils.ExternalPropertyBagAssemblyName;
        }

        static AssemblyDefinition CreateAssemblyDefinition(ICompiledAssembly compiledAssembly)
        {
            var resolver = new PostProcessorAssemblyResolver(compiledAssembly);

            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadingMode = ReadingMode.Deferred,

                // We _could_ be running in .NET core. In this case we need to force imports to resolve to mscorlib.
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider()
            };

            if (null != compiledAssembly.InMemoryAssembly.PdbData)
            {
                readerParameters.ReadSymbols = true;
                readerParameters.SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray());
                readerParameters.SymbolReaderProvider = new PortablePdbReaderProvider();
            }

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, readerParameters);

            resolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);

            return assemblyDefinition;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!ShouldProcess(compiledAssembly))
                return null;

#if UNITY_2020_1_OR_NEWER
            CodeGenUtils.CallInitializeOnLoadMethodsIfNeeded();
#endif

            using var assemblyDefinition = CreateAssemblyDefinition(compiledAssembly);
            return GeneratePropertyBags(assemblyDefinition, compiledAssembly.Defines);
        }

        static ILPostProcessResult GeneratePropertyBags(AssemblyDefinition compiledAssembly, string[] defines)
        {
            var module = compiledAssembly.MainModule;
            var context = new Context(module, defines);
            var fields = new List<FieldInfo>();
            var properties = new List<PropertyInfo>();
            var serializableContainerTypes = new HashSet<Type>();
            var assemblyInclusions = RuntimeSerializationSettingsUtils.GetAssemblyInclusions();
            var namespaceExceptions = RuntimeSerializationSettingsUtils.GetNamespaceExceptions();
            var typeExceptions = RuntimeSerializationSettingsUtils.GetTypeExceptions();

            var assemblies = new Dictionary<string, Assembly>();
            var assemblyPaths = new HashSet<string>();
            assemblyPaths.UnionWith(k_PreCompiledAssemblies);
            assemblyPaths.UnionWith(Directory.GetFiles(s_EditorAssemblyLocation, "*.dll", SearchOption.AllDirectories));
            foreach (var path in assemblyPaths)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(path);
                    assemblies[assembly.GetName().Name] = assembly;
                }
                catch
                {
                    // Skip assemblies that are already loaded
                }
            }

            foreach (var kvp in assemblies)
            {
                var assembly = kvp.Value;
                try
                {
                    if (assembly.IsDynamic)
                        continue;

                    if (!assemblyInclusions.Contains(kvp.Key))
                        continue;

                    PostProcessAssembly(namespaceExceptions, typeExceptions, assembly, fields, properties, serializableContainerTypes);
                }
                catch
                {
                    // ignored
                }
            }

            serializableContainerTypes.ExceptWith(k_IgnoredTypes);

            if (serializableContainerTypes.Count == 0)
                return null;

            GeneratePropertyBagsForSerializableTypes(context, serializableContainerTypes);

            return CreatePostProcessResult(compiledAssembly);
        }

        static void GeneratePropertyBagsForSerializableTypes(Context context, HashSet<Type> serializableContainerTypes)
        {
            var module = context.Module;
            var createValueMethod = module.ImportReference(UnityObjectReference.CreateValueMethod);
            var createArrayMethod = module.ImportReference(UnityObjectReference.CreateArrayMethod);
            var createListMethod = module.ImportReference(UnityObjectReference.CreateListMethod);
            var unityObjectPropertyType = context.ImportReference(typeof(UnityObjectReference));
            var unityObjectPropertyListType = context.ImportReference(typeof(List<UnityObjectReference>));

            var propertyBagDefinitions = new List<Tuple<TypeDefinition, TypeReference>>();
            foreach (var type in serializableContainerTypes)
            {
                Console.WriteLine($"GENERATE FOR {type}");
                var containerType = context.ImportReference(type);
                var propertyBagType = GeneratePropertyBag(context, containerType, createValueMethod, createArrayMethod, createListMethod, unityObjectPropertyType, unityObjectPropertyListType);
                module.Types.Add(propertyBagType);
                propertyBagDefinitions.Add(new Tuple<TypeDefinition, TypeReference>(propertyBagType, containerType));
            }

            var propertyBagRegistryTypeDefinition = PropertyBagRegistry.Generate(context, propertyBagDefinitions);
            module.Types.Add(propertyBagRegistryTypeDefinition);
        }

        static TypeDefinition GeneratePropertyBag(Context context, TypeReference containerType, MethodReference createValueMethod, MethodReference createArrayMethod, MethodReference createListMethod, TypeReference unityObjectReference, TypeReference unityObjectListReference)
        {
            var propertyBagType = PropertyBag.GeneratePropertyBagHeader(context, containerType, out var ctorMethod, out var addPropertyMethod);
            var ctorMethodBody = ctorMethod.Body;
            var il = ctorMethodBody.GetILProcessor();
            var baseCtorCall = ctorMethodBody.Instructions.Last();
            var instructions = ctorMethodBody.Instructions;
            var startMethod = il.Create(OpCodes.Nop);
            il.Append(startMethod);
            var lastInstruction = startMethod;
            var exceptionType = context.Module.ImportReference(typeof (Exception));
            foreach (var (member, nameOverride) in CodeGenUtils.GetPropertyMembers(containerType.Resolve()))
            {
                if (CodeGenUtils.TryGenerateUnityObjectProperty(context, containerType, null, member, il, addPropertyMethod, createValueMethod, createArrayMethod, createListMethod, unityObjectReference, unityObjectListReference))
                {
                    var last = instructions.Last();
                    var endCatch = il.Create(OpCodes.Nop);
                    il.InsertAfter(last, endCatch);

                    var leaveTry = il.Create (OpCodes.Leave, endCatch);
                    var startCatch = il.Create(OpCodes.Nop);
                    var leaveCatch = il.Create (OpCodes.Leave, endCatch);

                    il.InsertAfter(last, leaveTry);
                    il.InsertAfter(leaveTry, startCatch);
                    il.InsertAfter (startCatch, leaveCatch);

                    var handler = new ExceptionHandler (ExceptionHandlerType.Catch) {
                        TryStart = lastInstruction,
                        TryEnd = startCatch,
                        HandlerStart = startCatch,
                        HandlerEnd = endCatch,
                        CatchType = exceptionType
                    };

                    ctorMethodBody.ExceptionHandlers.Add (handler);
                    lastInstruction = endCatch;
                    continue;
                }

                var memberType = context.ImportReference(Utility.GetMemberType(member).ResolveGenericParameter(containerType));

                if (memberType.IsGenericInstance || memberType.IsArray)
                {
                    PropertyBag.RegisterCollectionTypes(context, containerType, memberType, il);
                }

                TypeDefinition propertyType;

                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (member.IsPublic())
                {
                    propertyType = Property.Generate(context, containerType, member, nameOverride);
                }
                else
                {
#if !NET_DOTS
                    propertyType = ReflectedProperty.Generate(context, containerType, member, nameOverride);
#else
                    throw new Exception("Private properties require reflection which is not supported in NET_DOTS.");
#endif
                }

                propertyBagType.NestedTypes.Add(propertyType);

                il.Emit(OpCodes.Ldarg_0); // this
                il.Emit(OpCodes.Newobj, propertyType.GetConstructors().First());
                il.Emit(OpCodes.Call, context.Module.ImportReference(addPropertyMethod.MakeGenericInstanceMethod(memberType)));
                var buffer = il.Create(OpCodes.Nop);
                il.Append(buffer);
                lastInstruction = buffer;
            }

            il.Emit(OpCodes.Ret);

            CodeGenUtils.PostProcessPropertyBag(context, ctorMethodBody, baseCtorCall);

            return propertyBagType;
        }

        static void PostProcessAssembly(HashSet<string> namespaceExceptions, HashSet<string> typeExceptions, Assembly assembly,
            List<FieldInfo> fields, List<PropertyInfo> properties, HashSet<Type> serializableContainerTypes)
        {
            foreach (var type in assembly.ExportedTypes)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (type.IsGenericType)
                    continue;

                if (!typeof(Component).IsAssignableFrom(type) && !CodeGenUtils.IsSerializableContainer(type))
                    continue;

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

                var includedProperties = ReflectedPropertyBagUtils.GetIncludedProperties(typeName);
                var ignoredProperties = ReflectedPropertyBagUtils.GetIgnoredProperties(typeName);

                serializableContainerTypes.Add(type);

                fields.Clear();
                type.GetFieldsRecursively(fields, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    PostProcessField(field, includedProperties, ignoredProperties, serializableContainerTypes);
                }

                properties.Clear();
                type.GetPropertiesRecursively(properties, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var property in properties)
                {
                    PostProcessProperty(property, includedProperties, ignoredProperties, serializableContainerTypes);
                }
            }
        }

        static void PostProcessField(FieldInfo field, HashSet<string> includedProperties, HashSet<string> ignoredProperties, HashSet<Type> serializableContainerTypes)
        {
            var fieldType = field.FieldType;
            if (serializableContainerTypes.Contains(fieldType))
                return;

            var fieldName = field.Name;
            var includeField = includedProperties != null && includedProperties.Contains(fieldName);
            if (!includeField)
            {
                if (ignoredProperties != null && ignoredProperties.Contains(fieldName))
                    return;

                if (!field.IsPublic)
                {
                    var isValidField = false;
                    foreach (var attribute in field.GetCustomAttributes())
                    {
                        var attributeType = attribute.GetType();
                        if (attributeType == k_NativeNameAttributeType || attributeType == typeof(SerializableAttribute))
                            isValidField = true;

                        if (attributeType == k_IgnoreAttributeType)
                        {
                            isValidField = false;
                            break;
                        }
                    }

                    if (!isValidField)
                        return;
                }
            }

            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();
            else if (ReflectedPropertyBagUtils.IsListType(fieldType))
                fieldType = fieldType.GenericTypeArguments[0];

            if (fieldType == null || fieldType.IsGenericParameter || fieldType.IsGenericType)
                return;

            if (!CodeGenUtils.IsSerializableContainer(fieldType))
                return;

            serializableContainerTypes.Add(fieldType);
        }

        static void PostProcessProperty(PropertyInfo property, HashSet<string> includedProperties, HashSet<string> ignoredProperties, HashSet<Type> serializableContainerTypes)
        {
            var propertyType = property.PropertyType;
            if (serializableContainerTypes.Contains(propertyType))
                return;

            var propertyName = property.Name;
            var includeField = includedProperties != null && includedProperties.Contains(propertyName);
            if (!includeField)
            {
                if (ignoredProperties != null && ignoredProperties.Contains(propertyName))
                    return;

                if (property.GetGetMethod(true) == null)
                    return;

                var setMethod = property.GetSetMethod(true);
                if (setMethod == null)
                    return;

                var isValidProperty = false;
                foreach (var attribute in property.CustomAttributes)
                {
                    var attributeType = attribute.AttributeType;
                    if (attributeType == k_NativePropertyAttributeType)
                    {
                        isValidProperty = true;
                        break;
                    }

                    if (attributeType == k_NativeNameAttributeType)
                    {
                        isValidProperty = true;
                        break;
                    }
                }

                // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                if ((setMethod.MethodImplementationFlags & MethodImplAttributes.InternalCall) != 0)
                    isValidProperty = true;

                if (!isValidProperty)
                    return;
            }

            if (propertyType.IsArray)
                propertyType = propertyType.GetElementType();
            else if (ReflectedPropertyBagUtils.IsListType(propertyType))
                propertyType = propertyType.GenericTypeArguments[0];

            if (!CodeGenUtils.IsSerializableContainer(propertyType))
                return;

            serializableContainerTypes.Add(propertyType);
        }

        static ILPostProcessResult CreatePostProcessResult(AssemblyDefinition assembly)
        {
            using var pe = new MemoryStream();
            using var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                WriteSymbols = true,
                SymbolStream = pdb,
                SymbolWriterProvider = new PortablePdbWriterProvider()
            };

            assembly.Write(pe, writerParameters);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));
        }
    }
}
