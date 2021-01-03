using System;
using Unity.Properties;
using UnityEditor;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    /// <summary>
    /// Represents a component which was added as a prefab override
    /// </summary>
    [Serializable, SkipGeneration]
    class RuntimeAddedComponent
    {
        class AddedComponentPropertyBag : ContainerPropertyBag<RuntimeAddedComponent>
        {
            static readonly DelegateProperty<RuntimeAddedComponent, string> k_TransformPath = new DelegateProperty<RuntimeAddedComponent, string>(
                "transformPath",
                (ref RuntimeAddedComponent container) => container.TransformPath,
                (ref RuntimeAddedComponent container, string value) => { container.TransformPath = value; }
            );

            static readonly DelegateProperty<RuntimeAddedComponent, Component> k_Component = new DelegateProperty<RuntimeAddedComponent, Component>(
                ComponentFieldName,
                (ref RuntimeAddedComponent container) => container.Component,
                (ref RuntimeAddedComponent container, Component value) => { container.Component = value; }
            );

            public AddedComponentPropertyBag()
            {
                AddProperty(k_TransformPath);
                AddProperty(k_Component);
            }
        }

        public const string ComponentFieldName = "component";

        public string TransformPath;
        public Component Component;

        public RuntimeAddedComponent() { }

        public RuntimeAddedComponent(string transformPath, Component addedComponent)
        {
            TransformPath = transformPath;
            Component = addedComponent;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void EditorInitializeOnLoad() { RegisterPropertyBag(); }
        [RuntimeInitializeOnLoadMethod]
        static void InitializeOnLoad() { /* Do Nothing */ }
#else
        [RuntimeInitializeOnLoadMethod]
        static void InitializeOnLoad() { RegisterPropertyBag(); }
#endif

        static void RegisterPropertyBag()
        {
            PropertyBag.Register(new AddedComponentPropertyBag());
        }
    }
}
