using System;
using Unity.Properties;
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
            class TransformPathProperty : Property<RuntimeAddedComponent, string>
            {
                public override string Name => TransformPathFieldName;
                public override bool IsReadOnly => false;
                public override string GetValue(ref RuntimeAddedComponent container) => container.TransformPath;
                public override void SetValue(ref RuntimeAddedComponent container, string value) => container.TransformPath = value;
            }

            class ComponentProperty : Property<RuntimeAddedComponent, Component>
            {
                public override string Name => ComponentFieldName;
                public override bool IsReadOnly => false;
                public override Component GetValue(ref RuntimeAddedComponent container) => container.Component;
                public override void SetValue(ref RuntimeAddedComponent container, Component value) => container.Component = value;
            }

            public AddedComponentPropertyBag()
            {
                AddProperty(new TransformPathProperty());
                AddProperty(new ComponentProperty());
            }
        }

        public const string TransformPathFieldName = "transformPath";
        public const string ComponentFieldName = "component";

        public string TransformPath;
        public Component Component;

        public RuntimeAddedComponent() { }

        public RuntimeAddedComponent(string transformPath, Component addedComponent)
        {
            TransformPath = transformPath;
            Component = addedComponent;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            PropertyBag.Register(new AddedComponentPropertyBag());
        }
    }
}
