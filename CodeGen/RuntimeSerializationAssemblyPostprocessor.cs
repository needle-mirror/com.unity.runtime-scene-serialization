﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.RuntimeSceneSerialization.Internal;
using UnityEditor;
using UnityEditor.Compilation;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    /// <summary>
    /// ILPostProcessor responsible for code generation that supports the GenericMethodWrapper API
    /// </summary>
    [InitializeOnLoad]
    class RuntimeSerializationAssemblyPostprocessor : ILPostProcessor
    {
        class TypeContainer
        {
            public TypeReference TypeReference;
            public int BaseTypeCount;
        }

        const string k_InvokeGenericMethodWrapper = "InvokeGenericMethodWrapper";
        const string k_CallGenericMethod = "CallGenericMethod";
        static readonly string k_SerializationUtilsTypeName = typeof(SerializationUtils).FullName;
        static readonly HashSet<string> k_PlayerAssemblies = new();
        static readonly HashSet<string> k_PlayerWithoutTestAssemblies = new();
        static readonly HashSet<string> k_PreCompiledAssemblies = new();

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

            return compiledAssembly.Name == CodeGenUtils.RuntimeSerializationAssemblyName;
        }

        static RuntimeSerializationAssemblyPostprocessor()
        {
            // Hard-code "Temp" because of missing method exception trying to use Application.temporaryCachePath
            var preCompiledAssembliesPath = Path.Combine("Temp", "PreCompiledAssemblies");
            var playerAssembliesPath = Path.Combine("Temp", "PlayerAssemblies");
            var playerWithoutTestAssembliesPath = Path.Combine("Temp", "PlayerWithoutTestAssemblies");
            CodeGenUtils.ReadAssemblyList(preCompiledAssembliesPath, k_PreCompiledAssemblies);
            CodeGenUtils.ReadAssemblyList(playerAssembliesPath, k_PlayerAssemblies);
            CodeGenUtils.ReadAssemblyList(playerWithoutTestAssembliesPath, k_PlayerWithoutTestAssemblies);

            EditorApplication.delayCall += () =>
            {
                var assemblies = UnityEditor.Compilation.CompilationPipeline.GetPrecompiledAssemblyPaths(UnityEditor.Compilation.CompilationPipeline.PrecompiledAssemblySources.All);
                CodeGenUtils.WriteAssemblyList(preCompiledAssembliesPath, k_PreCompiledAssemblies, assemblies);
                CodeGenUtils.WriteAssemblyList(playerAssembliesPath, k_PlayerAssemblies, AssembliesType.Player);
                CodeGenUtils.WriteAssemblyList(playerWithoutTestAssembliesPath, k_PlayerWithoutTestAssemblies, AssembliesType.PlayerWithoutTestAssemblies);
            };
        }

        static AssemblyDefinition CreateAssemblyDefinition(ICompiledAssembly compiledAssembly, out PostProcessorAssemblyResolver resolver)
        {
            resolver = new PostProcessorAssemblyResolver(compiledAssembly);

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

            var isTestBuild = compiledAssembly.Defines.Contains("UNITY_INCLUDE_TESTS");
            using var assemblyDefinition = CreateAssemblyDefinition(compiledAssembly, out var resolver);
            return ProcessAssembly(assemblyDefinition, resolver, isTestBuild);
        }

        static ILPostProcessResult ProcessAssembly(AssemblyDefinition compiledAssembly, PostProcessorAssemblyResolver resolver, bool isTestBuild)
        {
            var assemblies = new HashSet<string>();
            assemblies.UnionWith(isTestBuild ? k_PlayerAssemblies : k_PlayerWithoutTestAssemblies);
            assemblies.UnionWith(k_PreCompiledAssemblies);
            if (assemblies.Count == 0)
            {
                Console.WriteLine("Error: No assemblies to process in RuntimeSerializationAssemblyPostProcessor");
                return null;
            }

            var module = compiledAssembly.MainModule;
            var componentTypes = new List<TypeContainer>();

            var assemblyInclusions = RuntimeSerializationSettingsUtils.GetAssemblyInclusions();
            var namespaceExceptions = RuntimeSerializationSettingsUtils.GetNamespaceExceptions();
            var typeExceptions = RuntimeSerializationSettingsUtils.GetTypeExceptions();
            var stringType = module.ImportReference(typeof(string));
            var internalsVisibleToAttributeConstructor = module.ImportReference(
                typeof(InternalsVisibleToAttribute).GetConstructor(new[] { typeof(string) }));

            assemblyInclusions.Add(CodeGenUtils.RuntimeSerializationAssemblyName);

            var searchPaths = new HashSet<string>();
            foreach (var path in assemblies)
            {
                searchPaths.Add(Path.GetDirectoryName(path));
            }

            foreach (var assembly in ReflectionUtils.GetCachedAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                var location = assembly.Location;
                if (string.IsNullOrEmpty(location))
                    continue;

                assemblies.Add(location);

                var fullPath = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(fullPath))
                    searchPaths.Add(fullPath);
            }

            foreach (var path in searchPaths)
            {
                resolver.AddSearchDirectory(path);
            }

            foreach (var path in assemblies)
            {
                var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
                var name = assembly.Name.Name;
                if (!assemblyInclusions.Contains(name))
                    continue;

                var attribute = new CustomAttribute(internalsVisibleToAttributeConstructor);
                attribute.ConstructorArguments.Add(new CustomAttributeArgument(stringType, name));
                compiledAssembly.CustomAttributes.Add(attribute);

                foreach (var type in CodeGenUtils.PostProcessAssembly(namespaceExceptions, typeExceptions, assembly))
                {
                    componentTypes.Add(new TypeContainer
                    {
                        TypeReference = module.ImportReference(type),
                        BaseTypeCount = GetBaseTypeCount(type)
                    });
                }
            }

            PostProcessGenericMethodWrapper(componentTypes, module);

            return CreatePostProcessResult(compiledAssembly);
        }

        static void PostProcessGenericMethodWrapper(List<TypeContainer> componentTypes, ModuleDefinition module)
        {
            var serializationUtilsType = module.GetType(k_SerializationUtilsTypeName);
            if (serializationUtilsType == null)
            {
                Console.WriteLine($"Error: Could not find type: {k_SerializationUtilsTypeName}");
                return;
            }

            MethodDefinition invokeGenericMethodWrapperMethod = null;
            MethodDefinition callGenericMethodMethod = null;
            foreach (var method in serializationUtilsType.Methods)
            {
                switch (method.Name)
                {
                    case k_InvokeGenericMethodWrapper:
                        invokeGenericMethodWrapperMethod = method;
                        break;
                    case k_CallGenericMethod:
                        callGenericMethodMethod = method;
                        break;
                }
            }

            if (invokeGenericMethodWrapperMethod == null)
            {
                Console.WriteLine($"Error: Could not find type: {k_InvokeGenericMethodWrapper}");
                return;
            }

            if (callGenericMethodMethod == null)
            {
                Console.WriteLine($"Error: Could not find type: {k_CallGenericMethod}");
                return;
            }

            var methodBody = invokeGenericMethodWrapperMethod.Body;
            var il = methodBody.GetILProcessor();
            var instructions = methodBody.Instructions;
            var ret = instructions.Last();

            // Remove final Ret so we can use Append, and then add it back at the end
            il.Remove(ret);

            var firstBranch = instructions.FirstOrDefault(instruction => instruction.OpCode == OpCodes.Brfalse_S);
            if (firstBranch == null)
            {
                Console.WriteLine("Error: Failed to find the branch we need to replace");
                return;
            }

            // Set previousBranchPoint to the previous instruction so we can append the new on after it
            var previousInstruction = firstBranch.Previous;

            // Remove first branch as we will be adding it back with an instruction that points to the beginning of the next sequence
            il.Remove(firstBranch);

            il.Append(il.Create(OpCodes.Br, ret));

            componentTypes.Sort((a, b) => b.BaseTypeCount.CompareTo(a.BaseTypeCount));

            var boolType = module.ImportReference(typeof(bool));
            var variables = methodBody.Variables;
            foreach (var container in componentTypes)
            {
                var type = container.TypeReference;
                Console.WriteLine($"ADD WRAPPER FOR {type.Name}");

                var instanceVariable = new VariableDefinition(type);
                var boolVariable = new VariableDefinition(boolType);
                variables.Add(instanceVariable);
                variables.Add(boolVariable);

                var first = il.Create(OpCodes.Ldarg_0);

                // Add a branch targeting the first instruction of this sequence after the previous `IsInst` sequence
                il.InsertAfter(previousInstruction, il.Create(OpCodes.Brfalse, first));

                il.Append(first);

                // if (instanceObject is ComponentType component)
                il.Append(il.Create(OpCodes.Isinst, type));
                il.Append(il.Create(OpCodes.Dup));
                il.Append(il.Create(OpCodes.Stloc, instanceVariable));
                il.Append(il.Create(OpCodes.Ldnull));
                il.Append(il.Create(OpCodes.Cgt_Un));
                il.Append(il.Create(OpCodes.Stloc, boolVariable));

                // Update previousInstruction to point to the end of this sequence--we will be adding the branch on the next iteration
                previousInstruction = il.Create(OpCodes.Ldloc, boolVariable);
                il.Append(previousInstruction);

                // RuntimePrefabPropertyOverride.GetOverrides<ComponentType>(instanceObject2, overrides, transformPath, componentIndex);
                var method = callGenericMethodMethod.MakeGenericInstanceMethod(type);
                il.Append(il.Create(OpCodes.Ldloc, instanceVariable));
                il.Append(il.Create(OpCodes.Ldarg_1));
                il.Append(il.Create(OpCodes.Call, method));

                // Branch to end of method
                il.Append(il.Create(OpCodes.Br, ret));
            }

            // Add the final post-IsInst branch
            il.InsertAfter(previousInstruction, il.Create(OpCodes.Brfalse, ret));

            // Add back final Ret
            il.Append(ret);
        }

        static int GetBaseTypeCount(TypeDefinition type)
        {
            var count = 0;
            var baseType = type.BaseType;
            while (baseType != null)
            {
                count++;
                var resolved = baseType.Resolve();
                if (resolved == null)
                    break;

                baseType = resolved.BaseType;
            }

            return count;
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
