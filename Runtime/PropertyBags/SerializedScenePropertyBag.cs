using System;
using System.Collections.Generic;
using Unity.Properties;
using Unity.RuntimeSceneSerialization.Internal;
using Unity.RuntimeSceneSerialization.Json.Adapters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.RuntimeSceneSerialization.PropertyBags
{
    class SerializedScenePropertyBag : ContainerPropertyBag<SerializedScene>
    {
        class FormatVersionProperty : Property<SerializedScene, int>
        {
            const int k_FormatVersion = 2;
            public override string Name => "m_FormatVersion";

            // Return false so that deserialization does not fail
            public override bool IsReadOnly => false;
            public override int GetValue(ref SerializedScene container) => k_FormatVersion;

            public override void SetValue(ref SerializedScene container, int value)
            {
                if (value != k_FormatVersion)
                    throw new FormatException($"Scene formats do not match. Expected {k_FormatVersion} but got {value}");
            }
        }

        class RootGameObjectsProperty : Property<SerializedScene, List<GameObject>>
        {
            public override string Name => JsonAdapter.RootGameObjectsPropertyName;
            public override bool IsReadOnly => false;

            public override List<GameObject> GetValue(ref SerializedScene container)
            {
                return container.RootGameObjects;
            }

            public override void SetValue(ref SerializedScene container, List<GameObject> value)
            {
                container.RootGameObjects = value;
            }
        }

        class RenderSettingsProperty : Property<SerializedScene, SerializedRenderSettings>
        {
            public override string Name => "m_RenderSettingsProperty";
            public override bool IsReadOnly => false;

            public override SerializedRenderSettings GetValue(ref SerializedScene container)
            {
                return container.RenderSettings;
            }

            public override void SetValue(ref SerializedScene container, SerializedRenderSettings value)
            {
                container.RenderSettings = value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize() { PropertyBag.Register(new SerializedScenePropertyBag()); }

        SerializedScenePropertyBag()
        {
            PropertyBag.RegisterList<Scene, GameObject>();
            AddProperty(new FormatVersionProperty());
            AddProperty(new RootGameObjectsProperty());
            AddProperty(new RenderSettingsProperty());
        }
    }
}
