#define INCLUDE_ALL_SERIALIZABLE_CONTAINERS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.XRTools.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    class RuntimeSerializationSettingsProvider : SettingsProvider
    {
        class AssemblyRow
        {
            public readonly string DisplayName;
            public readonly string FullName;

            bool m_Expanded;
            readonly NamespaceGroup m_RootNamespaceGroup = new NamespaceGroup();
            readonly Action<HashSet<string>> m_GetAllNamespaces;
            readonly Action<HashSet<string>> m_GetAllTypes;
            readonly Action<HashSet<string>> m_RemoveAllNamespaces;
            readonly Action<HashSet<string>> m_RemoveAllTypes;

            public AssemblyRow(Assembly assembly)
            {
                FullName = assembly.FullName;
                DisplayName = $"Assembly: {assembly.GetName().Name}";

                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface)
                        continue;

                    if (type.IsGenericType)
                        continue;

                    if (!typeof(Component).IsAssignableFrom(type))
                    {
#if INCLUDE_ALL_SERIALIZABLE_CONTAINERS
                        if (!CodeGenUtils.IsSerializableContainer(type))
#endif
                            continue;
                    }

                    var typeName = type.FullName;
                    if (string.IsNullOrEmpty(typeName))
                        continue;

                    var typeNamespace = type.Namespace;
                    var group = m_RootNamespaceGroup;
                    if (!string.IsNullOrEmpty(typeNamespace))
                    {
                        var namespaceParts = typeNamespace.Split('.');
                        foreach (var part in namespaceParts)
                        {
                            var lastGroup = group;
                            if (!group.Children.TryGetValue(part, out group))
                            {
                                group = new NamespaceGroup();
                                lastGroup.Children.Add(part, group);
                            }
                        }
                    }

                    var typeRow = new TypeRow(type);
                    var types = group.Types;
                    types.Add(typeRow);
                }

                m_RootNamespaceGroup.PostProcessRecursively();
                m_GetAllNamespaces = GetAllNamespaces;
                m_GetAllTypes = GetAllTypes;
                m_RemoveAllNamespaces = RemoveAllNamespaces;
                m_RemoveAllTypes = RemoveAllTypes;
            }

            public void Draw(HashSet<string> assemblyExceptions, HashSet<string> namespaceExceptions, HashSet<string> typeExceptions)
            {
                var included = !assemblyExceptions.Contains(FullName);
                using (new GUILayout.HorizontalScope())
                {
                    var wasExpanded = m_Expanded;
                    m_Expanded = EditorGUILayout.Foldout(m_Expanded, DisplayName, true);
                    if (wasExpanded != m_Expanded && Event.current.alt)
                        m_RootNamespaceGroup.SetExpandedRecursively(m_Expanded);

                    var indentedRect = EditorGUI.IndentedRect(Rect.zero);
                    var nowIncluded = EditorGUILayout.Toggle("", included, GUILayout.Width(k_ToggleWidth + indentedRect.x));

                    if (included && !nowIncluded)
                    {
                        assemblyExceptions.Add(FullName);
                        if (Event.current.alt)
                            m_RootNamespaceGroup.GetAllNamespacesRecursively(namespaceExceptions);

                        RuntimeSerializationSettingsUtils.SaveSettings();
                    }

                    if (!included && nowIncluded)
                    {
                        assemblyExceptions.Remove(FullName);
                        if (Event.current.alt)
                            m_RootNamespaceGroup.RemoveAllNamespacesRecursively(namespaceExceptions);

                        RuntimeSerializationSettingsUtils.SaveSettings();
                    }
                }

                if (m_Expanded)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        using (new EditorGUI.DisabledScope(!included))
                        {
                            DrawButtons(namespaceExceptions, "namespaces", m_GetAllNamespaces, m_RemoveAllNamespaces);
                            DrawButtons(namespaceExceptions, "types", m_GetAllTypes, m_RemoveAllTypes);
                        }

                        foreach (var kvp in m_RootNamespaceGroup.Children)
                        {
                            kvp.Value.Draw(kvp.Key, included, namespaceExceptions, typeExceptions);
                        }
                    }
                }
            }

            void RemoveAllNamespaces(HashSet<string> namespaces)
            {
                m_RootNamespaceGroup.RemoveAllNamespacesRecursively(namespaces);
            }

            void RemoveAllTypes(HashSet<string> types)
            {
                m_RootNamespaceGroup.RemoveAllTypesRecursively(types);
            }

            public int GetTypeCount()
            {
                if (m_RootNamespaceGroup.Children.Count == 0)
                    return 0;

                return m_RootNamespaceGroup.GetTypeCountRecursively();
            }

            public void GetAllNamespaces(HashSet<string> namespaces)
            {
                m_RootNamespaceGroup.GetAllNamespacesRecursively(namespaces);
            }

            public void GetAllTypes(HashSet<string> type)
            {
                m_RootNamespaceGroup.GetAllTypesRecursively(type);
            }
        }

        class NamespaceGroup
        {
            readonly SortedDictionary<string, NamespaceGroup> m_Children = new SortedDictionary<string, NamespaceGroup>();
            readonly List<TypeRow> m_Types = new List<TypeRow>();

            bool m_Expanded = true;

            public SortedDictionary<string, NamespaceGroup> Children => m_Children;
            public List<TypeRow> Types => m_Types;

            // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
            static readonly List<string> k_ToRemove = new List<string>();
            readonly Action<HashSet<string>> m_GetAllTypesRecursively;
            readonly Action<HashSet<string>> m_GetAllNamespacesRecursively;
            readonly Action<HashSet<string>> m_RemoveAllNamespacesRecursively;
            readonly Action<HashSet<string>> m_RemoveAllTypesRecursively;

            public NamespaceGroup()
            {
                m_GetAllTypesRecursively = GetAllTypesRecursively;
                m_GetAllNamespacesRecursively = GetAllNamespacesRecursively;
                m_RemoveAllNamespacesRecursively = RemoveAllNamespacesRecursively;
                m_RemoveAllTypesRecursively = RemoveAllTypesRecursively;
            }

            public void Draw(string name, bool parentIncluded, HashSet<string> namespaceExceptions, HashSet<string> typeExceptions)
            {
                Draw(string.Empty, name, parentIncluded, namespaceExceptions, typeExceptions);
            }

            void Draw(string prefix, string name, bool parentIncluded, HashSet<string> namespaceExceptions, HashSet<string> typeExceptions)
            {
                using (new EditorGUI.DisabledScope(!parentIncluded))
                {
                    var fullName = name;
                    if (!string.IsNullOrEmpty(prefix))
                        fullName = $"{prefix}.{name}";

                    var included = parentIncluded && !namespaceExceptions.Contains(fullName);
                    using (new GUILayout.HorizontalScope())
                    {
                        var wasExpanded = m_Expanded;
                        m_Expanded = EditorGUILayout.Foldout(m_Expanded, name, true);
                        if (wasExpanded != m_Expanded && Event.current.alt)
                            SetExpandedRecursively(m_Expanded);

                        var indentedRect = EditorGUI.IndentedRect(Rect.zero);
                        var nowIncluded = EditorGUILayout.Toggle("", included, GUILayout.Width(k_ToggleWidth + indentedRect.x));

                        if (included && !nowIncluded)
                        {
                            namespaceExceptions.Add(fullName);
                            if (Event.current.alt)
                            {
                                GetAllNamespacesRecursively(prefix, name, namespaceExceptions);
                                GetAllTypesRecursively(typeExceptions);
                            }

                            RuntimeSerializationSettingsUtils.SaveSettings();
                        }

                        if (!included && nowIncluded)
                        {
                            namespaceExceptions.Remove(fullName);
                            if (Event.current.alt)
                            {
                                RemoveAllNamespacesRecursively(prefix, name, namespaceExceptions);
                                RemoveAllTypesRecursively(typeExceptions);
                            }

                            RuntimeSerializationSettingsUtils.SaveSettings();
                        }
                    }

                    if (m_Expanded)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            using (new EditorGUI.DisabledScope(!included))
                            {
                                DrawButtons(namespaceExceptions, "namespaces", m_GetAllNamespacesRecursively, m_RemoveAllNamespacesRecursively);
                                DrawButtons(typeExceptions, "types", m_GetAllTypesRecursively, m_RemoveAllTypesRecursively);
                            }

                            foreach (var kvp in m_Children)
                            {
                                kvp.Value.Draw(fullName, kvp.Key, included, namespaceExceptions, typeExceptions);
                            }

                            foreach (var typeRow in m_Types)
                            {
                                typeRow.Draw(included, typeExceptions);
                            }
                        }
                    }
                }
            }

            public void RemoveAllNamespacesRecursively(HashSet<string> namespaces)
            {
                RemoveAllNamespacesRecursively(string.Empty, string.Empty, namespaces);
            }

            void RemoveAllNamespacesRecursively(string prefix, string name, HashSet<string> namespaces)
            {
                var fullName = name;
                if (!string.IsNullOrEmpty(prefix))
                    fullName = $"{prefix}.{name}";

                if (!string.IsNullOrEmpty(fullName))
                {
                    namespaces.Remove(fullName);
                    return;
                }

                foreach (var kvp in m_Children)
                {
                    kvp.Value.RemoveAllNamespacesRecursively(fullName, kvp.Key, namespaces);
                }
            }

            public void RemoveAllTypesRecursively(HashSet<string> types)
            {
                foreach (var type in m_Types)
                {
                    types.Remove(type.FullName);
                }

                foreach (var kvp in m_Children)
                {
                    kvp.Value.RemoveAllTypesRecursively(types);
                }
            }


            public void PostProcessRecursively()
            {
                m_Types.Sort((a, b) => a.FullName.CompareTo(b.FullName));
                foreach (var kvp in m_Children)
                {
                    var namespaceGroup = kvp.Value;
                    namespaceGroup.PostProcessRecursively();
                    if (namespaceGroup.m_Children.Count == 0 && namespaceGroup.Types.Count == 0)
                        k_ToRemove.Add(kvp.Key);
                }

                foreach (var name in k_ToRemove)
                {
                    m_Children.Remove(name);
                }
            }

            public int GetTypeCountRecursively()
            {
                var count = m_Types.Count;
                foreach (var kvp in m_Children)
                {
                    count += kvp.Value.GetTypeCountRecursively();
                }

                return count;
            }

            public void GetAllTypesRecursively(HashSet<string> types)
            {
                foreach (var type in m_Types)
                {
                    types.Add(type.FullName);
                }

                foreach (var kvp in m_Children)
                {
                    kvp.Value.GetAllTypesRecursively(types);
                }
            }

            public void GetAllNamespacesRecursively(HashSet<string> namespaces)
            {
                GetAllNamespacesRecursively(string.Empty, string.Empty, namespaces);
            }
            void GetAllNamespacesRecursively(string prefix, string name, HashSet<string> namespaces)
            {
                var fullName = name;
                if (!string.IsNullOrEmpty(prefix))
                    fullName = $"{prefix}.{name}";

                if (!string.IsNullOrEmpty(fullName))
                    namespaces.Add(fullName);

                foreach (var kvp in m_Children)
                {
                    kvp.Value.GetAllNamespacesRecursively(fullName, kvp.Key, namespaces);
                }
            }

            public void SetExpandedRecursively(bool expanded)
            {
                m_Expanded = expanded;
                foreach (var kvp in m_Children)
                {
                    kvp.Value.SetExpandedRecursively(expanded);
                }
            }
        }

        class TypeRow
        {
            public readonly string FullName;
            readonly string m_DisplayName;

            public TypeRow(Type type)
            {
                FullName = type.FullName;
                m_DisplayName = type.Name;
            }

            public void Draw(bool parentIncluded, HashSet<string> typeExceptions)
            {
                using (new EditorGUI.DisabledScope(!parentIncluded))
                {
                    var included = parentIncluded && !typeExceptions.Contains(FullName);
                    using (new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(m_DisplayName);
                        var indentedRect = EditorGUI.IndentedRect(Rect.zero);
                        var nowIncluded = EditorGUILayout.Toggle("", included, GUILayout.Width(k_ToggleWidth + indentedRect.x));

                        if (included && !nowIncluded)
                        {
                            typeExceptions.Add(FullName);
                            RuntimeSerializationSettingsUtils.SaveSettings();
                        }

                        if (!included && nowIncluded)
                        {
                            typeExceptions.Remove(FullName);
                            RuntimeSerializationSettingsUtils.SaveSettings();
                        }
                    }
                }
            }
        }

        const string k_SettingsPath = "Project/Runtime Scene Serialization";
        const float k_ToggleWidth = 15f;
        const int k_Indent = 15;
        static readonly Action<HashSet<string>> k_ClearExceptions = ClearExceptions;

        static readonly string[] k_IgnoredAssemblies =
        {
            "Unity.RuntimeSceneSerialization",
            "Unity.RuntimeSceneSerialization.Editor",
            "Unity.RuntimeSceneSerialization.CodeGen",
            "Unity.RuntimeSceneSerialization.Generated"
        };

        readonly List<AssemblyRow> m_AssemblyRows = new List<AssemblyRow>();
        readonly Action<HashSet<string>> m_ExcludeAllAssemblies;
        readonly Action<HashSet<string>> m_ExcludeAllNamespaces;
        readonly Action<HashSet<string>> m_ExcludeAllTypes;

        RuntimeSerializationSettingsProvider()
            : base(k_SettingsPath, SettingsScope.Project, new List<string> { "serialization" })
        {
            m_ExcludeAllAssemblies = ExcludeAllAssemblies;
            m_ExcludeAllNamespaces = ExcludeAllNamespaces;
            m_ExcludeAllTypes = ExcludeAllTypes;
        }

        [SettingsProvider]
        public static SettingsProvider CreateRuntimeSerializationSettingsProvider()
        {
            var provider = new RuntimeSerializationSettingsProvider();
            return provider;
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);
            RuntimeSerializationSettingsUtils.ClearExceptionCache();
            m_AssemblyRows.Clear();
            ReflectionUtils.ForEachAssembly(assembly =>
            {
                if (CodeGenUtils.IsTestAssembly(assembly))
                    return;

                if (k_IgnoredAssemblies.Contains(assembly.GetName().Name))
                    return;

                var assemblyRow = new AssemblyRow(assembly);
                if (assemblyRow.GetTypeCount() > 0)
                    m_AssemblyRows.Add(assemblyRow);
            });

            m_AssemblyRows.Sort((a, b) => a.DisplayName.CompareTo(b.DisplayName));
        }

        public override void OnGUI(string searchContext)
        {
            base.OnGUI(searchContext);
            GUILayout.Label("AOT Code Generation Exceptions", EditorStyles.boldLabel);
            var assemblyExceptions = RuntimeSerializationSettingsUtils.GetAssemblyExceptions();
            var namespaceExceptions = RuntimeSerializationSettingsUtils.GetNamespaceExceptions();
            var typeExceptions = RuntimeSerializationSettingsUtils.GetTypeExceptions();

            DrawButtons(assemblyExceptions, "assemblies", m_ExcludeAllAssemblies, k_ClearExceptions);
            DrawButtons(namespaceExceptions, "namespaces", m_ExcludeAllNamespaces, k_ClearExceptions);
            DrawButtons(typeExceptions, "types", m_ExcludeAllTypes, k_ClearExceptions);

            foreach (var assemblyRow in m_AssemblyRows)
            {
                assemblyRow.Draw(assemblyExceptions, namespaceExceptions, typeExceptions);
            }
        }

        static void ClearExceptions(HashSet<string> exceptions)
        {
            exceptions.Clear();
        }

        void ExcludeAllAssemblies(HashSet<string> exceptions)
        {
            foreach (var assemblyRow in m_AssemblyRows)
            {
                exceptions.Add(assemblyRow.FullName);
            }
        }

        void ExcludeAllNamespaces(HashSet<string> exceptions)
        {
            foreach (var assemblyRow in m_AssemblyRows)
            {
                assemblyRow.GetAllNamespaces(exceptions);
            }
        }

        void ExcludeAllTypes(HashSet<string> exceptions)
        {
            foreach (var assemblyRow in m_AssemblyRows)
            {
                assemblyRow.GetAllTypes(exceptions);
            }
        }

        static void DrawButtons(HashSet<string> exceptions, string type, Action<HashSet<string>> excludeAll, Action<HashSet<string>> includeAll)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * k_Indent);
                if (GUILayout.Button($"Exclude all {type}"))
                {
                    excludeAll(exceptions);
                    RuntimeSerializationSettingsUtils.SaveSettings();
                }

                if (GUILayout.Button($"Include all {type}"))
                {
                    includeAll(exceptions);
                    RuntimeSerializationSettingsUtils.SaveSettings();
                }
            }
        }
    }
}
