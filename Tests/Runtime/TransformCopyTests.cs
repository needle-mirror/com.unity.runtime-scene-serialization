using NUnit.Framework;
using Unity.Properties;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Tests
{
    class TransformCopyTests
    {
        class CopyVisitor<T> : PropertyVisitor
        {
            public T destination;

            protected override void VisitProperty<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value)
            {
                if (destination is TContainer destinationContainer)
                {
                    property.SetValue(ref destinationContainer, value);
                }

                base.VisitProperty(property, ref container, ref value);
            }
        }

        static readonly Vector3 k_TargetPosition = Vector3.left;
        static readonly Quaternion k_TargetRotation = Quaternion.AngleAxis(90, Vector3.up);
        static readonly Vector3 k_TargetScale = Vector3.one * 2f;

        Transform m_Original;
        Transform m_Target;

        [SetUp]
        public void SetUp()
        {
            m_Original = new GameObject("Original Transform").transform;
            m_Original.position = k_TargetPosition;
            m_Original.rotation = k_TargetRotation;
            m_Original.localScale = k_TargetScale;

            m_Target = new GameObject("Target Transform").transform;
        }

        [TearDown]
        public void TearDown()
        {
            UnityObject.Destroy(m_Original.gameObject);
            UnityObject.Destroy(m_Target.gameObject);
        }

        [Test]
        public void CopyTransform()
        {
#if !NET_DOTS && !ENABLE_IL2CPP
            SceneSerialization.RegisterPropertyBagRecursively(typeof(Transform));
#endif

            var visitor = new CopyVisitor<Transform>();
            visitor.destination = m_Target;
            PropertyContainer.Accept(visitor, ref m_Original);

            // TODO: Investigate why transforms are not exactly equal on Android
            Assert.IsTrue(m_Original.position == m_Target.position);
            Assert.IsTrue(m_Original.rotation == m_Target.rotation);
            Assert.IsTrue(m_Original.localScale == m_Target.localScale);
        }

        [Test]
        public void CopyTransformJson()
        {
            var jsonString = SceneSerialization.ToJson(m_Original);
            SceneSerialization.FromJsonOverride(jsonString, ref m_Target);

            // TODO: Investigate why transforms are not exactly equal on Android
            Assert.IsTrue(m_Original.position == m_Target.position);
            Assert.IsTrue(m_Original.rotation == m_Target.rotation);
            Assert.IsTrue(m_Original.localScale == m_Target.localScale);
        }
    }
}
