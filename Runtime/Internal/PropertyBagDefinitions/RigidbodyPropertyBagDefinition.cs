#if INCLUDE_PHYSICS
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class RigidbodyPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(Rigidbody), new HashSet<string>
            {
                nameof(Rigidbody.maxDepenetrationVelocity),
                nameof(Rigidbody.freezeRotation),
                nameof(Rigidbody.detectCollisions),
                nameof(Rigidbody.solverIterations),
                nameof(Rigidbody.sleepThreshold),
                nameof(Rigidbody.maxAngularVelocity),
                nameof(Rigidbody.solverVelocityIterations),
#if !UNITY_2021_1_OR_NEWER
#pragma warning disable 618
                nameof(Rigidbody.sleepVelocity),
                nameof(Rigidbody.sleepAngularVelocity)
#pragma warning restore 618
#endif
            });
        }

        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(Rigidbody body)
        {
            body.mass = body.mass;
            body.drag = body.drag;
            body.angularDrag = body.angularDrag;
            body.useGravity = body.useGravity;
            body.isKinematic = body.isKinematic;
            body.interpolation = body.interpolation;
            body.constraints = body.constraints;
            body.collisionDetectionMode = body.collisionDetectionMode;
        }
    }
}
#endif
