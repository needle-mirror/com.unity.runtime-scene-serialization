using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Serializable struct for storing render settings
    /// </summary>
    [Serializable]
    public class SerializedRenderSettings
    {
        [SerializeField]
        AmbientMode m_AmbientMode = AmbientMode.Skybox;

        [SerializeField]
        Color m_AmbientSkyColor = new Color(0.212f, 0.227f, 0.259f);

        [SerializeField]
        Color m_AmbientEquatorColor = new Color(0.115f, 0.125f, 0.135f);

        [SerializeField]
        Color m_AmbientGroundColor = new Color(0.047f, 0.045f, 0.035f);

        [SerializeField]
        float m_AmbientIntensity = 1f;

        [SerializeField]
        Color m_AmbientLight = new Color(0.212f, 0.227f, 0.259f);

        [SerializeField]
        Color m_SubtractiveShadowColor = new Color(0.42f, 0.478f, 0.627f);

        [SerializeField]
        float m_ReflectionIntensity = 1f;

        [SerializeField]
        int m_ReflectionBounces = 1;

        [SerializeField]
        DefaultReflectionMode m_DefaultReflectionMode = DefaultReflectionMode.Skybox;

        [SerializeField]
        int m_DefaultReflectionResolution = 128;

        [SerializeField]
        float m_HaloStrength = 0.5f;

        [SerializeField]
        float m_FlareStrength = 1f;

        [SerializeField]

        float m_FlareFadeSpeed = 3f;

#pragma warning disable 649
        [SerializeField]
        Material m_Skybox;

        [SerializeField]
#if UNITY_2021_2_OR_NEWER
        Texture m_CustomReflection;
#else
        Cubemap m_CustomReflection;
#endif

        [SerializeField]
        Light m_Sun;
#pragma warning restore 649

        /// <summary>
        /// Create a new SerializedRenderSettings using the current render settings
        /// </summary>
        /// <returns>A SerializedRenderSettings containing the current render settings</returns>
        public static SerializedRenderSettings CreateFromActiveScene()
        {
            var settings = new SerializedRenderSettings();
            settings.UpdateValuesFromRenderSettings();
            return settings;
        }

        /// <summary>
        /// Update the values on this SerializedRenderSettings to the current values in the static RenderSettings manager
        /// </summary>
        public void UpdateValuesFromRenderSettings()
        {
            m_AmbientMode = RenderSettings.ambientMode;
            m_AmbientSkyColor = RenderSettings.ambientSkyColor;
            m_AmbientEquatorColor = RenderSettings.ambientEquatorColor;
            m_AmbientGroundColor = RenderSettings.ambientGroundColor;
            m_AmbientIntensity = RenderSettings.ambientIntensity;
            m_AmbientLight = RenderSettings.ambientLight;
            m_SubtractiveShadowColor = RenderSettings.subtractiveShadowColor;
            m_ReflectionIntensity = RenderSettings.reflectionIntensity;
            m_ReflectionBounces = RenderSettings.reflectionBounces;
            m_DefaultReflectionMode = RenderSettings.defaultReflectionMode;
            m_DefaultReflectionResolution = RenderSettings.defaultReflectionResolution;
            m_HaloStrength = RenderSettings.haloStrength;
            m_FlareStrength = RenderSettings.flareStrength;
            m_FlareFadeSpeed = RenderSettings.flareFadeSpeed;
            m_Skybox = RenderSettings.skybox;
#if UNITY_2022_1_OR_NEWER
            m_CustomReflection = RenderSettings.customReflectionTexture;
#elif UNITY_2021_2_OR_NEWER
            if (RenderSettings.customReflection is Cubemap customReflection)
                m_CustomReflection = customReflection;
#else
            m_CustomReflection = RenderSettings.customReflection;
#endif

            m_Sun = RenderSettings.sun;
        }

        /// <summary>
        /// Apply the values on this SerializedRenderSettings to the static RenderSettings manager
        /// </summary>
        public void ApplyValuesToRenderSettings()
        {
            RenderSettings.ambientMode = m_AmbientMode;
            RenderSettings.ambientSkyColor = m_AmbientSkyColor;
            RenderSettings.ambientEquatorColor = m_AmbientEquatorColor;
            RenderSettings.ambientGroundColor = m_AmbientGroundColor;
            RenderSettings.ambientIntensity = m_AmbientIntensity;
            RenderSettings.ambientLight = m_AmbientLight;
            RenderSettings.subtractiveShadowColor = m_SubtractiveShadowColor;
            RenderSettings.skybox = m_Skybox;
            RenderSettings.sun = m_Sun;
#if UNITY_2022_1_OR_NEWER
            RenderSettings.customReflectionTexture = m_CustomReflection;
#else
            RenderSettings.customReflection = m_CustomReflection;
#endif
            RenderSettings.reflectionIntensity = m_ReflectionIntensity;
            RenderSettings.reflectionBounces = m_ReflectionBounces;
            RenderSettings.defaultReflectionMode = m_DefaultReflectionMode;
            RenderSettings.defaultReflectionResolution = m_DefaultReflectionResolution;
            RenderSettings.haloStrength = m_HaloStrength;
            RenderSettings.flareStrength = m_FlareStrength;
            RenderSettings.flareFadeSpeed = m_FlareFadeSpeed;
        }
    }
}
