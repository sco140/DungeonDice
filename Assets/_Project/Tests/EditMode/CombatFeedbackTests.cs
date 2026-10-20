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
    /// <summary>Unit events the screen animates from, and the combat log.</summary>
    public class CombatFeedbackTests
    {
        private readonly List<ScriptableObject> m_created = new();
        private DieDefinition m_resource;
        private HeroDefinition m_knight, m_arcanist;

        [SetUp]
        public void SetUp()
        {
            m_resource = TestDice.Resource(m_created);
            m_knight = TestHeroes.Knight(m_created);
            m_arcanist = TestHeroes.Arcanist(m_created);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) Object.DestroyImmediate(so);
            m_created.Clear();
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
            return (run, new CombatState(run.Party, new[] { enemy }, m_resource, new CombatRules(), new RollService(3)));
        }

        private void Roll(CombatState state, params int[] faces)
        {
            state.ReceiveRoll(faces.Select((f, i) => (i, new RollResult(m_resource, f))));
            if (state.Phase == CombatPhase.Reroll) state.KeepAll();
        }

        [Test]
        public void UnitEvents_FireForShieldDamageStatusAndDeath()
        {
            var (run, state) = Fight(Enemy(4, IntentKind.Attack, 5), m_knight, m_knight, m_arcanist);
            var events = new List<CombatEvent>();
            state.UnitEvent += events.Add;
            Roll(state, 1, 1, 1, 3, 3, 3); // 3 Red, 3 Blue

            state.Cast(0, 1);            // Shield 4 on self
            var shield = events.Single();
            Assert.AreEqual(CombatEventKind.Shield, shield.Kind);
            Assert.AreEqual(4, shield.Amount);
            Assert.IsFalse(shield.TargetIsEnemy);
            Assert.AreEqual(0, shield.Target);
            events.Clear();

            state.Cast(2, 0, 0);         // Burn 2 on the enemy
            Assert.AreEqual(CombatEventKind.Status, events.Single().Kind);
            Assert.AreEqual("Burn", events.Single().Detail);
            Assert.IsTrue(events.Single().TargetIsEnemy);
            events.Clear();

            state.Cast(1, 0, 0);         // Strike 3 (+1 from no Hex) kills the 4-HP enemy? 3 damage leaves 1.
            Assert.AreEqual(CombatEventKind.Damage, events.Single().Kind);
            Assert.AreEqual(3, events.Single().Amount);
            events.Clear();

            state.Cast(2, 0, 0);         // Burn again: 4 stacks, still alive
            events.Clear();
            state.EndHeroTurn();         // Burn ticks 4 on a 1-HP enemy
            Assert.AreEqual(CombatEventKind.Damage, events[0].Kind);
            Assert.AreEqual(1, events[0].Amount, "only the HP actually lost is reported");
            Assert.AreEqual("Burn", events[0].Detail);
            Assert.AreEqual(CombatEventKind.Death, events[1].Kind);
            Assert.AreEqual(CombatPhase.Won, state.Phase);
            run.Clear();
        }

        [Test]
        public void UnitEvents_EnemyAttackReportsDamageTakenAfterShield()
        {
            var (run, state) = Fight(Enemy(50, IntentKind.Attack, 5), m_knight, m_knight, m_arcanist);
            var events = new List<CombatEvent>();
            Roll(state, 1, 1, 1, 1, 1, 1);
            state.Cast(0, 1);                                   // Shield 4
            state.Hero(0).Statuses.Set(StatusKind.Taunt, 1);    // make the Knight the target
            state.UnitEvent += events.Add;
            state.EndHeroTurn();
            var hit = events.First(e => e.Kind == CombatEventKind.Damage);
            Assert.IsFalse(hit.TargetIsEnemy);
            Assert.AreEqual(0, hit.Target, "the taunter was hit");
            Assert.AreEqual(1, hit.Amount, "5 incoming, 4 absorbed");
            Assert.AreEqual("absorbed", hit.Detail);
            run.Clear();
        }

        [Test]
        public void Log_NarratesTheTurn()
        {
            var (run, state) = Fight(Enemy(50, IntentKind.Block, 1), m_knight, m_knight, m_arcanist);
            Roll(state, 1, 2, 3, 4, 5, 6);
            Assert.That(state.Log.Last(), Does.Contain("Turn 1").And.Contain("R R B B Y Y").And.Contain("Red 2, Blue 2, Yellow 2"));
            state.Cast(0, 0, 0);
            Assert.That(state.Log.Last(), Does.Contain("Knight: Strike hits Enemy for 3"));
            Assert.IsFalse(state.Cast(0, 2), "Bulwark needs 3 Red");
            Assert.AreEqual(0, state.Log.Count(l => l.StartsWith("Cannot cast")), "an unaffordable cast is refused silently by CanCast");
            run.Clear();
        }
    }
}
