using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using DungeonDice.Dice;
using DungeonDice.Run;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    public class RewardTests
    {
        private readonly List<ScriptableObject> m_created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) UnityEngine.Object.DestroyImmediate(so);
            m_created.Clear();
        }

        private RunState Run(int resourceDice = RunState.DefaultResourceDice)
        {
            var run = new RunState();
            run.StartNewRun(new[] { TestHeroes.Simple(m_created, "A"), TestHeroes.Simple(m_created, "B"), TestHeroes.Simple(m_created, "C") }, 5, resourceDice);
            return run;
        }

        [Test]
        public void Generate_OffersOneTraversalDie_AndAResourceDieWhileUnderTheCap()
        {
            var run = Run();
            var pool = new[] { TestDice.Numbers(m_created, "Low", 1, 1, 2, 2, 3, 3), TestDice.Numbers(m_created, "Pilgrim", 1, 2, 3, 4, 5, 6) };

            var offer = RewardOffer.Generate(run, pool, new RollService(11));
            Assert.AreEqual(2, offer.Options.Count);
            Assert.AreEqual(RewardKind.TraversalDie, offer.Options[0].Kind);
            Assert.That(pool, Does.Contain(offer.Options[0].Die));
            Assert.AreEqual(RewardKind.ResourceDie, offer.Options[1].Kind);
            Assert.That(offer.Options[1].Title, Does.Contain("#7"));
            run.Clear();
        }

        [Test]
        public void Generate_SkipsTheResourceDie_AtTheCap()
        {
            var run = Run(RunState.MaxResourceDice);
            var offer = RewardOffer.Generate(run, new[] { TestDice.Numbers(m_created, "Low", 1, 1, 2, 2, 3, 3) }, new RollService(1));
            Assert.AreEqual(1, offer.Options.Count);
            Assert.AreEqual(RewardKind.TraversalDie, offer.Options[0].Kind);
            run.Clear();
        }

        [Test]
        public void Apply_TraversalDie_AddsToTraversalPool()
        {
            var run = Run();
            var pilgrim = TestDice.Numbers(m_created, "Pilgrim", 1, 2, 3, 4, 5, 6);
            run.SetTraversalDice(new[] { TestDice.Numbers(m_created, "Low", 1, 1, 2, 2, 3, 3) });

            var offer = RewardOffer.Generate(run, new[] { pilgrim }, new RollService(3));
            string summary = offer.Apply(0, run);
            Assert.AreEqual(2, run.TraversalDice.Count);
            Assert.AreSame(pilgrim, run.TraversalDice[1]);
            Assert.That(summary, Does.Contain("Pilgrim"));
            Assert.AreEqual(RunState.DefaultResourceDice, run.ResourceDiceCount, "the resource count is untouched");
            run.Clear();
        }

        [Test]
        public void Apply_ResourceDie_RaisesTheCount_UpToTheCap()
        {
            var run = Run(RunState.MaxResourceDice - 1);
            var offer = RewardOffer.Generate(run, Array.Empty<DieDefinition>(), new RollService(3));
            Assert.AreEqual(1, offer.Options.Count);
            Assert.AreEqual(RewardKind.ResourceDie, offer.Options[0].Kind);

            string summary = offer.Apply(0, run);
            Assert.AreEqual(RunState.MaxResourceDice, run.ResourceDiceCount);
            Assert.That(summary, Does.Contain("eighth"));
            Assert.IsFalse(run.AddResourceDie(), "capped");
            Assert.AreEqual(0, RewardOffer.Generate(run, Array.Empty<DieDefinition>(), new RollService(3)).Options.Count, "nothing left to offer");
            Assert.Throws<ArgumentOutOfRangeException>(() => offer.Apply(5, run));
            run.Clear();
        }

        [Test]
        public void Generate_IsDeterministicForASeed()
        {
            var run = Run();
            var pool = new[] { TestDice.Numbers(m_created, "Low", 1, 1, 2, 2, 3, 3), TestDice.Numbers(m_created, "High", 4, 4, 5, 5, 6, 6), TestDice.Numbers(m_created, "Wild", 1, 1, 1, 6, 6, 6) };

            string Describe(int seed) => string.Join(" | ", RewardOffer.Generate(run, pool, new RollService(seed)).Options.Select(o => $"{o.Title}:{o.Description}"));
            Assert.AreEqual(Describe(42), Describe(42));
            run.Clear();
        }
    }
}
