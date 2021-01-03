using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Internal
{
    interface IUnityObjectReferenceProperty<TContainer, TValue>
    {
        TValue GetValue(ref TContainer container, SerializationMetadata metadata);
        void SetValue(ref TContainer container, TValue value, SerializationMetadata metadata);
    }

    struct UnityObjectReference
    {
        class UnityObjectReferencePropertyBag : ContainerPropertyBag<UnityObjectReference>
        {
            static readonly DelegateProperty<UnityObjectReference, int> k_SceneID = new DelegateProperty<UnityObjectReference, int>(
                "sceneID",
                (ref UnityObjectReference container) => container.sceneID,
                (ref UnityObjectReference container, int value) => { container.sceneID = value; }
            );

            static readonly DelegateProperty<UnityObjectReference, string> k_Guid = new DelegateProperty<UnityObjectReference, string>(
                "guid",
                (ref UnityObjectReference container) => container.guid,
                (ref UnityObjectReference container, string value) => { container.guid = value; }
            );

            static readonly DelegateProperty<UnityObjectReference, long> k_FileId = new DelegateProperty<UnityObjectReference, long>(
                "fileId",
                (ref UnityObjectReference container) => container.fileId,
                (ref UnityObjectReference container, long value) => container.fileId = value
            );

            public UnityObjectReferencePropertyBag()
            {
                AddProperty(k_SceneID);
                AddProperty(k_Guid);
                AddProperty(k_FileId);
            }
        }

        internal static readonly MethodInfo CreateValueMethod = typeof(UnityObjectReference).GetMethod(nameof(CreateValueProperty), BindingFlags.Static | BindingFlags.Public);
        internal static readonly MethodInfo CreateArrayMethod = typeof(UnityObjectReference).GetMethod(nameof(CreateArrayProperty), BindingFlags.Static | BindingFlags.Public);
        internal static readonly MethodInfo CreateListMethod = typeof(UnityObjectReference).GetMethod(nameof(CreateListProperty), BindingFlags.Static | BindingFlags.Public);

        public static readonly UnityObjectReference NullObjectReference = new UnityObjectReference { sceneID = SerializationMetadata.InvalidID };

        public int sceneID;
        public string guid;
        public long fileId;

        static UnityObjectReference() { PropertyBag.Register(new UnityObjectReferencePropertyBag()); }

        public override string ToString() => $"sceneID: {sceneID}, guid: {guid}, fileId: {fileId}";

        public static UnityObjectReference GetReferenceForObject(UnityObject obj, SerializationMetadata metadata)
        {
            if (obj)
            {
                if (metadata == null)
                    return NullObjectReference;

                var sceneID = metadata.GetSceneID(obj);
                if (sceneID != SerializationMetadata.InvalidID)
                    return new UnityObjectReference { sceneID = sceneID };

                string guid;
                long fileId;
                metadata.GetAssetMetadata(obj, out guid, out fileId);

                if (string.IsNullOrEmpty(guid))
                {
                    // Check if target object is marked as "DontSave"--that means it is a scene object but won't be found in metadata
                    if ((obj.hideFlags & HideFlags.DontSave) != HideFlags.None)
                        return NullObjectReference;

                    // Suppress warning if scene object metadata is not setup (i.e. during deserialization)
                    if (metadata.SceneObjectsSetup)
                        Debug.LogWarningFormat("Could not find GUID for {0}", obj);

                    return NullObjectReference;
                }

                if (fileId < 0)
                {
                    Debug.LogWarningFormat("Could not find sub-asset for {0} at fileId {1}", obj, fileId);
                    return NullObjectReference;
                }

                return new UnityObjectReference { guid = guid, fileId = fileId };
            }

            return NullObjectReference;
        }

#if UNITY_EDITOR
        static UnityObject GetAsset(string guid, long fileId)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null)
            {
                Debug.LogWarningFormat("Could not load asset with guid {0} at path {1}", guid, path);
                return null;
            }

            foreach (var asset in assets)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long assetFileId))
                {
                    if (assetFileId == fileId)
                        return asset;
                }
            }

            Debug.LogWarningFormat("FileId {0} is invalid for asset {1} at path {2}", fileId, guid, path);
            return null;
        }
