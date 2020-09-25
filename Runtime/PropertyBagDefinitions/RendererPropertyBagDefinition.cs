using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class RendererPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(Renderer), new HashSet<string>
            {
                nameof(Renderer.sharedMaterials)
            });
        }

#pragma warning disable 618

        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(MeshRenderer renderer)
        {
            renderer.castShadows = renderer.castShadows;
            renderer.shadowCastingMode = renderer.shadowCastingMode;
            renderer.receiveShadows = renderer.receiveShadows;
            renderer.allowOcclusionWhenDynamic = renderer.allowOcclusionWhenDynamic;
            renderer.motionVectors = renderer.motionVectors;
            renderer.motionVectorGenerationMode = renderer.motionVectorGenerationMode;
            renderer.lightProbeUsage = renderer.lightProbeUsage;
            renderer.reflectionProbeUsage = renderer.reflectionProbeUsage;
            renderer.rayTracingMode = renderer.rayTracingMode;
            renderer.renderingLayerMask = renderer.renderingLayerMask;
            renderer.rendererPriority = renderer.rendererPriority;
            renderer.sharedMaterials = renderer.sharedMaterials;
            renderer.probeAnchor = renderer.probeAnchor;
            renderer.lightProbeProxyVolumeOverride = renderer.lightProbeProxyVolumeOverride;
            renderer.sortingLayerID = renderer.sortingLayerID;
            renderer.sortingOrder = renderer.sortingOrder;
        }

        public static void Unused(SkinnedMeshRenderer renderer)
        {
            renderer.castShadows = renderer.castShadows;
            renderer.receiveShadows = renderer.receiveShadows;
            renderer.allowOcclusionWhenDynamic = renderer.allowOcclusionWhenDynamic;
            renderer.motionVectors = renderer.motionVectors;
            renderer.motionVectorGenerationMode = renderer.motionVectorGenerationMode;
            renderer.lightProbeUsage = renderer.lightProbeUsage;
            renderer.reflectionProbeUsage = renderer.reflectionProbeUsage;
            renderer.rayTracingMode = renderer.rayTracingMode;
            renderer.renderingLayerMask = renderer.renderingLayerMask;
            renderer.rendererPriority = renderer.rendererPriority;
            renderer.sharedMaterials = renderer.sharedMaterials;
            renderer.probeAnchor = renderer.probeAnchor;
            renderer.lightProbeProxyVolumeOverride = renderer.lightProbeProxyVolumeOverride;
            renderer.sortingLayerID = renderer.sortingLayerID;
            renderer.sortingOrder = renderer.sortingOrder;
            renderer.quality = renderer.quality;
            renderer.updateWhenOffscreen = renderer.updateWhenOffscreen;
            renderer.skinnedMotionVectors = renderer.skinnedMotionVectors;
            renderer.sharedMesh = renderer.sharedMesh;
            renderer.bones = renderer.bones;
            renderer.rootBone = renderer.rootBone;
            renderer.localBounds = renderer.localBounds;
        }
#pragma warning restore 618
    }
}
