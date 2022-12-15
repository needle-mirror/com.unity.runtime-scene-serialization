using Unity.RuntimeSceneSerialization.Internal;
using Unity.Serialization.Json;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    partial class JsonAdapter : IJsonAdapter<SerializedScene>
    {
        internal const string RootGameObjectsPropertyName = "m_RootGameObjects";

        const int k_FormatVersion = 2;
        const string k_FormatVersionPropertyName = "m_FormatVersion";
        const string k_RenderSettingsPropertyName = "m_RenderSettings";

        bool m_FirstPassCompleted;

        void IJsonAdapter<SerializedScene>.Serialize(in JsonSerializationContext<SerializedScene> context, SerializedScene value)
        {
            var writer = context.Writer;
            using (writer.WriteObjectScope())
            {
                writer.WriteKeyValue(k_FormatVersionPropertyName, k_FormatVersion);
                writer.WriteKey(RootGameObjectsPropertyName);

                using (writer.WriteArrayScope())
                {
                    foreach (var gameObject in value.RootGameObjects)
                    {
                        JsonSerialization.ToJson(writer, gameObject, m_Parameters);
                    }
                }

                writer.WriteKey(k_RenderSettingsPropertyName);
                using (new SerializeAsReferenceScope(this))
                {
                    JsonSerialization.ToJson(writer, value.RenderSettings, m_Parameters);
                }
            }
        }

        SerializedScene IJsonAdapter<SerializedScene>.Deserialize(in JsonDeserializationContext<SerializedScene> context)
        {
            var rootCount = m_SceneRoot.childCount;
            if (rootCount > 0)
            {
                var count = 0;
                m_FirstPassCompleted = true;
                var serializedObject = context.SerializedValue.AsObjectView();
                if (serializedObject.TryGetValue(RootGameObjectsPropertyName, out var roots))
                {
                    foreach (var rootView in roots.AsArrayView())
                    {
                        var gameObject = m_SceneRoot.GetChild(count++).gameObject;
                        Deserialize(rootView.AsObjectView(), m_SceneRoot, gameObject);
                    }
                }
            }
            else
            {
                context.ContinueVisitation();
            }

            return context.GetInstance();
        }
    }
}
