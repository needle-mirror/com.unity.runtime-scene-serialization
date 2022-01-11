//Currently Property based serialization is visiting the data recursively so it can't handle back-pointers or other cycles
//Native serialization supports cycles with SerializeReference via a special "registry" listing all the distinct SR objects
//and associated ids with them, so that it can detect and safely represent cycles
// TODO: Support cycles

// #define EXPECT_CYCLES_TO_WORK

using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Tests
{
    class MonoBehaviourSerializedRef : MonoBehaviour
    {
        [Serializable]
        public class Node
        {
            public int m_data;

            [SerializeReference]
            public List<Node> m_nodes = new List<Node>();

            [SerializeReference]
            public Node m_BackPointer;
        }

        // TODO: For consistency with unity serialization, SerializeReference should automatically make it serialized,
        // without need for public or SerializeField
        [SerializeReference]
        [SerializeField]
        Node m_Root;

#if UNITY_2019_4_OR_NEWER
        [SerializeReference]
        // ReSharper disable once MemberCanBePrivate.Global
        public object polyMorphic;
#endif

        public void SetKnownState()
        {
            var i = 0;
            m_Root = new Node { m_data = i++ };

#if EXPECT_CYCLES_TO_WORK
            var backPointer = m_Root;
#else
            const Node backPointer = null;
#endif

            m_Root.m_nodes.Add(new Node { m_data = i++, m_BackPointer = backPointer});
            m_Root.m_nodes.Add(new Node { m_data = i++, m_BackPointer = backPointer});

            m_Root.m_nodes[0].m_nodes.Add(new Node { m_data = i++, m_BackPointer = backPointer});
            m_Root.m_nodes[1].m_nodes.Add(new Node { m_data = i++, m_BackPointer = backPointer});

#if UNITY_2019_4_OR_NEWER
            polyMorphic = new Node { m_data = i };
#endif
        }

        public void TestKnownState()
        {
            var i = 0;
            Assert.AreEqual(i++, m_Root.m_data);
            Assert.AreEqual(i++, m_Root.m_nodes[0].m_data);
            Assert.AreEqual(i++, m_Root.m_nodes[1].m_data);
            Assert.AreEqual(i++, m_Root.m_nodes[0].m_nodes[0].m_data);
            Assert.AreEqual(i++, m_Root.m_nodes[1].m_nodes[0].m_data);

#if UNITY_2019_4_OR_NEWER
            Assert.AreEqual(i, ((Node)polyMorphic).m_data);
#endif

            // With SerializeReference Null is supported
            Assert.IsNull(m_Root.m_BackPointer);

#if EXPECT_CYCLES_TO_WORK
            Assert.AreEqual(m_Root, m_Root.m_nodes[0].m_BackPointer);
#else
            Assert.AreEqual(null, m_Root.m_nodes[0].m_BackPointer);
#endif
        }
    }
}
