using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    /// <summary>
    /// Represents a component which was added as a prefab override
    /// </summary>
    [Serializable, SkipGeneration]
    class RuntimeAddedGameObject
    {
        class AddedGameObjectPropertyBag : ContainerPropertyBag<RuntimeAddedGameObject>
        {
            class TransformPathProperty : Property<RuntimeAddedGameObject, string>
            {
                public override string Name => nameof(TransformPath);
                public override bool IsReadOnly => false;
                public override string GetValue(ref RuntimeAddedGameObject container) => container.TransformPath;
                public override void SetValue(ref RuntimeAddedGameObject container, string value) => container.TransformPath = value;
            }

            class GameObjectProperty : Property<RuntimeAddedGameObject, GameObject>
            {
                public override string Name => nameof(GameObject);
                public override bool IsReadOnly => false;
                public override GameObject GetValue(ref RuntimeAddedGameObject container) => container.GameObject;
                public override void SetValue(ref RuntimeAddedGameObject container, GameObject value) => container.GameObject = value;
            }

            public AddedGameObjectPropertyBag()
            {
                AddProperty(new TransformPathProperty());
                AddProperty(new GameObjectProperty());
            }
        }

        public string TransformPath;
        public GameObject GameObject;

        public RuntimeAddedGameObject() { }

        public RuntimeAddedGameObject(string transformPath, GameObject gameObject)
        {
            TransformPath = transformPath;
            GameObject = gameObject;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            PropertyBag.Register(new AddedGameObjectPropertyBag());
        }
    }
}
