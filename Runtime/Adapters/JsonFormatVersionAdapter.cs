using Unity.Serialization.Json;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    class JsonFormatVersionAdapter : IContravariantJsonAdapter<IFormatVersion>
    {
        void IContravariantJsonAdapter<IFormatVersion>.Serialize(IJsonSerializationContext context, IFormatVersion value)
        {
            // Do nothing--format version should be a regular serialized field
            context.ContinueVisitation();
        }

        object IContravariantJsonAdapter<IFormatVersion>.Deserialize(IJsonDeserializationContext context)
        {
            var value = context.ContinueVisitation();

            if (value is IFormatVersion callbackReceiver)
                callbackReceiver.CheckFormatVersion();

            return value;
        }
    }
}
