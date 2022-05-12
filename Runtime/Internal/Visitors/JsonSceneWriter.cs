using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Properties;
using Unity.Properties.Internal;
using Unity.Serialization;
using Unity.Serialization.Json;
using Unity.Serialization.Json.Adapters;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    /// <summary>
    /// A visitor that traverses a property container and outputs a JSON string.
    /// </summary>
    class JsonSceneWriter : JsonPropertyVisitor,
        IPropertyBagVisitor,
        ICollectionPropertyBagVisitor,
        IListPropertyBagVisitor,
        IDictionaryPropertyBagVisitor,
        IPropertyVisitor
    {
        struct SerializedType
        {
            public Type Type;
        }

        class SerializedTypeProperty : Property<SerializedType, string>
        {
            public override string Name => k_SerializedTypeKey;
            public override bool IsReadOnly => true;
            public override string GetValue(ref SerializedType container) => $"{GetAssemblyQualifiedName(container.Type)}";
            public override void SetValue(ref SerializedType container, string value) => throw new InvalidOperationException("Property is ReadOnly.");
        }

        static string GetAssemblyQualifiedName(Type type)
        {
            var name = type.FullName;
            if (type.IsGenericType)
            {
                var arguments = new List<string>();
                foreach (var argument in type.GetGenericArguments())
                {
                    arguments.Add($"[{GetAssemblyQualifiedName(argument)}]");
                }

                name = $"{name}[{string.Join(", ", arguments)}]";
            }

            return $"{name}, {type.Module.Assembly.GetName().Name}";
        }

        struct SerializedContainerMetadata
        {
            public bool HasSerializedType;

            /// <summary>
            /// Returns true if there is any metadata to write out.
            /// </summary>
            public bool Exists => HasSerializedType;
        }

        /// <summary>
        /// Shared property used to write the serialized type metadata.
        /// </summary>
        static readonly SerializedTypeProperty k_SerializedTypeProperty = new SerializedTypeProperty();

        readonly SerializationMetadata m_SerializationMetadata;
        JsonWriter m_Writer;
        Type m_RootType;
        bool m_DisableRootAdapters;
        JsonAdapterCollection m_Adapters;

        public void SetWriter(JsonWriter writer)
            => m_Writer = writer;

        public void SetSerializedType(Type type)
            => m_RootType = type;

        public void SetDisableRootAdapters(bool disableRootAdapters)
            => m_DisableRootAdapters = disableRootAdapters;

        public void SetGlobalAdapters(List<IJsonAdapter> adapters)
            => m_Adapters.Global = adapters;

        public void SetUserDefinedAdapters(List<IJsonAdapter> adapters)
            => m_Adapters.UserDefined = adapters;

        public JsonSceneWriter(SerializationMetadata metadata)
        {
            m_Adapters.InternalAdapter = new JsonAdapter();
            m_SerializationMetadata = metadata;
        }

        SerializedContainerMetadata GetSerializedContainerMetadata<TContainer>()
        {
            var type = typeof(TContainer);

            // Never write metadata for special json types.
            if (type == typeof(JsonObject) || type == typeof(JsonArray)) return default;

            var metadata = default(SerializedContainerMetadata);
            // This is a very common case. At serialize time we are serializing something that is polymorphic or an object
            // However at deserialize time the user will provide the System.Type, we can avoid writing out the fully qualified type name in this case.
            var isRootAndTypeWasGiven = Property is IPropertyWrapper && null != m_RootType;
            metadata.HasSerializedType = Property.DeclaredValueType() != type && !isRootAndTypeWasGiven;
            return metadata;
        }

        void WriteSerializedContainerMetadata<TContainer>(ref TContainer container, SerializedContainerMetadata metadata)
        {
            if (metadata.HasSerializedType)
            {
                using (CreatePropertyScope(k_SerializedTypeProperty))
                {
                    var typeInfo = new SerializedType {Type = container.GetType()};
                    ((IPropertyAccept<SerializedType>) k_SerializedTypeProperty).Accept(this, ref typeInfo);
                }
            }
        }

        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
        {
            try
            {
                if (container is ISerializationCallbackReceiver receiver)
                    receiver.OnBeforeSerialize();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            var isRootContainer = properties is IPropertyWrapper;
            if (!isRootContainer)
            {
                var metadata = GetSerializedContainerMetadata<TContainer>();
                m_Writer.WriteBeginObject();
                WriteSerializedContainerMetadata(ref container, metadata);
            }

            if (properties is IPropertyList<TContainer> propertyList)
            {
                // no boxing
                foreach (var property in propertyList.GetProperties(ref container))
                {
                    if (property.HasAttribute<NonSerializedAttribute>() || property.HasAttribute<DontSerializeAttribute>())
                        continue;

                    using (CreatePropertyScope(property))
                        ((IPropertyAccept<TContainer>) property).Accept(this, ref container);
                }
            }
            else
            {
                // boxing
                foreach (var property in properties.GetProperties(ref container))
                {
                    if (property.HasAttribute<NonSerializedAttribute>() || property.HasAttribute<DontSerializeAttribute>())
                        continue;

                    using (CreatePropertyScope(property))
                        ((IPropertyAccept<TContainer>) property).Accept(this, ref container);
                }
            }

            if (!isRootContainer)
            {
                m_Writer.WriteEndObject();
            }
        }

        void ICollectionPropertyBagVisitor.Visit<TCollection, TElement>(ICollectionPropertyBag<TCollection, TElement> properties, ref TCollection container)
        {
            var metadata = GetSerializedContainerMetadata<TCollection>();
            if (metadata.Exists)
            {
                m_Writer.WriteBeginObject();
                WriteSerializedContainerMetadata(ref container, metadata);
                m_Writer.WriteKey(k_SerializedElementsKey);
            }

            using (m_Writer.WriteArrayScope())
            {
                foreach (var property in properties.GetProperties(ref container))
                {
                    using (CreatePropertyScope(property))
                        ((IPropertyAccept<TCollection>) property).Accept(this, ref container);
                }
            }

            if (metadata.Exists)
            {
                m_Writer.WriteEndObject();
            }
        }

        void IListPropertyBagVisitor.Visit<TList, TElement>(IListPropertyBag<TList, TElement> properties, ref TList container)
        {
            var metadata = GetSerializedContainerMetadata<TList>();
            if (metadata.Exists)
            {
                m_Writer.WriteBeginObject();
                WriteSerializedContainerMetadata(ref container, metadata);
                m_Writer.WriteKey(k_SerializedElementsKey);
            }

            using (m_Writer.WriteArrayScope())
            {
                foreach (var property in properties.GetProperties(ref container))
                {
                    using (CreatePropertyScope(property))
                        ((IPropertyAccept<TList>) property).Accept(this, ref container);
                }
            }

            if (metadata.Exists)
            {
                m_Writer.WriteEndObject();
            }
        }

        void IDictionaryPropertyBagVisitor.Visit<TDictionary, TKey, TValue>(IDictionaryPropertyBag<TDictionary, TKey, TValue> properties, ref TDictionary container)
        {
            if (typeof(TKey) != typeof(string))
            {
                ((ICollectionPropertyBagVisitor) this).Visit(properties, ref container);
            }
            else
            {
                var metadata = GetSerializedContainerMetadata<TDictionary>();
                if (metadata.Exists)
                {
                    m_Writer.WriteBeginObject();
                    WriteSerializedContainerMetadata(ref container, metadata);
                    m_Writer.WriteKey(k_SerializedElementsKey);
                }

                using (m_Writer.WriteObjectScope())
                {
                    // @FIXME allocations
                    var property = new DictionaryElementProperty<TDictionary, TKey, TValue>();

                    foreach (var kvp in container)
                    {
                        property.Key = kvp.Key;
                        ((IPropertyAccept<TDictionary>) property).Accept(this, ref container);
                    }
                }

                if (metadata.Exists)
                {
                    m_Writer.WriteEndObject();
                }
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var isRootProperty = property is IPropertyWrapper;

            if (!isRootProperty && !(property is ICollectionElementProperty))
            {
                m_Writer.WriteKey(property.Name);
            }

            TValue value;
            if (property is IUnityObjectReferenceProperty<TContainer, TValue> unityObjectReferenceProperty)
                value = unityObjectReferenceProperty.GetValue(ref container, m_SerializationMetadata);
            else
                value = property.GetValue(ref container);

            WriteValue(ref value, isRootProperty);
        }

        void WriteValue<TValue>(ref TValue value, bool isRoot)
        {
            var runAdapters = !(isRoot && m_DisableRootAdapters);

            switch (value)
            {
                //HACK: Work around incorrect primitive serialization
                case ulong uLongValue:
                    m_Writer.WriteValueLiteral(uLongValue.ToString());
                    return;
                case float doubleValue:
                    m_Writer.WriteValueLiteral(doubleValue.ToString("G9", CultureInfo.InvariantCulture));
                    return;
                case double doubleValue:
                    m_Writer.WriteValueLiteral(doubleValue.ToString("G17", CultureInfo.InvariantCulture));
                    return;
                case decimal decimalValue:
                    m_Writer.WriteValueLiteral(decimalValue.ToString(CultureInfo.InvariantCulture));
                    return;
            }

            if (runAdapters && !RuntimeTypeInfoCache<TValue>.IsContainerType && m_Adapters.TrySerialize(m_Writer, ref value))
                return;

            if (RuntimeTypeInfoCache<TValue>.IsEnum)
            {
                WritePrimitiveBoxed(m_Writer, value, Enum.GetUnderlyingType(typeof(TValue)));
                return;
            }

            if (RuntimeTypeInfoCache<TValue>.CanBeNull && EqualityComparer<TValue>.Default.Equals(value, default) )
            {
                m_Writer.WriteNull();
                return;
            }

            if (RuntimeTypeInfoCache<TValue>.IsNullable)
            {
                WritePrimitiveBoxed(m_Writer, value, Nullable.GetUnderlyingType(typeof(TValue)));
                return;
            }

            var type = value.GetType();
            if (RuntimeTypeInfoCache<TValue>.IsObjectType && !RuntimeTypeInfoCache.IsContainerType(type))
            {
                WritePrimitiveBoxed(m_Writer, value, type);
                return;
            }

            if (RuntimeTypeInfoCache<TValue>.IsContainerType)
            {
#if !NET_DOTS && !ENABLE_IL2CPP
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
                return;
            }

            throw new Exception($"Unsupported Type {type}.");
        }

        static void WritePrimitiveBoxed(JsonWriter writer, object value, Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                    writer.WriteValue((sbyte) value);
                    return;
                case TypeCode.Int16:
                    writer.WriteValue((short) value);
                    return;
                case TypeCode.Int32:
                    writer.WriteValue((int) value);
                    return;
                case TypeCode.Int64:
                    writer.WriteValue((long) value);
                    return;
                case TypeCode.Byte:
                    writer.WriteValue((byte) value);
                    return;
                case TypeCode.UInt16:
                    writer.WriteValue((ushort) value);
                    return;
                case TypeCode.UInt32:
                    writer.WriteValue((uint) value);
                    return;
                case TypeCode.UInt64:
                    writer.WriteValue((ulong) value);
                    return;
                case TypeCode.Single:
                    writer.WriteValue((float) value);
                    return;
                case TypeCode.Double:
                    writer.WriteValue((double) value);
                    return;
                case TypeCode.Boolean:
                    writer.WriteValueLiteral((bool) value ? "true" : "false");
                    return;
                case TypeCode.Char:
                    writer.WriteValue((char) value);
                    return;
                case TypeCode.String:
                    writer.WriteValue(value as string);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
