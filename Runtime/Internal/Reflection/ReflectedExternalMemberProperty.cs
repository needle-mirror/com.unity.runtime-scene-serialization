#if !NET_DOTS
using System;
using System.Collections;
using System.Reflection;
using Unity.Collections;
using Unity.Properties;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class ReflectedMemberPropertyName
    {
        internal const string ContainerFieldName = "Container";
    }

    /// <summary>
    /// A <see cref="ReflectedMemberProperty{TContainer,TValue}"/> provides strongly typed access to an underlying <see cref="FieldInfo"/> or <see cref="PropertyInfo"/> object.
    /// </summary>
    /// <remarks>
    /// The implementation uses slow reflection calls internally. This is intended to be used as an intermediate solution for quick editor iteration.
    /// </remarks>
    /// <typeparam name="TContainer">The container type for this property.</typeparam>
    /// <typeparam name="TValue">The value type for this property.</typeparam>
    class ReflectedExternalMemberProperty<TContainer, TValue> : Property<TContainer, TValue>
    {
        static readonly bool k_MemberIsArray = typeof(TValue).IsArray;
        static readonly bool k_MemberIsList = typeof(IList).IsAssignableFrom(typeof(TValue)) && typeof(TValue).IsGenericType;

        static readonly FieldInfo k_ExternalMemberField;
        static readonly FieldInfo k_ExternalContainerField;

        readonly IMemberInfo m_Info;
        readonly Type m_ExternalContainerType;
        readonly Type m_ExternalMemberType;

        TValue m_ExternalMemberContainer;
        Array m_ExternalMemberContainerArray;
        IList m_ExternalMemberContainerList;

        /// <inheritdoc/>
        public override string Name { get; }

        /// <inheritdoc/>
        public override bool IsReadOnly { get; }

        static ReflectedExternalMemberProperty()
        {
            var valueType = typeof(TValue);
            if (k_MemberIsArray)
                valueType = typeof(TValue).GetElementType();
            else if (k_MemberIsList)
                valueType = typeof(TValue).GenericTypeArguments[0];

            k_ExternalMemberField = valueType?.GetField(ReflectedMemberPropertyName.ContainerFieldName);
            k_ExternalContainerField = typeof(TContainer).GetField(ReflectedMemberPropertyName.ContainerFieldName);
        }

        /// <summary>
        /// Initializes a new <see cref="ReflectedMemberProperty{TContainer,TValue}"/> instance for the specified <see cref="FieldInfo"/>.
        /// </summary>
        /// <param name="info">The system reflection field info.</param>
        /// <param name="name">Use this name property--this might override the MemberInfo name</param>
        /// <param name="externalContainerType">The external type which should be wrapped, if any</param>
        // ReSharper disable once UnusedMember.Global
        public ReflectedExternalMemberProperty(FieldInfo info, string name, string externalContainerType)
            : this(new Unity.Properties.FieldMember(info), name, externalContainerType) { }

        /// <summary>
        /// Initializes a new <see cref="ReflectedMemberProperty{TContainer,TValue}"/> instance for the specified <see cref="PropertyInfo"/>.
        /// </summary>
        /// <param name="info">The system reflection property info.</param>
        /// <param name="name">Use this name property--this might override the MemberInfo name</param>
        /// <param name="externalContainerType">The external type which should be wrapped, if any</param>
        // ReSharper disable once UnusedMember.Global
        public ReflectedExternalMemberProperty(PropertyInfo info, string name, string externalContainerType)
            : this(new Unity.Properties.PropertyMember(info), name, externalContainerType) { }

        /// <summary>
        /// Initializes a new <see cref="ReflectedMemberProperty{TContainer,TValue}"/> instance. This is an internal constructor.
        /// </summary>
        /// <param name="info">The reflected info object backing this property.</param>
        /// <param name="name">Use this name property--this might override the MemberInfo name</param>
        /// <param name="externalContainerType">The external type which should be wrapped, if any</param>
        ReflectedExternalMemberProperty(IMemberInfo info, string name, string externalContainerType)
        {
            Name = name;
            m_Info = info;
            if (!string.IsNullOrEmpty(externalContainerType))
                m_ExternalContainerType = Type.GetType(externalContainerType);

            var memberType = info.ValueType;
            if (memberType != typeof(TValue))
                m_ExternalMemberType = memberType;

            AddAttributes(info.GetCustomAttributes());
            var isReadOnly = m_Info.IsReadOnly || HasAttribute<ReadOnlyAttribute>();
            IsReadOnly = isReadOnly;
        }

        /// <inheritdoc/>
        public override TValue GetValue(ref TContainer container)
        {
            if (m_ExternalContainerType != null)
            {
                var containerValue = GetContainerValue(container);
                if (m_ExternalMemberType != null)
                    return GetExternalMemberValue(containerValue);

                return (TValue)m_Info.GetValue(containerValue);
            }

            if (m_ExternalMemberType != null)
                return GetExternalMemberValue(container);

            // Should be unreachable but fall back to default
            return default;
        }

        object GetContainerValue(TContainer container)
        {
            return k_ExternalContainerField.GetValue(container) ?? Activator.CreateInstance(m_ExternalContainerType);
        }

        TValue GetExternalMemberValue(object container)
        {
            if (k_MemberIsArray)
            {
                var elementType = typeof(TValue).GetElementType();
                if (elementType == null)
                {
                    Debug.LogWarning($"Could not get element type from {typeof(TValue)}");
                    return default;
                }

                var array = (Array)m_Info.GetValue(container);
                if (array == null)
                    return default;

                var length = array.Length;
                if (m_ExternalMemberContainerArray == null || m_ExternalMemberContainerArray.Length != length)
                    m_ExternalMemberContainerArray = Array.CreateInstance(elementType, length);

                for (var i = 0; i < length; i++)
                {
                    var element = m_ExternalMemberContainerArray.GetValue(i) ?? Activator.CreateInstance(elementType);
                    k_ExternalMemberField.SetValue(element, array.GetValue(i));
                    m_ExternalMemberContainerArray.SetValue(element, i);
                }

                return (TValue)(object)m_ExternalMemberContainerArray;
            }

            if (k_MemberIsList)
            {
                var elementType = typeof(TValue).GenericTypeArguments[0];
                if (elementType == null)
                {
                    Debug.LogWarning($"Could not get element type from {typeof(TValue)}");
                    return default;
                }

                var list = (IList)m_Info.GetValue(container);
                if (list == null)
                    return default;

                var count = list.Count;
                if (m_ExternalMemberContainerList == null || m_ExternalMemberContainerList.Count != count)
                    m_ExternalMemberContainerList = (IList)Activator.CreateInstance<TValue>();

                while (m_ExternalMemberContainerList.Count > count)
                {
                    m_ExternalMemberContainerList.RemoveAt(m_ExternalMemberContainerList.Count - 1);
                }

                while (m_ExternalMemberContainerList.Count < count)
                {
                    m_ExternalMemberContainerList.Add(null);
                }

                for (var i = 0; i < count; i++)
                {
                    var element = m_ExternalMemberContainerList[i] ?? Activator.CreateInstance(elementType);
                    k_ExternalMemberField.SetValue(element, list[i]);
                    m_ExternalMemberContainerList[i] = element;
                }

                return (TValue)m_ExternalMemberContainerList;
            }

            if (m_ExternalMemberContainer == null)
                m_ExternalMemberContainer = Activator.CreateInstance<TValue>();

            k_ExternalMemberField.SetValue(m_ExternalMemberContainer, m_Info.GetValue(container));
            return m_ExternalMemberContainer;
        }

        /// <inheritdoc/>
        public override void SetValue(ref TContainer container, TValue value)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Property is ReadOnly.");
            }

            if (m_ExternalContainerType != null)
            {
                var containerValue = GetContainerValue(container);
                SetValueInternal(containerValue, value);
                k_ExternalContainerField.SetValue(container, containerValue);
                return;
            }

            SetValueInternal(container, value);
        }

        void SetValueInternal(object container, TValue value)
        {
            if (m_ExternalMemberType != null)
            {
                if (value == null)
                {
                    m_Info.SetValue(container, null);
                    return;
                }

                if (k_MemberIsArray)
                {
                    var array = (Array)(object)value;
                    var length = array.Length;
                    var destArray = (Array)m_Info.GetValue(container);
                    if (destArray == null || destArray.Length != length)
                        destArray = Array.CreateInstance(m_ExternalMemberType, length);

                    for (var i = 0; i < length; i++)
                    {
                        var element = array.GetValue(i);
                        if (element != null)
                            element = k_ExternalMemberField.GetValue(element);

                        destArray.SetValue(element, i);
                    }

                    m_Info.SetValue(container, destArray);
                    return;
                }

                if (k_MemberIsList)
                {
                    var list = (IList)value;
                    var count = list.Count;
                    var destList = (IList)m_Info.GetValue(container);
                    if (destList == null || destList.Count != count)
                        destList = (IList)Activator.CreateInstance(m_ExternalMemberType);

                    while (destList.Count > count)
                    {
                        destList.RemoveAt(destList.Count - 1);
                    }

                    while (destList.Count < count)
                    {
                        destList.Add(null);
                    }

                    for (var i = 0; i < count; i++)
                    {
                        var element = list[i];
                        if (element != null)
                            element = k_ExternalMemberField.GetValue(element);

                        destList[i] = element;
                    }

                    m_Info.SetValue(container, destList);
                    return;
                }

                var memberValue = k_ExternalMemberField.GetValue(value);
                m_Info.SetValue(container, memberValue);
                return;
            }

            m_Info.SetValue(container, value);
        }
    }
}
#endif
