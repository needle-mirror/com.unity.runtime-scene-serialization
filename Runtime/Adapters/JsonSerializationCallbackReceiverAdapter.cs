using System;
using Unity.Serialization.Json;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    class JsonSerializationCallbackReceiverAdapter : IContravariantJsonAdapter<ISerializationCallbackReceiver>
    {
        void IContravariantJsonAdapter<ISerializationCallbackReceiver>.Serialize(IJsonSerializationContext context, ISerializationCallbackReceiver value)
        {
            try
            {
                value.OnBeforeSerialize();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            context.ContinueVisitation();
        }

        object IContravariantJsonAdapter<ISerializationCallbackReceiver>.Deserialize(IJsonDeserializationContext context)
        {
            var value = context.ContinueVisitation();

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
