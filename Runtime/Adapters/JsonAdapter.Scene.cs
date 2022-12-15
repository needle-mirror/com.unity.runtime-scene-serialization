using Unity.Serialization.Json;
using UnityEngine.SceneManagement;

namespace Unity.RuntimeSceneSerialization.Json.Adapters
{
    partial class JsonAdapter : IJsonAdapter<Scene>
    {
        SerializedRenderSettings m_RenderSettings;

        public SerializedRenderSettings RenderSettings => m_RenderSettings;

        void IJsonAdapter<Scene>.Serialize(in JsonSerializationContext<Scene> context, Scene value)
        {
            // Not used
        }

        Scene IJsonAdapter<Scene>.Deserialize(in JsonDeserializationContext<Scene> context)
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

                // ReSharper disable once InvertIf
                if (serializedObject.TryGetValue(k_RenderSettingsPropertyName, out var renderSettings))
                {
                    using (new SerializeAsReferenceScope(this))
                    {
                        var parameters = m_Parameters;
                        parameters.DisableRootAdapters = true;
                        m_RenderSettings = JsonSerialization.FromJson<SerializedRenderSettings>(renderSettings, parameters);
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
