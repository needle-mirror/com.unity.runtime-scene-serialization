using Unity.Properties;
using UnityEditor;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Utility class for setting up special property bags to override defaults set up by Properties API
    /// </summary>
    public static class PropertyBagOverrides
    {
        // Override Bounds to use m_Center and m_Extent "NativeName" for properties
        class BoundsPropertyBagOverride : ContainerPropertyBag<Bounds>
        {
            public BoundsPropertyBagOverride()
            {
                AddProperty(new CenterProperty());
                AddProperty(new ExtentsProperty());
            }

            class CenterProperty : Property<Bounds, Vector3>
            {
                public override string Name => "m_Center";
                public override bool IsReadOnly => false;
                public override Vector3 GetValue(ref Bounds container) => container.center;
                public override void SetValue(ref Bounds container, Vector3 value) => container.center = value;
            }

            class ExtentsProperty : Property<Bounds, Vector3>
            {
                public override string Name => "m_Extent";
                public override bool IsReadOnly => false;
                public override Vector3 GetValue(ref Bounds container) => container.extents;
                public override void SetValue(ref Bounds container, Vector3 value) => container.extents = value;
            }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void InitializeEditorOverrides()
        {
            EditorApplication.delayCall += InitializeOverrides;
        }
#endif

        /// <summary>
        /// Initialize property bag overrides for Bounds and GameObject (call this after InitializeOnLoad)
        /// </summary>
        public static void InitializeOverrides()
        {
            PropertyBag.Register(new BoundsPropertyBagOverride());
        }
    }
}
