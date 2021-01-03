using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Properties;
using Unity.Properties.Internal;
using UnityEngine;
using UnityObject = UnityEngine.Object;

#if NET_DOTS || ENABLE_IL2CPP
using Unity.RuntimeSceneSerialization.Internal;
#else
using System.Reflection;
#endif

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    [Serializable]
    class RuntimePrefabPropertyOverride<TOverrideValue> : RuntimePrefabPropertyOverride
    {
#if NET_DOTS || ENABLE_IL2CPP
        class SetPropertyMethodFactory : SerializationUtils.IGenericMethodFactory
        {
            public RuntimePrefabPropertyOverride<TOverrideValue> Override;
            public string PropertyPath;
            public SerializationMetadata Metadata;

            public Action<T> GetGenericMethod<T>() where T : UnityObject
            {
                var factory = this;
                return obj =>
                {
                    factory.Override.SetProperty(ref obj, factory.PropertyPath, Metadata);
                };
            }
        }

        static readonly SetPropertyMethodFactory k_Factory = new SetPropertyMethodFactory();
#else
        static readonly MethodInfo k_SetPropertyMethod = typeof(RuntimePrefabPropertyOverride<TOverrideValue>).GetMethod(nameof(SetProperty), BindingFlags.Instance | BindingFlags.NonPublic);

        // ReSharper disable StaticMemberInGenericType
        static readonly Dictionary<Type, MethodInfo> k_SetPropertyMethods = new Dictionary<Type, MethodInfo>();
        static readonly object[] k_SetPropertyArguments = new object[3];
        // ReSharper restore StaticMemberInGenericType
#endif

        protected class SetPropertyVisitor : UnityObjectPropertyVisitor
        {
            readonly RuntimePrefabPropertyOverride<TOverrideValue> m_PrefabOverride;
            readonly string m_PropertyPath;
            readonly string m_FirstProperty;

            public SetPropertyVisitor(RuntimePrefabPropertyOverride<TOverrideValue> prefabOverride, string propertyPath,
                SerializationMetadata metadata) : base(metadata)
            {
                m_PrefabOverride = prefabOverride;
                m_PropertyPath = propertyPath;
                m_FirstProperty = propertyPath.Split('.')[0];
            }

            protected override void VisitProperty<TContainer, TValue>(Property<TContainer, TValue> property,
                ref TContainer container, ref TValue value)
            {
                if (!IsFirstProperty(property.Name))
                    return;

                if (m_PropertyPath.Contains("."))
                {
                    SetIntermediatePropertyValue(property, ref value);
                    return;
                }

                var overrideValue = m_PrefabOverride.m_Value;
                if (overrideValue is TValue val)
                {
                    // Value will be set during general visitation
                    value = val;
                    return;
                }

                var valueType = typeof(TValue);
                if (valueType.IsEnum)
                {
                    SetConvertedProperty(valueType, out value);
                    return;
                }

                // Integer and Float properties do not specify precision, so we need to cast to use the proper type conversion
                if (typeof(TOverrideValue) == typeof(long))
                {
                    SetConvertedProperty(out value);
                    return;
                }

                if (typeof(TOverrideValue) == typeof(float))
                {
                    if (valueType == typeof(double))
                    {
                        SetConvertedProperty(out value);
                        return;
                    }
                }

                Debug.LogError($"Could not cast {overrideValue} to {typeof(TValue)}");
            }

            void SetConvertedProperty<TConverted>(out TConverted value)
            {
                value = (TConverted)Convert.ChangeType(m_PrefabOverride.m_Value, typeof(TConverted));
            }

            void SetConvertedProperty<TConverted>(Type conversionType, out TConverted value)
            {
                value = (TConverted)ChangeType(m_PrefabOverride.m_Value, conversionType);
            }

            protected override void VisitList<TContainer, TList, TElement>(Property<TContainer, TList> property,
                ref TContainer container, ref TList value)
            {
                if (!IsFirstProperty(property.Name))
                    return;

                if (typeof(TList) == typeof(List<TElement>))
                {
                    VisitListPrivate<TContainer, TList, TElement>(property, ref container);
                    return;
                }

                if (typeof(TList) == typeof(TElement[]))
                {
                    VisitArray<TContainer, TList, TElement>(property, ref container);
                }
            }

            bool IsFirstProperty(string propertyName)
            {
                return m_FirstProperty == propertyName || m_FirstProperty == GetAlternateName(propertyName);
            }

            void VisitArray<TContainer, TArray, TElement>(Property<TContainer, TArray> property, ref TContainer container)
            {
                const string arrayToken = ".Array.data[";
                const int arrayTokenLength = 12;
                TArray array;
                TElement[] convertedArray;
                var unityObjectReferenceProperty = property as IUnityObjectReferenceProperty<TContainer, TArray>;
                if (!m_PropertyPath.Contains(arrayToken))
                {
                    const string arraySizeToken = ".Array.size";
                    if (m_PropertyPath.EndsWith(arraySizeToken) && m_PrefabOverride.m_Value is int arraySize)
                    {
                        array = unityObjectReferenceProperty == null
                            ? property.GetValue(ref container)
                            : unityObjectReferenceProperty.GetValue(ref container, m_SerializationMetadata);

                        convertedArray = array as TElement[];
                        if (convertedArray == null)
                        {
                            convertedArray = new TElement[arraySize];
                            for (var i = 0; i < arraySize; i++)
                            {
                                convertedArray[i] = CreateNewDefaultObject<TElement>();
                            }

                            // ReSharper disable once PatternNeverMatches
                            // ReSharper disable HeuristicUnreachableCode
                            if (convertedArray is TArray newArray)
                                property.SetValue(ref container, newArray);
                            else
                                Debug.LogError($"Could not convert {convertedArray} to {typeof(TArray)}");
                            // ReSharper restore HeuristicUnreachableCode
                        }
                        else
                        {
                            var length = convertedArray.Length;
                            {
                                if (length > arraySize)
                                {
                                    Array.Resize(ref convertedArray, arraySize);
                                }
                                else if (length < arraySize)
                                {
                                    Array.Resize(ref convertedArray, arraySize);
                                    for (var i = length; i < arraySize; i++)
                                    {
                                        convertedArray[i] = CreateNewDefaultObject<TElement>();
                                    }
                                }

                                // ReSharper disable once PatternNeverMatches
                                // ReSharper disable HeuristicUnreachableCode
                                if (convertedArray is TArray result)
                                    property.SetValue(ref container, result);
                                else
                                    Debug.LogError($"Could not convert {convertedArray} to {typeof(TArray)}");
                                // ReSharper restore HeuristicUnreachableCode
                            }
                        }
                        return;
                    }

                    Debug.LogWarning($"Property path {m_PropertyPath} is invalid for array property");
                    return;
                }

                var startIndex = m_PropertyPath.IndexOf(arrayToken) + arrayTokenLength;
                var endIndex = m_PropertyPath.IndexOf(']');
                var arrayIndexString = m_PropertyPath.Substring(startIndex, endIndex - startIndex);
                if (!int.TryParse(arrayIndexString, out var arrayIndex))
                {
                    Debug.LogWarning($"Could not parse array index from property path {m_PropertyPath}");
                    return;
                }

                var minSize = arrayIndex + 1;
                array = unityObjectReferenceProperty == null
                    ? property.GetValue(ref container)
                    : unityObjectReferenceProperty.GetValue(ref container, m_SerializationMetadata);

                if (array == null)
                {
                    convertedArray = new TElement[minSize];
                    for (var i = 0; i < minSize; i++)
                    {
                        convertedArray[i] = CreateNewDefaultObject<TElement>();
                    }

                    // ReSharper disable once PatternNeverMatches
                    // ReSharper disable HeuristicUnreachableCode
                    if (convertedArray is TArray result)
                        property.SetValue(ref container, result);
                    else
                        Debug.LogError($"Could not convert {convertedArray} to {typeof(TArray)}");
                    // ReSharper restore HeuristicUnreachableCode
                }
                else
                {
                    convertedArray = array as TElement[];
                    if (convertedArray == null)
                    {
                        Debug.LogError($"Could not convert {array} to {typeof(TElement[])}");
                        return;
                    }

                    var length = convertedArray.Length;
                    if (length < minSize)
                    {
                        Array.Resize(ref convertedArray, minSize);
                        for (var i = length; i < minSize; i++)
                        {
                            convertedArray[i] = CreateNewDefaultObject<TElement>();
                        }

                        // ReSharper disable once PatternNeverMatches
                        // ReSharper disable HeuristicUnreachableCode
                        if (convertedArray is TArray result)
                            property.SetValue(ref container, result);
                        else
                            Debug.LogError($"Could not convert {convertedArray} to {typeof(TArray)}");
                        // ReSharper restore HeuristicUnreachableCode
                    }
                }

                // Strip out ]. if there is a remainder
                endIndex += 2;
                if (m_PropertyPath.Length > endIndex)
                {
                    var remainder = m_PropertyPath.Substring(endIndex, m_PropertyPath.Length - endIndex);
                    var element = convertedArray[arrayIndex];
                    if (element == null)
                    {
                        try
                        {
                            element = CreateNewDefaultObject<TElement>();
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }

                        if (element == null)
                        {
                            Debug.LogWarning($"Could not set property with property path {m_PropertyPath} -- could not activate array element");
                            return;
                        }
                    }

                    m_PrefabOverride.SetProperty(ref element, remainder, m_SerializationMetadata);
                    convertedArray[arrayIndex] = element;
                }
                else if (convertedArray is TOverrideValue[] typedArray)
                {
                    typedArray[arrayIndex] = m_PrefabOverride.m_Value;
                }
                else
                {
                    try
                    {
                        convertedArray[arrayIndex] = (TElement)ChangeType(m_PrefabOverride.m_Value, typeof(TElement));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Could not set list element at {m_PropertyPath} -- Cannot convert {typeof(TOverrideValue)} to {typeof(TElement)}\n{e.Message}");
                    }
                }

                // ReSharper disable once PatternNeverMatches
                // ReSharper disable HeuristicUnreachableCode
                if (convertedArray is TArray finalResult)
                    property.SetValue(ref container, finalResult);
                else
                    Debug.LogError($"Could not convert {convertedArray} to {typeof(TArray)}");
                // ReSharper restore HeuristicUnreachableCode
            }

            void VisitListPrivate<TContainer, TList, TElement>(Property<TContainer, TList> property, ref TContainer container)
            {
                const string arrayToken = ".Array.data[";
                const int arrayTokenLength = 12;
                TList list;
                List<TElement> convertedList;
                var unityObjectReferenceProperty = property as IUnityObjectReferenceProperty<TContainer, TList>;
                if (!m_PropertyPath.Contains(arrayToken))
                {
                    const string arraySizeToken = ".Array.size";
                    if (m_PropertyPath.EndsWith(arraySizeToken) && m_PrefabOverride.m_Value is int arraySize)
                    {
                        list = unityObjectReferenceProperty == null
                            ? property.GetValue(ref container)
                            : unityObjectReferenceProperty.GetValue(ref container, m_SerializationMetadata);

                        convertedList = list as List<TElement> ?? new List<TElement>();
                        var count = convertedList.Count;
                        if (count > arraySize)
                        {
                            var excess = count - arraySize;
                            convertedList.RemoveRange(count - excess, excess);
                        }
                        else
                        {
                            while (convertedList.Count < arraySize)
                            {
                                convertedList.Add(CreateNewDefaultObject<TElement>());
                            }
                        }

                        if (convertedList is TList result)
                        {
                            if (unityObjectReferenceProperty == null)
                                property.SetValue(ref container, result);
                            else
                                unityObjectReferenceProperty.SetValue(ref container, result, m_SerializationMetadata);
                        }
                        else
                        {
                            Debug.LogError($"Could not convert list {convertedList} to {typeof(TList)}");
                        }

                        return;
                    }

                    Debug.LogWarning($"Property path {m_PropertyPath} is invalid for list property");
                    return;
                }

                var startIndex = m_PropertyPath.IndexOf(arrayToken) + arrayTokenLength;
                var endIndex = m_PropertyPath.IndexOf(']');
                var arrayIndexString = m_PropertyPath.Substring(startIndex, endIndex - startIndex);
                if (!int.TryParse(arrayIndexString, out var arrayIndex))
                {
                    Debug.LogWarning($"Could not parse array index from property path {m_PropertyPath}");
                    return;
                }

                list = unityObjectReferenceProperty == null
                    ? property.GetValue(ref container)
                    : unityObjectReferenceProperty.GetValue(ref container, m_SerializationMetadata);

                convertedList = list as List<TElement> ?? new List<TElement>();
                var minSize = arrayIndex + 1;
                while (convertedList.Count < minSize)
                {
                    convertedList.Add(CreateNewDefaultObject<TElement>());
                }

                // Strip out ]. if there is a remainder
                endIndex += 2;
                if (m_PropertyPath.Length > endIndex)
                {
                    var remainder = m_PropertyPath.Substring(endIndex, m_PropertyPath.Length - endIndex);
                    var element = convertedList[arrayIndex];
                    if (element == null)
                    {
                        try
                        {
                            element = CreateNewDefaultObject<TElement>();
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }

                        if (element == null)
                        {
                            Debug.LogWarning($"Could not set property with property path {m_PropertyPath} -- could not activate list element");
                            return;
                        }
                    }

                    m_PrefabOverride.SetProperty(ref element, remainder, m_SerializationMetadata);
                }
                else if (convertedList is List<TOverrideValue> typedList)
                {
                    typedList[arrayIndex] = m_PrefabOverride.m_Value;
                }
                else
                {
                    try
                    {
                        convertedList[arrayIndex] = (TElement)ChangeType(m_PrefabOverride.m_Value, typeof(TElement));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Could not set list element at {m_PropertyPath} -- Cannot convert {typeof(TOverrideValue)} to {typeof(TElement)}\n{e.Message}");
                    }
                }

                if (convertedList is TList finalResult)
                {
                    if (unityObjectReferenceProperty == null)
                        property.SetValue(ref container, finalResult);
                    else
                        unityObjectReferenceProperty.SetValue(ref container, finalResult, m_SerializationMetadata);
                }
                else
                {
                    Debug.LogError($"Could not convert {convertedList} to {typeof(TList)}");
                }
            }

            void SetIntermediatePropertyValue<TContainer, TValue>(Property<TContainer, TValue> property, ref TValue value)
            {
                if (value == null)
                {
                    try
                    {
                        value = (TValue)Activator.CreateInstance(property.DeclaredValueType());
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

                    if (value == null)
                    {
                        Debug.LogWarning($"Could not set property with property path {m_PropertyPath} -- Could not activate value");
                        return;
                    }
                }

                var startIndex = m_PropertyPath.IndexOf('.') + 1;
                var subPath = m_PropertyPath.Substring(startIndex, m_PropertyPath.Length - startIndex);

                // Value will be set on property as part of normal visitation
                m_PrefabOverride.SetProperty(ref value, subPath, m_SerializationMetadata);
            }
        }

        // ReSharper disable once Unity.RedundantSerializeFieldAttribute
        [SerializeField]
        protected TOverrideValue m_Value;

        public TOverrideValue Value { get => m_Value; set => m_Value = value; }

        public RuntimePrefabPropertyOverride() { }

        public RuntimePrefabPropertyOverride(string propertyPath, string transformPath, int componentIndex, TOverrideValue value)
            : base(propertyPath, transformPath, componentIndex)
        {
            m_Value = value;
        }

        protected internal override void ApplyOverrideToTarget(UnityObject target, SerializationMetadata metadata)
        {
#if NET_DOTS || ENABLE_IL2CPP
            var factory = new SetPropertyMethodFactory
            {
                Override = this,
                PropertyPath = PropertyPath,
                Metadata = metadata
            };

            k_Factory.Override = this;
            k_Factory.PropertyPath = PropertyPath;
            var rootContainer = target;
            SerializationUtils.InvokeGenericMethodWrapper(rootContainer, factory);
#else
            var type = target.GetType();
            if (!k_SetPropertyMethods.TryGetValue(type, out var method))
            {
                method = k_SetPropertyMethod.MakeGenericMethod(type);
            }

            k_SetPropertyArguments[0] = target;
            k_SetPropertyArguments[1] = PropertyPath;
            k_SetPropertyArguments[2] = metadata;
            method.Invoke(this, k_SetPropertyArguments);
#endif
        }

        protected void SetProperty<TContainer>(ref TContainer container, string propertyPath, SerializationMetadata metadata)
        {
            // TODO: re-use the same visitor and tokenize path
            var visitor = new SetPropertyVisitor(this, propertyPath, metadata);
            PropertyContainer.Visit(ref container, visitor);
        }

        static Type GetElementType(Type listType)
        {
            Type elementType = null;
            if (listType.IsArray)
            {
                elementType = listType.GetElementType();
            }
            else if (typeof(IList).IsAssignableFrom(listType))
            {
                elementType = listType.GetGenericArguments()[0];
            }

            if (elementType != null && typeof(UnityObjectReference).IsAssignableFrom(elementType))
                elementType = null;

            return elementType;
        }

        static TCreate CreateNewDefaultObject<TCreate>()
        {
            if (typeof(TCreate) == typeof(UnityObjectReference))
                return (TCreate)(object)UnityObjectReference.NullObjectReference;

            var newObject = Activator.CreateInstance(typeof(TCreate));
            if (newObject == null)
            {
                Debug.LogWarning($"Could not activate {typeof(TCreate)}");
                return default;
            }

            var typedNewObject = (TCreate)newObject;

            // Clear out default values to match behavior in Editor
            if (!RuntimeTypeInfoCache.IsContainerType(typeof(TCreate)))
                return typedNewObject;

            var propertyBag = PropertyBagStore.GetPropertyBag(typeof(TCreate));
            if (propertyBag is IPropertyList<TCreate> typedPropertyList)
            {
                foreach (var prop in typedPropertyList.GetProperties(ref typedNewObject))
                {
                    var valueType = prop.DeclaredValueType();
                    if (valueType.IsValueType)
                        prop.TrySetValue(ref typedNewObject, Activator.CreateInstance(valueType));
                    else
                        prop.TrySetValue(ref typedNewObject, null);
                }
            }
            else
            {
                Debug.LogError($"Could not get property bag as IPropertyList for {typeof(TCreate)}");
            }

            return typedNewObject;
        }

        public static object ChangeType(object value, Type conversionType)
        {
            if (value == null)
                return null;

            if (!conversionType.IsValueType)
                return value;

            if (conversionType.IsEnum)
            {
                // TODO: Enum conversion without string
                return Enum.Parse(conversionType, value.ToString());
            }

            if (conversionType == typeof(char))
            {
                var stringValue = (string)value;
                return string.IsNullOrEmpty(stringValue) ? null : (object)stringValue[0];
            }

            return Convert.ChangeType(value, conversionType);
        }

        static string GetAlternateName(string name)
        {
            if (name.StartsWith("m_"))
            {
                var end = name.Substring(3, name.Length - 3);
                var firstLetter = name.Substring(2, 1);
                return $"{firstLetter.ToLower()}{end}";
            }

            {
                var end = name.Substring(1, name.Length - 1);
                var firstLetter = name.Substring(0, 1);
                return $"m_{firstLetter.ToUpper()}{end}";
            }
        }
    }
}
