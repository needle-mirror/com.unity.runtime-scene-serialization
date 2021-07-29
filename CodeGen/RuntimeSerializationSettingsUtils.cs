using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    static class RuntimeSerializationSettingsUtils
    {
        const string k_AssemblyInclusionsPath = "ProjectSettings/RuntimeSerializationAssemblyInclusions.txt";
        const string k_NamespaceExceptionsPath = "ProjectSettings/RuntimeSerializationNamespaceExceptions.txt";
        const string k_TypeExceptionsPath = "ProjectSettings/RuntimeSerializationTypeExceptions.txt";

        static HashSet<string> s_AssemblyInclusions;
        static HashSet<string> s_NamespaceExceptions;
        static HashSet<string> s_TypeExceptions;

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<string> k_SortableList = new List<string>();

        internal static void ClearSettingsCache()
        {
            s_AssemblyInclusions = null;
            s_NamespaceExceptions = null;
            s_TypeExceptions = null;
        }

        internal static HashSet<string> GetAssemblyInclusions()
        {
            if (s_AssemblyInclusions != null)
                return s_AssemblyInclusions;

            s_AssemblyInclusions = File.Exists(k_AssemblyInclusionsPath)
                ? new HashSet<string>(File.ReadAllLines(k_AssemblyInclusionsPath))
                : new HashSet<string>();

            return s_AssemblyInclusions;
        }

        internal static HashSet<string> GetNamespaceExceptions()
        {
            return s_NamespaceExceptions ?? (s_NamespaceExceptions = File.Exists(k_NamespaceExceptionsPath)
                ? new HashSet<string>(File.ReadAllLines(k_NamespaceExceptionsPath))
                : new HashSet<string>());
        }

        internal static HashSet<string> GetTypeExceptions()
        {
            return s_TypeExceptions ?? (s_TypeExceptions = File.Exists(k_TypeExceptionsPath)
                ? new HashSet<string>(File.ReadAllLines(k_TypeExceptionsPath))
                : new HashSet<string>());
        }

        internal static void SaveSettings()
        {
            SaveExceptions(k_AssemblyInclusionsPath, s_AssemblyInclusions);
            SaveExceptions(k_NamespaceExceptionsPath, s_NamespaceExceptions);
            SaveExceptions(k_TypeExceptionsPath, s_TypeExceptions);
        }

        static void SaveExceptions(string path, HashSet<string> exceptions)
        {
            if (exceptions == null || exceptions.Count == 0)
            {
                File.Delete(path);
            }
            else
            {
                k_SortableList.Clear();
                k_SortableList.AddRange(exceptions);
                k_SortableList.Sort();
                File.WriteAllLines(path, k_SortableList);
            }
        }
    }
}
