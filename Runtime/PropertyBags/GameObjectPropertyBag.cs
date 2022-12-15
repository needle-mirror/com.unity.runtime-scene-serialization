using System.Collections.Generic;
using Unity.Properties;
using Unity.RuntimeSceneSerialization.Prefabs;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.PropertyBags
{
    class GameObjectPropertyBag : ContainerPropertyBag<GameObject>
    {
        class NameProperty : Property<GameObject, string>
        {
            public override string Name => nameof(GameObject.name);
            public override bool IsReadOnly => false;
            public override string GetValue(ref GameObject container) => container.name;
            public override void SetValue(ref GameObject container, string value) => container.name = value;
        }

        class HideFlagsProperty : Property<GameObject, HideFlags>
        {
            public override string Name => nameof(GameObject.hideFlags);
            public override bool IsReadOnly => false;
            public override HideFlags GetValue(ref GameObject container) => container.hideFlags;
            public override void SetValue(ref GameObject container, HideFlags value) => container.hideFlags = value;
        }

        class TagProperty : Property<GameObject, string>
        {
            public override string Name => nameof(GameObject.tag);
            public override bool IsReadOnly => false;
            public override string GetValue(ref GameObject container) => container.tag;
            public override void SetValue(ref GameObject container, string value) => container.tag = value;
        }

        class ActiveProperty : Property<GameObject, bool>
        {
            public override string Name => ActivePropertyName;
            public override bool IsReadOnly => false;
            public override bool GetValue(ref GameObject container) => container.activeSelf;
            public override void SetValue(ref GameObject container, bool value) => container.SetActive(value);
        }

        class LayerProperty : Property<GameObject, int>
        {
            public override string Name => nameof(GameObject.layer);
            public override bool IsReadOnly => false;
            public override int GetValue(ref GameObject container) => container.layer;
            public override void SetValue(ref GameObject container, int value) => container.layer = value;
        }

        class ComponentsProperty : Property<GameObject, List<Component>>
        {
            static readonly List<Component> k_Components = new();
            public override string Name => ComponentsPropertyName;

            // Return false so that deserialization does not fail
            public override bool IsReadOnly => false;
            public override List<Component> GetValue(ref GameObject container)
            {
                var components = new List<Component>();

                // GetComponents clears the list
                container.GetComponents(k_Components);

                foreach (var component in k_Components)
                {
                    if (component == null)
                    {
                        Debug.LogWarningFormat("Found missing script on {0} during serialization", container.name);
                        continue;
                    }

                    if (component.GetType() != typeof(PrefabMetadata) && (component.hideFlags & HideFlags.DontSave) != 0)
                        continue;

                    components.Add(component);
                }

                k_Components.Clear();

                return components;
            }

            public override void SetValue(ref GameObject container, List<Component> value)
            {
                // Do nothing--GameObjects are created within the active scene
            }
        }

        class ChildrenProperty : Property<GameObject, List<GameObject>>
        {
            public override string Name => ChildrenPropertyName;

            // Return false so that deserialization does not fail
            public override bool IsReadOnly => false;
            public override List<GameObject> GetValue(ref GameObject container)
            {
                var children = new List<GameObject>();
                foreach (Transform child in container.transform)
                {
                    var childGameObject = child.gameObject;
                    if ((childGameObject.hideFlags & HideFlags.DontSave) != 0)
                        continue;

                    children.Add(childGameObject);
                }

                return children;
            }

            public override void SetValue(ref GameObject container, List<GameObject> value)
            {
                // Do nothing--GameObjects are created within the active scene
            }
        }

        internal const string PrefabMetadataPropertyName = "prefabMetadata";
        internal const string ActivePropertyName = "active";
        internal const string ComponentsPropertyName = "components";
        internal const string ChildrenPropertyName = "children";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            PropertyBag.Register(new GameObjectPropertyBag());
        }

        GameObjectPropertyBag()
        {
            AddProperty(new NameProperty());
            AddProperty(new HideFlagsProperty());
            AddProperty(new LayerProperty());
            AddProperty(new TagProperty());
            AddProperty(new ActiveProperty());

            PropertyBag.RegisterList<GameObject, Component>();
            AddProperty(new ComponentsProperty());

            PropertyBag.RegisterList<GameObject, GameObject>();
            AddProperty(new ChildrenProperty());
        }
    }
}
