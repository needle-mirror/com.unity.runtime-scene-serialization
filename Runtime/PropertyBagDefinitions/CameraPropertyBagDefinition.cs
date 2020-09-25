using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class CameraPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(Camera), new HashSet<string>
            {
                nameof(Camera.backgroundColor),
                nameof(Camera.sensorSize),
                nameof(Camera.lensShift)
            });

            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(Camera), new HashSet<string>
            {
                nameof(Camera.pixelRect),
                nameof(Camera.aspect)
            });
        }

        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(Camera camera)
        {
            camera.clearFlags = camera.clearFlags;
            camera.backgroundColor = camera.backgroundColor;
            camera.gateFit = camera.gateFit;
            camera.sensorSize = camera.sensorSize;
            camera.lensShift = camera.lensShift;
            camera.focalLength = camera.focalLength;
            camera.rect = camera.rect;
            camera.nearClipPlane = camera.nearClipPlane;
            camera.farClipPlane = camera.farClipPlane;
            camera.fieldOfView = camera.fieldOfView;
            camera.orthographic = camera.orthographic;
            camera.orthographicSize = camera.orthographicSize;
            camera.depth = camera.depth;
            camera.cullingMask = camera.cullingMask;
            camera.renderingPath = camera.renderingPath;
            camera.targetTexture = camera.targetTexture;
            camera.targetDisplay = camera.targetDisplay;
            camera.stereoTargetEye = camera.stereoTargetEye;
            camera.allowHDR = camera.allowHDR;
            camera.allowMSAA = camera.allowMSAA;
            camera.allowDynamicResolution = camera.allowDynamicResolution;
            camera.forceIntoRenderTexture = camera.forceIntoRenderTexture;
            camera.useOcclusionCulling = camera.useOcclusionCulling;
            camera.stereoConvergence = camera.stereoConvergence;
            camera.stereoSeparation = camera.stereoSeparation;
        }
    }
}
