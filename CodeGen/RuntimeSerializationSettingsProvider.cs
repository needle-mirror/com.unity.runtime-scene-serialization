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
            public readonly string Name;

            bool m_Expanded;
            readonly NamespaceGroup m_RootNamespaceGroup = new NamespaceGroup();
            readonly Action<string, HashSet<string>> m_GetAllNamespaces;
            readonly Action<string, HashSet<string>> m_GetAllTypes;
            readonly Action<string, HashSet<string>> m_RemoveAllNamespaces;
            readonly Action<string, HashSet<string>> m_RemoveAllTypes;

            public AssemblyRow(Assembly assembly)
            {
                Name = assembly.GetName().Name;
                DisplayName = $"Assembly: {Name}";

                foreach (var type in assembly.GetTypes())
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

            public void Draw(HashSet<string> assemblyInclusions, HashSet<string> namespaceExceptions, HashSet<string> typeExceptions)
            {
                var included = assemblyInclusions.Contains(Name);
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
                        assemblyInclusions.Remove(Name);

                        // Alt-click on the toggle will also exclude namespaces
                        if (Event.current.alt)
                            m_RootNamespaceGroup.AddAllNamespacesRecursively(string.Empty, namespaceExceptions);

                        RuntimeSerializationSettingsUtils.SaveSettings();
                    }

                    if (!included && nowIncluded)
                    {
                        assemblyInclusions.Add(Name);

                        // Alt-click on the toggle will also include namespaces
                        if (Event.current.alt)
                            m_RootNamespaceGroup.RemoveAllNamespacesRecursively(string.Empty, namespaceExceptions);

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

            void RemoveAllNamespaces(string prefix, HashSet<string> namespaces)
            {
                if (!string.IsNullOrEmpty(prefix))
                    namespaces.Remove(prefix);

                m_RootNamespaceGroup.RemoveAllNamespacesRecursively(prefix, namespaces);
            }

            void RemoveAllTypes(string prefix, HashSet<string> types)
            {
                m_RootNamespaceGroup.RemoveAllTypesRecursively(prefix, types);
            }

            public int GetTypeCount()
            {
                if (m_RootNamespaceGroup.Children.Count == 0)
                    return 0;

                return m_RootNamespaceGroup.GetTypeCountRecursively();
            }

            public void GetAllNamespaces(string prefix, HashSet<string> namespaces)
            {
                m_RootNamespaceGroup.AddAllNamespacesRecursively(prefix, namespaces);
            }

            public void GetAllTypes(string prefix, HashSet<string> type)
            {
                m_RootNamespaceGroup.AddAllTypesRecursively(prefix, type);
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
            readonly Action<string, HashSet<string>> m_AddAllTypesRecursively;
            readonly Action<string, HashSet<string>> m_AddAllNamespacesRecursively;
            readonly Action<string, HashSet<string>> m_RemoveAllNamespacesRecursively;
            readonly Action<string, HashSet<string>> m_RemoveAllTypesRecursively;

            public NamespaceGroup()
            {
                m_AddAllTypesRecursively = AddAllTypesRecursively;
                m_AddAllNamespacesRecursively = AddAllNamespacesRecursively;
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
                                AddAllNamespacesRecursively(prefix, namespaceExceptions);
                                AddAllTypesRecursively(prefix, typeExceptions);
                            }

                            RuntimeSerializationSettingsUtils.SaveSettings();
                        }

                        if (!included && nowIncluded)
                        {
                            namespaceExceptions.Remove(fullName);
                            if (Event.current.alt)
                            {
                                RemoveAllNamespacesRecursively(prefix, namespaceExceptions);
                                RemoveAllTypesRecursively(prefix, typeExceptions);
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
                                if (m_Children.Count > 0)
                                    DrawButtons(namespaceExceptions, "namespaces", m_AddAllNamespacesRecursively, m_RemoveAllNamespacesRecursively, fullName);

                                DrawButtons(typeExceptions, "types", m_AddAllTypesRecursively, m_RemoveAllTypesRecursively, fullName);
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

            public void RemoveAllNamespacesRecursively(string prefix, HashSet<string> namespaces)
            {
                foreach (var kvp in m_Children)
                {
                    var fullName = kvp.Key;
                    if (!string.IsNullOrEmpty(prefix))
                        fullName = $"{prefix}.{fullName}";

                    namespaces.Remove(fullName);
                    kvp.Value.RemoveAllNamespacesRecursively(fullName, namespaces);
                }
            }

            public void RemoveAllTypesRecursively(string prefix, HashSet<string> types)
            {
                foreach (var type in m_Types)
                {
                    types.Remove(type.FullName);
                }

                foreach (var kvp in m_Children)
                {
                    kvp.Value.RemoveAllTypesRecursively(prefix, types);
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

            public void AddAllTypesRecursively(string prefix, HashSet<string> types)
            {
                foreach (var type in m_Types)
                {
                    types.Add(type.FullName);
                }

                foreach (var kvp in m_Children)
                {
                    kvp.Value.AddAllTypesRecursively(prefix, types);
                }
            }

            public void AddAllNamespacesRecursively(string prefix, HashSet<string> namespaces)
            {
                foreach (var kvp in m_Children)
                {
                    var fullName = kvp.Key;
                    if (!string.IsNullOrEmpty(prefix))
                        fullName = $"{prefix}.{fullName}";

                    namespaces.Add(fullName);

                    kvp.Value.AddAllNamespacesRecursively(fullName, namespaces);
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
        static readonly Action<string, HashSet<string>> k_ClearExceptions = ClearExceptions;

        static readonly string[] k_IgnoredAssemblies =
        {
            CodeGenUtils.RuntimeSerializationAssemblyName,
            CodeGenUtils.RuntimeSerializationEditorAssemblyName,
            CodeGenUtils.RuntimeSerializationCodeGenAssemblyName,
            CodeGenUtils.ExternalPropertyBagAssemblyName
        };

        readonly List<AssemblyRow> m_AssemblyRows = new List<AssemblyRow>();
        readonly Action<string, HashSet<string>> m_ExcludeAllAssemblies;
        readonly Action<string, HashSet<string>> m_ExcludeAllNamespaces;
        readonly Action<string, HashSet<string>> m_ExcludeAllTypes;

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
            RuntimeSerializationSettingsUtils.ClearSettingsCache();
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
            GUILayout.Label("AOT Code Generation Settings", EditorStyles.boldLabel);
            var assemblyInclusions = RuntimeSerializationSettingsUtils.GetAssemblyInclusions();
            var namespaceExceptions = RuntimeSerializationSettingsUtils.GetNamespaceExceptions();
            var typeExceptions = RuntimeSerializationSettingsUtils.GetTypeExceptions();

            DrawButtons(assemblyInclusions, "assemblies", k_ClearExceptions, m_ExcludeAllAssemblies);
            DrawButtons(namespaceExceptions, "namespaces", m_ExcludeAllNamespaces, k_ClearExceptions);
            DrawButtons(typeExceptions, "types", m_ExcludeAllTypes, k_ClearExceptions);

            foreach (var assemblyRow in m_AssemblyRows)
            {
                assemblyRow.Draw(assemblyInclusions, namespaceExceptions, typeExceptions);
            }
        }

        static void ClearExceptions(string prefix, HashSet<string> exceptions)
        {
            exceptions.Clear();
        }

        void ExcludeAllAssemblies(string prefix, HashSet<string> exceptions)
        {
            foreach (var assemblyRow in m_AssemblyRows)
            {
                exceptions.Add(assemblyRow.Name);
            }
        }

        void ExcludeAllNamespaces(string prefix, HashSet<string> exceptions)
        {
            foreach (var assemblyRow in m_AssemblyRows)
            {
                assemblyRow.GetAllNamespaces(prefix, exceptions);
            }
        }

        void ExcludeAllTypes(string prefix, HashSet<string> exceptions)
        {
            foreach (var assemblyRow in m_AssemblyRows)
            {
                assemblyRow.GetAllTypes(prefix, exceptions);
            }
        }

        static void DrawButtons(HashSet<string> set, string type,
            Action<string, HashSet<string>> excludeAll, Action<string, HashSet<string>> includeAll, string prefix = null)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * k_Indent);
                if (GUILayout.Button($"Exclude all {type}"))
                {
                    excludeAll(prefix, set);
                    RuntimeSerializationSettingsUtils.SaveSettings();
                }

                if (GUILayout.Button($"Include all {type}"))
                {
                    includeAll(prefix, set);
                    RuntimeSerializationSettingsUtils.SaveSettings();
                }
            }
        }
    }
}
