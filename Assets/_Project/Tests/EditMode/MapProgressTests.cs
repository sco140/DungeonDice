using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;
using DungeonDice.Map;
using DungeonDice.Run;
using DungeonDice.UI;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    /// <summary>RunState's map progress and the traversal pick rules.</summary>
    public class MapProgressTests
    {
        private readonly List<ScriptableObject> m_created = new();
        private ActDefinition m_act;

        [SetUp]
        public void SetUp()
        {
            m_act = ScriptableObject.CreateInstance<ActDefinition>();
            m_act.startNode = 0;
            m_act.nodes = PrototypeAct.BuildNodes();
            m_created.Add(m_act);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) UnityEngine.Object.DestroyImmediate(so);
            m_created.Clear();
        }

        private DieDefinition Die(string name)
        {
            var die = ScriptableObject.CreateInstance<DieDefinition>();
            die.displayName = name;
            die.SetFaces(Enumerable.Range(1, 6).Select(DieFace.Number).ToArray());
            m_created.Add(die);
            return die;
        }

        private HeroDefinition Hero(string name)
        {
            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            hero.displayName = name;
            hero.playableInPrototype = true;
            hero.dice.Add(Die(name + " die"));
            m_created.Add(hero);
            return hero;
        }

        private RunState ActiveRun()
        {
            var run = new RunState();
            run.StartNewRun(new[] { Hero("A"), Hero("B"), Hero("C") }, 1);
            return run;
        }

        [Test]
        public void EnterAct_StartsAtAResolvedStartNode()
        {
            var run = ActiveRun();
            run.EnterAct(m_act);

            Assert.AreEqual(RunPhase.Map, run.Phase);
            Assert.AreEqual(0, run.CurrentNodeIndex);
            Assert.AreEqual(NodeType.Start, run.CurrentNode.type);
            Assert.IsTrue(run.CurrentNodeResolved, "the Start node has no content");
            Assert.IsTrue(run.CurrentNode.HasFork);
            Assert.AreEqual(new[] { 0 }, run.VisitedNodes.ToArray());
            run.Clear();
        }

        [Test]
        public void MoveTo_MarksVisited_AndNeedsResolving()
        {
            var run = ActiveRun();
            run.EnterAct(m_act);

            run.MoveTo(2);
            Assert.AreEqual(2, run.CurrentNodeIndex);
            Assert.IsFalse(run.CurrentNodeResolved);
            Assert.That(run.VisitedNodes, Is.EquivalentTo(new[] { 0, 2 }));

            run.ResolveCurrentNode();
            Assert.IsTrue(run.CurrentNodeResolved);
            Assert.AreEqual(4, run.CurrentNode.nextNode, "Combat at node 2 leads to the Rest at node 4");

            Assert.Throws<ArgumentOutOfRangeException>(() => run.MoveTo(99));
            run.Clear();
        }

        [Test]
        public void EndRun_SetsOutcomeAndPhase_ClearResetsThem()
        {
            var run = ActiveRun();
            run.EnterAct(m_act);
            run.EndRun(RunOutcome.Victory);
            Assert.AreEqual(RunOutcome.Victory, run.Outcome);
            Assert.AreEqual(RunPhase.RunEnd, run.Phase);

            run.Clear();
            Assert.AreEqual(RunOutcome.None, run.Outcome);
            Assert.IsNull(run.Act);
            Assert.IsNull(run.CurrentNode);
        }

        [Test]
        public void Heal_DoesNothingForTheDead()
        {
            var run = ActiveRun();
            run.Party[0].TakeDamage(999);
            Assert.AreEqual(0, run.Party[0].Heal(10));
            Assert.IsFalse(run.Party[0].IsAlive);
            run.Clear();
        }

        [Test]
        public void TraversalPick_ThreeDistinctFromPool_ToggleRemoves()
        {
            var pool = new[] { Die("Low"), Die("Steady"), Die("High"), Die("Wild") };
            var pick = new TraversalPickSelection(pool, 3);
            int changes = 0;
            pick.Changed += () => changes++;

            Assert.IsTrue(pick.Toggle(pool[0]));
            Assert.IsTrue(pick.Toggle(pool[1]));
            Assert.IsTrue(pick.Toggle(pool[2]));
            Assert.IsTrue(pick.CanConfirm);
            Assert.IsFalse(pick.Toggle(pool[3]), "a fourth distinct die is refused");
            Assert.IsFalse(pick.Toggle(Die("Stranger")), "dice outside the pool are refused");

            Assert.IsTrue(pick.Toggle(pool[1]), "toggling a chosen die removes it");
            Assert.IsFalse(pick.IsChosen(pool[1]));
            Assert.IsFalse(pick.CanConfirm);
            Assert.IsTrue(pick.Toggle(pool[3]));
            Assert.AreEqual(new[] { "Low", "High", "Wild" }, pick.Chosen.Select(d => d.displayName).ToArray());
            Assert.AreEqual(5, changes);
        }
    }
}
