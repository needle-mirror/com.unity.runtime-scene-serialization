#if INCLUDE_ANIMATION
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class AnimatorPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(Animator), new HashSet<string>
            {
                nameof(Animator.playbackTime),
                nameof(Animator.fireEvents),
                nameof(Animator.stabilizeFeet),
                nameof(Animator.feetPivotActive),
                nameof(Animator.speed),
                nameof(Animator.layersAffectMassCenter),
                nameof(Animator.logWarnings)
            });
        }

#pragma warning disable 618

        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(Animator animator)
        {
            animator.avatar = animator.avatar;
            animator.runtimeAnimatorController = animator.runtimeAnimatorController;
            animator.cullingMode = animator.cullingMode;
            animator.updateMode = animator.updateMode;
            animator.applyRootMotion = animator.applyRootMotion;
            animator.linearVelocityBlending = animator.linearVelocityBlending;
            animator.logWarnings = animator.logWarnings;
            animator.keepAnimatorControllerStateOnDisable = animator.keepAnimatorControllerStateOnDisable;
        }
#pragma warning restore 618
    }
}
#endif
