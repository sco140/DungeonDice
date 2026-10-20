using System.Linq;
using DungeonDice.Map;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    public class MapLayoutTests
    {
        private ActDefinition m_act;

        [SetUp]
        public void SetUp()
        {
            m_act = ScriptableObject.CreateInstance<ActDefinition>();
            m_act.startNode = 0;
            m_act.nodes = PrototypeAct.BuildNodes();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(m_act);

        [Test]
        public void PrototypeAct_LaysOutLeftToRight_WithBossLast()
        {
            var layout = MapLayout.Compute(m_act);
            Assert.AreEqual(m_act.nodes.Count, layout.Cells.Count, "every node placed once");
            Assert.AreEqual(0, layout.For(0).Column, "start is the first column");
            int bossIndex = m_act.nodes.FindIndex(n => n.type == NodeType.Boss);
            Assert.AreEqual(layout.Columns - 1, layout.For(bossIndex).Column, "boss is the last column");

            // Every edge goes strictly rightwards.
            foreach (var (from, to, _) in MapLayout.Edges(m_act))
                Assert.Less(layout.For(from).Column, layout.For(to).Column, $"edge {from}->{to}");

            // The three first-ring nodes share a column with distinct rows.
            var ring = new[] { 1, 2, 3 }.Select(layout.For).ToList();
            Assert.IsTrue(ring.All(c => c.Column == 1));
            Assert.AreEqual(3, ring.Select(c => c.Row).Distinct().Count());
            Assert.AreEqual(3, layout.Rows);
        }

        [Test]
        public void Edges_CarryForkPaths()
        {
            var edges = MapLayout.Edges(m_act).ToList();
            var fromStart = edges.Where(e => e.from == 0).ToList();
            Assert.AreEqual(3, fromStart.Count);
            Assert.IsTrue(fromStart.All(e => e.path != null));
            var single = edges.First(e => e.from == 1);
            Assert.IsNull(single.path);
            Assert.AreEqual(4, single.to);
        }

        [Test]
        public void LongestPath_PutsSkippedNodesBeforeTheirSkipTarget()
        {
            // Node 8 forks to 9 (then 10) or straight to 10: 10 must sit right of 9.
            var layout = MapLayout.Compute(m_act);
            Assert.Greater(layout.For(10).Column, layout.For(9).Column);
        }
    }
}
