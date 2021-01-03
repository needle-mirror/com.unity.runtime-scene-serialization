using System;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Provides access to unity object properties
    /// </summary>
    /// <typeparam name="TContainer"></typeparam>
    public interface IUnityObjectReferenceValueProperty<TContainer>
    {
        /// <summary>
        /// Get the value directly without going through UnityObjectReference
        /// </summary>
        /// <param name="container">The container object on from which the property will be accessed</param>
        /// <returns>The property value</returns>
        UnityObject GetMemberValue(ref TContainer container);

        /// <summary>
        /// Set the value directly without going through UnityObjectReference
        /// </summary>
        /// <param name="container">The container object on which the property will be set</param>
        /// <param name="value">The value to set</param>
        void SetMemberValue(ref TContainer container, UnityObject value);

        /// <summary>
        /// The original type of UnityObject, which is hidden by UnityObjectReference
        /// </summary>
        Type OriginalValueType { get; }
    }
}
