using System;
using System.Collections.Generic;
using Unity.Properties;
using Unity.Properties.Internal;
using Unity.RuntimeSceneSerialization.Internal.Prefabs;
using Unity.Serialization.Json;
using Unity.Serialization.Json.Unsafe;
using UnityEngine;
using UnityEngine.Scripting;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal
{
    class GameObjectVisitor : JsonSceneReader
    {
        readonly JsonSerializationParameters m_Parameters;
        readonly List<DeserializationEvent> m_Events;
        Transform m_Parent;
        GameObjectContainer m_GameObjectContainer;

        /// <summary>
        /// Reference to the GameObjectContainer for the current prefab root
        /// </summary>
        GameObjectContainer m_PrefabRoot;

        public GameObjectVisitor(UnsafeValueView view, List<DeserializationEvent> events, SerializationMetadata metadata,
            Transform parent, GameObjectContainer prefabRoot, JsonSerializationParameters parameters) : base(metadata)
        {
            SetView(view);
            SetEvents(events);
            m_Events = events;
            m_Parameters = parameters;
            m_Parent = parent;
            m_PrefabRoot = prefabRoot;
        }

        protected override void AcceptProperty<TContainer>(ref TContainer container, UnsafeObjectView view, IProperty<TContainer> property)
        {
            var name = property.Name;
            if (view.TryGetValue(name, out var value))
            {
                using (CreateViewScope(value))
                {
                    if (container is GameObjectContainer gameObjectContainer)
                    {
                        gameObjectContainer.Parent = m_Parent;
                        m_GameObjectContainer = gameObjectContainer;
                        // TODO: Detect by type?
                        if (name == GameObjectContainer.PrefabMetadataProperty)
                        {
                            ((IPropertyAccept<TContainer>)property).Accept(this, ref container);
                            return;
                        }

                        if (name == GameObjectContainer.ComponentsProperty)
                        {
                            var gameObject = gameObjectContainer.GameObject;
                            ((IPropertyAccept<TContainer>)property).Accept(
                                new ComponentVisitor(gameObject, m_View, m_Events, m_SerializationMetadata),
                                ref container);

                            var prefabMetadata = gameObjectContainer.PrefabMetadataContainer;
                            if (prefabMetadata != null)
                            {
                                gameObjectContainer.FinalizePrefab(prefabMetadata, m_SerializationMetadata);
                                m_PrefabRoot = null;
                            }

                            return;
                        }

                        if (name == GameObjectContainer.ChildrenProperty)
                        {
                            ((IPropertyAccept<TContainer>)property).Accept(
                                new GameObjectVisitor(m_View, m_Events, m_SerializationMetadata,
                                    gameObjectContainer.GameObject.transform, m_PrefabRoot, m_Parameters),
                                ref container);

                            return;
                        }
                    }
                    else if (container is RuntimeAddedComponent addedComponent)
                    {
                        if (name == RuntimeAddedComponent.ComponentFieldName)
                        {
                            var target = m_PrefabRoot.GameObject.transform.GetTransformAtPath(addedComponent.TransformPath);
                            ((IPropertyAccept<TContainer>)property).Accept(
                                new ComponentVisitor(target.gameObject, m_View, m_Events, m_SerializationMetadata),
                                ref container);

                            return;
                        }
                    }
                    else if (container is RuntimeAddedGameObject addedGameObject)
                    {
                        if (name == nameof(addedGameObject.GameObject))
                        {
                            var parent = m_PrefabRoot.GameObject.transform.GetTransformAtPath(addedGameObject.TransformPath);
                            ((IPropertyAccept<TContainer>)property).Accept(
                                new GameObjectVisitor(m_View, m_Events, m_SerializationMetadata, parent, m_PrefabRoot, m_Parameters),
                                ref container);

                            return;
                        }
                    }
                    else if (container is PrefabMetadataContainer prefabMetadataContainer)
                    {
                        if (name == PrefabMetadataContainer.GuidFieldName)
                        {
                            m_PrefabRoot = m_GameObjectContainer;
                            ((IPropertyAccept<TContainer>)property).Accept(this, ref container);
                            m_PrefabRoot.InstantiatePrefab(prefabMetadataContainer.Guid, m_SerializationMetadata.AssetPack);
                            return;
                        }
                    }

                    ((IPropertyAccept<TContainer>)property).Accept(this, ref container);
                }
            }
        }

        [Preserve]
        static void PreserveAOT()
        {
            var visitor = new GameObjectVisitor(default, default, default, default, default, default);

            var removedComponent = new RuntimeRemovedComponent();
            visitor.AcceptProperty(ref removedComponent, default, default);

            var addedComponent = new RuntimeAddedComponent();
            visitor.AcceptProperty(ref addedComponent, default, default);

            var addedGameObject = new RuntimeAddedGameObject();
            visitor.AcceptProperty(ref addedGameObject, default, default);

            var unityObjectReference = new UnityObjectReference();
            visitor.AcceptProperty(ref unityObjectReference, default, default);

            var unityObject = new UnityObject();
            visitor.AcceptProperty(ref unityObject, default, default);

            var longValue = 0L;
            visitor.AcceptProperty(ref longValue, default, default);

            var boolValue = false;
            visitor.AcceptProperty(ref boolValue, default, default);

            var floatValue = 0f;
            visitor.AcceptProperty(ref floatValue, default, default);

            var stringValue = string.Empty;
            visitor.AcceptProperty(ref stringValue, default, default);

            var colorValue = new Color();
            visitor.AcceptProperty(ref colorValue, default, default);

            var intValue = 0;
            visitor.AcceptProperty(ref intValue, default, default);

            var charValue = 0;
            visitor.AcceptProperty(ref charValue, default, default);

            var animationCurve = new AnimationCurve();
            visitor.AcceptProperty(ref animationCurve, default, default);
        }
    }
}
