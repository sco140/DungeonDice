using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Run;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    public class RunStateTests
    {
        private readonly List<ScriptableObject> m_created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) UnityEngine.Object.DestroyImmediate(so);
            m_created.Clear();
        }

        private HeroDefinition Hero(string name, bool playable = true, int hp = 20) => TestHeroes.Simple(m_created, name, hp, playable);

        [Test]
        public void StartNewRun_BuildsPartyWithFullHealth_AndTheDefaultResourceDice()
        {
            var run = new RunState();
            var knight = Hero("Knight", hp: 30);

            run.StartNewRun(new[] { knight, Hero("Arcanist"), Hero("Trickster") }, seed: 42);

            Assert.IsTrue(run.IsActive);
            Assert.AreEqual(42, run.Seed);
            Assert.AreEqual(RunPhase.TraversalPick, run.Phase);
            Assert.AreEqual(3, run.Party.Count);
            Assert.AreEqual(30, run.Party[0].CurrentHealth);
            Assert.AreEqual(new[] { 0, 1, 2 }, run.Party.Select(h => h.Slot).ToArray());
            Assert.AreSame(knight, run.Party[0].Definition);
            Assert.AreEqual(RunState.DefaultResourceDice, run.ResourceDiceCount);
            run.Clear();
        }

        [Test]
        public void ResourceDice_StartFromTheRules_AndAreCapped()
        {
            var run = new RunState();
            run.StartNewRun(new[] { Hero("A"), Hero("B"), Hero("C") }, 1, resourceDice: 7);
            Assert.AreEqual(7, run.ResourceDiceCount);
            Assert.IsTrue(run.CanAddResourceDie);
            Assert.IsTrue(run.AddResourceDie());
            Assert.AreEqual(RunState.MaxResourceDice, run.ResourceDiceCount);
            Assert.IsFalse(run.AddResourceDie());
            Assert.AreEqual(RunState.MaxResourceDice, run.ResourceDiceCount);

            run.StartNewRun(new[] { Hero("A"), Hero("B"), Hero("C") }, 1, resourceDice: 99);
            Assert.AreEqual(RunState.MaxResourceDice, run.ResourceDiceCount, "clamped on start");
            run.Clear();
        }

        [Test]
        public void StartNewRun_RejectsWrongSizeAndLockedHeroes()
        {
            var run = new RunState();
            var a = Hero("A");
            var b = Hero("B");
            var locked = Hero("Locked", playable: false);

            Assert.Throws<ArgumentException>(() => run.StartNewRun(new[] { a, b }, 1));
            Assert.Throws<ArgumentException>(() => run.StartNewRun(new[] { a, b, locked }, 1));
            Assert.Throws<ArgumentException>(() => run.StartNewRun(new[] { a, b, null }, 1));
            Assert.IsFalse(run.IsActive);
        }

        [Test]
        public void HeroHealth_ClampsAndReportsWipe()
        {
            var run = new RunState();
            run.StartNewRun(new[] { Hero("A", hp: 10), Hero("B", hp: 10), Hero("C", hp: 10) }, 7);

            Assert.AreEqual(10, run.Party[0].TakeDamage(99));
            Assert.IsFalse(run.Party[0].IsAlive);
            Assert.AreEqual(2, run.LivingHeroes.Count());
            Assert.AreEqual(0, run.Party[0].Heal(-5), "negative heal is ignored");
            Assert.AreEqual(3, run.Party[1].TakeDamage(3));
            Assert.AreEqual(3, run.Party[1].Heal(50), "heal clamps to max");
            Assert.IsFalse(run.PartyWiped);

            run.Party[1].TakeDamage(10);
            run.Party[2].TakeDamage(10);
            Assert.IsTrue(run.PartyWiped);

            run.Clear();
        }

        [Test]
        public void SameSeed_GivesSameRngSequence()
        {
            var a = new RunState();
            var b = new RunState();
            var heroes = new[] { Hero("A"), Hero("B"), Hero("C") };
            a.StartNewRun(heroes, 1234);
            b.StartNewRun(heroes, 1234);

            var seqA = Enumerable.Range(0, 20).Select(_ => a.Rng.Next(0, 1000)).ToArray();
            var seqB = Enumerable.Range(0, 20).Select(_ => b.Rng.Next(0, 1000)).ToArray();
            Assert.AreEqual(seqA, seqB);

            a.Clear();
            b.Clear();
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var run = new RunState();
            run.StartNewRun(new[] { Hero("A"), Hero("B"), Hero("C") }, 5, resourceDice: 7);
            run.SetTraversalDice(new[] { TestDice.Numbers(m_created, "T", 1, 2, 3, 4, 5, 6) });

            run.Clear();

            Assert.IsFalse(run.IsActive);
            Assert.AreEqual(0, run.Party.Count);
            Assert.AreEqual(0, run.TraversalDice.Count);
            Assert.AreEqual(RunState.DefaultResourceDice, run.ResourceDiceCount);
            Assert.AreEqual(0, run.CurrentNodeIndex);
            Assert.AreEqual(RunPhase.HeroPick, run.Phase);
            Assert.IsFalse(run.PartyWiped, "an inactive run is not a wipe");
        }
    }
}
