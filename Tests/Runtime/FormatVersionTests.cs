using NUnit.Framework;
using System;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.RuntimeSceneSerialization.Tests
{
    class FormatVersionTests
    {
        [Serializable]
        class OldFormat : IFormatVersion
        {
            const int k_FormatVersion = 1;

            [SerializeField]
            int m_FormatVersion = k_FormatVersion;

            void IFormatVersion.CheckFormatVersion()
            {
                if (m_FormatVersion != k_FormatVersion)
                    throw new FormatException($"Formats do not match. Expected {k_FormatVersion} but got {m_FormatVersion}");
            }
        }

        [Serializable]
        class NewFormat : IFormatVersion
        {
            const int k_FormatVersion = 2;

            [SerializeField]
            int m_FormatVersion = k_FormatVersion;

            void IFormatVersion.CheckFormatVersion()
            {
                if (m_FormatVersion != k_FormatVersion)
                    throw new FormatException($"Formats do not match. Expected {k_FormatVersion} but got {m_FormatVersion}");
            }
        }

        class SettableFormatMonoBehaviour : MonoBehaviour, IFormatVersion
        {
            internal static int FormatVersion;

            [SerializeField]
            int m_FormatVersion = FormatVersion;

            void IFormatVersion.CheckFormatVersion()
            {
                if (m_FormatVersion != FormatVersion)
                    throw new FormatException($"Formats do not match. Expected {FormatVersion} but got {m_FormatVersion}");
            }
        }

        const string k_OldFormatScenePath = "RuntimeSceneSerializationTests/Old Format Scene";

        [Test]
        public void DeserializeVanillaClassWithIncorrectFormatVersionThrows()
        {
            var json = SceneSerialization.ToJson(new OldFormat());
            var threwException = false;
            try
            {
                SceneSerialization.FromJson<NewFormat>(json);
            }
            catch (FormatException)
            {
                threwException = true;
            }

            Assert.IsTrue(threwException);
        }

        [Test]
        public void DeserializeGameObjectWithMonoBehaviourWithIncorrectFormatVersionThrows()
        {
            SettableFormatMonoBehaviour.FormatVersion = 1;
            var gameObject = new GameObject("Settable Format", typeof(SettableFormatMonoBehaviour));
            var json = SceneSerialization.ToJson(gameObject);
            var threwException = false;
            try
            {
                SettableFormatMonoBehaviour.FormatVersion = 2;
                SceneSerialization.FromJson<GameObject>(json);
            }
            catch (FormatException)
            {
                threwException = true;
            }

            Assert.IsTrue(threwException);

            UnityObject.Destroy(gameObject);
        }

        [Test]
        public void DeserializeMonoBehaviourWithIncorrectFormatVersionThrows()
        {
            SettableFormatMonoBehaviour.FormatVersion = 1;
            var behaviour = new GameObject("Settable Format").AddComponent<SettableFormatMonoBehaviour>();
            var json = SceneSerialization.ToJson(behaviour);
            var threwException = false;
            try
            {
                SettableFormatMonoBehaviour.FormatVersion = 2;
                SceneSerialization.FromJsonOverride(json, ref behaviour);
            }
            catch (FormatException)
            {
                threwException = true;
            }

            Assert.IsTrue(threwException);

            UnityObject.Destroy(behaviour.gameObject);
        }

        [Test]
        public void DeserializeSceneWithIncorrectFormatVersionThrows()
        {
            var json = Resources.Load<TextAsset>(k_OldFormatScenePath).text;
            var threwException = false;
            try
            {
                SceneSerialization.ImportScene(json);
            }
            catch (FormatException)
            {
                threwException = true;
            }

            Assert.IsTrue(threwException);
        }
    }
}
