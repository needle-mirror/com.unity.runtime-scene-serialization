using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Tests
{
    class MonoBehaviourSerializationCallbackReceiver : MonoBehaviour, ISerializationCallbackReceiver
    {
        bool m_BeforeSerialize;
        bool m_AfterDeserialize;

        public bool BeforeSerialize => m_BeforeSerialize;
        public bool AfterDeserialize => m_AfterDeserialize;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_BeforeSerialize = true;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_AfterDeserialize = true;
        }
    }
}
