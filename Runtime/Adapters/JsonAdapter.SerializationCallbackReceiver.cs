using System;
using Unity.Serialization.Json;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    partial class JsonAdapter : IContravariantJsonAdapter<ISerializationCallbackReceiver>
    {
        void IContravariantJsonAdapter<ISerializationCallbackReceiver>.Serialize(IJsonSerializationContext context, ISerializationCallbackReceiver value)
        {
            if (!m_SerializeAsReference)
            {
                try
                {
                    value.OnBeforeSerialize();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            context.ContinueVisitation();
        }

        object IContravariantJsonAdapter<ISerializationCallbackReceiver>.Deserialize(IJsonDeserializationContext context)
        {
            var value = context.ContinueVisitation();
            if (m_SerializeAsReference)
                return value;

            if (value is ISerializationCallbackReceiver callbackReceiver)
            {
                try
                {
                    callbackReceiver.OnAfterDeserialize();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            return value;
        }
    }
}
