﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Properties;
using Unity.RuntimeSceneSerialization.Prefabs;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal
{
    /// <summary>
    /// Utility methods for Runtime Scene Serialization
    /// </summary>
    static class SerializationUtils
    {
#if !NET_DOTS && !ENABLE_IL2CPP
        static readonly MethodInfo k_RegisterPropertyBagsForPropertiesMethod = typeof(SerializationUtils).GetMethod(nameof(RegisterPropertyBagsForProperties), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly Dictionary<Type, MethodInfo> k_RegisterPropertyBagsMethods = new();
#endif

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Component> k_Components = new();

        internal static readonly HashSet<string> IgnoredTypes = new()
        {
            typeof(AnimationCurve).FullName,
            typeof(Keyframe).FullName,
            typeof(Vector2Int).FullName,
            typeof(Vector3Int).FullName,
            typeof(Rect).FullName,
            typeof(RectInt).FullName,
            typeof(BoundsInt).FullName,
            typeof(Version).FullName,
            typeof(SerializedScene).FullName
        };

        internal static void SortComponentList(List<Component> components, List<(Component, bool)> sortedComponents)
        {
            // Remove PrefabMetadata because it is runtime-only
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;

                if (component.GetType() == typeof(PrefabMetadata))
                    components.Remove(component);
            }

            //TODO: handle chained dependencies
            sortedComponents.Clear();
            foreach (var component in components)
            {
                if (!component) // Check for missing scripts
                    continue;

                var pair = (component, false);
                var customAttributes = component.GetType().GetCustomAttributes(typeof(RequireComponent), true);
                if (customAttributes.Length > 0)
                {
                    var requireAttribute = (RequireComponent)customAttributes[0];
                    if (requireAttribute.m_Type0 != typeof(Transform) || requireAttribute.m_Type1 != null && requireAttribute.m_Type1 != typeof(Transform))
                    {
                        pair = (component, true);
                    }
                }

                sortedComponents.Add(pair);
            }

            sortedComponents.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        }

        internal static UnityObject GetTargetObjectWithComponentIndex(this GameObject gameObject, int index)
        {
            if (index < 0)
                return gameObject;

            gameObject.GetComponents(k_Components);
            if (index >= k_Components.Count)
            {
                Debug.LogError($"Component at {index} not found on {gameObject}");
                return null;
            }

            return k_Components[index];
        }

        internal static void GetTransformPathAndComponentIndex(Transform root, UnityObject target,
            out string transformPath, out int componentIndex)
        {
            switch (target)
            {
                case GameObject targetGameObject:
                    transformPath = root.GetTransformPath(targetGameObject.transform);
                    componentIndex = -1;
                    return;
                case Component targetComponent:
                {
                    transformPath = root.GetTransformPath(targetComponent.transform);
                    targetComponent.GetComponents(k_Components);
                    var index = k_Components.IndexOf(targetComponent);
                    if (index < 0)
                    {
                        Debug.LogError($"Could not get find {targetComponent} on {targetComponent.gameObject}");
                        componentIndex = -1;
                        return;
                    }

                    componentIndex = index;
                    return;
                }
            }

            Debug.LogError($"Could not get transform path and component index for object {target}");
            transformPath = string.Empty;
            componentIndex = -1;
        }

#if NET_DOTS || ENABLE_IL2CPP
        /// <summary>
        /// Used by InvokeGenericMethodWrapper to define the method by which generic method delegates are provided
        /// </summary>
        internal interface IGenericMethodFactory
        {
            /// <summary>
            /// Get a method which returns void and has a single argument of type T
            /// </summary>
            /// <typeparam name="T">The specific </typeparam>
            /// <returns>The method, which will be invoked by InvokeGenericMethodWrapper</returns>
            Action<T> GetGenericMethod<T>() where T : UnityObject;
        }

        /// <summary>
        /// Invoke a generic method with a UnityObject as an argument based on its specific type
        /// This is currently implemented for the GameObject type. All Component types in loaded assemblies will be
        /// implemented by appending code via the PrefabOverrideAssemblyPostProcessor
        /// </summary>
        /// <param name="argument">The UnityObject which will be passed as an argument, and whose type will be used to
        /// determine the specific generic implementation to use</param>
        /// <param name="methodFactory">A factory object which can provide a generic method delegate on demand</param>
        internal static void InvokeGenericMethodWrapper(UnityObject argument, IGenericMethodFactory methodFactory)
        {
            if (argument is GameObject gameObject)
                CallGenericMethod(gameObject, methodFactory);
        }

        internal static void CallGenericMethod<T>(T obj, IGenericMethodFactory methodFactory) where T : UnityObject
        {
            var method = methodFactory.GetGenericMethod<T>();
            method.Invoke(obj);
        }
#else
        internal static MethodInfo GetRegisterPropertyBagsForPropertiesMethod(Type type)
        {
            if (k_RegisterPropertyBagsMethods.TryGetValue(type, out var method))
                return method;

            method = k_RegisterPropertyBagsForPropertiesMethod.MakeGenericMethod(type);
            k_RegisterPropertyBagsMethods[type] = method;
            return method;
        }

        internal static void RegisterPropertyBagsForProperties<TContainer>(IPropertyBag propertyBag)
        {
            if (!(propertyBag is PropertyBag<TContainer> typedPropertyBag))
                return;

            PropertyBag.Register(typedPropertyBag);
            var containerType = typeof(TContainer);
            if (containerType.IsArray)
            {
                SceneSerialization.RegisterPropertyBagRecursively(containerType.GetElementType());
                return;
            }

            if (ReflectedPropertyBagUtils.IsListType(containerType))
            {
                SceneSerialization.RegisterPropertyBagRecursively(containerType.GenericTypeArguments[0]);
                return;
            }

            var container = default(TContainer);
            foreach (var property in typedPropertyBag.GetProperties(ref container))
            {
                SceneSerialization.RegisterPropertyBagRecursively(property.DeclaredValueType());
            }
        }
#endif
    }
}
