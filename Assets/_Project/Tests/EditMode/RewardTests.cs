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

        private DieDefinition NumberDie(string name, params int[] pips)
        {
            var die = ScriptableObject.CreateInstance<DieDefinition>();
            die.displayName = name;
            die.SetFaces(pips.Select(DieFace.Number).ToArray());
            m_created.Add(die);
            return die;
        }

        private HeroDefinition Hero(string name, int upgrades)
        {
            var die = ScriptableObject.CreateInstance<DieDefinition>();
            die.displayName = name + " die";
            die.SetFaces(Enumerable.Range(1, 6).Select(i => DieFace.Of($"Strike {i}", new FaceEffectEntry(FaceEffectKind.Strike, i))).ToArray());
            m_created.Add(die);

            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            hero.displayName = name;
            hero.playableInPrototype = true;
            hero.dice.Add(die);
            for (int u = 0; u < upgrades; u++)
                hero.faceUpgrades.Add(new FaceUpgrade { faceIndex = u + 1, replacement = DieFace.Of($"Strike {10 + u}", new FaceEffectEntry(FaceEffectKind.Strike, 10 + u)) });
            m_created.Add(hero);
            return hero;
        }

        [Test]
        public void Generate_OffersOneDie_AndUpgradesForUpToTwoLivingHeroes()
        {
            var run = new RunState();
            run.StartNewRun(new[] { Hero("A", 2), Hero("B", 1), Hero("C", 3) }, 5);
            var pool = new[] { NumberDie("Low", 1, 1, 2, 2, 3, 3), NumberDie("Pilgrim", 1, 2, 3, 4, 5, 6) };

            var offer = RewardOffer.Generate(run, pool, new RollService(11));
            Assert.AreEqual(3, offer.Options.Count);
            Assert.AreEqual(1, offer.Options.Count(o => o.Kind == RewardKind.TraversalDie));
            Assert.AreEqual(2, offer.Options.Count(o => o.Kind == RewardKind.FaceUpgrade));
            Assert.That(pool, Does.Contain(offer.Options[0].Die));
            Assert.AreEqual(2, offer.Options.Where(o => o.Kind == RewardKind.FaceUpgrade).Select(o => o.HeroSlot).Distinct().Count(), "one upgrade per hero");
            run.Clear();
        }

        [Test]
        public void Generate_SkipsDeadHeroes_AndHeroesWithNoUpgradesLeft()
        {
            var run = new RunState();
            run.StartNewRun(new[] { Hero("A", 1), Hero("B", 1), Hero("C", 0) }, 5);
            run.Party[0].TakeDamage(999);
            var pool = new[] { NumberDie("Low", 1, 1, 2, 2, 3, 3) };

            var offer = RewardOffer.Generate(run, pool, new RollService(1));
            var upgrades = offer.Options.Where(o => o.Kind == RewardKind.FaceUpgrade).ToList();
            Assert.AreEqual(1, upgrades.Count, "only B qualifies: A is dead, C has no upgrades");
            Assert.AreEqual(1, upgrades[0].HeroSlot);

            offer.Apply(offer.Options.ToList().IndexOf(upgrades[0]), run);
            var again = RewardOffer.Generate(run, pool, new RollService(2));
            Assert.IsFalse(again.Options.Any(o => o.Kind == RewardKind.FaceUpgrade), "B's only upgrade is taken");
            Assert.AreEqual(1, again.Options.Count, "the die is still offered");
            run.Clear();
        }

        [Test]
        public void Apply_Die_AddsToTraversalPool()
        {
            var run = new RunState();
            run.StartNewRun(new[] { Hero("A", 0), Hero("B", 0), Hero("C", 0) }, 5);
            var pilgrim = NumberDie("Pilgrim", 1, 2, 3, 4, 5, 6);
            run.SetTraversalDice(new[] { NumberDie("Low", 1, 1, 2, 2, 3, 3) });

            var offer = RewardOffer.Generate(run, new[] { pilgrim }, new RollService(3));
            string summary = offer.Apply(0, run);
            Assert.AreEqual(2, run.TraversalDice.Count);
            Assert.AreSame(pilgrim, run.TraversalDice[1]);
            Assert.That(summary, Does.Contain("Pilgrim"));
            run.Clear();
        }

        [Test]
        public void Apply_Upgrade_ChangesTheHeroDieCopyOnly_AndCannotRepeat()
        {
            var run = new RunState();
            var hero = Hero("A", 1);
            run.StartNewRun(new[] { hero, Hero("B", 0), Hero("C", 0) }, 5);

            var offer = RewardOffer.Generate(run, Array.Empty<DieDefinition>(), new RollService(3));
            Assert.AreEqual(1, offer.Options.Count);
            var option = offer.Options[0];
            Assert.AreEqual(RewardKind.FaceUpgrade, option.Kind);
            Assert.That(option.Description, Does.Contain("Strike 1 → Strike 10"));

            string summary = offer.Apply(0, run);
            Assert.AreEqual(10, run.Party[0].Die.GetFace(1).effects[0].value);
            Assert.AreEqual(1, hero.PrimaryDie.GetFace(1).effects[0].value, "asset untouched");
            Assert.That(summary, Does.Contain("upgraded"));
            Assert.AreEqual(new[] { 0 }, run.Party[0].AppliedUpgrades.ToArray());
            Assert.Throws<InvalidOperationException>(() => run.Party[0].ApplyUpgrade(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => offer.Apply(5, run));
            run.Clear();
        }

        [Test]
        public void Generate_IsDeterministicForASeed()
        {
            var run = new RunState();
            run.StartNewRun(new[] { Hero("A", 3), Hero("B", 3), Hero("C", 3) }, 5);
            var pool = new[] { NumberDie("Low", 1, 1, 2, 2, 3, 3), NumberDie("High", 4, 4, 5, 5, 6, 6), NumberDie("Wild", 1, 1, 1, 6, 6, 6) };

            string Describe(int seed) => string.Join(" | ", RewardOffer.Generate(run, pool, new RollService(seed)).Options.Select(o => $"{o.Title}:{o.Description}"));
            Assert.AreEqual(Describe(42), Describe(42));
            run.Clear();
        }
    }
}
