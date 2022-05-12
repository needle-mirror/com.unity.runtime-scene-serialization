using System;
using NUnit.Framework;

namespace Unity.RuntimeSceneSerialization.Tests
{
    class JsonParsingTests
    {
        [Serializable]
        class PrimitiveTestClass
        {
            public bool boolValue;
            public byte byteValue;
            public sbyte sByteValue;
            public char charValue;
            public decimal decimalValue;
            public double doubleValue;
            public float floatValue;
            public int intValue;
            public uint uIntValue;
            public long longValue;
            public ulong uLongValue;
            public short shortValue;
            public ushort uShortValue;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestBool(bool value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {boolValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.boolValue);
        }

        [TestCase(byte.MinValue)]
        [TestCase(byte.MaxValue)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(123)]
        public void TestByte(byte value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {byteValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.byteValue);
        }

        [TestCase(sbyte.MinValue)]
        [TestCase(sbyte.MaxValue)]
        [TestCase(-123)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(123)]
        public void TestSByte(sbyte value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {sByteValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.sByteValue);
        }

        [TestCase(char.MinValue)]
        [TestCase(char.MaxValue)]
        [TestCase('æ')]
        [TestCase('ø')]
        [TestCase((char)0)]
        [TestCase((char)1)]
        [TestCase((char)123)]
        public void TestChar(char value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {charValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.charValue);
        }

        [TestCase(-7.7987250162475039E+18)]
        [TestCase(7.7987250162475039E+18)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(42)]
        public void TestDecimal(decimal value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {decimalValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.decimalValue);
        }

        [TestCase(double.NaN)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.MinValue)]
        [TestCase(double.MaxValue)]
        [TestCase(-double.Epsilon)]
        [TestCase(double.Epsilon)]
        [TestCase(float.MinValue)]
        [TestCase(float.MaxValue)]
        [TestCase(-float.Epsilon)]
        [TestCase(float.Epsilon)]
        [TestCase(-7.7987250162475039E+18)]
        [TestCase(7.7987250162475039E+18)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(42)]
        public void TestDouble(double value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {doubleValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.doubleValue);
        }

        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.MinValue)]
        [TestCase(float.MaxValue)]
        [TestCase(-float.Epsilon)]
        [TestCase(float.Epsilon)]
        [TestCase(-7.798725E+08f)]
        [TestCase(7.798725E+08f)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(42)]
        public void TestFloat(float value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {floatValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.floatValue);
        }

        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(779872501)]
        [TestCase(-465114796)]
        public void TestInt(int value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {intValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.intValue);
        }

        [TestCase(uint.MinValue)]
        [TestCase(uint.MaxValue)]
        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(779872501u)]
        public void TestUInt(uint value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {uIntValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.uIntValue);
        }

        [TestCase(long.MinValue)]
        [TestCase(long.MaxValue)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(7798725016247503745L)]
        [TestCase(4651147963768500152L)]
        public void TestLong(long value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {longValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.longValue);
        }

        [TestCase(ulong.MinValue)]
        [TestCase(ulong.MaxValue)]
        [TestCase(0UL)]
        [TestCase(1UL)]
        [TestCase(7798725016247503745UL)]
        [TestCase(4651147963768500152UL)]
        public void TestULong(ulong value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {uLongValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.uLongValue);
        }

        [TestCase(short.MinValue)]
        [TestCase(short.MaxValue)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(7798)]
        [TestCase(-4651)]
        public void TestShort(short value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {shortValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.shortValue);
        }

        [TestCase(ushort.MinValue)]
        [TestCase(ushort.MaxValue)]
        [TestCase((ushort)0)]
        [TestCase((ushort)1)]
        [TestCase((ushort)1234)]
        public void TestUShort(ushort value)
        {
            var jsonText = SceneSerialization.ToJson(new PrimitiveTestClass {uShortValue = value});
            var testObject = SceneSerialization.FromJson<PrimitiveTestClass>(jsonText);
            Assert.AreEqual(value, testObject.uShortValue);
        }
    }
}
