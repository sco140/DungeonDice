using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    public class RollResultTests
    {
        private readonly List<ScriptableObject> m_created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) UnityEngine.Object.DestroyImmediate(so);
            m_created.Clear();
        }

        private DieDefinition NumberDie(params int[] pips)
        {
            var die = ScriptableObject.CreateInstance<DieDefinition>();
            die.displayName = string.Join(",", pips);
            die.SetFaces(pips.Select(DieFace.Number).ToArray());
            m_created.Add(die);
            return die;
        }

        private DieDefinition Pilgrim() => NumberDie(1, 2, 3, 4, 5, 6);

        [Test]
        public void Flip_MapsEveryFaceToItsOpposite()
        {
            var die = Pilgrim();
            for (int face = 1; face <= 6; face++)
            {
                var result = new RollResult(die, face);
                result.Apply(new FlipModifier());
                Assert.AreEqual(7 - face, result.FaceIndex, $"Flip of {face}");
                Assert.AreEqual(face, result.RolledFaceIndex, "physical read must be preserved");
            }
        }

        [Test]
        public void Flip_Twice_RestoresOriginal()
        {
            var result = new RollResult(Pilgrim(), 2);
            result.Apply(new FlipModifier());
            result.Apply(new FlipModifier());
            Assert.AreEqual(2, result.FaceIndex);
            Assert.IsFalse(result.WasModified);
            Assert.AreEqual(2, result.Applied.Count);
        }

        [TestCase(3, 1, 4)]
        [TestCase(3, -1, 2)]
        [TestCase(6, 1, 6)]
        [TestCase(1, -1, 1)]
        [TestCase(2, 10, 6)]
        public void Nudge_ShiftsAndClampsToOneThroughSix(int rolled, int delta, int expected)
        {
            var result = new RollResult(Pilgrim(), rolled);
            result.Apply(new NudgeModifier(delta));
            Assert.AreEqual(expected, result.FaceIndex);
        }

        [Test]
        public void ChooseResult_OverridesRolledFace()
        {
            var result = new RollResult(Pilgrim(), 1);
            result.Apply(new ChooseResultModifier(6));
            Assert.AreEqual(6, result.FaceIndex);
            Assert.IsTrue(result.WasModified);
        }

        [Test]
        public void ChooseResult_RejectsInvalidFace()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChooseResultModifier(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChooseResultModifier(7));
        }

        [Test]
        public void Lock_SetsLockedWithoutChangingFace()
        {
            var result = new RollResult(Pilgrim(), 4);
            result.Apply(new LockModifier());
            Assert.IsTrue(result.Locked);
            Assert.AreEqual(4, result.FaceIndex);
            Assert.IsFalse(result.WasModified);
        }

        [Test]
        public void Pips_FollowTheDieFaceTable_NotTheFaceIndex()
        {
            var lowRoad = NumberDie(1, 1, 2, 2, 3, 3);
            Assert.AreEqual(1, new RollResult(lowRoad, 2).Pips);
            Assert.AreEqual(2, new RollResult(lowRoad, 4).Pips);
            Assert.AreEqual(3, new RollResult(lowRoad, 6).Pips);

            var wild = NumberDie(1, 1, 1, 6, 6, 6);
            var flipped = new RollResult(wild, 1);
            flipped.Apply(new FlipModifier());
            Assert.AreEqual(6, flipped.Pips, "Flip on Wild goes from a 1 to a 6");
        }

        [Test]
        public void RollCompletedEvent_TotalsPipsAcrossDice()
        {
            var steady = NumberDie(3, 3, 3, 4, 4, 4);
            var highRoad = NumberDie(4, 4, 5, 5, 6, 6);
            var evt = new RollCompletedEvent(new[]
            {
                new RollResult(steady, 1),   // 3
                new RollResult(steady, 6),   // 4
                new RollResult(highRoad, 5), // 6
            });
            Assert.AreEqual(13, evt.TotalPips);
        }

        [Test]
        public void RollResult_RejectsInvalidFaceIndexAndNullDie()
        {
            var die = Pilgrim();
            Assert.Throws<ArgumentOutOfRangeException>(() => new RollResult(die, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RollResult(die, 7));
            Assert.Throws<ArgumentNullException>(() => new RollResult(null, 1));
        }

        [Test]
        public void WithFaceReplaced_ReturnsCopyAndLeavesOriginalUntouched()
        {
            var steady = NumberDie(3, 3, 3, 4, 4, 4);
            var upgraded = steady.WithFaceReplaced(1, DieFace.Number(6));
            m_created.Add(upgraded);

            Assert.AreNotSame(steady, upgraded);
            Assert.AreEqual(6, upgraded.Pips(1));
            Assert.AreEqual(3, steady.Pips(1), "original must not change");
            Assert.AreEqual(3, upgraded.Pips(2), "other faces are copied");
        }

        [Test]
        public void ResourceDie_FacesYieldColours_AndFaceIndexOfColorFindsThem()
        {
            var resource = TestDice.Resource(m_created);
            Assert.IsTrue(resource.IsResource);
            Assert.IsFalse(resource.IsTraversal);
            Assert.AreEqual(DieColor.Red, new RollResult(resource, 2).ManaColor);
            Assert.AreEqual(DieColor.Blue, new RollResult(resource, 3).ManaColor);
            Assert.AreEqual(DieColor.Yellow, new RollResult(resource, 6).ManaColor);
            Assert.AreEqual(0, new RollResult(resource, 6).Pips, "colour faces carry no pips");
            Assert.AreEqual(DieColor.None, new RollResult(resource, 1).Color, "the die itself is uncoloured");
            Assert.AreEqual(1, resource.FaceIndexOfColor(DieColor.Red));
            Assert.AreEqual(3, resource.FaceIndexOfColor(DieColor.Blue));
            Assert.AreEqual(5, resource.FaceIndexOfColor(DieColor.Yellow));
            Assert.AreEqual(0, resource.FaceIndexOfColor(DieColor.None));
            Assert.AreEqual(DieColor.None, Pilgrim().GetFace(1).ManaColor);
        }

        [Test]
        public void OppositeFace_SumsToSeven()
        {
            for (int face = 1; face <= 6; face++)
                Assert.AreEqual(7, face + DieDefinition.OppositeFace(face));
        }
    }
}
