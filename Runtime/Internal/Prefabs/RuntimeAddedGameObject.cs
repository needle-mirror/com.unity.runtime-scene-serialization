﻿using System;
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

            class GameObjectProperty : Property<RuntimeAddedGameObject, GameObjectContainer>
            {
                public override string Name => nameof(GameObject);
                public override bool IsReadOnly => false;
                public override GameObjectContainer GetValue(ref RuntimeAddedGameObject container) => container.GameObject;
                public override void SetValue(ref RuntimeAddedGameObject container, GameObjectContainer value) => container.GameObject = value;
            }

            public AddedGameObjectPropertyBag()
            {
                AddProperty(new TransformPathProperty());
                AddProperty(new GameObjectProperty());
            }
        }

        public string TransformPath;
        public GameObjectContainer GameObject;

        public RuntimeAddedGameObject() { }

        public RuntimeAddedGameObject(string transformPath, GameObject gameObject, SerializationMetadata metadata)
        {
            TransformPath = transformPath;
            GameObject = new GameObjectContainer(gameObject, metadata);
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
