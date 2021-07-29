using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class LightPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(Light), new HashSet<string>
            {
                nameof(Light.color),
#if UNITY_EDITOR
                nameof(Light.areaSize)
#endif
            });

            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(Light), new HashSet<string>
            {
                nameof(Light.layerShadowCullDistances)
            });
        }

        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(Light light)
        {
            light.type = light.type;
            light.shape = light.shape;
            light.color = light.color;
            light.intensity = light.intensity;
            light.range = light.range;
            light.spotAngle = light.spotAngle;
            light.innerSpotAngle = light.innerSpotAngle;
            light.cookieSize = light.cookieSize;
            light.cookie = light.cookie;
            light.shadows = light.shadows;
            light.shadowResolution = light.shadowResolution;
            light.shadowCustomResolution = light.shadowCustomResolution;
            light.shadowStrength = light.shadowStrength;
            light.shadowBias = light.shadowBias;
            light.shadowNormalBias = light.shadowNormalBias;
            light.shadowNearPlane = light.shadowNearPlane;
            light.shadowMatrixOverride = light.shadowMatrixOverride;
            light.useShadowMatrixOverride = light.useShadowMatrixOverride;
            light.color = light.color;
            light.flare = light.flare;
            light.renderMode = light.renderMode;
            light.cullingMask = light.cullingMask;
            light.renderingLayerMask = light.renderingLayerMask;
            light.lightShadowCasterMode = light.lightShadowCasterMode;
            light.bounceIntensity = light.bounceIntensity;
            light.colorTemperature = light.colorTemperature;
            light.useColorTemperature = light.useColorTemperature;
            light.boundingSphereOverride = light.boundingSphereOverride;
            light.useBoundingSphereOverride = light.useBoundingSphereOverride;
        }
    }
}
