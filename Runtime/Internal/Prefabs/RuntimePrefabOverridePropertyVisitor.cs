using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal.Prefabs
{
    abstract class RuntimePrefabOverridePropertyVisitor
    {
        // We need to keep a quaternion around for rotation overrides, because getting and setting normalizes the quaternion
        protected static Quaternion s_TemporaryQuaternion;
        internal static bool TemporaryQuaternionIsDirty { get; set; }
    }

    class RuntimePrefabOverridePropertyVisitor<TOverrideValue> : RuntimePrefabOverridePropertyVisitor,
        IPropertyBagVisitor, IPropertyVisitor, IListPropertyVisitor
    {
        internal class DefaultValueOverrideVisitor : PropertyVisitor
        {
            protected override void VisitProperty<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value)
            {
                var valueType = typeof(TValue);
                if (typeof(UnityObject).IsAssignableFrom(valueType))
                    return;

                value ??= CreateNewDefaultObject<TValue>();
                PropertyContainer.Accept(this, ref value);

                if (!TypeTraits<TValue>.IsContainer)
                    value = default;
            }
        }

        readonly RuntimePrefabPropertyOverride<TOverrideValue> m_PrefabOverride;
        readonly string m_PropertyPath;
        readonly string m_FirstProperty;

        public RuntimePrefabOverridePropertyVisitor(RuntimePrefabPropertyOverride<TOverrideValue> prefabOverride, string propertyPath)
        {
            m_PrefabOverride = prefabOverride;
            m_PropertyPath = propertyPath;
            m_FirstProperty = propertyPath.Split('.')[0];
        }

        /// <inheritdoc/>
        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
        {
            foreach (var property in properties.GetProperties(ref container))
            {
                property.Accept(this, ref container);
            }
        }

        /// <inheritdoc/>
        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var value = property.GetValue(ref container);
            if (PropertyBag.TryGetPropertyBagForValue(ref value, out var valuePropertyBag))
            {
                switch (valuePropertyBag)
                {
                    // ReSharper disable SuspiciousTypeConversion.Global
                    case IListPropertyAccept<TValue> accept:
                        accept.Accept(this, property, ref container, ref value);
                        return;

                    // ReSharper restore SuspiciousTypeConversion.Global
                }
            }

            VisitProperty(property, ref value);

            if (!property.IsReadOnly)
            {
                property.SetValue(ref container, value);
            }
        }

        void VisitProperty<TValue>(IProperty property, ref TValue value)
        {
            if (!IsFirstProperty(property.Name))
                return;

            if (m_PropertyPath.Contains("."))
            {
                if (value is Quaternion quaternion)
                {
                    if (TemporaryQuaternionIsDirty)
                    {
                        s_TemporaryQuaternion = quaternion;
                        TemporaryQuaternionIsDirty = false;
                    }

                    SetIntermediatePropertyValue(property, ref s_TemporaryQuaternion);
                    value = (TValue)(object)s_TemporaryQuaternion;
                    return;
                }

                SetIntermediatePropertyValue(property, ref value);
                return;
            }

            var overrideValue = m_PrefabOverride.Value;
            if (overrideValue == null)
            {
                value = default;
                return;
            }

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
            value = (TConverted)Convert.ChangeType(m_PrefabOverride.Value, typeof(TConverted));
        }

        void SetConvertedProperty<TConverted>(Type conversionType, out TConverted value)
        {
            value = (TConverted)ChangeType(m_PrefabOverride.Value, conversionType);
        }

        void IListPropertyVisitor.Visit<TContainer, TList, TElement>(Property<TContainer, TList> property,
            ref TContainer container, ref TList list)
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

        void VisitArray<TContainer, TArray, TElement>(Property<TContainer, TArray> property, ref TContainer container)
        {
            const string arrayToken = ".Array.data[";
            const int arrayTokenLength = 12;
            TArray array;
            TElement[] convertedArray;
            if (!m_PropertyPath.Contains(arrayToken))
            {
                const string arraySizeToken = ".Array.size";
                if (m_PropertyPath.EndsWith(arraySizeToken) && m_PrefabOverride.Value is int arraySize)
                {
                    array = property.GetValue(ref container);
                    convertedArray = array as TElement[];
                    if (convertedArray == null)
                    {
                        convertedArray = new TElement[arraySize];
                        if (!typeof(UnityObject).IsAssignableFrom(typeof(TElement)))
                        {
                            for (var i = 0; i < arraySize; i++)
                            {
                                convertedArray[i] = CreateNewDefaultObject<TElement>();
                            }
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
                                if (!typeof(UnityObject).IsAssignableFrom(typeof(TElement)))
                                {
                                    for (var i = length; i < arraySize; i++)
                                    {
                                        convertedArray[i] = CreateNewDefaultObject<TElement>();
                                    }
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
            array = property.GetValue(ref container);
            if (array == null)
            {
                convertedArray = new TElement[minSize];
                if (!typeof(UnityObject).IsAssignableFrom(typeof(TElement)))
                {
                    for (var i = 0; i < minSize; i++)
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
                    if (!typeof(UnityObject).IsAssignableFrom(typeof(TElement)))
                    {
                        for (var i = length; i < minSize; i++)
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

                m_PrefabOverride.SetProperty(ref element, remainder);
                convertedArray[arrayIndex] = element;
            }
            else if (convertedArray is TOverrideValue[] typedArray)
            {
                typedArray[arrayIndex] = m_PrefabOverride.Value;
            }
            else
            {
                try
                {
                    convertedArray[arrayIndex] = (TElement)ChangeType(m_PrefabOverride.Value, typeof(TElement));
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
            var value = m_PrefabOverride.Value;
            if (!m_PropertyPath.Contains(arrayToken))
            {
                const string arraySizeToken = ".Array.size";
                if (m_PropertyPath.EndsWith(arraySizeToken) && value is int arraySize)
                {
                    list = property.GetValue(ref container);
                    convertedList = list as List<TElement> ?? new List<TElement>();
                    var count = convertedList.Count;
                    if (count > arraySize)
                    {
                        var excess = count - arraySize;
                        convertedList.RemoveRange(count - excess, excess);
                    }
                    else
                    {
                        if (typeof(UnityObject).IsAssignableFrom(typeof(TElement)))
                        {
                            while (convertedList.Count < arraySize)
                            {
                                convertedList.Add(default);
                            }
                        }
                        else
                        {
                            while (convertedList.Count < arraySize)
                            {
                                convertedList.Add(CreateNewDefaultObject<TElement>());
                            }
                        }
                    }

                    if (convertedList is TList result)
                    {
                        property.SetValue(ref container, result);
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

            list = property.GetValue(ref container);
            convertedList = list as List<TElement> ?? new List<TElement>();
            var minSize = arrayIndex + 1;
            if (typeof(UnityObject).IsAssignableFrom(typeof(TElement)))
            {
                while (convertedList.Count < minSize)
                {
                    convertedList.Add(default);
                }
            }
            else
            {
                while (convertedList.Count < minSize)
                {
                    convertedList.Add(CreateNewDefaultObject<TElement>());
                }
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

                m_PrefabOverride.SetProperty(ref element, remainder);
            }
            else if (convertedList is List<TOverrideValue> typedList)
            {
                typedList[arrayIndex] = value;
            }
            else
            {
                try
                {
                    if (value == null)
                        convertedList[arrayIndex] = default;
                    else
                        convertedList[arrayIndex] = (TElement)ChangeType(value, typeof(TElement));
                }
                catch (Exception e)
                {
                    Debug.LogError($"Could not set list element at {m_PropertyPath} -- Cannot convert {typeof(TOverrideValue)} to {typeof(TElement)}\n{e.Message}");
                }
            }

            if (convertedList is TList finalResult)
            {
                property.SetValue(ref container, finalResult);
            }
            else
            {
                Debug.LogError($"Could not convert {convertedList} to {typeof(TList)}");
            }
        }

        void SetIntermediatePropertyValue<TValue>(IProperty property, ref TValue value)
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
            m_PrefabOverride.SetProperty(ref value, subPath);
        }

        static TCreate CreateNewDefaultObject<TCreate>()
        {
            var newObjectType = typeof(TCreate);
            if (newObjectType == typeof(string))
                return (TCreate)(object)string.Empty;

            if (newObjectType.IsArray)
            {
                var elementType = newObjectType.GetElementType();
                if (elementType == null)
                    return default;

                return (TCreate)(object)Array.CreateInstance(elementType, 0);
            }

            var newObject = Activator.CreateInstance(newObjectType);
            if (newObject == null)
            {
                Debug.LogWarning($"Could not activate {newObjectType}");
                return default;
            }

            var typedNewObject = (TCreate)newObject;

            if (!TypeTraits.IsContainer(newObjectType))
                return typedNewObject;

            // Clear out default values to match behavior in Editor
            var visitor = new DefaultValueOverrideVisitor();
            PropertyContainer.Accept(visitor, ref typedNewObject);
            return typedNewObject;
        }

        static object ChangeType(object value, Type conversionType)
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

                // Cast stringValue to avoid boxing in the case of null result
                // ReSharper disable once RedundantCast
                return string.IsNullOrEmpty(stringValue) ? null : (object)stringValue[0];
            }

            return Convert.ChangeType(value, conversionType);
        }
    }
}
