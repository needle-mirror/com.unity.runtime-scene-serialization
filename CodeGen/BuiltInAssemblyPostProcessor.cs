using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.RuntimeSceneSerialization.CodeGen.Blocks;
using Unity.RuntimeSceneSerialization.Internal;
using UnityEditor;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    /// <summary>
    /// ILPostProcessor responsible for generating property bags for types built into Unity
    /// </summary>
    [InitializeOnLoad]
    class BuiltInAssemblyPostProcessor : ILPostProcessor
    {
        class BuiltInAssemblyResolver : IAssemblyResolver
        {
            public readonly Dictionary<string, AssemblyDefinition> Assemblies = new ();
            public void Dispose() {}

            public AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                return Assemblies.TryGetValue(name.Name, out var assemblyDefinition) ? assemblyDefinition : null;
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                return Assemblies.TryGetValue(name.Name, out var assemblyDefinition) ? assemblyDefinition : null;
            }
        }
        const string k_NativePropertyAttributeType = "NativePropertyAttribute";
        const string k_NativeNameAttributeType = "NativeNameAttribute";
        const string k_SerializableAttributeType = "SerializableAttribute";
        const string k_IgnoreAttributeType = "IgnoreAttribute";

        static string s_EditorAssemblyLocation;

        static readonly HashSet<string> k_IgnoredTypes = new()
        {
            "AnimationCurve",
            "Keyframe",
            "Vector2Int",
            "Vector3Int",
            "Rect",
            "RectInt",
            "BoundsIn"
        };

        static readonly HashSet<string> k_PreCompiledAssemblies = new();

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

            CodeGenUtils.CallInitializeOnLoadMethodsIfNeeded();

            using var assemblyDefinition = CreateAssemblyDefinition(compiledAssembly);
            return GeneratePropertyBags(assemblyDefinition, compiledAssembly.Defines);
        }

        static ILPostProcessResult GeneratePropertyBags(AssemblyDefinition compiledAssembly, string[] defines)
        {
            var module = compiledAssembly.MainModule;
            var context = new Context(module, defines);
            var fields = new List<FieldDefinition>();
            var properties = new List<PropertyDefinition>();
            var serializableContainerTypes = new HashSet<TypeDefinition>();
            var assemblyInclusions = RuntimeSerializationSettingsUtils.GetAssemblyInclusions();
            var namespaceExceptions = RuntimeSerializationSettingsUtils.GetNamespaceExceptions();
            var typeExceptions = RuntimeSerializationSettingsUtils.GetTypeExceptions();

            var assemblyResolver = new BuiltInAssemblyResolver();
            var assemblies = assemblyResolver.Assemblies;
            var assemblyPaths = new HashSet<string>();
            assemblyPaths.UnionWith(k_PreCompiledAssemblies);
            assemblyPaths.UnionWith(Directory.GetFiles(s_EditorAssemblyLocation, "*.dll", SearchOption.AllDirectories));
            foreach (var path in assemblyPaths)
            {
                try
                {
                    var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = assemblyResolver });
                    assemblies[assembly.Name.Name] = assembly;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception loading assembly at path {path}:");
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }

            foreach (var kvp in assemblies)
            {
                var assembly = kvp.Value;
                try
                {
                    var assemblyName = assembly.Name.Name;
                    if (!assemblyInclusions.Contains(assemblyName))
                        continue;

                    PostProcessAssembly(namespaceExceptions, typeExceptions, assembly, fields, properties, serializableContainerTypes);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception processing assembly {kvp.Key}:");
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }

            if (serializableContainerTypes.Count == 0)
                return null;

            GeneratePropertyBagsForSerializableTypes(context, serializableContainerTypes);

            return CreatePostProcessResult(compiledAssembly);
        }

        static void GeneratePropertyBagsForSerializableTypes(Context context, HashSet<TypeDefinition> serializableContainerTypes)
        {
            var module = context.Module;
            var propertyBagDefinitions = new List<Tuple<TypeDefinition, TypeReference>>();
            foreach (var type in serializableContainerTypes)
            {
                Console.WriteLine($"GENERATE FOR {type}");
                var containerType = context.ImportReference(type);
                var propertyBagType = GeneratePropertyBag(context, containerType);

                module.Types.Add(propertyBagType);
                propertyBagDefinitions.Add(new Tuple<TypeDefinition, TypeReference>(propertyBagType, containerType));
            }

            var propertyBagRegistryTypeDefinition = PropertyBagRegistry.Generate(context, propertyBagDefinitions);
            module.Types.Add(propertyBagRegistryTypeDefinition);
        }

        static TypeDefinition GeneratePropertyBag(Context context, TypeReference containerType)
        {
            var propertyBagType = PropertyBag.GeneratePropertyBagHeader(context, containerType, out var ctorMethod, out var addPropertyMethod);
            var ctorMethodBody = ctorMethod.Body;
            var il = ctorMethodBody.GetILProcessor();
            var baseCtorCall = ctorMethodBody.Instructions.Last();
            foreach (var (member, nameOverride) in CodeGenUtils.GetPropertyMembers(containerType.Resolve()))
            {
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
            }

            il.Emit(OpCodes.Ret);

            CodeGenUtils.PostProcessPropertyBag(context, ctorMethodBody, baseCtorCall);

            return propertyBagType;
        }

        static void PostProcessAssembly(HashSet<string> namespaceExceptions, HashSet<string> typeExceptions, AssemblyDefinition assembly,
            List<FieldDefinition> fields, List<PropertyDefinition> properties, HashSet<TypeDefinition> serializableContainerTypes)
        {
            if (assembly.MainModule == null)
            {
                Console.WriteLine($"Error processing assembly {assembly.Name?.Name}. MainModule was null.");
                return;
            }

            foreach (var type in assembly.MainModule.Types)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (type.HasGenericParameters && !type.IsGenericInstance)
                    continue;

                if (k_IgnoredTypes.Contains(type.Name))
                    continue;

                if (!CodeGenUtils.IsAssignableToComponent(type) && !CodeGenUtils.IsSerializableContainer(type))
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
                type.GetFieldsRecursively(fields);
                foreach (var field in fields)
                {
                    // Static fields should not be serialized
                    if (field.IsStatic)
                        continue;

                    PostProcessField(field, includedProperties, ignoredProperties, serializableContainerTypes);
                }

                properties.Clear();
                type.GetPropertiesRecursively(properties);
                foreach (var property in properties)
                {
                    PostProcessProperty(property, includedProperties, ignoredProperties, serializableContainerTypes);
                }
            }
        }

        static void PostProcessField(FieldDefinition field, HashSet<string> includedProperties, HashSet<string> ignoredProperties, HashSet<TypeDefinition> serializableContainerTypes)
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
                    foreach (var attribute in field.CustomAttributes)
                    {
                        var attributeTypeName = attribute.GetType().Name;
                        if (attributeTypeName == k_NativeNameAttributeType || attributeTypeName == k_SerializableAttributeType)
                            isValidField = true;

                        if (attributeTypeName == k_IgnoreAttributeType)
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
            else if (CodeGenUtils.IsListType(fieldType, out var genericInstance))
                fieldType = genericInstance as TypeReference;

            if (fieldType == null || fieldType.IsGenericParameter || !fieldType.IsGenericInstance)
                return;

            var fieldTypeDefinition = fieldType.Resolve();
            if (fieldTypeDefinition == null)
            {
                Console.WriteLine($"Error processing field {fieldName} in {field.DeclaringType}. Failed to resolve type {fieldType}");
                return;
            }

            if (!CodeGenUtils.IsSerializableContainer(fieldTypeDefinition))
                return;

            serializableContainerTypes.Add(fieldTypeDefinition);
        }

        static void PostProcessProperty(PropertyDefinition property, HashSet<string> includedProperties, HashSet<string> ignoredProperties, HashSet<TypeDefinition> serializableContainerTypes)
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

                if (property.GetMethod != null)
                    return;

                var setMethod = property.SetMethod;
                if (setMethod == null)
                    return;

                var isValidProperty = false;
                foreach (var attribute in property.CustomAttributes)
                {
                    var attributeTypeName = attribute.AttributeType.Name;
                    if (attributeTypeName == k_NativePropertyAttributeType)
                    {
                        isValidProperty = true;
                        break;
                    }

                    if (attributeTypeName == k_NativeNameAttributeType)
                    {
                        isValidProperty = true;
                        break;
                    }
                }

                // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                if ((setMethod.ImplAttributes & MethodImplAttributes.InternalCall) != 0)
                    isValidProperty = true;

                if (!isValidProperty)
                    return;
            }

            if (propertyType.IsArray)
                propertyType = propertyType.GetElementType();
            else if (CodeGenUtils.IsListType(propertyType, out var genericInstance))
                propertyType = genericInstance as TypeReference;

            if (propertyType == null)
            {
                Console.WriteLine($"Error processing property {propertyName} in {property.DeclaringType}. Type was null.");
                return;
            }

            var propertyTypeDefinition = propertyType.Resolve();
            if (propertyTypeDefinition == null)
            {
                Console.WriteLine($"Error processing property {propertyName} in {property.DeclaringType}. Failed to resolve type {propertyType}.");
                return;
            }

            if (!CodeGenUtils.IsSerializableContainer(propertyTypeDefinition))
                return;

            serializableContainerTypes.Add(propertyTypeDefinition);
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