#endif

        public static UnityObject GetAsset(string guid, long fileId, AssetPack assetPack)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var asset = GetAsset(guid, fileId);
                if (asset != null)
                    return asset;

                Debug.LogWarning("Cannot find a local asset with " + guid + ". Falling back to asset pack.");
            }
#endif

            if (assetPack == null)
            {
                Debug.LogWarning("Cannot import asset with guid: " + guid + " and no asset pack");
                return null;
            }

            return assetPack.GetAsset(guid, fileId);
        }

        public static UnityObjectReferenceValueProperty<TContainer> CreateValueProperty<TContainer>(string memberName,
            bool isProperty, string containerName)
        {
            Type externalContainerType = null;
            if (!string.IsNullOrEmpty(containerName))
                externalContainerType = Type.GetType(containerName);

            var memberInfo = GetMemberInfo(externalContainerType ?? typeof(TContainer), memberName, isProperty);
            return new UnityObjectReferenceValueProperty<TContainer>(memberInfo, externalContainerType);
        }

        public static UnityObjectReferenceArrayProperty<TContainer, TElement>
            CreateArrayProperty<TContainer, TElement>(string memberName, bool isProperty, string containerName)
            where TElement : UnityObject
        {
            Type externalContainerType = null;
            if (!string.IsNullOrEmpty(containerName))
                externalContainerType = Type.GetType(containerName);

            var memberInfo = GetMemberInfo(externalContainerType ?? typeof(TContainer), memberName, isProperty);
            return new UnityObjectReferenceArrayProperty<TContainer, TElement>(memberInfo, externalContainerType);
        }

        public static UnityObjectReferenceListProperty<TContainer, TElement>
            CreateListProperty<TContainer, TElement>(string memberName, bool isProperty, string containerName)
            where TElement : UnityObject
        {
            Type externalContainerType = null;
            if (!string.IsNullOrEmpty(containerName))
                externalContainerType = Type.GetType(containerName);

            var memberInfo = GetMemberInfo(externalContainerType ?? typeof(TContainer), memberName, isProperty);
            return new UnityObjectReferenceListProperty<TContainer, TElement>(memberInfo, externalContainerType);
        }

        static IMemberInfo GetMemberInfo(Type containerType, string memberName, bool isProperty)
        {
            IMemberInfo memberInfo;
            if (isProperty)
            {
                PropertyInfo propertyInfo = null;
                while (containerType != null)
                {
                    propertyInfo = containerType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (propertyInfo != null)
                        break;

                    containerType = containerType.BaseType;
                }

                if (propertyInfo == null)
                    throw new ArgumentException($"No member with name {memberName} found on {containerType}");

                memberInfo = new Unity.Properties.PropertyMember(propertyInfo);
            }
            else
            {
                FieldInfo fieldInfo = null;
                while (containerType != null)
                {
                    fieldInfo = containerType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fieldInfo != null)
                        break;

                    containerType = containerType.BaseType;
                }

                if (fieldInfo == null)
                    throw new ArgumentException($"No member with name {memberName} found on {containerType}");

                memberInfo = new Unity.Properties.FieldMember(fieldInfo);
            }

            return memberInfo;
        }

        public static UnityObject GetObject(UnityObjectReference objectReference, SerializationMetadata metadata)
        {
            var guid = objectReference.guid;
            if (!string.IsNullOrEmpty(guid))
                return GetAsset(guid, objectReference.fileId, metadata?.AssetPack);

            var sceneId = objectReference.sceneID;
            return sceneId == SerializationMetadata.InvalidID ? null : metadata?.GetSceneObject(sceneId);
        }
    }

    class UnityObjectReferenceListProperty<TContainer, TElement> : Property<TContainer, List<UnityObjectReference>>,
        IUnityObjectReferenceProperty<TContainer, List<UnityObjectReference>>
    {
        static readonly FieldInfo k_ExternalContainerField = typeof(TContainer).GetField(ReflectedMemberPropertyName.ContainerFieldName);

        public override string Name => m_MemberInfo.Name;
        public override bool IsReadOnly => false;

        readonly IMemberInfo m_MemberInfo;
        readonly Type m_ExternalContainerType;

        internal UnityObjectReferenceListProperty(IMemberInfo memberInfo, Type externalContainerType)
        {
            m_MemberInfo = memberInfo;
            m_ExternalContainerType = externalContainerType;
        }

        public override List<UnityObjectReference> GetValue(ref TContainer container) { return GetValue(ref container, null); }
        public override void SetValue(ref TContainer container, List<UnityObjectReference> value) { SetValue(ref container, value, null); }

        public List<UnityObjectReference> GetValue(ref TContainer container, SerializationMetadata metadata)
        {
            if (m_ExternalContainerType != null)
                return GetValueInternal(GetContainerValue(ref container), metadata);

            return GetValueInternal(container, metadata);
        }

        object GetContainerValue(ref TContainer container) { return k_ExternalContainerField.GetValue(container) ?? Activator.CreateInstance(m_ExternalContainerType); }

        List<UnityObjectReference> GetValueInternal(object container, SerializationMetadata metadata)
        {
            var value = m_MemberInfo.GetValue(container);
            if (value == null)
                return null;

            // Use typeless IList cast to support reflected private element types
            if (!(value is IList list))
                return null;

            var result = new List<UnityObjectReference>(list.Count);
            foreach (var element in list)
            {
                if (!(element is UnityObject unityObject))
                {
                    result.Add(UnityObjectReference.NullObjectReference);
                    continue;
                }

                result.Add(UnityObjectReference.GetReferenceForObject(unityObject, metadata));
            }

            return result;
        }

        public void SetValue(ref TContainer container, List<UnityObjectReference> value, SerializationMetadata metadata)
        {
            if (m_ExternalContainerType != null)
            {
                var containerValue = GetContainerValue(ref container);
                SetValueInternal(containerValue, value, metadata);
                k_ExternalContainerField.SetValue(container, containerValue);
                return;
            }

            SetValueInternal(container, value, metadata);
        }

        void SetValueInternal(object container, List<UnityObjectReference> referenceList, SerializationMetadata metadata)
        {
            var valueType = m_MemberInfo.ValueType;
            if (valueType.GetElementType() == typeof(TElement))
            {
                SetValueTyped(referenceList, m_MemberInfo, container, metadata);
            }
            else
            {
                SetValueTypeless(referenceList, m_MemberInfo, container, valueType, metadata);
            }
        }

        static void SetValueTyped(List<UnityObjectReference> referenceList, IMemberInfo memberInfoClosure,
            object container, SerializationMetadata metadata)
        {
            void SetValue()
            {
                var count = referenceList.Count;
                var list = new List<TElement>(count);

                void Add(UnityObject unityObject)
                {
                    if (!(unityObject is TElement element))
                    {
                        list.Add(default);
                        return;
                    }

                    list.Add(element);
                }

                for (var i = 0; i < count; i++)
                {
                    Add(UnityObjectReference.GetObject(referenceList[i], metadata));
                }

                memberInfoClosure.SetValue(container, list);
            }

            if (metadata != null)
                metadata.SetSceneReference(SetValue);
            else
                SetValue();
        }

        static void SetValueTypeless(List<UnityObjectReference> referenceList, IMemberInfo memberInfoClosure,
            object container, Type valueType, SerializationMetadata metadata)
        {
            void SetValue()
            {
                var count = referenceList.Count;
                var list = (IList)Activator.CreateInstance(valueType);
                for (var i = 0; i < count; i++)
                {
                    list.Add(UnityObjectReference.GetObject(referenceList[i], metadata));
                }

                memberInfoClosure.SetValue(container, list);
            }

            if (metadata != null)
                metadata.SetSceneReference(SetValue);
            else
                SetValue();
        }
    }

    class UnityObjectReferenceArrayProperty<TContainer, TElement> : Property<TContainer, List<UnityObjectReference>>,
        IUnityObjectReferenceProperty<TContainer, List<UnityObjectReference>>
    {
        static readonly FieldInfo k_ExternalContainerField = typeof(TContainer).GetField(ReflectedMemberPropertyName.ContainerFieldName);

        public override string Name => m_MemberInfo.Name;
        public override bool IsReadOnly => false;

        readonly IMemberInfo m_MemberInfo;
        readonly Type m_ExternalContainerType;

        internal UnityObjectReferenceArrayProperty(IMemberInfo memberInfo, Type externalContainerType)
        {
            m_MemberInfo = memberInfo;
            m_ExternalContainerType = externalContainerType;
        }

        public override List<UnityObjectReference> GetValue(ref TContainer container) { return GetValue(ref container, null); }
        public override void SetValue(ref TContainer container, List<UnityObjectReference> value) { SetValue(ref container, value, null); }

        public List<UnityObjectReference> GetValue(ref TContainer container, SerializationMetadata metadata)
        {
            if (m_ExternalContainerType != null)
                return GetValueInternal(GetContainerValue(ref container), metadata);

            return GetValueInternal(container, metadata);
        }

        object GetContainerValue(ref TContainer container) { return k_ExternalContainerField.GetValue(container) ?? Activator.CreateInstance(m_ExternalContainerType); }

        List<UnityObjectReference> GetValueInternal(object container, SerializationMetadata metadata)
        {
            var value = m_MemberInfo.GetValue(container);
            if (value == null)
                return null;

            // Use typeless Array cast to support reflected private element types
            if (!(value is Array array))
                return null;

            var result = new List<UnityObjectReference>(array.Length);
            foreach (var element in array)
            {
                if (!(element is UnityObject unityObject))
                {
                    result.Add(UnityObjectReference.NullObjectReference);
                    continue;
                }

                result.Add(UnityObjectReference.GetReferenceForObject(unityObject, metadata));
            }

            return result;
        }

        public void SetValue(ref TContainer container, List<UnityObjectReference> list, SerializationMetadata metadata)
        {
            if (m_ExternalContainerType != null)
            {
                var containerValue = GetContainerValue(ref container);
                SetValueInternal(containerValue, list, metadata);
                k_ExternalContainerField.SetValue(container, containerValue);
                return;
            }

            SetValueInternal(container, list, metadata);
        }

        void SetValueInternal(object container, List<UnityObjectReference> list, SerializationMetadata metadata)
        {
            var elementType = m_MemberInfo.ValueType.GetElementType();
            if (elementType == typeof(TElement))
            {
                SetValueTyped(list, container, metadata);
            }
            else
            {
                SetValueTypeless(list, container, elementType, metadata);
            }
        }

        void SetValueTypeless(List<UnityObjectReference> list, object container, Type elementType, SerializationMetadata metadata)
        {
            void SetValueAction()
            {
                var count = list.Count;
                var array = Array.CreateInstance(elementType, count);
                for (var i = 0; i < count; i++)
                {
                    array.SetValue(UnityObjectReference.GetObject(list[i], metadata), i);
                }

                m_MemberInfo.SetValue(container, array);
            }

            if (metadata != null)
                metadata.SetSceneReference(SetValueAction);
            else
                SetValueAction();
        }

        void SetValueTyped(List<UnityObjectReference> list, object container, SerializationMetadata metadata)
        {
            void SetValueAction()
            {
                var count = list.Count;
                var array = new TElement[count];

                void SetArrayValue(UnityObject unityObject, int i)
                {
                    if (!(unityObject is TElement element))
                        return;

                    array[i] = element;
                }

                for (var i = 0; i < count; i++)
                {
                    SetArrayValue(UnityObjectReference.GetObject(list[i], metadata), i);
                }

                m_MemberInfo.SetValue(container, array);
            }

            if (metadata != null)
                metadata.SetSceneReference(SetValueAction);
            else
                SetValueAction();
        }
    }

    class UnityObjectReferenceValueProperty<TContainer> : Property<TContainer, UnityObjectReference>,
        IUnityObjectReferenceProperty<TContainer, UnityObjectReference>, IUnityObjectReferenceValueProperty<TContainer>
    {
        static readonly FieldInfo k_ExternalContainerField = typeof(TContainer).GetField(ReflectedMemberPropertyName.ContainerFieldName);

        public override string Name => m_MemberInfo.Name;
        public override bool IsReadOnly => m_MemberInfo.IsReadOnly;

        readonly IMemberInfo m_MemberInfo;
        readonly Type m_ExternalContainerType;

        public Type OriginalValueType => m_MemberInfo.ValueType;

        internal UnityObjectReferenceValueProperty(IMemberInfo memberInfo, Type externalContainerType)
        {
            m_MemberInfo = memberInfo;
            m_ExternalContainerType = externalContainerType;
        }

        public override UnityObjectReference GetValue(ref TContainer container) { return GetValue(ref container, null); }
        public override void SetValue(ref TContainer container, UnityObjectReference value) { SetValue(ref container, value, null); }

        public UnityObjectReference GetValue(ref TContainer container, SerializationMetadata metadata)
        {
            if (m_ExternalContainerType != null)
                return GetValueInternal(GetExternalContainerValue(ref container), metadata);

            return GetValueInternal(container, metadata);
        }

        object GetExternalContainerValue(ref TContainer container) { return k_ExternalContainerField.GetValue(container) ?? Activator.CreateInstance(m_ExternalContainerType); }

        UnityObjectReference GetValueInternal(object container, SerializationMetadata metadata)
        {
            var obj = (UnityObject)m_MemberInfo.GetValue(container);
            return UnityObjectReference.GetReferenceForObject(obj, metadata);
        }

        public void SetValue(ref TContainer container, UnityObjectReference value, SerializationMetadata metadata)
        {
            if (m_ExternalContainerType != null)
            {
                var containerValue = GetExternalContainerValue(ref container);
                SetValueInternal(containerValue, value, metadata);
                k_ExternalContainerField.SetValue(container, containerValue);
                return;
            }

            SetValueInternal(container, value, metadata);
        }

        void SetValueInternal(object container, UnityObjectReference value, SerializationMetadata metadata)
        {
            var guid = value.guid;
            if (!string.IsNullOrEmpty(guid))
            {
                var assetPack = metadata?.AssetPack;
                m_MemberInfo.SetValue(container, UnityObjectReference.GetAsset(guid, value.fileId, assetPack));
                return;
            }

            var sceneId = value.sceneID;
            if (sceneId == SerializationMetadata.InvalidID)
            {
                m_MemberInfo.SetValue(container, null);
                return;
            }

            var containerClosure = container;
            var memberInfoClosure = m_MemberInfo;
            metadata?.EnqueuePostSerializationAction(() =>
            {
                try
                {
                    memberInfoClosure.SetValue(containerClosure, metadata.GetSceneObject(sceneId));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        public void SetMemberValue(ref TContainer container, UnityObject value)
        {
            m_MemberInfo.SetValue(container, value);
        }

        public UnityObject GetMemberValue(ref TContainer container)
        {
            return (UnityObject)m_MemberInfo.GetValue(container);
        }
    }
}
