using NUnit.Framework;
using System;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Tests
{
    [Serializable]
    class VanillaSerializationCallbackReceiver : ISerializationCallbackReceiver
    {
        bool m_BeforeSerialize;
        bool m_AfterDeserialize;

        void ISerializationCallbackReceiver.OnBeforeSerialize() => m_BeforeSerialize = true;
        void ISerializationCallbackReceiver.OnAfterDeserialize() => m_AfterDeserialize = true;

        public static void SaveLoadTest()
        {
            var receiver = new VanillaSerializationCallbackReceiver();
            var jsonText = SceneSerialization.ToJson(receiver);
            Assert.IsTrue(receiver.m_BeforeSerialize);
            Assert.IsFalse(receiver.m_AfterDeserialize);

            receiver = new VanillaSerializationCallbackReceiver();
            SceneSerialization.FromJsonOverride(jsonText, ref receiver);
            Assert.IsFalse(receiver.m_BeforeSerialize);
            Assert.IsTrue(receiver.m_AfterDeserialize);

            receiver = SceneSerialization.FromJson<VanillaSerializationCallbackReceiver>(jsonText);
            Assert.IsFalse(receiver.m_BeforeSerialize);
            Assert.IsTrue(receiver.m_AfterDeserialize);
        }
    }
}
