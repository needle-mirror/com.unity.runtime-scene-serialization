using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Properties;
using Unity.Properties.Internal;
using Unity.Serialization;
using Unity.Serialization.Json;
using Unity.Serialization.Json.Unsafe;
using UnityEngine;
using UnityEngine.Scripting;

namespace Unity.RuntimeSceneSerialization.Internal
{
    class ComponentVisitor : JsonSceneReader
    {
        class ComponentTypeConstructor : ICustomSerializedTypeProvider
        {
            readonly GameObject m_GameObject;
            readonly ISerializedTypeProvider m_GenericConstructor;
            readonly SerializationMetadata m_SerializationMetadata;

            internal ComponentTypeConstructor(GameObject gameObject, ISerializedTypeProvider genericConstructor,
                SerializationMetadata metadata)
            {
                m_GameObject = gameObject;
                m_GenericConstructor = genericConstructor;
                m_SerializationMetadata = metadata;
            }

            public object Construct(Type type, UnsafeValueView view)
            {
                if (type != typeof(Component) || m_GameObject == null)
                    return null;

                try
                {
                    var componentType = m_GenericConstructor.GetSerializedType();
                    if (componentType == null)
                        return null;

#if !NET_DOTS && !ENABLE_IL2CPP
                    SceneSerialization.RegisterPropertyBag(componentType);
#endif

                    var properties = PropertyBagStore.GetPropertyBag(componentType);
                    if (properties == null)
                    {
                        Debug.LogWarning($"Could not resolve component type {componentType} deserializing {m_GameObject.name}. Will preserve serialized contents as JSON string");
                        return CreateMissingComponent(view);
                    }

                    if (componentType == typeof(MissingComponent))
                    {
                        try
                        {
                            var value = new JsonObject();
                            var visitor = new JsonSceneReader(m_SerializationMetadata);
                            visitor.SetView(view);
                            PropertyContainer.Visit(ref value, visitor);
                            var jsonString = value[nameof(MissingComponent.JsonString)].ToString();

                            // For some reason, newlines are read as null characters which break parsing
                            jsonString = jsonString.Replace('\0', '\n');

                            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
                            {
                                using (var reader = new SerializedObjectReader(stream))
                                {
                                    reader.Read(out var document);
                                    var componentVisitor = new ComponentVisitor(m_GameObject, document.AsUnsafe(),
                                        null, m_SerializationMetadata);

                                    Component missingComponent = null;
                                    componentVisitor.ReadValue(ref missingComponent, document.AsUnsafe());
                                    return missingComponent;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.Log($"Encountered an exception trying to deserialize MissingComponent. Preserving it as-is. Exception follows:\n{e}");
                        }
                    }

                    return componentType == typeof(Transform) ? m_GameObject.GetComponent<Transform>() : m_GameObject.AddComponent(componentType);
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning($"Could not resolve component type deserializing {m_GameObject.name}. Will preserve serialized contents as JSON string");
                    return CreateMissingComponent(view);
                }
            }

            MissingComponent CreateMissingComponent(UnsafeValueView view)
            {
                var missingBehaviour = m_GameObject.AddComponent<MissingComponent>();

                var value = new JsonObject();
                var visitor = new JsonSceneReader(m_SerializationMetadata);
                visitor.SetView(view);
                PropertyContainer.Visit(ref value, visitor);
                missingBehaviour.JsonString = JsonSerialization.ToJson(value);
                return missingBehaviour;
            }
        }

        public ComponentVisitor(GameObject gameObject, UnsafeValueView view, List<DeserializationEvent> events,
            SerializationMetadata metadata) : base(metadata)
        {
            SetView(view);
            SetEvents(events);
            m_CustomTypeConstructor = new ComponentTypeConstructor(gameObject, GenericTypeConstructor, metadata);
        }

        [Preserve]
        static void PreserveAOT()
        {
            var container = new UnityObjectReference();
            new ComponentVisitor(default, default, default, default).AcceptProperty(ref container, default, default);
        }
    }
}
