using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;
using DungeonDice.Run;
using DungeonDice.UI;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    public class HeroPickSelectionTests
    {
        private readonly List<ScriptableObject> m_created = new();
        private HeroDefinition m_knight, m_trickster, m_locked;

        [SetUp]
        public void SetUp()
        {
            m_knight = Hero("Knight", true);
            m_trickster = Hero("Trickster", true);
            m_locked = Hero("Rogue", false);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) Object.DestroyImmediate(so);
            m_created.Clear();
        }

        private HeroDefinition Hero(string name, bool playable)
        {
            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            hero.displayName = name;
            hero.playableInPrototype = playable;
            if (playable)
            {
                var die = ScriptableObject.CreateInstance<DieDefinition>();
                die.SetFaces(Enumerable.Range(1, 6).Select(DieFace.Number).ToArray());
                m_created.Add(die);
                hero.dice.Add(die);
            }
            m_created.Add(hero);
            return hero;
        }

        [Test]
        public void Add_AllowsDuplicates_RefusesLocked_StopsAtCapacity()
        {
            var selection = new HeroPickSelection();
            int changes = 0;
            selection.Changed += () => changes++;

            Assert.IsTrue(selection.TryAdd(m_knight));
            Assert.IsTrue(selection.TryAdd(m_knight), "duplicates are allowed");
            Assert.IsFalse(selection.TryAdd(m_locked), "locked heroes are refused");
            Assert.IsFalse(selection.CanConfirm);
            Assert.IsTrue(selection.TryAdd(m_trickster));
            Assert.IsTrue(selection.IsFull);
            Assert.IsTrue(selection.CanConfirm);
            Assert.IsFalse(selection.TryAdd(m_trickster), "a fourth pick is refused");
            Assert.AreEqual(3, changes, "Changed fires once per successful add");
            Assert.AreEqual(new[] { "Knight", "Knight", "Trickster" }, selection.Party.Select(h => h.displayName).ToArray());
        }

        [Test]
        public void RemoveAt_FreesTheSlot()
        {
            var selection = new HeroPickSelection();
            selection.TryAdd(m_knight);
            selection.TryAdd(m_trickster);

            Assert.IsFalse(selection.RemoveAt(5));
            Assert.IsTrue(selection.RemoveAt(0));
            Assert.AreEqual(1, selection.Party.Count);
            Assert.AreSame(m_trickster, selection.Party[0]);
        }

        [Test]
        public void IsPickable_RequiresPlayableAndADie()
        {
            Assert.IsTrue(HeroPickSelection.IsPickable(m_knight));
            Assert.IsFalse(HeroPickSelection.IsPickable(m_locked));
            Assert.IsFalse(HeroPickSelection.IsPickable(null));

            var noDie = ScriptableObject.CreateInstance<HeroDefinition>();
            noDie.playableInPrototype = true;
            m_created.Add(noDie);
            Assert.IsFalse(HeroPickSelection.IsPickable(noDie), "a playable hero without a die cannot be picked");
        }
    }
}
