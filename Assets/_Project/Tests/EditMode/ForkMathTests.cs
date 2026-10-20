using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;
using DungeonDice.Map;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    public class ForkMathTests
    {
        private readonly List<ScriptableObject> m_created = new();
        private DieDefinition m_pilgrim, m_lowRoad, m_wild;

        [SetUp]
        public void SetUp()
        {
            m_pilgrim = Die(1, 2, 3, 4, 5, 6);
            m_lowRoad = Die(1, 1, 2, 2, 3, 3);
            m_wild = Die(1, 1, 1, 6, 6, 6);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) Object.DestroyImmediate(so);
            m_created.Clear();
        }

        private DieDefinition Die(params int[] pips) => TestDice.Numbers(m_created, string.Join(",", pips), pips);

        /// <summary>2–3 dice: node 1 always open, node 2 at 6+, node 3 at 11+.</summary>
        private static Fork ExampleFork() => new()
        {
            minDice = 2, maxDice = 3,
            paths = new List<ForkPath>
            {
                new() { threshold = 2, targetNode = 1 },
                new() { threshold = 6, targetNode = 2 },
                new() { threshold = 11, targetNode = 3 },
            },
        };

        // --- odds ------------------------------------------------------------------

        [Test]
        public void Distribution_OneFairDie_IsUniform()
        {
            var d = ForkOdds.Distribution(new[] { m_pilgrim });
            Assert.AreEqual(6, d.Count);
            foreach (var p in d.Values) Assert.AreEqual(1.0 / 6, p, 1e-9);
        }

        [Test]
        public void Distribution_TwoLowRoads_SumsToOne_AndPeaksAtFour()
        {
            var d = ForkOdds.Distribution(new[] { m_lowRoad, m_lowRoad });
            Assert.AreEqual(1.0, d.Values.Sum(), 1e-9);
            Assert.AreEqual(2, d.Keys.Min());
            Assert.AreEqual(6, d.Keys.Max());
            Assert.AreEqual(3.0 / 9, d[4], 1e-9, "1+3, 2+2, 3+1 each 1/9");
        }

        [Test]
        public void PathProbabilities_AreUnlockChances_NotExclusive()
        {
            // Two Low Roads: totals 2–6. The 6+ path opens only on 3+3 (1/9); 11+ never.
            var odds = ForkOdds.PathProbabilities(ExampleFork(), new[] { m_lowRoad, m_lowRoad });
            Assert.AreEqual(1.0, odds[0], 1e-9, "the lowest path is always open");
            Assert.AreEqual(1.0 / 9, odds[1], 1e-9);
            Assert.AreEqual(0.0, odds[2], 1e-9);
        }

        [Test]
        public void PathProbabilities_ThreeWild_IsAllOrNothing()
        {
            var odds = ForkOdds.PathProbabilities(ExampleFork(), new[] { m_wild, m_wild, m_wild });
            // totals: 3 (1/8), 8 (3/8), 13 (3/8), 18 (1/8)
            Assert.AreEqual(1.0, odds[0], 1e-9);
            Assert.AreEqual(7.0 / 8, odds[1], 1e-9, "6+ on anything but 1+1+1");
            Assert.AreEqual(4.0 / 8, odds[2], 1e-9, "11+ on 13 or 18");
        }

        [Test]
        public void Mean_FollowsFaces()
        {
            Assert.AreEqual(3.5, ForkOdds.Mean(m_pilgrim), 1e-9);
            Assert.AreEqual(2.0, ForkOdds.Mean(m_lowRoad), 1e-9);
            Assert.AreEqual(3.5, ForkOdds.Mean(m_wild), 1e-9);
        }

        // --- resolver --------------------------------------------------------------

        [TestCase(2, new[] { 1 })]
        [TestCase(5, new[] { 1 })]
        [TestCase(6, new[] { 1, 2 })]
        [TestCase(10, new[] { 1, 2 })]
        [TestCase(11, new[] { 1, 2, 3 })]
        [TestCase(18, new[] { 1, 2, 3 })]
        public void Unlocked_OpensEveryPathAtOrBelowTheTotal(int total, int[] expectedNodes)
        {
            Assert.AreEqual(expectedNodes, ForkResolver.UnlockedTargets(ExampleFork(), total).ToArray());
        }

        [Test]
        public void Unlocked_ClampsStrayTotalsToTheEasiestPath()
        {
            Assert.AreEqual(new[] { 1 }, ForkResolver.UnlockedTargets(ExampleFork(), 0).ToArray());
            Assert.IsEmpty(ForkResolver.Unlocked(null, 5));
        }

        // --- validator -------------------------------------------------------------

        [Test]
        public void Validator_AcceptsAForkWithAnAlwaysOpenPath()
        {
            Assert.IsEmpty(ForkValidator.ValidateFork(ExampleFork(), nodeCount: 4));
        }

        [Test]
        public void Validator_FindsStrandingUnreachableAndMissingTargets()
        {
            var fork = ExampleFork();
            fork.paths[0].threshold = 3;         // nothing opens on a total of 2
            fork.paths[2].threshold = 19;        // beyond 3×6
            fork.paths[2].targetNode = 42;       // missing node

            var errors = ForkValidator.ValidateFork(fork, nodeCount: 4);
            Assert.That(errors.Any(e => e.Contains("no path unlocks on the lowest total 2")), string.Join("\n", errors));
            Assert.That(errors.Any(e => e.Contains("19+ is unreachable")), string.Join("\n", errors));
            Assert.That(errors.Any(e => e.Contains("missing node 42")), string.Join("\n", errors));
        }

        [Test]
        public void Validator_RejectsSinglePathAndBadDiceRange()
        {
            var fork = new Fork { minDice = 3, maxDice = 2, paths = new List<ForkPath> { new() { threshold = 3, targetNode = 0 } } };
            var errors = ForkValidator.ValidateFork(fork, nodeCount: 1);
            Assert.That(errors.Any(e => e.Contains("at least two paths")));
            Assert.That(errors.Any(e => e.Contains("dice range")));
        }

        [Test]
        public void PrototypeAct_IsValid_HasOneBoss_AndEveryNodeIsReachable()
        {
            var act = ScriptableObject.CreateInstance<ActDefinition>();
            m_created.Add(act);
            act.startNode = 0;
            act.nodes = PrototypeAct.BuildNodes();

            Assert.IsEmpty(ForkValidator.ValidateAct(act), string.Join("\n", ForkValidator.ValidateAct(act)));
            Assert.AreEqual(1, act.nodes.Count(n => n.type == NodeType.Boss));
            Assert.AreEqual(3, act.nodes.Count(n => n.HasFork), "three forks");
            Assert.That(act.nodes.Count, Is.InRange(8, 12));

            // Reachability from the start node.
            var reached = new HashSet<int>();
            var frontier = new Stack<int>();
            frontier.Push(act.startNode);
            while (frontier.Count > 0)
            {
                int i = frontier.Pop();
                if (!reached.Add(i)) continue;
                var node = act.nodes[i];
                if (node.HasFork) foreach (var p in node.fork.paths) frontier.Push(p.targetNode);
                else if (node.nextNode >= 0) frontier.Push(node.nextNode);
            }
            Assert.AreEqual(act.nodes.Count, reached.Count, "every node is reachable");
        }

        [Test]
        public void Label_ReadsAsThreshold()
        {
            var fork = ExampleFork();
            Assert.AreEqual("any", fork.paths[0].Label(fork));
            Assert.AreEqual("6+", fork.paths[1].Label(fork));
            Assert.AreEqual("11+", fork.paths[2].Label(fork));
        }
    }
}
