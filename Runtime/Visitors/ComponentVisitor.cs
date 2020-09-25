using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Properties;
using Unity.Serialization;
using Unity.Serialization.Json;
using Unity.Serialization.Json.Unsafe;
using UnityEngine;
using UnityEngine.Scripting;

namespace Unity.RuntimeSceneSerialization
{
    class ComponentVisitor : JsonSceneReader
    {
        class ComponentTypeConstructor : ICustomSerializedTypeProvider
        {
            GameObject m_GameObject;
            ISerializedTypeProvider m_GenericConstructor;

            internal ComponentTypeConstructor(GameObject gameObject, ISerializedTypeProvider genericConstructor)
            {
                m_GameObject = gameObject;
                m_GenericConstructor = genericConstructor;
            }

            public object Construct(Type type, UnsafeValueView view)
            {
                if (type != typeof(Component) || m_GameObject == null)
                    return null;

                try
                {
                    var componentType = m_GenericConstructor.GetSerializedType();
                    if (componentType == typeof(MissingMonoBehaviour))
                    {
                        try
                        {
                            var value = new JsonObject();
                            var visitor = new JsonSceneReader();
                            visitor.SetView(view);
                            PropertyContainer.Visit(ref value, visitor);
                            var jsonString = value[nameof(MissingMonoBehaviour.JsonString)].ToString();

                            // For some reason, newlines are read as null characters which break parsing
                            jsonString = jsonString.Replace('\0', '\n');

                            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
                            {
                                using (var reader = new SerializedObjectReader(stream))
                                {
                                    reader.Read(out var document);
                                    var componentVisitor = new ComponentVisitor(m_GameObject, document.AsUnsafe(), null);
                                    Component missingComponent = null;
                                    componentVisitor.ReadValue(ref missingComponent, document.AsUnsafe());
                                    return missingComponent;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.Log($"Encountered an exception trying to deserialize MissingMonoBehavior. Preserving it as-is. Exception follows:\n{e}");
                        }
                    }

                    return componentType == typeof(Transform) ? m_GameObject.GetComponent<Transform>() : m_GameObject.AddComponent(componentType);
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning($"Could not resolve component type deserializing {m_GameObject.name}. Will preserve serialized contents as JSON string");
                    var missingBehaviour = m_GameObject.AddComponent<MissingMonoBehaviour>();

                    var value = new JsonObject();
                    var visitor = new JsonSceneReader();
                    visitor.SetView(view);
                    PropertyContainer.Visit(ref value, visitor);
                    missingBehaviour.JsonString = JsonSerialization.ToJson(value);

                    return missingBehaviour;
                }
            }
        }

        public ComponentVisitor(GameObject gameObject, UnsafeValueView view, List<DeserializationEvent> events)
        {
            SetView(view);
            SetEvents(events);
            m_CustomTypeConstructor = new ComponentTypeConstructor(gameObject, GenericTypeConstructor);
        }

        [Preserve]
        static void PreserveAOT()
        {
            var container = new UnityObjectReference();
            new ComponentVisitor(default, default, default).AcceptProperty(ref container, default, default);
        }
    }
}
