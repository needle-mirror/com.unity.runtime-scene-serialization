using System;
using Unity.Serialization.Json;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    partial class JsonAdapter : IContravariantJsonAdapter<SerializedRenderSettings>
    {
        void IContravariantJsonAdapter<SerializedRenderSettings>.Serialize(IJsonSerializationContext context, SerializedRenderSettings value)
        {
            context.ContinueVisitation();
        }

        object IContravariantJsonAdapter<SerializedRenderSettings>.Deserialize(IJsonDeserializationContext context)
        {
            using (new SerializeAsReferenceScope(this))
            {
                return context.ContinueVisitation();
            }
        }
    }
}

