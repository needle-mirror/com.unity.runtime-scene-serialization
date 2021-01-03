#if INCLUDE_PHYSICS
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class ColliderPropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(Collider), new HashSet<string>
            {
                nameof(Collider.sharedMaterial)
            });

            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(Collider), new HashSet<string>
            {
                nameof(Collider.contactOffset),
                nameof(Collider.material)
            });

            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(BoxCollider), new HashSet<string>
            {
                nameof(BoxCollider.center)
            });

            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(CapsuleCollider), new HashSet<string>
            {
                nameof(CapsuleCollider.center)
            });

            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(SphereCollider), new HashSet<string>
            {
                nameof(SphereCollider.center)
            });
        }

        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(CapsuleCollider collider)
        {
            collider.material = collider.material;
            collider.isTrigger = collider.isTrigger;
            collider.radius = collider.radius;
            collider.height = collider.height;
            collider.direction = collider.direction;
            collider.center = collider.center;
        }

        public static void Unused(BoxCollider collider)
        {
            collider.material = collider.material;
            collider.isTrigger = collider.isTrigger;
            collider.size = collider.size;
            collider.center = collider.center;
        }

        public static void Unused(MeshCollider collider)
        {
            collider.material = collider.material;
            collider.isTrigger = collider.isTrigger;
            collider.convex = collider.convex;
            collider.cookingOptions = collider.cookingOptions;
            collider.sharedMesh = collider.sharedMesh;
        }

        public static void Unused(SphereCollider collider)
        {
            collider.material = collider.material;
            collider.isTrigger = collider.isTrigger;
            collider.radius = collider.radius;
            collider.center = collider.center;
        }
    }
}
#endif
