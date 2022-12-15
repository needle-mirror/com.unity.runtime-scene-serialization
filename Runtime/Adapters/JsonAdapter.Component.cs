using System;
using Unity.Serialization.Json;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    partial class JsonAdapter : IContravariantJsonAdapter<Component>
    {
        void IContravariantJsonAdapter<Component>.Serialize(IJsonSerializationContext context, Component value)
        {
            if (m_SerializeAsReference)
            {
                WriteUnityObjectReference(context, value);
                return;
            }

            if (value == null)
            {
                context.ContinueVisitationWithoutAdapters();
                return;
            }

            // Only serialize top-level components
            using (new SerializeAsReferenceScope(this))
            {
#if !NET_DOTS && !ENABLE_IL2CPP
                SceneSerialization.RegisterPropertyBagRecursively(value.GetType());
#endif

                if (value is ISerializationCallbackReceiver receiver)
                {
                    try
                    {
                        receiver.OnBeforeSerialize();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                var parameters = m_Parameters;
                parameters.DisableRootAdapters = true;
                JsonSerialization.ToJson(context.Writer, value, parameters);
            }
        }

        object IContravariantJsonAdapter<Component>.Deserialize(IJsonDeserializationContext context)
        {
            if (m_SerializeAsReference)
            {
                if (!m_FirstPassCompleted)
                    return null;

                return (Component)ReadUnityObjectReference(context);
            }

            var componentView = context.SerializedValue.AsObjectView();
            var assemblyQualifiedTypeName = componentView[k_TypePropertyName].AsStringView().ToString();
            var type = Type.GetType(assemblyQualifiedTypeName);

#if !NET_DOTS && !ENABLE_IL2CPP
            SceneSerialization.RegisterPropertyBagRecursively(type);
#endif

            var component = context.GetInstance();
            if (component == null)
                return null;

            using (new SerializeAsReferenceScope(this))
            {
                var parameters = m_Parameters;
                parameters.DisableRootAdapters = true;
                JsonSerialization.FromJsonOverride(componentView, ref component, parameters);
            }

            if (component is IFormatVersion formatVersion)
                formatVersion.CheckFormatVersion();

            if (component is ISerializationCallbackReceiver receiver)
            {
                try
                {
                    receiver.OnAfterDeserialize();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            return component;
        }
    }
}
