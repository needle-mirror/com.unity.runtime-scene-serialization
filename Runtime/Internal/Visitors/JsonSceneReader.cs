using System;
using System.Collections.Generic;
using Unity.Properties;
using Unity.Properties.Internal;
using Unity.Serialization;
using Unity.Serialization.Json;
using Unity.Serialization.Json.Unsafe;
using UnityEngine;
using EventType = Unity.Serialization.Json.EventType;

namespace Unity.RuntimeSceneSerialization.Internal
{
    interface ICustomSerializedTypeProvider
    {
        object Construct(Type type, UnsafeValueView view);
    }

    class JsonSceneReader : JsonPropertyVisitor,
        IPropertyBagVisitor,
        ISetPropertyBagVisitor,
        IListPropertyBagVisitor,
        IDictionaryPropertyBagVisitor,
        IPropertyVisitor
    {
        class SerializedTypeProvider : ISerializedTypeProvider
        {
            public List<DeserializationEvent> Events;

            public UnsafeValueView View;
            public Type SerializedType;

            public Type GetSerializedType()
            {
                if (SerializedType != null)
                {
                    return SerializedType;
                }

                if (View.Type != TokenType.Object || !View.AsObjectView().TryGetValue(k_SerializedTypeKey, out var typeNameView))
                {
                    return null;
                }

                if (typeNameView.Type != TokenType.String)
                {
                    throw new ArgumentException($"Failed to construct type. Property=[{k_SerializedTypeKey}] is expected to be a string.");
                }

                var assemblyQualifiedTypeName = typeNameView.AsStringView().ToString();

                if (string.IsNullOrEmpty(assemblyQualifiedTypeName))
                {
                    throw new ArgumentException($"Failed to construct type. Property=[{k_SerializedTypeKey}] is expected to be a fully qualified type name.");
                }

                var serializedType = Type.GetType(assemblyQualifiedTypeName);

                if (null == serializedType)
                {
                    if (FormerNameAttribute.TryGetCurrentTypeName(assemblyQualifiedTypeName, out var currentAssemblyQualifiedTypeName))
                    {
                        serializedType = Type.GetType(currentAssemblyQualifiedTypeName);
                    }

                    if (null == serializedType)
                    {
                        throw new ArgumentException($"Failed to construct type. Could not resolve type from TypeName=[{assemblyQualifiedTypeName}].");
                    }

                    Events.Add(new DeserializationEvent(EventType.Log, $"Type construction encountered a type name remap OldType=[{assemblyQualifiedTypeName}] CurrentType=[{currentAssemblyQualifiedTypeName}]"));
                }

                return serializedType;
            }

            public int GetArrayLength()
            {
                if (View.Type == TokenType.Array)
                {
                    return View.AsArrayView().Count();
                }

                if (View.Type == TokenType.Object)
                {
                    if (View.AsObjectView().TryGetValue(k_SerializedElementsKey, out var elements))
                    {
                        return elements.AsArrayView().Count();
                    }
                }

                return 0;
            }

            public object GetDefaultObject()
            {
                // At this point we are really out of options.
                // Construct a type to dump the data in to.
                switch (View.Type)
                {
                    case TokenType.Object:
                        return new JsonObject();
                    case TokenType.Array:
                        return new JsonArray();
                }

                return null;
            }
        }

        struct SerializedContainerMetadata
        {
            public bool HasElements;
        }

        readonly struct SerializedContainerMetadataScope : IDisposable
        {
            readonly JsonSceneReader m_Visitor;
            readonly SerializedContainerMetadata m_Metadata;

            public SerializedContainerMetadataScope(JsonSceneReader visitor, SerializedContainerMetadata metadata)
            {
                m_Visitor = visitor;
                m_Metadata = m_Visitor.m_Metadata;
                m_Visitor.m_Metadata = metadata;
            }

            public void Dispose()
            {
                m_Visitor.m_Metadata = m_Metadata;
            }
        }

        internal readonly struct UnsafeViewScope : IDisposable
        {
            readonly JsonSceneReader m_Visitor;
            readonly UnsafeValueView m_View;

            public UnsafeViewScope(JsonSceneReader visitor, UnsafeValueView view)
            {
                m_View = visitor.m_View;
                m_Visitor = visitor;
                m_Visitor.m_View = view;
            }

            public void Dispose()
            {
                m_Visitor.m_View = m_View;
            }
        }

        internal readonly struct SerializedTypeScope : IDisposable
        {
            readonly JsonSceneReader m_Visitor;
            readonly Type m_SerializedType;

            public SerializedTypeScope(JsonSceneReader visitor, Type type)
            {
                m_SerializedType = visitor.m_SerializedType;
                m_Visitor = visitor;
                m_Visitor.m_SerializedType = type;
            }

            public void Dispose()
            {
                m_Visitor.m_SerializedType = m_SerializedType;
            }
        }

        protected UnsafeValueView m_View;
        Type m_SerializedType;
        SerializedContainerMetadata m_Metadata;
        readonly SerializedTypeProvider m_SerializedTypeProvider;
        protected readonly SerializationMetadata m_SerializationMetadata;
        protected ICustomSerializedTypeProvider m_CustomTypeConstructor;
        protected ISerializedTypeProvider GenericTypeConstructor => m_SerializedTypeProvider;

        public void SetView(UnsafeValueView view)
            => m_View = view;

        public void SetSerializedType(Type type)
            => m_SerializedType = type;

        public void SetEvents(List<DeserializationEvent> events)
            => m_SerializedTypeProvider.Events = events;

        public JsonSceneReader(SerializationMetadata metadata)
        {
            m_SerializedTypeProvider = new SerializedTypeProvider();
            m_SerializationMetadata = metadata;
        }

        static SerializedContainerMetadata GetSerializedContainerMetadata(UnsafeObjectView view)
        {
            var metadata = default(SerializedContainerMetadata);

            foreach (var member in view)
            {
                var name = member.Key().AsStringView();

                if (name.Length() == 0 || name[0] != '$')
                    break;

                metadata.HasElements |= name.Equals(k_SerializedElementsKey);
            }

            return metadata;
        }

        internal UnsafeViewScope CreateViewScope(UnsafeValueView view)
            => new UnsafeViewScope(this, view);

        internal SerializedTypeScope CreateSerializedTypeScope(Type serializedType)
            => new SerializedTypeScope(this, serializedType);

        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
        {
            if (properties is IPropertyWrapper)
            {
                if (properties is IPropertyList<TContainer> propertyList)
                {
                    foreach (var property in propertyList.GetProperties(ref container))
                    {
                        using (CreatePropertyScope(property))
                        {
                            ((IPropertyAccept<TContainer>)property).Accept(this, ref container);
                        }
                    }
                }
                else
                {
                    throw new Exception("PropertyWrapper is missing the built in property bag.");
                }
            }
            else
            {
                if (m_View.Type != TokenType.Object)
                {
                    throw new ArgumentException();
                }

                var obj = m_View.AsObjectView();
                if (properties is IPropertyList<TContainer> propertyList)
                {
                    foreach (var property in propertyList.GetProperties(ref container))
                    {
                        AcceptProperty(ref container, obj, property);
                    }
                }
                else
                {
                    foreach (var property in properties.GetProperties(ref container))
                    {
                        AcceptProperty(ref container, obj, property);
                    }
                }
            }

            try
            {
                if (container is ISerializationCallbackReceiver receiver)
                    receiver.OnAfterDeserialize();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        protected virtual void AcceptProperty<TContainer>(ref TContainer container, UnsafeObjectView view, IProperty<TContainer> property)
        {
            if (view.TryGetValue(property.Name, out var value))
            {
                using (CreatePropertyScope(property))
                using (CreateViewScope(value))
                {
                    ((IPropertyAccept<TContainer>)property).Accept(this, ref container);
                }

                if (container is IFormatVersion formatVersion)
                    formatVersion.CheckFormatVersion();

                return;
            }

            foreach (var attribute in property.GetAttributes<FormerNameAttribute>())
            {
                if (view.TryGetValue(attribute.OldName, out value))
                {
                    using (CreatePropertyScope(property))
                    using (CreateViewScope(value))
                    {
                        ((IPropertyAccept<TContainer>)property).Accept(this, ref container);
                    }

                    return;
                }
            }

            foreach (var attribute in property.GetAttributes<UnityEngine.Serialization.FormerlySerializedAsAttribute>())
            {
                if (view.TryGetValue(attribute.oldName, out value))
                {
                    using (CreatePropertyScope(property))
                    using (CreateViewScope(value))
                    {
                        ((IPropertyAccept<TContainer>)property).Accept(this, ref container);
                    }

                    return;
                }
            }
        }

        void ISetPropertyBagVisitor.Visit<TSet, TElement>(ISetPropertyBag<TSet, TElement> properties, ref TSet container)
        {
            var elements = m_Metadata.HasElements ? m_View.AsObjectView()[k_SerializedElementsKey] : m_View;

            if (elements.Type != TokenType.Array)
            {
                throw new ArgumentException();
            }

            var arr = elements.AsArrayView();

            container.Clear();

            foreach (var element in arr)
            {
                var value = default(TElement);
                ReadValue(ref value, element);
                container.Add(value);
            }
        }

        void IListPropertyBagVisitor.Visit<TList, TElement>(IListPropertyBag<TList, TElement> properties, ref TList container)
        {
            var elements = m_Metadata.HasElements ? m_View.AsObjectView()[k_SerializedElementsKey] : m_View;

            if (elements.Type != TokenType.Array)
            {
                throw new ArgumentException();
            }

            var arr = elements.AsArrayView();

            if (typeof(TList).IsArray)
            {
                var index = 0;

                // @FIXME boxing
                foreach (var element in arr)
                {
                    var value = default(TElement);
                    ReadValue(ref value, element);
                    container[index++] = value;
                }
            }
            else
            {
                container.Clear();

                foreach (var element in arr)
                {
                    var value = default(TElement);
                    ReadValue(ref value, element);
                    container.Add(value);
                }
            }
        }

        void IDictionaryPropertyBagVisitor.Visit<TDictionary, TKey, TValue>(IDictionaryPropertyBag<TDictionary, TKey, TValue> properties, ref TDictionary container)
        {
            var elements = m_Metadata.HasElements ? m_View.AsObjectView()[k_SerializedElementsKey] : m_View;

            container.Clear();

            switch (elements.Type)
            {
                case TokenType.Array:
                {
                    var arr = elements.AsArrayView();

                    foreach (var element in arr)
                    {
                        if (element.Type != TokenType.Object)
                        {
                            continue;
                        }

                        var obj = element.AsObjectView();

                        if (obj.TryGetValue("Key", out var kView) && obj.TryGetValue("Value", out var vView))
                        {
                            var key = default(TKey);
                            ReadValue(ref key, kView);

                            var value = default(TValue);
                            ReadValue(ref value, vView);

                            container.Add(key, value);
                        }
                    }

                    break;
                }
                case TokenType.Object:
                {
                    var obj = elements.AsObjectView();

                    foreach (var member in obj)
                    {
                        var key = default(TKey);
                        ReadValue(ref key, member.Key());

                        var value = default(TValue);
                        ReadValue(ref value, member.Value());

                        container.Add(key, value);
                    }
                    break;
                }
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var unityObjectReferenceProperty = property as IUnityObjectReferenceProperty<TContainer, TValue>;
            var value = unityObjectReferenceProperty != null
                ? unityObjectReferenceProperty.GetValue(ref container, m_SerializationMetadata)
                : property.GetValue(ref container);

            var isRootProperty = property is IPropertyWrapper;

            ReadValue(ref value, m_View, isRootProperty);

            if (!property.IsReadOnly)
            {
                if (unityObjectReferenceProperty != null)
                    unityObjectReferenceProperty.SetValue(ref container, value, m_SerializationMetadata);
                else
                    property.SetValue(ref container, value);
            }
            else if (PropertyChecks.CheckReadOnlyPropertyForDeserialization(property, ref container, ref value, out var error))
            {
                m_SerializedTypeProvider.Events.Add(new DeserializationEvent(EventType.Exception, new SerializationException(error)));
            }
        }

        protected void ReadValue<TValue>(ref TValue value, UnsafeValueView view, bool isRoot = false)
        {
            switch (view.Type)
            {
                case TokenType.String:
                {
                    TypeConversion.TryConvert(view.AsStringView().ToString(), out value);
                    break;
                }
                case TokenType.Primitive:
                {
                    var p = view.AsPrimitiveView();

                    if (p.IsIntegral())
                    {
                        if (p.IsSigned())
                        {
                            TypeConversion.TryConvert(p.AsInt64(), out value);
                        }
                        else if (value is long)
                        {
                            TypeConversion.TryConvert(p.AsInt64(), out value);
                        }
                        else
                        {
                            TypeConversion.TryConvert(p.AsUInt64(), out value);
                        }
                    }
                    else if (p.IsDecimal() || p.IsInfinity() || p.IsNaN())
                    {
                        TypeConversion.TryConvert(p.AsFloat(), out value);
                    }
                    else if (p.IsBoolean())
                    {
                        TypeConversion.TryConvert(p.AsBoolean(), out value);
                    }
                    else if (p.IsNull())
                    {
                        value = default;
                    }

                    break;
                }
                default:
                {
                    var metadata = view.Type == TokenType.Object ? GetSerializedContainerMetadata(view.AsObjectView()) : default;

                    m_SerializedTypeProvider.View = view;
                    m_SerializedTypeProvider.SerializedType = isRoot ? m_SerializedType : null;

                    try
                    {
                        var customValue = m_CustomTypeConstructor?.Construct(typeof(TValue), view);
                        if (customValue != null)
                            value = (TValue)customValue;

                        if (value == null)
                        {
                            DefaultTypeConstruction.Construct(ref value, m_SerializedTypeProvider);
                        }
                    }
                    catch (ArgumentException e)
                    {
                        m_SerializedTypeProvider.Events.Add(new DeserializationEvent(EventType.Exception, new ArgumentException(e.Message)));
                        return;
                    }

                    using (new SerializedContainerMetadataScope(this, metadata))
                    using (new UnsafeViewScope(this, view))
                    {
                        var type = value?.GetType();
#if !NET_DOTS && !ENABLE_IL2CPP
                        if (type != null)
                            SceneSerialization.RegisterPropertyBag(type);
#endif

                        if (!PropertyContainer.Visit(ref value, this, out var errorCode))
                        {
                            switch (errorCode)
                            {
                                case VisitErrorCode.NullContainer:
                                    throw new ArgumentNullException(nameof(value));
                                case VisitErrorCode.InvalidContainerType:
                                    throw new InvalidContainerTypeException(type);
                                case VisitErrorCode.MissingPropertyBag:
                                    throw new MissingPropertyBagException(type);
                                default:
                                    throw new Exception($"Unexpected {nameof(VisitErrorCode)}=[{errorCode}]");
                            }
                        }
                    }

                    break;
                }
            }
        }
    }
}
