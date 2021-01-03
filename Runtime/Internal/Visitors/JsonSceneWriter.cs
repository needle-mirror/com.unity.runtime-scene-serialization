﻿using System;
using System.Collections.Generic;
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
        /// Constants for styling and special keys.
        /// </summary>
        static class Style
        {
            /// <summary>
            /// Spaces for indentation
            /// </summary>
            public const int Space = 4;
        }

        /// <summary>
        /// Shared property used to write the serialized type metadata.
        /// </summary>
        static readonly SerializedTypeProperty k_SerializedTypeProperty = new SerializedTypeProperty();

        /// <summary>
        /// Gets or sets the current indent level.
        /// </summary>
        int Indent { get; set; }

        readonly SerializationMetadata m_SerializationMetadata;
        JsonStringBuffer m_Writer;
        Type m_RootType;
        bool m_DisableRootAdapters;
        JsonAdapterCollection m_Adapters;
        bool m_Minified;
        bool m_Simplified;

        public void SetStringWriter(JsonStringBuffer writer)
            => m_Writer = writer;

        public void SetSerializedType(Type type)
            => m_RootType = type;

        public void SetDisableRootAdapters(bool disableRootAdapters)
            => m_DisableRootAdapters = disableRootAdapters;

        public void SetGlobalAdapters(List<IJsonAdapter> adapters)
            => m_Adapters.Global = adapters;

        public void SetUserDefinedAdapters(List<IJsonAdapter> adapters)
            => m_Adapters.UserDefined = adapters;

        public void SetMinified(bool minified)
            => m_Minified = minified;

        public void SetSimplified(bool simplified)
            => m_Simplified = simplified;

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

        void WriteSerializedContainerMetadata<TContainer>(ref TContainer container, SerializedContainerMetadata metadata, ref int count)
        {
            if (metadata.HasSerializedType)
            {
                using (CreatePropertyScope(k_SerializedTypeProperty))
                {
                    WriteMemberSeparator(ref count);
                    var typeInfo = new SerializedType {Type = container.GetType()};
                    ((IPropertyAccept<SerializedType>) k_SerializedTypeProperty).Accept(this, ref typeInfo);
                }
            }
        }

        void WriteBeginObject()
        {
            m_Writer.Write('{');

            if (!m_Minified)
                Indent++;
        }

        void WriteEndObject(int memberCount)
        {
            if (!m_Minified)
            {
                Indent--;

                if (memberCount > 0)
                    WriteIndent();
            }

            m_Writer.Write('}');
        }

        void WriteBeginCollection()
        {
            m_Writer.Write('[');

            if (!m_Minified)
            {
                Indent++;
            }
        }

        void WriteEndCollection(int memberCount)
        {
            if (!m_Minified)
            {
                Indent--;

                if (memberCount > 0)
                    WriteIndent();
            }

            m_Writer.Write(']');
        }

        void WriteIndent()
        {
            if (m_Minified)
                return;

            m_Writer.Write('\n');
            m_Writer.Write(' ', Style.Space * Indent);
        }

        void WriteMemberSeparator(ref int memberCount)
        {
            if (!m_Simplified)
            {
                if (memberCount > 0)
                    m_Writer.Write(',');

                if (!m_Minified)
                    WriteIndent();
            }
            else
            {
                if (!m_Minified)
                    WriteIndent();
                else if (memberCount > 0)
                    m_Writer.Write(' ');
            }

            memberCount++;
        }

        void WritePropertyName(string name)
        {
            var useQuotes = !m_Simplified || ContainsAnySpecialCharacters(name);

            if (useQuotes)
                m_Writer.Write('\"');

            m_Writer.Write(name);

            if (useQuotes)
                m_Writer.Write('\"');

            if (!m_Minified && m_Simplified)
                m_Writer.Write(' ');

            m_Writer.Write(m_Simplified ? '=' : ':');

            if (!m_Minified)
                m_Writer.Write(' ');
        }

        static unsafe bool ContainsAnySpecialCharacters(string key)
        {
            fixed (char* chars = key)
            {
                for (var i = 0; i < key.Length; i++)
                {
                    var c = chars[i];
                    if (c == ' ' ||
                        c == '\t' ||
                        c == '\r' ||
                        c == '\n' ||
                        c == '\0' ||
                        c == ',' ||
                        c == ']' ||
                        c == '}' ||
                        c == ':' ||
                        c == '=')
                        return true;
                }
            }

            return false;
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

            var count = 0;

            if (!isRootContainer)
            {
                var metadata = GetSerializedContainerMetadata<TContainer>();
                WriteBeginObject();
                WriteSerializedContainerMetadata(ref container, metadata, ref count);
            }

            if (properties is IPropertyList<TContainer> propertyList)
            {
                // no boxing
                foreach (var property in propertyList.GetProperties(ref container))
                {
                    if (property.HasAttribute<NonSerializedAttribute>() || property.HasAttribute<DontSerializeAttribute>())
                        continue;

                    using (CreatePropertyScope(property))
                    {
                        if (!isRootContainer) WriteMemberSeparator(ref count);
                        ((IPropertyAccept<TContainer>) property).Accept(this, ref container);
                    }
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
                    {
                        if (!isRootContainer) WriteMemberSeparator(ref count);
                        ((IPropertyAccept<TContainer>) property).Accept(this, ref container);
                    }
                }
            }

            if (!isRootContainer)
            {
                WriteEndObject(count);
            }
        }

        void ICollectionPropertyBagVisitor.Visit<TCollection, TElement>(ICollectionPropertyBag<TCollection, TElement> properties, ref TCollection container)
        {
            var metadata = GetSerializedContainerMetadata<TCollection>();
            var metadataCount = 0;

            if (metadata.Exists)
            {
                WriteBeginObject();
                WriteSerializedContainerMetadata(ref container, metadata, ref metadataCount);
                WriteMemberSeparator(ref metadataCount);
                WritePropertyName(k_SerializedElementsKey);
            }

            WriteBeginCollection();

            var elementCount = 0;

            foreach (var property in properties.GetProperties(ref container))
            {
                using (CreatePropertyScope(property))
                {
                    WriteMemberSeparator(ref elementCount);
                    ((IPropertyAccept<TCollection>) property).Accept(this, ref container);
                }
            }

            WriteEndCollection(elementCount);

            if (metadata.Exists)
            {
                WriteEndObject(metadataCount);
            }
        }

        void IListPropertyBagVisitor.Visit<TList, TElement>(IListPropertyBag<TList, TElement> properties, ref TList container)
        {
            var metadata = GetSerializedContainerMetadata<TList>();
            var metadataCount = 0;
            if (metadata.Exists)
            {
                WriteBeginObject();
                WriteSerializedContainerMetadata(ref container, metadata, ref metadataCount);
                WriteMemberSeparator(ref metadataCount);
                WritePropertyName(k_SerializedElementsKey);
            }

            WriteBeginCollection();

            var elementCount = 0;

            foreach (var property in properties.GetProperties(ref container))
            {
                using (CreatePropertyScope(property))
                {
                    WriteMemberSeparator(ref elementCount);
                    ((IPropertyAccept<TList>) property).Accept(this, ref container);
                }
            }

            WriteEndCollection(elementCount);

            if (metadata.Exists)
            {
                WriteEndObject(metadataCount);
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
                var metadataCount = 0;

                if (metadata.Exists)
                {
                    WriteBeginObject();
                    WriteSerializedContainerMetadata(ref container, metadata, ref metadataCount);
                    WriteMemberSeparator(ref metadataCount);
                    WritePropertyName(k_SerializedElementsKey);
                }

                WriteBeginObject();

                var elementCount = 0;

                // @FIXME allocations
                var property = new DictionaryElementProperty<TDictionary, TKey, TValue>();

                foreach (var kvp in container)
                {
                    WriteMemberSeparator(ref elementCount);
                    property.Key = kvp.Key;
                    ((IPropertyAccept<TDictionary>) property).Accept(this, ref container);
                }

                WriteEndObject(elementCount);

                if (metadata.Exists)
                {
                    WriteEndObject(metadataCount);
                }
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var isRootProperty = property is IPropertyWrapper;

            if (!isRootProperty && !(property is ICollectionElementProperty))
            {
                WritePropertyName(property.Name);
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
            if (runAdapters && !RuntimeTypeInfoCache<TValue>.IsContainerType && m_Adapters.TrySerialize(m_Writer, ref value))
                return;

            if (RuntimeTypeInfoCache<TValue>.IsEnum)
            {
                WritePrimitiveBoxed(m_Writer, value, Enum.GetUnderlyingType(typeof(TValue)));
                return;
            }

            if (RuntimeTypeInfoCache<TValue>.CanBeNull && EqualityComparer<TValue>.Default.Equals(value, default) )
            {
                m_Writer.Write("null");
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

        static void WritePrimitiveBoxed(JsonStringBuffer writer, object value, Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                    writer.Write((sbyte) value);
                    return;
                case TypeCode.Int16:
                    writer.Write((short) value);
                    return;
                case TypeCode.Int32:
                    writer.Write((int) value);
                    return;
                case TypeCode.Int64:
                    writer.Write((long) value);
                    return;
                case TypeCode.Byte:
                    writer.Write((byte) value);
                    return;
                case TypeCode.UInt16:
                    writer.Write((ushort) value);
                    return;
                case TypeCode.UInt32:
                    writer.Write((uint) value);
                    return;
                case TypeCode.UInt64:
                    writer.Write((ulong) value);
                    return;
                case TypeCode.Single:
                    writer.Write((float) value);
                    return;
                case TypeCode.Double:
                    writer.Write((double) value);
                    return;
                case TypeCode.Boolean:
                    writer.Write((bool) value ? "true" : "false");
                    return;
                case TypeCode.Char:
                    writer.WriteEncodedJsonString((char) value);
                    return;
                case TypeCode.String:
                    writer.WriteEncodedJsonString(value as string);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
