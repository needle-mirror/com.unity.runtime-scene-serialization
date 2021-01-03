using System;
using System.Collections.Generic;
using Unity.Properties;
using Unity.Properties.Internal;
using Unity.Serialization.Json;
using Unity.Serialization.Json.Unsafe;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    class SceneVisitor : JsonSceneReader
    {
        readonly JsonSerializationParameters m_Parameters;
        readonly Transform m_SceneRoot;
        readonly List<DeserializationEvent> m_Events;

        public SceneVisitor(Transform sceneRoot, UnsafeValueView view, List<DeserializationEvent> events,
            JsonSerializationParameters parameters, SerializationMetadata metadata) : base(metadata)
        {
            SetView(view);
            SetEvents(events);
            m_Events = events;
            m_SceneRoot = sceneRoot;
            m_Parameters = parameters;
        }

        protected override void AcceptProperty<TContainer>(ref TContainer container, UnsafeObjectView view, IProperty<TContainer> property)
        {
            var name = property.Name;
            if (view.TryGetValue(name, out var value))
            {
                using (CreateViewScope(value))
                {
                    if (property is Property<TContainer, List<GameObjectContainer>>)
                    {
                        ((IPropertyAccept<TContainer>) property).Accept(
                            new GameObjectVisitor(m_View, m_Events, m_SerializationMetadata, m_SceneRoot, null, m_Parameters),
                            ref container);
                    }
                    else
                    {
                        ((IPropertyAccept<TContainer>) property).Accept(this, ref container);
                    }
                }
            }
        }
    }
}
