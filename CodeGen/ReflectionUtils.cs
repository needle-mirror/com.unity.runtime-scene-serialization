using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    /// <summary>
    /// Utility methods for common reflection-based operations
    /// </summary>
    static class ReflectionUtils
    {
        static readonly Lazy<MethodInfo> k_GetAssemblyLoadContextMethod = new (() => Type.GetType("System.Runtime.Loader.AssemblyLoadContext")?.GetMethod("GetLoadContext", BindingFlags.Static | BindingFlags.Public));
        static readonly Dictionary<object, bool> k_KnownAssemblyLoadContextStates = new();

        static Assembly[] s_Assemblies;
        static List<Type[]> s_TypesPerAssembly;
        static FieldInfo s_AssemblyLoadContextStateField;

        public static Assembly[] GetCachedAssemblies()
        {
            if (s_Assemblies != null)
                return s_Assemblies;

            s_Assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(IsFromLiveAssemblyLoadContext).ToArray();
            return s_Assemblies;
        }

        static bool IsFromLiveAssemblyLoadContext(Assembly assembly)
        {
            var getAssemblyLoadContextMethod = k_GetAssemblyLoadContextMethod.Value;
            if (getAssemblyLoadContextMethod == null)
                return true;

            var assemblyLoadContext = getAssemblyLoadContextMethod.Invoke(null, new object[] { assembly });
            if (assemblyLoadContext == null)
                return true;

            if (k_KnownAssemblyLoadContextStates.TryGetValue(assemblyLoadContext, out var knownState))
                return knownState;

            if (s_AssemblyLoadContextStateField == null)
                s_AssemblyLoadContextStateField = getAssemblyLoadContextMethod.DeclaringType?.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);

            var state = s_AssemblyLoadContextStateField?.GetValue(assemblyLoadContext);
            var result = 0 == Convert.ToInt32(state);
            k_KnownAssemblyLoadContextStates[assemblyLoadContext] = result;
            return result;
        }

        static List<Type[]> GetCachedTypesPerAssembly()
        {
            if (s_TypesPerAssembly == null)
            {
                var assemblies = GetCachedAssemblies();
                s_TypesPerAssembly = new(assemblies.Length);
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        s_TypesPerAssembly.Add(assembly.GetTypes());
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        // Skip any assemblies that don't load properly -- suppress errors
                    }
                }
            }

            return s_TypesPerAssembly;
        }

        /// <summary>
        /// Iterate through all assemblies and execute a method on each one
        /// Catches ReflectionTypeLoadExceptions in each iteration of the loop
        /// </summary>
        /// <param name="callback">The callback method to execute for each assembly</param>
        public static void ForEachAssembly(Action<Assembly> callback)
        {
            var assemblies = GetCachedAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    callback(assembly);
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip any assemblies that don't load properly -- suppress errors
                }
            }
        }

        /// <summary>
        /// Search all assemblies for a type that matches a given predicate delegate
        /// </summary>
        /// <param name="predicate">The predicate; Returns true for the type that matches the search</param>
        /// <returns>The type found, or null if no matching type exists</returns>
        public static Type FindType(Func<Type, bool> predicate)
        {
            var typesPerAssembly = GetCachedTypesPerAssembly();
            foreach (var types in typesPerAssembly)
            {
                foreach (var type in types)
                {
                    if (predicate(type))
                        return type;
                }
            }

            return null;
        }
    }
}
