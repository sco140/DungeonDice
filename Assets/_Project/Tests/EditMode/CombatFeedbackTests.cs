using System.Collections.Generic;
using System.Linq;
using DungeonDice.Combat;
using DungeonDice.Core;
using DungeonDice.Dice;
using DungeonDice.Run;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDice.Tests
{
    /// <summary>Unit events for the screen's feedback, and modifier previews for the Trickster UI.</summary>
    public class CombatFeedbackTests
    {
        private readonly List<ScriptableObject> m_created = new();
        private HeroDefinition m_knight, m_trickster;

        [SetUp]
        public void SetUp()
        {
            var knightDie = Die("Knight", DieColor.Red,
                F("Strike 3", E(FaceEffectKind.Strike, 3)), F("Shield 4", E(FaceEffectKind.Shield, 4)),
                F("Strike 3", E(FaceEffectKind.Strike, 3)), F("Shield 4", E(FaceEffectKind.Shield, 4)),
                F("Taunt + Shield 2", E(FaceEffectKind.Taunt), E(FaceEffectKind.Shield, 2)), F("Strike 5", E(FaceEffectKind.Strike, 5)));
            var tricksterDie = Die("Trickster", DieColor.Yellow,
                F("Nudge ±1", E(FaceEffectKind.Nudge, 1)), F("Nudge ±1", E(FaceEffectKind.Nudge, 1)),
                F("Flip", E(FaceEffectKind.Flip)), F("Reroll ally", E(FaceEffectKind.RerollAlly)),
                F("Mirror", E(FaceEffectKind.Mirror)), F("Jab 2", E(FaceEffectKind.Jab, 2)));
            m_knight = Hero("Knight", DieColor.Red, 30, knightDie);
            m_trickster = Hero("Trickster", DieColor.Yellow, 24, tricksterDie);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) Object.DestroyImmediate(so);
            m_created.Clear();
        }

        private static FaceEffectEntry E(FaceEffectKind k, int v = 0) => new(k, v);
        private static DieFace F(string label, params FaceEffectEntry[] e) => DieFace.Of(label, e);

        private DieDefinition Die(string name, DieColor color, params DieFace[] faces)
        {
            var die = ScriptableObject.CreateInstance<DieDefinition>();
            die.displayName = name; die.color = color; die.SetFaces(faces);
            m_created.Add(die);
            return die;
        }

        private HeroDefinition Hero(string name, DieColor color, int hp, DieDefinition die)
        {
            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            hero.displayName = name; hero.color = color; hero.maxHealth = hp; hero.playableInPrototype = true; hero.dice.Add(die);
            m_created.Add(hero);
            return hero;
        }

        private EnemyDefinition Enemy(int hp, IntentKind kind, int value)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            enemy.displayName = "Enemy"; enemy.maxHealth = hp;
            enemy.intents = new List<EnemyIntent> { new() { kind = kind, value = value } };
            m_created.Add(enemy);
            return enemy;
        }

        private (RunState run, CombatState state) Fight(EnemyDefinition enemy, params HeroDefinition[] party)
        {
            var run = new RunState();
            run.StartNewRun(party, 1);
            return (run, new CombatState(run.Party, new[] { enemy }, new RollService(3)));
        }

        private static void Roll(CombatState state, RunState run, params (int slot, int face)[] faces)
        {
            state.ReceiveRoll(faces.Select(f => (f.slot, new RollResult(run.Party[f.slot].Die, f.face))));
            if (state.Phase == CombatPhase.Reroll) state.KeepAll();
        }

        [Test]
        public void UnitEvents_FireForDamageShieldStatusAndDeath()
        {
            var (run, state) = Fight(Enemy(4, IntentKind.Attack, 5), m_knight, m_knight, m_trickster);
            var events = new List<CombatEvent>();
            state.UnitEvent += events.Add;
            Roll(state, run, (0, 5), (1, 6), (2, 6)); // Taunt + Shield 2, Strike 5, Jab 2

            state.UseDie(0);            // taunt + shield on self (effects resolve in face order)
            Assert.That(events.Select(e => e.Kind), Is.EquivalentTo(new[] { CombatEventKind.Status, CombatEventKind.Shield }));
            var shield = events.First(e => e.Kind == CombatEventKind.Shield);
            Assert.AreEqual(2, shield.Amount);
            Assert.IsFalse(shield.TargetIsEnemy);
            Assert.AreEqual("Taunt", events.First(e => e.Kind == CombatEventKind.Status).Detail);
            events.Clear();

            state.UseDie(1, 0);         // Strike 5 kills the 4-HP enemy
            Assert.AreEqual(CombatEventKind.Damage, events[0].Kind);
            Assert.IsTrue(events[0].TargetIsEnemy);
            Assert.AreEqual(4, events[0].Amount, "only the HP actually lost is reported");
            Assert.AreEqual(CombatEventKind.Death, events[1].Kind);
            run.Clear();
        }

        [Test]
        public void UnitEvents_EnemyAttackReportsDamageTakenAfterShield()
        {
            var (run, state) = Fight(Enemy(50, IntentKind.Attack, 5), m_knight, m_knight, m_trickster);
            var events = new List<CombatEvent>();
            Roll(state, run, (0, 5), (1, 1), (2, 6));
            state.UseDie(0); // taunt + shield 2
            state.UnitEvent += events.Add;
            state.EndHeroTurn();
            var hit = events.First(e => e.Kind == CombatEventKind.Damage);
            Assert.IsFalse(hit.TargetIsEnemy);
            Assert.AreEqual(0, hit.Target, "the taunter was hit");
            Assert.AreEqual(3, hit.Amount, "5 incoming, 2 absorbed");
            Assert.AreEqual("absorbed", hit.Detail);
            run.Clear();
        }

        [Test]
        public void Previews_DescribeFlipNudgeMirrorAndLoadedDice_WithoutChangingAnything()
        {
            var (run, state) = Fight(Enemy(50, IntentKind.Block, 1), m_knight, m_trickster, m_trickster);
            Roll(state, run, (0, 1), (1, 3), (2, 1)); // Knight Strike 3 (face 1), Flip, Nudge

            Assert.AreEqual("Strike 3 → Strike 5", state.PreviewDieAction(1, 0), "flip of face 1 is face 6");
            Assert.AreEqual("Strike 3 → Shield 4", state.PreviewDieAction(2, 0, nudgeDelta: 1));
            Assert.AreEqual("Strike 3 → Strike 3 (no change)", state.PreviewDieAction(2, 0, nudgeDelta: -1), "face 1 nudged down clamps to 1");
            Assert.AreEqual(1, state.Die(0).Result.FaceIndex, "preview did not apply");
            Assert.AreEqual("Strike 3 → Taunt + Shield 2", state.PreviewLoadedDice(0, 5));
            Assert.IsNull(state.PreviewDieAction(1, 1), "a die cannot target itself");

            // Mirror preview from a Mirror face.
            Roll(state, run, (2, 5));
            Assert.That(state.PreviewDieAction(2, 0), Does.Contain("copy of Strike 3"));
            run.Clear();
        }

        [Test]
        public void Flip_OnTheKnightDie_AlwaysChangesTheFace()
        {
            // With the authored face order every opposite pair differs, so Flip is never a no-op.
            var die = m_knight.PrimaryDie;
            for (int face = 1; face <= 6; face++)
                Assert.AreNotEqual(die.GetFace(face).ToString(), die.GetFace(DieDefinition.OppositeFace(face)).ToString(), $"face {face}");
        }
    }
}
