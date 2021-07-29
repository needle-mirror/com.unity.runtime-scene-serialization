using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Properties.CodeGen;
using Unity.Properties.CodeGen.Blocks;
using Unity.RuntimeSceneSerialization.Internal;
using UnityEngine;
using UnityEngine.Scripting;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    /// <summary>
    /// ILPostProcessor responsible for generating property bags for user types (including packages)
    /// </summary>
    class UserAssemblyPostProcessor : ILPostProcessor
    {
        static readonly MethodInfo k_GetTypeMethod = typeof(Type).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(x => x.GetParameters().Length == 1 && x.Name == nameof(Type.GetType));

        static readonly HashSet<string> k_IgnoredTypeNames = new HashSet<string>
        {
            typeof(AnimationCurve).FullName,
            typeof(Keyframe).FullName,
            typeof(Vector2Int).FullName,
            typeof(Vector3Int).FullName,
            typeof(Rect).FullName,
            typeof(RectInt).FullName,
            typeof(BoundsInt).FullName,
            typeof(Version).FullName
        };

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
            if (!compiledAssembly.RequiresCodegen())
                return false;

            var assemblyName = compiledAssembly.Name;
            switch (assemblyName)
            {
                case CodeGenUtils.RuntimeSerializationAssemblyName:
                    return true;
                case CodeGenUtils.ExternalPropertyBagAssemblyName:
                case CodeGenUtils.RuntimeSerializationEditorAssemblyName:
                case CodeGenUtils.RuntimeSerializationCodeGenAssemblyName:
                    return false;
                default:
                    return RuntimeSerializationSettingsUtils.GetAssemblyInclusions().Contains(assemblyName);
            }
        }

        static AssemblyDefinition CreateAssemblyDefinition(ICompiledAssembly compiledAssembly)
        {
            var resolver = new AssemblyResolver(compiledAssembly);

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

            using (var assemblyDefinition = CreateAssemblyDefinition(compiledAssembly))
            {
                return GeneratePropertyBags(assemblyDefinition, compiledAssembly.Defines);
            }
        }

        static ILPostProcessResult GeneratePropertyBags(AssemblyDefinition compiledAssembly, string[] defines)
        {
            var module = compiledAssembly.MainModule;
            var context = new Context(module, defines);
            var serializableTypes = new HashSet<TypeDefinition>();
            var namespaceExceptions = RuntimeSerializationSettingsUtils.GetNamespaceExceptions();
            var typeExceptions = RuntimeSerializationSettingsUtils.GetTypeExceptions();

            foreach (var type in CodeGenUtils.PostProcessAssembly(namespaceExceptions, typeExceptions, compiledAssembly, true))
            {
                if (type.Module != module || k_IgnoredTypeNames.Contains(type.FullName))
                    continue;

                serializableTypes.Add(type);
                PostProcessType(type, serializableTypes);
            }

            if (serializableTypes.Count == 0)
                return null;

            GeneratePropertyBagsForSerializableTypes(context, serializableTypes);

            if (!Directory.Exists("Logs"))
                Directory.CreateDirectory("Logs");

            File.AppendAllText("Logs/RuntimeSerializationLogs.txt",
                $"Generated {serializableTypes.Count} property bags in {compiledAssembly.Name}\n");

            return CreatePostProcessResult(compiledAssembly);
        }

        static void PostProcessType(TypeDefinition type, HashSet<TypeDefinition> serializableTypes)
        {
            if (type.HasGenericParameters && !type.IsGenericInstance)
                return;

            foreach (var (member, _) in CodeGenUtils.GetPropertyMembers(type))
            {
                var memberType = Utility.GetMemberType(member).ResolveGenericParameter(type);
                var resolvedMemberType = memberType.Resolve();
                if (resolvedMemberType == null)
                {
                    Console.Error.WriteLine($"Couldn't resolve {memberType}");
                    continue;
                }

                if (memberType.IsArray)
                    resolvedMemberType = resolvedMemberType.GetElementType().Resolve();
                else if (CodeGenUtils.IsListType(memberType, out var genericInstance))
                    resolvedMemberType = genericInstance.GenericArguments[0].Resolve();
                else
                    resolvedMemberType = resolvedMemberType.Resolve();

                if (resolvedMemberType.HasGenericParameters && !resolvedMemberType.IsGenericInstance)
                    continue;

                var (hasSkipGenerationAttribute, hasCompilerGeneratedAttribute) = CodeGenUtils.GetSerializationAttributes(resolvedMemberType);
                if (hasSkipGenerationAttribute || hasCompilerGeneratedAttribute)
                    continue;

                // Skip UnityObjectTypes because we will be using UnityObjectReferenceProperty
                if (CodeGenUtils.IsAssignableToUnityObject(resolvedMemberType))
                    continue;

                if (!CodeGenUtils.IsSerializableContainer(resolvedMemberType))
                {
                    if (!resolvedMemberType.IsValueType || CodeGenUtils.TypeIsPrimitive(resolvedMemberType, resolvedMemberType.FullName))
                        continue;
                }

                if (serializableTypes.Add(resolvedMemberType))
                    PostProcessType(resolvedMemberType, serializableTypes);
            }
        }

        static void GeneratePropertyBagsForSerializableTypes(Context context, HashSet<TypeDefinition> serializableContainerTypes)
        {
            var module = context.Module;
            var createValueMethod = module.ImportReference(UnityObjectReference.CreateValueMethod);
            var createArrayMethod = module.ImportReference(UnityObjectReference.CreateArrayMethod);
            var createListMethod = module.ImportReference(UnityObjectReference.CreateListMethod);
            var getTypeMethod = module.ImportReference(k_GetTypeMethod);
            var objectReference = context.ImportReference(typeof(object));
            var unityObjectReference = context.ImportReference(typeof(UnityObjectReference));
            var unityObjectListReference = context.ImportReference(typeof(List<UnityObjectReference>));
            var listType = context.ImportReference(typeof(List<>));
            var preserveAttributeConstructor = module.ImportReference(typeof(PreserveAttribute).GetConstructor(Type.EmptyTypes));

            var propertyBagDefinitions = new List<Tuple<TypeDefinition, TypeReference>>();
            var externalContainerTypes = new Dictionary<string, TypeDefinition>();
            foreach (var type in serializableContainerTypes)
            {
                var externalContainer = TryGenerateExternalContainerType(type, context, objectReference, preserveAttributeConstructor);
                if (externalContainer != null)
                    externalContainerTypes[type.FullName] = externalContainer;
            }

            var moduleTypes = module.Types;
            foreach (var type in serializableContainerTypes)
            {
                Console.WriteLine($"GENERATE FOR {type}");
                var containerType = type.Module == module ? type : context.ImportReference(type);
                externalContainerTypes.TryGetValue(type.FullName, out var externalContainer);
                var propertyBagType = GeneratePropertyBag(context, containerType, externalContainer, externalContainerTypes, getTypeMethod, createValueMethod, createArrayMethod, createListMethod, unityObjectReference, unityObjectListReference, listType);
                moduleTypes.Add(propertyBagType);
                propertyBagDefinitions.Add(new Tuple<TypeDefinition, TypeReference>(propertyBagType, externalContainer ?? containerType));
            }

            var propertyBagRegistryTypeDefinition = PropertyBagRegistry.Generate(context, propertyBagDefinitions);
            var containsRegistry = false;
            foreach (var type in moduleTypes)
            {
                if (type.FullName == propertyBagRegistryTypeDefinition.FullName)
                {
                    containsRegistry = true;
                    break;
                }
            }

            if (!containsRegistry)
                moduleTypes.Add(propertyBagRegistryTypeDefinition);
        }

        static TypeDefinition TryGenerateExternalContainerType(TypeDefinition type, Context context, TypeReference objectReference, MethodReference preserveAttributeConstructor)
        {
            if (type.Module == context.Module || type.IsPublic)
                return null;

            var externalTypeContainer = new TypeDefinition
            (
                Context.kNamespace,
                Utility.GetSanitizedName(type.FullName, "_Container__"),
                TypeAttributes.Class | TypeAttributes.NotPublic,
                objectReference
            );

            var baseEmptyConstructor = context.Module.ImportReference(objectReference.Resolve().GetConstructors().First());
            const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            var constructor = new MethodDefinition(".ctor", methodAttributes, context.ImportReference(typeof(void)));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, baseEmptyConstructor));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            constructor.CustomAttributes.Add(new CustomAttribute(preserveAttributeConstructor));
            externalTypeContainer.Methods.Add(constructor);

            var containerField = new FieldDefinition(ReflectedMemberPropertyName.ContainerFieldName, FieldAttributes.Public, objectReference);
            containerField.CustomAttributes.Add(new CustomAttribute(preserveAttributeConstructor));
            externalTypeContainer.Fields.Add(containerField);

            context.Module.Types.Add(externalTypeContainer);

            return externalTypeContainer;
        }

        static TypeDefinition GeneratePropertyBag(Context context, TypeReference containerType, TypeDefinition externalContainer,
            Dictionary<string, TypeDefinition> externalContainerTypes, MethodReference getTypeMethod,
            MethodReference createValueMethod, MethodReference createArrayMethod, MethodReference createListMethod,
            TypeReference unityObjectReference, TypeReference unityObjectListReference, TypeReference listType)
        {
            var effectiveContainerType = externalContainer ?? containerType;
            var propertyBagType = PropertyBag.GeneratePropertyBagHeader(context, effectiveContainerType, out var ctorMethod, out var addPropertyMethod);
            var il = ctorMethod.Body.GetILProcessor();
            foreach (var (member, nameOverride) in CodeGenUtils.GetPropertyMembers(containerType.Resolve()))
            {
                if (CodeGenUtils.TryGenerateUnityObjectProperty(context, containerType, externalContainer, member, il, addPropertyMethod, createValueMethod, createArrayMethod, createListMethod, unityObjectReference, unityObjectListReference))
                    continue;

                var memberType = context.ImportReference(Utility.GetMemberType(member).ResolveGenericParameter(containerType));
                TypeReference externalMember;
                if (memberType.IsArray)
                {
                    externalContainerTypes.TryGetValue(memberType.GetElementType().FullName, out var externalMemberDefinition);
                    externalMember = externalMemberDefinition?.MakeArrayType();
                }
                else if (CodeGenUtils.IsListType(memberType, out var genericInstance))
                {
                    externalContainerTypes.TryGetValue(genericInstance.GenericArguments[0].FullName, out var externalMemberDefinition);
                    externalMember = externalMemberDefinition == null ? null : listType.MakeGenericInstanceType(externalMemberDefinition);
                }
                else
                {
                    externalContainerTypes.TryGetValue(memberType.FullName, out var externalMemberDefinition);
                    externalMember = externalMemberDefinition;
                }

                var effectiveMemberType = externalMember ?? memberType;
                if (memberType.IsGenericInstance || memberType.IsArray)
                    PropertyBag.RegisterCollectionTypes(context, effectiveContainerType, effectiveMemberType, il);

                TypeDefinition propertyType;
                if (externalMember != null || externalContainer != null)
                {
                    propertyType = ReflectedExternalProperty.Generate(context, containerType, externalContainer, member, externalMember, getTypeMethod, nameOverride);
                }
                else if (member.IsPublicOrAssembly())
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
                il.Emit(OpCodes.Call, context.Module.ImportReference(addPropertyMethod.MakeGenericInstanceMethod(effectiveMemberType)));
            }

            il.Emit(OpCodes.Ret);
            return propertyBagType;
        }

        static ILPostProcessResult CreatePostProcessResult(AssemblyDefinition assembly)
        {
            using (var pe = new MemoryStream())
            using (var pdb = new MemoryStream())
            {
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
}
