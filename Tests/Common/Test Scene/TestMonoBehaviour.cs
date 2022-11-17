using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable UnusedMember.Local
// ReSharper disable NotAccessedField.Local

namespace Unity.RuntimeSceneSerialization.Tests
{
    class TestMonoBehaviour : MonoBehaviour
    {
#pragma warning disable 414, 649
        enum LongEnum
        {
            Value1,
            Value2 = int.MaxValue
        }

        [Serializable]
        class SerializableClass
        {
            [SerializeField]
            char m_Char;

            [SerializeField]
            bool m_B;

            public int[] intArray;
            public Vector3 vector;
            public string str;
            public OtherSerializableClass otherClass;

            [SerializeField]
            OtherSerializableClass[] otherClasses;

            [SerializeField]
            List<OtherSerializableClass> other;

            [SerializeField]
            Color m_Color;
        }

        [Serializable]
        public class OtherSerializableClass
        {
            public int a;

            [SerializeField]
            GameObject[] m_GameObjects;

            [SerializeField]
            List<GameObject> m_GameObjectList;

            [SerializeField]
            Color m_Color = Color.red;
        }

        [SerializeField]
        SerializableClass m_ClassObject;

        public GameObject m_OtherObject;

        [SerializeField]
        Renderer m_OtherRenderer;

        [SerializeField]
        LongEnum m_LongEnum = LongEnum.Value2;

        [SerializeField]
        float m_Float = float.MaxValue;

        [SerializeField]
        bool m_Bool = true;

        [SerializeField]
        int m_Int = int.MaxValue;

        [SerializeField]
        Color m_Color = Color.blue;

        [SerializeField]
        Bounds m_Bounds;

        [SerializeField]
        Rect m_Rect;

        [SerializeField]
        GameObject[] m_GameObjects;

        [SerializeField]
        Quaternion m_Quaternion;

        [SerializeField]
        Vector2Int m_Vector2Int;

        [SerializeField]
        Vector3Int m_Vector3Int;

        [SerializeField]
        RectInt m_RectInt;

        [SerializeField]
        BoundsInt m_BoundsInt;

        [SerializeField]
        List<GameObject> m_GameObjectList;

        [SerializeField]
        List<SerializableClass> m_Classes;

        [SerializeField]
        SerializableClass[] m_ClassArray;

        // TODO: fix gradient type
        // [SerializeField]
        // Gradient m_Gradient;
#pragma warning restore 414, 649
    }
}
