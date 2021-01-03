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

        class GameObjectPropertyBagOverride : ContainerPropertyBag<GameObject>
        {
            static readonly DelegateProperty<GameObject, string> k_Name = new DelegateProperty<GameObject, string>(
                "name",
                (ref GameObject gameObject) => gameObject.name,
                (ref GameObject gameObject, string value) => { gameObject.name = value; }
            );

            static readonly DelegateProperty<GameObject, HideFlags> k_HideFlags = new DelegateProperty<GameObject, HideFlags>(
                "hideFlags",
                (ref GameObject gameObject) => gameObject.hideFlags,
                (ref GameObject gameObject, HideFlags value) => { gameObject.hideFlags = value; }
            );

            static readonly DelegateProperty<GameObject, int> k_Layer = new DelegateProperty<GameObject, int>(
                "layer",
                (ref GameObject gameObject) => gameObject.layer,
                (ref GameObject gameObject, int value) => { gameObject.layer = value; }
            );

            static readonly DelegateProperty<GameObject, string> k_Tag = new DelegateProperty<GameObject, string>(
                "tag",
                (ref GameObject gameObject) => gameObject.tag,
                (ref GameObject gameObject, string value) => { gameObject.tag = value; }
            );

            static readonly DelegateProperty<GameObject, bool> k_Active = new DelegateProperty<GameObject, bool>(
                "active",
                (ref GameObject gameObject) => gameObject.activeSelf,
                (ref GameObject gameObject, bool value) => { gameObject.SetActive(value); }
            );

            static readonly DelegateProperty<GameObject, bool> k_IsStatic = new DelegateProperty<GameObject, bool>(
                "isStatic",
                (ref GameObject gameObject) => gameObject.activeSelf,
                (ref GameObject gameObject, bool value) => { gameObject.SetActive(value); }
            );

            public GameObjectPropertyBagOverride()
            {
                AddProperty(k_Name);
                AddProperty(k_HideFlags);
                AddProperty(k_Layer);
                AddProperty(k_Tag);
                AddProperty(k_Active);
                AddProperty(k_IsStatic);
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
            PropertyBag.Register(new GameObjectPropertyBagOverride());
        }
    }
}
