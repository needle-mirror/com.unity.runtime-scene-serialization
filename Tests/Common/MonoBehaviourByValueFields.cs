using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Tests
{
    class MonoBehaviourByValueFields : MonoBehaviour
    {
        [Serializable]
        public struct SerializedStruct
        {
            public double m_DoubleInStruct;
        }

        [Serializable]
        public class SerializedClass
        {
            [SerializeField]
            SerializedStruct m_StructInClass;

            public double ValueInPrivateStruct
            {
                get => m_StructInClass.m_DoubleInStruct;
                set => m_StructInClass.m_DoubleInStruct = value;
            }
        }

        [Flags]
        public enum SomeFlags : uint
        {
            None = 0,
            Flag10 = 1 << 9,
            Flag32 = 1u << 31,
        }

        // TODO: Non-ascii utf8 characters
        // Check for escaping issues
        const string k_KnownValueWithJsonSpecialCharacters = "Kn<wnValu}?>e{;";

        // TODO: Fix tab and newline getting replaced with /0
        //const string k_KnownValueWithEscape = "\"newline:\n tab:\t'";

        public int publicInt = 55;

        [SerializeField]
        int m_PrivateSerializedInt = 55;

        [SerializeField]
        int m_SerializeBackingForProperty = 55;

        public Vector2 vector2 = new Vector2(55.0f, 55.0f);
        public long[] arrayOfLong;

        public SerializedStruct @struct;

        [SerializeField]
        List<SerializedStruct> m_Structs;

        [SerializeField]
        SerializedClass m_ByValueClass;

        public SomeFlags m_UintFlag;

        // ReSharper disable once MemberCanBePrivate.Global
        public int PropertyWithSerializedBackingField
        {
            get => m_SerializeBackingForProperty;
            private set => m_SerializeBackingForProperty = value;
        }

        public string m_String = "default";

        // TODO: Fix tab and newline getting replaced with /0
        // public string m_StringWithEscaping = "default";

        // TODO: tests that private fields and other "non-serialized" data are correctly reset back to defaults after round trip
        // (as done in SerializationRuleTests in Serialization test suite)

        public void SetKnownState()
        {
            var i = 0;
            publicInt = i++;
            m_PrivateSerializedInt = i++;
            PropertyWithSerializedBackingField = i++;
            vector2 = new Vector2(i++, i++);
            arrayOfLong = new long[] { i++, i++, i++ };
            @struct.m_DoubleInStruct = i++;

            m_Structs = new List<SerializedStruct> { new SerializedStruct { m_DoubleInStruct = i++ } };

            m_ByValueClass = new SerializedClass();
            m_ByValueClass.ValueInPrivateStruct = i;

            m_UintFlag = SomeFlags.Flag10 | SomeFlags.Flag32;
            m_String = k_KnownValueWithJsonSpecialCharacters;

            // TODO: Fix tab and newline getting replaced with /0
            // m_StringWithEscaping = k_KnownValueWithEscape;
        }

        public void TestKnownState()
        {
            var i = 0;
            Assert.AreEqual(i++, publicInt);
            Assert.AreEqual(i++, m_PrivateSerializedInt);
            Assert.AreEqual(i++, PropertyWithSerializedBackingField);
            Assert.AreEqual(new Vector2(i++, i++), vector2);
            Assert.AreEqual(3, arrayOfLong.Length);
            for (var pos = 0 ; pos < arrayOfLong.Length; pos++)
                Assert.AreEqual(i++, arrayOfLong[pos]);

            Assert.AreEqual(i++,@struct.m_DoubleInStruct);
            Assert.AreEqual(i++,m_Structs[0].m_DoubleInStruct);
            Assert.AreEqual(i, m_ByValueClass.ValueInPrivateStruct);

            Assert.AreEqual(SomeFlags.Flag10 | SomeFlags.Flag32, m_UintFlag);
            Assert.AreEqual(k_KnownValueWithJsonSpecialCharacters, m_String);

            // TODO: Fix tab and newline getting replaced with /0
            // Assert.AreEqual( k_KnownValueWithEscape, m_StringWithEscaping);
        }
    }
}
