using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Properties.CodeGen;
using Unity.RuntimeSceneSerialization.Internal;
using Unity.XRTools.Utils;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;
using OpCodes = Mono.Cecil.Cil.OpCodes;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    /// <summary>
    /// ILPostProcessor responsible for codegen that supports the GenericMethodWrapper API
    /// </summary>
    [InitializeOnLoad]
    class RuntimeSerializationAssemblyPostprocessor : ILPostProcessor
    {
        class TypeContainer
        {
            public TypeReference TypeReference;
            public int BaseTypeCount;
        }

#if UNITY_2020_2_OR_NEWER
        static readonly int k_EditorAssemblyPathSuffix = "/Contents/Managed/UnityEngine/UnityEditor.CoreModule.dll".Length;
#endif

        const string k_SerializationAssemblyName = "Unity.RuntimeSceneSerialization";
        const string k_InvokeGenericMethodWrapper = "InvokeGenericMethodWrapper";
        const string k_CallGenericMethod = "CallGenericMethod";
        static readonly string k_SerializationUtilsTypeName = typeof(SerializationUtils).FullName;
        static readonly HashSet<string> k_PlayerAssemblies = new HashSet<string>();

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

            return compiledAssembly.Name == k_SerializationAssemblyName;
        }

        static RuntimeSerializationAssemblyPostprocessor()
        {
            // Hard-code "Temp" because of missing method exception trying to use Application.temporaryCachePath
            var tempAssembliesPath = Path.Combine("Temp", "PlayerAssemblies");
            if (File.Exists(tempAssembliesPath))
            {
                var assemblies = File.ReadAllText(tempAssembliesPath);
                var split = assemblies.Split(',');
                foreach (var assembly in split)
                {
                    k_PlayerAssemblies.Add(assembly);
                }
            }

            EditorApplication.delayCall += () =>
            {
                k_PlayerAssemblies.Clear();
                var assemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
                foreach (var assembly in assemblies)
                {
                    k_PlayerAssemblies.Add(assembly.name);
                }

                File.WriteAllText(tempAssembliesPath, string.Join(",", k_PlayerAssemblies));
            };
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

            using (var assemblyDefinition = CreateAssemblyDefinition(compiledAssembly))
            {
                return ProcessAssembly(assemblyDefinition);
            }
        }

        static ILPostProcessResult ProcessAssembly(AssemblyDefinition compiledAssembly)
        {
            var module = compiledAssembly.MainModule;
            var componentTypes = new List<TypeContainer>();

#if UNITY_2020_2_OR_NEWER
            var editorAssemblyPath = Assembly.GetAssembly(typeof(EditorApplication)).Location;
            var editorPath = editorAssemblyPath.Substring(0, editorAssemblyPath.Length - k_EditorAssemblyPathSuffix);
#else
            var editorPath = EditorApplication.applicationContentsPath;
#endif

            var assemblyExceptions = RuntimeSerializationSettingsUtils.GetAssemblyExceptions();
            var namespaceExceptions = RuntimeSerializationSettingsUtils.GetNamespaceExceptions();
            var typeExceptions = RuntimeSerializationSettingsUtils.GetTypeExceptions();
            ReflectionUtils.ForEachAssembly(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                if (!CodeGenUtils.IsBuiltInAssembly(assembly, editorPath) && !k_PlayerAssemblies.Contains(assembly.GetName().Name))
                    return;

                if (assemblyExceptions.Contains(assembly.FullName))
                    return;

                PostProcessAssembly(namespaceExceptions, typeExceptions, module, assembly, componentTypes);
            });

            PostProcessGenericMethodWrapper(componentTypes, module);

            return CreatePostProcessResult(compiledAssembly);
        }

        static void PostProcessGenericMethodWrapper(List<TypeContainer> componentTypes, ModuleDefinition module)
        {
            var serializationUtilsType = module.GetType(k_SerializationUtilsTypeName);
            if (serializationUtilsType == null)
            {
                Console.Error.WriteLine($"Could not find {k_SerializationUtilsTypeName}");
                return;
            }

            MethodDefinition invokeGenericMethodWrapperMethod = null;
            MethodDefinition callGenericMethodMethod = null;
            foreach (var method in serializationUtilsType.Methods)
            {
                if (method.Name == k_InvokeGenericMethodWrapper)
                    invokeGenericMethodWrapperMethod = method;

                if (method.Name == k_CallGenericMethod)
                    callGenericMethodMethod = method;
            }

            if (invokeGenericMethodWrapperMethod == null)
            {
                Console.Error.WriteLine($"Could not find {k_InvokeGenericMethodWrapper}");
                return;
            }

            if (callGenericMethodMethod == null)
            {
                Console.Error.WriteLine($"Could not find {k_CallGenericMethod}");
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
                Console.Error.WriteLine("Failed to find the branch we need to replace");
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

        static void PostProcessAssembly(HashSet<string> namespaceExceptions, HashSet<string> typeExceptions,
            ModuleDefinition module, Assembly assembly, List<TypeContainer> componentTypes)
        {
            var componentType = typeof(Component);
            var skipGeneration = typeof(SkipGeneration);
            foreach (var type in assembly.ExportedTypes)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (type.IsGenericType)
                    continue;

                if (!componentType.IsAssignableFrom(type))
                    continue;

                if (type.IsDefined(skipGeneration, false))
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

                componentTypes.Add(new TypeContainer
                {
                    TypeReference = module.ImportReference(type),
                    BaseTypeCount = GetBaseTypeCount(type)
                });
            }
        }

        static int GetBaseTypeCount(Type type)
        {
            var count = 0;
            var baseType = type.BaseType;
            while (baseType != null)
            {
                count++;
                baseType = baseType.BaseType;
            }

            return count;
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
