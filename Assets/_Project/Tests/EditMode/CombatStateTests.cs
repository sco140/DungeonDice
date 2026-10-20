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
    /// <summary>The combat rules on fake heroes and enemies, no scene, no physics.</summary>
    public class CombatStateTests
    {
        private readonly List<ScriptableObject> m_created = new();
        private DieDefinition m_resource;
        private HeroDefinition m_knight, m_arcanist, m_trickster;

        [SetUp]
        public void SetUp()
        {
            m_resource = TestDice.Resource(m_created);
            m_knight = TestHeroes.Knight(m_created);
            m_arcanist = TestHeroes.Arcanist(m_created);
            m_trickster = TestHeroes.Trickster(m_created);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) Object.DestroyImmediate(so);
            m_created.Clear();
        }

        // --- fixtures ----------------------------------------------------------------

        private EnemyDefinition Enemy(string name, int hp, params (IntentKind kind, int value)[] intents)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            enemy.displayName = name;
            enemy.maxHealth = hp;
            enemy.intents = intents.Select(i => new EnemyIntent { kind = i.kind, value = i.value }).ToList();
            m_created.Add(enemy);
            return enemy;
        }

        private (RunState run, CombatState state) Fight(IEnumerable<EnemyDefinition> enemies, CombatRules rules = null, params HeroDefinition[] party)
        {
            var run = new RunState();
            run.StartNewRun(party.Length == 0 ? new[] { m_knight, m_arcanist, m_trickster } : party, 1);
            var state = new CombatState(run.Party, enemies, m_resource, rules ?? new CombatRules(), new RollService(7));
            return (run, state);
        }

        /// <summary>Feed a throw: face 1–2 Red, 3–4 Blue, 5–6 Yellow.</summary>
        private void Roll(CombatState state, params int[] faces)
        {
            state.ReceiveRoll(faces.Select((f, i) => (i, new RollResult(m_resource, f))));
            if (state.Phase == CombatPhase.Reroll) state.KeepAll();
        }

        private static int Ability(CombatHero hero, string name) => hero.Abilities.ToList().FindIndex(a => a.name == name);

        /// <summary>Rules with no per-hero action limit, for tests that exercise several casts by one hero.</summary>
        private static CombatRules Unlimited() => new() { actionsPerHeroPerTurn = 0 };

        // --- throw and pool ----------------------------------------------------------

        [Test]
        public void Throw_AddsOneManaPerDieColour_AndSkipsRerollWhenRulesSayNone()
        {
            var (run, state) = Fight(new[] { Enemy("Rat", 10, (IntentKind.Attack, 2)) });
            Assert.AreEqual(CombatPhase.Throw, state.Phase);
            Assert.AreEqual(6, state.DiceToThrow(6).Count);
            Assert.IsTrue(state.DiceToThrow(6).All(d => d == m_resource));

            Roll(state, 1, 2, 3, 5, 6, 6); // R R B Y Y Y
            Assert.AreEqual(CombatPhase.Act, state.Phase, "no reroll step by default");
            Assert.AreEqual((2, 1, 3), (state.Red, state.Blue, state.Yellow));
            Assert.AreEqual(6, state.Dice.Count);
            Assert.AreEqual(DieColor.Blue, state.Die(2).Color);
            run.Clear();
        }

        [Test]
        public void RerollStep_OpensWhenRulesAllow_PullsManaAndRejoins()
        {
            var rules = new CombatRules { rerollsPerTurn = 1 };
            var (run, state) = Fight(new[] { Enemy("Rat", 10, (IntentKind.Attack, 2)) }, rules);
            IReadOnlyList<int> requested = null;
            state.DiceRerollRequested += ids => requested = ids;

            state.ReceiveRoll(new[] { 1, 1, 1, 1, 1, 1 }.Select((f, i) => (i, new RollResult(m_resource, f))));
            Assert.AreEqual(CombatPhase.Reroll, state.Phase);
            Assert.IsTrue(state.CanReroll);
            Assert.AreEqual(6, state.Red);

            Assert.IsTrue(state.RequestReroll(new[] { 0, 1 }));
            Assert.AreEqual(new[] { 0, 1 }, requested.ToArray());
            Assert.AreEqual(4, state.Red, "re-thrown dice leave the pool while in flight");
            Assert.IsTrue(state.AnyDiePending);
            Assert.IsFalse(state.CanAct);

            state.ReceiveRoll(new[] { (0, new RollResult(m_resource, 3)), (1, new RollResult(m_resource, 6)) });
            Assert.AreEqual(CombatPhase.Act, state.Phase);
            Assert.AreEqual((4, 1, 1), (state.Red, state.Blue, state.Yellow));
            Assert.AreEqual(1, state.RerollsUsed);
            Assert.IsFalse(state.CanReroll);
            run.Clear();
        }

        [Test]
        public void Mana_CarriesBetweenTurns_UnlessTheRuleIsOff()
        {
            var enemy = Enemy("Wall", 50, (IntentKind.Block, 1));
            var (run, carry) = Fight(new[] { enemy });
            Roll(carry, 1, 1, 1, 3, 3, 5);
            carry.EndHeroTurn();
            Assert.AreEqual(2, carry.Turn);
            Assert.AreEqual((3, 2, 1), (carry.Red, carry.Blue, carry.Yellow), "pool survives the turn");
            Assert.AreEqual(0, carry.Dice.Count, "but the dice are collected for the next throw");
            run.Clear();

            var (run2, wipe) = Fight(new[] { enemy }, new CombatRules { manaCarriesBetweenTurns = false });
            Roll(wipe, 1, 1, 1, 3, 3, 5);
            wipe.EndHeroTurn();
            Assert.AreEqual((0, 0, 0), (wipe.Red, wipe.Blue, wipe.Yellow), "pool wiped");
            run2.Clear();
        }

        // --- abilities ---------------------------------------------------------------

        [Test]
        public void Knight_Strike_Shield_Bulwark_CostRedAndRepeat()
        {
            var (run, state) = Fight(new[] { Enemy("Rat", 10, (IntentKind.Attack, 2)) }, Unlimited());
            Roll(state, 1, 1, 1, 1, 1, 2); // 6 Red
            var knight = state.Hero(0);

            Assert.IsTrue(state.Cast(0, Ability(knight, "Strike"), 0));
            Assert.IsTrue(state.Cast(0, Ability(knight, "Strike"), 0), "with no action limit, abilities repeat while mana lasts");
            Assert.AreEqual(4, state.Enemies[0].Health);
            Assert.AreEqual(4, state.Red);

            Assert.IsTrue(state.Cast(0, Ability(knight, "Shield")));
            Assert.AreEqual(4, knight.Shield);
            Assert.AreEqual(2, state.Red);

            Assert.IsFalse(state.CanCast(0, Ability(knight, "Bulwark")), "3 Red needed, 2 left");
            Assert.IsFalse(state.Cast(0, Ability(knight, "Bulwark")));
            state.GrantManaForTests(DieColor.Red, 1);
            Assert.IsTrue(state.Cast(0, Ability(knight, "Bulwark")));
            Assert.IsTrue(state.Heroes.All(h => h.Shield >= 6));
            Assert.AreEqual(0, state.Red);
            run.Clear();
        }

        [Test]
        public void Arcanist_BurnPoisonDetonate_AndStatusTicks()
        {
            var (run, state) = Fight(new[] { Enemy("Wall", 30, (IntentKind.Block, 2)) }, Unlimited());
            Roll(state, 3, 3, 3, 3, 3, 4); // 6 Blue
            var arcanist = state.Hero(1);
            var enemy = state.Enemies[0];

            Assert.IsTrue(state.Cast(1, Ability(arcanist, "Burn"), 0));
            Assert.IsTrue(state.Cast(1, Ability(arcanist, "Poison"), 0));
            Assert.AreEqual(2, enemy.Statuses.Get(StatusKind.Burn));
            Assert.AreEqual(3, enemy.Statuses.Get(StatusKind.Poison));
            Assert.AreEqual(3, state.Blue);

            Assert.IsTrue(state.Cast(1, Ability(arcanist, "Detonate"), 0));
            Assert.AreEqual(0, enemy.Statuses.Total, "stacks consumed");
            Assert.AreEqual(25, enemy.Health, "5 stacks → 5 damage");
            Assert.AreEqual(0, state.Blue);

            // Status ticks: Burn at the start of the enemy turn then decays; Poison at the end, no decay.
            state.GrantManaForTests(DieColor.Blue, 3);
            state.Cast(1, Ability(arcanist, "Burn"), 0);
            state.Cast(1, Ability(arcanist, "Poison"), 0);
            state.EndHeroTurn();
            Assert.AreEqual(25 - 2 - 3, enemy.Health);
            Assert.AreEqual(1, enemy.Statuses.Get(StatusKind.Burn));
            Assert.AreEqual(3, enemy.Statuses.Get(StatusKind.Poison));
            run.Clear();
        }

        [Test]
        public void Trickster_Recolour_MovesManaAndRaisesDieRecolored()
        {
            var (run, state) = Fight(new[] { Enemy("Rat", 10, (IntentKind.Attack, 2)) });
            Roll(state, 1, 1, 3, 5, 5, 5); // R R B Y Y Y
            var trickster = state.Hero(2);
            (int index, DieColor color) recolored = (-1, DieColor.None);
            state.DieRecolored += (i, c) => recolored = (i, c);

            Assert.IsTrue(state.CanTargetDie(0));
            Assert.IsTrue(state.Cast(2, Ability(trickster, "Recolour"), 0, DieColor.Blue));
            Assert.AreEqual((1, 2, 1), (state.Red, state.Blue, state.Yellow), "one Red became Blue, 2 Yellow paid");
            Assert.AreEqual(DieColor.Blue, state.Die(0).Color);
            Assert.AreEqual((0, DieColor.Blue), recolored);

            Assert.IsFalse(state.Cast(2, Ability(trickster, "Recolour"), 1, DieColor.Red), "1 Yellow left, costs 2");
            run.Clear();
        }

        [Test]
        public void Trickster_Reroll_PullsTheDie_ThenItsNewColourRejoins()
        {
            var (run, state) = Fight(new[] { Enemy("Rat", 10, (IntentKind.Attack, 2)) });
            Roll(state, 1, 1, 1, 1, 5, 5); // R R R R Y Y
            var trickster = state.Hero(2);
            IReadOnlyList<int> requested = null;
            state.DiceRerollRequested += ids => requested = ids;

            Assert.IsTrue(state.Cast(2, Ability(trickster, "Reroll"), 0));
            Assert.AreEqual(new[] { 0 }, requested.ToArray());
            Assert.AreEqual((3, 0, 1), (state.Red, state.Blue, state.Yellow), "die 0's Red pulled, 1 Yellow paid");
            Assert.IsTrue(state.Die(0).Pending);
            Assert.IsFalse(state.CanAct, "no acting while a die is in the air");
            Assert.IsFalse(state.CanCast(0, 0));

            state.ReceiveRoll(new[] { (0, new RollResult(m_resource, 4)) });
            Assert.AreEqual((3, 1, 1), (state.Red, state.Blue, state.Yellow));
            Assert.IsFalse(state.Die(0).Pending);
            Assert.IsTrue(state.CanAct);
            run.Clear();
        }

        [Test]
        public void DieTargets_MustLeaveTheCostCovered_AndSpentDiceCannotBeTargeted()
        {
            var (run, state) = Fight(new[] { Enemy("Rat", 10, (IntentKind.Attack, 2)) });
            Roll(state, 1, 3, 3, 3, 3, 5); // R B B B B Y: exactly one Yellow
            var trickster = state.Hero(2);
            int reroll = Ability(trickster, "Reroll");

            Assert.IsTrue(state.CanCast(2, reroll), "1 Yellow covers the cost");
            Assert.IsFalse(state.CanTargetDie(5, trickster.Abilities[reroll]), "re-throwing the only Yellow die would leave nothing to pay with");
            Assert.IsFalse(state.Cast(2, reroll, 5));
            Assert.IsTrue(state.CanTargetDie(0, trickster.Abilities[reroll]));

            // Spend the Red on Strike: die 0's mana is gone, so it is no longer a target.
            state.Cast(0, Ability(state.Hero(0), "Strike"), 0);
            Assert.IsFalse(state.CanTargetDie(0));
            Assert.IsFalse(state.Cast(2, reroll, 0));
            run.Clear();
        }

        [Test]
        public void ByDefault_EachHeroCastsOncePerTurn_RefusalsAreFree_AndTheLimitResets()
        {
            var (run, state) = Fight(new[] { Enemy("Wall", 50, (IntentKind.Block, 1)) });
            Assert.AreEqual(1, state.Rules.actionsPerHeroPerTurn, "one action per hero is the default");
            Roll(state, 1, 1, 1, 1, 3, 5); // 4 Red, 1 Blue, 1 Yellow
            var knight = state.Hero(0);

            Assert.IsFalse(state.Cast(0, Ability(knight, "Strike"), 99), "a refused cast (bad target) does not use the action");
            Assert.IsTrue(state.HasActionLeft(0));
            Assert.IsTrue(state.Cast(0, Ability(knight, "Strike"), 0));
            Assert.IsFalse(state.HasActionLeft(0));
            Assert.IsFalse(state.CanCast(0, Ability(knight, "Shield")), "the Knight has acted, even with Red left");
            Assert.IsFalse(state.Cast(0, Ability(knight, "Strike"), 0));
            Assert.AreEqual(3, state.Red);

            Assert.IsTrue(state.Cast(1, Ability(state.Hero(1), "Burn"), 0), "other heroes still have their action");
            Assert.IsTrue(state.Cast(2, Ability(state.Hero(2), "Jab"), 0));

            state.EndHeroTurn();
            Roll(state, 1, 1, 1, 1, 1, 1);
            Assert.IsTrue(state.HasActionLeft(0), "actions reset each turn");
            Assert.IsTrue(state.Cast(0, Ability(knight, "Strike"), 0));
            run.Clear();
        }

        // --- enemy turn --------------------------------------------------------------

        [Test]
        public void EnemyTurn_AttackGoesToTaunter_ShieldAbsorbs_IntentsRedrawn()
        {
            var (run, state) = Fight(new[] { Enemy("Brute", 30, (IntentKind.Attack, 5)) });
            Roll(state, 1, 1, 1, 1, 1, 1);
            var knight = state.Hero(0);
            state.Cast(0, Ability(knight, "Shield"));           // 4 shield on the Knight
            knight.Statuses.Set(StatusKind.Taunt, 1);           // Guard is not on the Knight's list; force a taunt

            int before = knight.Health;
            state.EndHeroTurn();
            Assert.AreEqual(before - 1, knight.Health, "5 incoming, 4 absorbed");
            Assert.AreEqual(0, knight.Shield, "hero shields expire at the end of the enemy turn");
            Assert.IsFalse(knight.Statuses.Has(StatusKind.Taunt));
            Assert.IsTrue(state.Heroes.Where(h => h.Slot != 0).All(h => h.Health == h.MaxHealth), "the taunter took the hit");
            Assert.IsNotNull(state.Enemies[0].Intent);
            Assert.AreEqual(CombatPhase.Throw, state.Phase);
            run.Clear();
        }

        [Test]
        public void EnemyTurn_WeakenAndHex_AndBlockShieldsTheEnemyUntilItActs()
        {
            var (run, state) = Fight(new[] { Enemy("Guard", 20, (IntentKind.Block, 3)) });
            Roll(state, 1, 1, 1, 1, 1, 1);
            var enemy = state.Enemies[0];
            enemy.Statuses.Set(StatusKind.Hex, 1);
            state.Cast(0, Ability(state.Hero(0), "Strike"), 0);
            Assert.AreEqual(16, enemy.Health, "Hex adds +1");

            state.EndHeroTurn();
            Assert.AreEqual(3, enemy.Shield, "Block held until the enemy's next action");
            Assert.IsFalse(enemy.Statuses.Has(StatusKind.Hex), "Hex expires");
            Roll(state, 1, 1, 1, 1, 1, 1);
            state.Cast(0, Ability(state.Hero(0), "Strike"), 0);
            Assert.AreEqual(16, enemy.Health, "3 of the 3 damage absorbed by Block");
            Assert.AreEqual(0, enemy.Shield);
            run.Clear();
        }

        [Test]
        public void Victory_WhenTheLastEnemyDies_AndDefeat_WhenThePartyDoes()
        {
            var (run, win) = Fight(new[] { Enemy("Rat", 3, (IntentKind.Attack, 1)), Enemy("Rat", 3, (IntentKind.Attack, 1)) }, Unlimited());
            Roll(win, 1, 1, 1, 1, 1, 1);
            win.Cast(0, 0, 0);
            Assert.AreEqual(CombatPhase.Act, win.Phase, "one enemy still up");
            win.Cast(0, 0, 1);
            Assert.AreEqual(CombatPhase.Won, win.Phase);
            Assert.IsTrue(win.IsOver);
            run.Clear();

            var (run2, lose) = Fight(new[] { Enemy("Dragon", 99, (IntentKind.AttackAll, 99)) });
            Roll(lose, 1, 1, 1, 1, 1, 1);
            lose.EndHeroTurn();
            Assert.AreEqual(CombatPhase.Lost, lose.Phase);
            Assert.IsTrue(run2.PartyWiped);
            run2.Clear();
        }

        [Test]
        public void DeadHero_LosesAbilities_ButTheDiceCountStays()
        {
            var (run, state) = Fight(new[] { Enemy("Rat", 10, (IntentKind.Attack, 2)) });
            run.Party[0].TakeDamage(999);
            Roll(state, 1, 1, 1, 1, 1, 1);
            Assert.AreEqual(6, state.Dice.Count);
            Assert.AreEqual(6, state.Red, "dead heroes do not shrink the party pool");
            Assert.IsFalse(state.CanCast(0, 0), "dead Knight cannot Strike");
            Assert.IsTrue(state.CanCast(2, Ability(state.Hero(2), "Jab")) == false, "Jab costs Yellow; none rolled");
            Assert.AreEqual(6, state.DiceToThrow(6).Count);
            run.Clear();
        }

        [Test]
        public void Constructor_RejectsNoEnemies_AndSameSeedGivesSameIntents()
        {
            var run = new RunState();
            run.StartNewRun(new[] { m_knight, m_arcanist, m_trickster }, 1);
            Assert.Throws<System.ArgumentException>(() => new CombatState(run.Party, System.Array.Empty<EnemyDefinition>(), m_resource, new CombatRules(), new RollService(1)));

            var enemy = Enemy("Many", 10, (IntentKind.Attack, 1), (IntentKind.Block, 2), (IntentKind.AttackAll, 3));
            string Intents(int seed) => string.Join(",", Enumerable.Range(0, 4).Select(_ => new CombatState(run.Party, new[] { enemy }, m_resource, new CombatRules(), new RollService(seed)).Enemies[0].Intent.Describe()));
            Assert.AreEqual(Intents(5), Intents(5));
            run.Clear();
        }
    }
}
