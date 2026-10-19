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
        private DieDefinition m_knightDie, m_arcanistDie, m_tricksterDie;
        private HeroDefinition m_knight, m_arcanist, m_trickster;

        [SetUp]
        public void SetUp()
        {
            m_knightDie = Die("Knight", DieColor.Red,
                F("Strike 3", E(FaceEffectKind.Strike, 3)), F("Strike 3", E(FaceEffectKind.Strike, 3)),
                F("Shield 4", E(FaceEffectKind.Shield, 4)), F("Shield 4", E(FaceEffectKind.Shield, 4)),
                F("Taunt + Shield 2", E(FaceEffectKind.Taunt), E(FaceEffectKind.Shield, 2)), F("Strike 5", E(FaceEffectKind.Strike, 5)));
            m_arcanistDie = Die("Arcanist", DieColor.Blue,
                F("Burn 2", E(FaceEffectKind.Burn, 2)), F("Burn 2", E(FaceEffectKind.Burn, 2)),
                F("Poison 3", E(FaceEffectKind.Poison, 3)), F("Weaken", E(FaceEffectKind.Weaken, 1)),
                F("Hex", E(FaceEffectKind.Hex, 1)), F("Fizzle", E(FaceEffectKind.Fizzle)));
            m_tricksterDie = Die("Trickster", DieColor.Yellow,
                F("Nudge", E(FaceEffectKind.Nudge, 1)), F("Nudge", E(FaceEffectKind.Nudge, 1)),
                F("Flip", E(FaceEffectKind.Flip)), F("Reroll ally", E(FaceEffectKind.RerollAlly)),
                F("Mirror", E(FaceEffectKind.Mirror)), F("Jab 2", E(FaceEffectKind.Jab, 2)));

            m_knight = Hero("Knight", DieColor.Red, 30, SignatureKind.Bulwark, m_knightDie);
            m_arcanist = Hero("Arcanist", DieColor.Blue, 20, SignatureKind.Detonate, m_arcanistDie);
            m_trickster = Hero("Trickster", DieColor.Yellow, 24, SignatureKind.LoadedDice, m_tricksterDie);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in m_created) Object.DestroyImmediate(so);
            m_created.Clear();
        }

        // --- fixtures ----------------------------------------------------------------

        private static FaceEffectEntry E(FaceEffectKind k, int v = 0) => new(k, v);
        private static DieFace F(string label, params FaceEffectEntry[] e) => DieFace.Of(label, e);

        private DieDefinition Die(string name, DieColor color, params DieFace[] faces)
        {
            var die = ScriptableObject.CreateInstance<DieDefinition>();
            die.displayName = name;
            die.color = color;
            die.SetFaces(faces);
            m_created.Add(die);
            return die;
        }

        private HeroDefinition Hero(string name, DieColor color, int hp, SignatureKind signature, DieDefinition die)
        {
            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            hero.displayName = name;
            hero.color = color;
            hero.maxHealth = hp;
            hero.playableInPrototype = true;
            hero.signatureKind = signature;
            hero.signatureName = signature.ToString();
            hero.dice.Add(die);
            m_created.Add(hero);
            return hero;
        }

        private EnemyDefinition Enemy(string name, int hp, params (IntentKind kind, int value)[] intents)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            enemy.displayName = name;
            enemy.maxHealth = hp;
            enemy.intents = intents.Select(i => new EnemyIntent { kind = i.kind, value = i.value }).ToList();
            m_created.Add(enemy);
            return enemy;
        }

        private RunState Run(params HeroDefinition[] party)
        {
            var run = new RunState();
            run.StartNewRun(party, 123);
            return run;
        }

        private static CombatState Fight(RunState run, params EnemyDefinition[] enemies) =>
            new(run.Party, enemies, new RollService(7));

        /// <summary>Feed settled faces (slot → face index 1–6) as if the box had reported them.</summary>
        private static void Roll(CombatState state, RunState run, params (int slot, int face)[] faces) =>
            state.ReceiveRoll(faces.Select(f => (f.slot, new RollResult(run.Party[f.slot].Die, f.face))));

        private static void RollAndKeep(CombatState state, RunState run, params (int slot, int face)[] faces)
        {
            Roll(state, run, faces);
            if (state.Phase == CombatPhase.Reroll) state.KeepAll();
        }

        // --- flow --------------------------------------------------------------------

        [Test]
        public void Flow_Throw_Reroll_Act_EnemyTurn_NewTurn()
        {
            var run = Run(m_knight, m_arcanist, m_trickster);
            var state = Fight(run, Enemy("Guard", 12, (IntentKind.Block, 3)));
            Assert.AreEqual(CombatPhase.Throw, state.Phase);
            Assert.IsNotNull(state.Enemies[0].Intent, "intent telegraphed before the first throw");
            Assert.AreEqual(3, state.DiceToThrow(out var slots).Count);
            Assert.AreEqual(new[] { 0, 1, 2 }, slots.ToArray());

            Roll(state, run, (0, 1), (1, 1), (2, 1));
            Assert.AreEqual(CombatPhase.Reroll, state.Phase);
            Assert.IsTrue(state.CanReroll);

            Roll(state, run, (0, 6)); // re-threw the Knight die
            Assert.AreEqual(CombatPhase.Act, state.Phase);
            Assert.IsTrue(state.RerollUsed);
            Assert.AreEqual("Strike 5", state.Die(0).Face.label);
            Assert.AreEqual("Burn 2", state.Die(1).Face.label, "kept dice unchanged");

            state.EndHeroTurn();
            Assert.AreEqual(CombatPhase.Throw, state.Phase);
            Assert.AreEqual(2, state.Turn);
            Assert.IsFalse(state.RerollUsed, "reroll is available again next turn");
            Assert.AreEqual(0, state.Dice.Count);
            run.Clear();
        }

        [Test]
        public void Bank_GivesOneManaOfTheHeroColour_AndSpendsTheDie()
        {
            var run = Run(m_knight, m_arcanist, m_trickster);
            var state = Fight(run, Enemy("Guard", 12, (IntentKind.Block, 3)));
            RollAndKeep(state, run, (0, 1), (1, 1), (2, 1));

            Assert.IsTrue(state.BankDie(0));
            Assert.IsTrue(state.BankDie(1));
            Assert.IsTrue(state.BankDie(2));
            Assert.AreEqual((1, 1, 1), (state.Red, state.Blue, state.Yellow));
            Assert.IsFalse(state.BankDie(0), "a spent die cannot be banked again");
            Assert.IsFalse(state.UseDie(0, 0), "nor used");
            run.Clear();
        }

        [Test]
        public void Strike_DamagesEnemy_HexAddsOne_ShieldAbsorbsFirst()
        {
            var run = Run(m_knight, m_arcanist, m_trickster);
            var state = Fight(run, Enemy("Guard", 12, (IntentKind.Block, 3)));
            state.Enemies[0].Shield = 2;
            RollAndKeep(state, run, (0, 1), (1, 5), (2, 6)); // Strike 3, Hex, Jab 2

            Assert.IsTrue(state.UseDie(1, 0)); // Hex
            Assert.IsTrue(state.UseDie(0, 0)); // Strike 3 + 1 = 4: 2 into shield, 2 into HP
            Assert.AreEqual(10, state.Enemies[0].Health);
            Assert.AreEqual(0, state.Enemies[0].Shield);
            Assert.IsTrue(state.UseDie(2, 0)); // Jab 2 + 1 = 3
            Assert.AreEqual(7, state.Enemies[0].Health);
            run.Clear();
        }

        [Test]
        public void Taunt_RedirectsAttack_ShieldAbsorbs_BothExpire()
        {
            var run = Run(m_knight, m_arcanist, m_trickster);
            var state = Fight(run, Enemy("Guard", 12, (IntentKind.Attack, 4)));
            RollAndKeep(state, run, (0, 5), (1, 6), (2, 6)); // Taunt + Shield 2, Fizzle, Jab

            Assert.IsTrue(state.UseDie(0)); // self
            Assert.AreEqual(2, state.Heroes[0].Shield);
            Assert.IsTrue(state.Heroes[0].Statuses.Has(StatusKind.Taunt));

            state.EndHeroTurn();
            Assert.AreEqual(28, state.Heroes[0].Health, "4 damage: 2 absorbed, 2 taken, by the taunter");
            Assert.AreEqual(20, state.Heroes[1].Health);
            Assert.AreEqual(24, state.Heroes[2].Health);
            Assert.AreEqual(0, state.Heroes[0].Shield, "shield expires with the enemy turn");
            Assert.IsFalse(state.Heroes[0].Statuses.Has(StatusKind.Taunt));
            run.Clear();
        }

        [Test]
        public void Burn_TicksAndDecays_Poison_TicksWithoutDecay_Weaken_ReducesDamage()
        {
            var run = Run(m_knight, m_arcanist, m_arcanist); // two Arcanists
            var state = Fight(run, Enemy("Guard", 20, (IntentKind.Attack, 4)));
            RollAndKeep(state, run, (0, 5), (1, 1), (2, 3)); // Taunt, Burn 2, Poison 3
            state.UseDie(0);
            state.UseDie(1, 0);
            state.UseDie(2, 0);
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKind.Burn));
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKind.Poison));

            state.EndHeroTurn(); // burn 2 then poison 3
            Assert.AreEqual(15, state.Enemies[0].Health);
            Assert.AreEqual(1, state.Enemies[0].Statuses.Get(StatusKind.Burn), "burn decays");
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKind.Poison), "poison does not");
            Assert.AreEqual(28, state.Heroes[0].Health, "taunting Knight took the 4 (shield 2)");

            RollAndKeep(state, run, (0, 5), (1, 4), (2, 6)); // Taunt, Weaken, Fizzle
            state.UseDie(0);
            state.UseDie(1, 0);
            state.EndHeroTurn(); // burn 1, attack 4-1=3 vs shield 2, poison 3
            Assert.AreEqual(11, state.Enemies[0].Health);
            Assert.AreEqual(0, state.Enemies[0].Statuses.Get(StatusKind.Burn));
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKind.Weaken), "one-turn status cleared");
            Assert.AreEqual(27, state.Heroes[0].Health, "weakened attack: 3 damage, 2 absorbed");
            run.Clear();
        }

        [Test]
        public void Signatures_CostThreeManaOfColour_AndResolve()
        {
            // Three Knights: bank all → Red 3 → Bulwark.
            var run = Run(m_knight, m_knight, m_knight);
            var state = Fight(run, Enemy("Guard", 12, (IntentKind.Block, 1)));
            RollAndKeep(state, run, (0, 1), (1, 1), (2, 1));
            Assert.IsFalse(state.CanCastSignature(0), "no mana yet");
            state.BankDie(0); state.BankDie(1); state.BankDie(2);
            Assert.IsTrue(state.CanCastSignature(0));
            Assert.IsTrue(state.CastSignature(0));
            Assert.AreEqual(0, state.Red);
            Assert.IsTrue(state.Heroes.All(h => h.Shield == 6));
            Assert.IsFalse(state.CanCastSignature(1), "pool is spent");
            run.Clear();

            // Three Arcanists: turn 1 bank → Blue 3; turn 2 statuses then Detonate.
            run = Run(m_arcanist, m_arcanist, m_arcanist);
            state = Fight(run, Enemy("Guard", 20, (IntentKind.Block, 1)));
            RollAndKeep(state, run, (0, 6), (1, 6), (2, 6));
            state.BankDie(0); state.BankDie(1); state.BankDie(2);
            state.EndHeroTurn();
            RollAndKeep(state, run, (0, 1), (1, 1), (2, 3)); // Burn 2, Burn 2, Poison 3
            state.UseDie(0, 0); state.UseDie(1, 0); state.UseDie(2, 0);
            Assert.AreEqual(7, state.Enemies[0].Statuses.Total);
            Assert.IsTrue(state.CastSignature(0, 0));
            Assert.AreEqual(14, state.Enemies[0].Health, "7 stacks as damage, 1 absorbed by the Block the enemy raised last turn");
            Assert.AreEqual(0, state.Enemies[0].Statuses.Total);
            Assert.AreEqual(0, state.Blue);
            run.Clear();

            // Three Tricksters: Loaded Dice sets a die to a chosen face.
            run = Run(m_trickster, m_trickster, m_trickster);
            state = Fight(run, Enemy("Guard", 20, (IntentKind.Block, 1)));
            RollAndKeep(state, run, (0, 6), (1, 6), (2, 6));
            state.BankDie(0); state.BankDie(1); state.BankDie(2);
            state.EndHeroTurn();
            RollAndKeep(state, run, (0, 1), (1, 1), (2, 1));
            int modified = 0;
            state.DieModified += (_, _) => modified++;
            Assert.IsTrue(state.CastSignature(0, target: 1, faceIndex: 6));
            Assert.AreEqual("Jab 2", state.Die(1).Face.label);
            Assert.AreEqual(1, modified, "the box is told to turn the die");
            Assert.IsFalse(state.CastSignature(1, 2, 6), "yellow pool is spent");
            run.Clear();
        }

        [Test]
        public void TricksterFaces_FlipNudgeMirror_ChangeOtherDice()
        {
            var run = Run(m_knight, m_trickster, m_trickster);
            var state = Fight(run, Enemy("Guard", 20, (IntentKind.Block, 1)));
            RollAndKeep(state, run, (0, 1), (1, 3), (2, 1)); // Strike 3, Flip, Nudge

            Assert.IsTrue(state.UseDie(1, 0)); // Flip Knight's die: face 1 -> 6 (Strike 5)
            Assert.AreEqual(6, state.Die(0).Result.FaceIndex);
            Assert.AreEqual("Strike 5", state.Die(0).Face.label);
            Assert.IsTrue(state.UseDie(2, 0, nudgeDelta: -1)); // Nudge -1: face 6 -> 5 (Taunt + Shield 2)
            Assert.AreEqual(5, state.Die(0).Result.FaceIndex);
            Assert.IsFalse(state.UseDie(2, 0), "nudge die is spent");
            run.Clear();

            run = Run(m_knight, m_trickster, m_arcanist);
            state = Fight(run, Enemy("Guard", 20, (IntentKind.Block, 1)));
            RollAndKeep(state, run, (0, 6), (1, 5), (2, 6)); // Strike 5, Mirror, Fizzle
            Assert.IsFalse(state.UseDie(1, 1), "cannot mirror itself");
            Assert.IsTrue(state.UseDie(1, 0));
            Assert.IsFalse(state.Die(1).Spent, "mirror leaves the die in hand as the copied face");
            Assert.AreEqual("Strike 5", state.Die(1).Face.label);
            Assert.AreEqual(TargetKind.Enemy, state.TargetFor(1));
            Assert.IsTrue(state.UseDie(1, 0));
            Assert.AreEqual(15, state.Enemies[0].Health);
            run.Clear();
        }

        [Test]
        public void RerollAlly_RequestsAReThrow_AndTakesTheNewResult()
        {
            var run = Run(m_knight, m_trickster, m_arcanist);
            var state = Fight(run, Enemy("Guard", 20, (IntentKind.Block, 1)));
            RollAndKeep(state, run, (0, 1), (1, 4), (2, 6)); // Strike 3, Reroll ally, Fizzle
            int requested = -1;
            state.AllyRerollRequested += s => requested = s;

            Assert.IsTrue(state.UseDie(1, 0));
            Assert.AreEqual(0, requested);
            Assert.AreEqual(0, state.PendingAllyReroll);
            Assert.IsFalse(state.CanAct(0), "nothing acts while a re-throw is pending");

            Roll(state, run, (0, 6)); // the box reports the Knight's new face
            Assert.AreEqual(-1, state.PendingAllyReroll);
            Assert.AreEqual("Strike 5", state.Die(0).Face.label);
            Assert.IsTrue(state.CanAct(0));
            run.Clear();
        }

        [Test]
        public void Victory_WhenEnemiesDie_Defeat_WhenPartyWiped()
        {
            var run = Run(m_knight, m_knight, m_knight);
            var state = Fight(run, Enemy("Weak", 5, (IntentKind.Attack, 1)));
            RollAndKeep(state, run, (0, 6), (1, 1), (2, 1));
            Assert.IsTrue(state.UseDie(0, 0)); // Strike 5 kills it
            Assert.AreEqual(CombatPhase.Won, state.Phase);
            Assert.IsTrue(state.IsOver);
            Assert.IsFalse(state.BankDie(1), "no actions after the fight ends");
            run.Clear();

            run = Run(m_arcanist, m_arcanist, m_arcanist);
            foreach (var h in run.Party) h.TakeDamage(h.MaxHealth - 1); // 1 HP each
            state = Fight(run, Enemy("Brute", 50, (IntentKind.AttackAll, 5)));
            RollAndKeep(state, run, (0, 6), (1, 6), (2, 6));
            state.EndHeroTurn();
            Assert.AreEqual(CombatPhase.Lost, state.Phase);
            Assert.IsTrue(run.PartyWiped);
            run.Clear();
        }

        [Test]
        public void DeadHero_DieLeavesTheThrow()
        {
            var run = Run(m_knight, m_arcanist, m_trickster);
            run.Party[1].TakeDamage(999);
            var state = Fight(run, Enemy("Guard", 12, (IntentKind.Block, 1)));
            var dice = state.DiceToThrow(out var slots);
            Assert.AreEqual(2, dice.Count);
            Assert.AreEqual(new[] { 0, 2 }, slots.ToArray());
            Assert.IsFalse(state.CanCastSignature(1));
            run.Clear();
        }

        [Test]
        public void SameSeed_SameIntentsAndTargets()
        {
            string Play(int seed)
            {
                var run = Run(m_knight, m_arcanist, m_trickster);
                var state = new CombatState(run.Party, new[] { Enemy("A", 30, (IntentKind.Attack, 3), (IntentKind.Block, 2), (IntentKind.AttackAll, 1)), Enemy("B", 30, (IntentKind.Attack, 2), (IntentKind.Attack, 5)) }, new RollService(seed));
                for (int t = 0; t < 4; t++)
                {
                    RollAndKeep(state, run, (0, 6), (1, 6), (2, 6));
                    state.EndHeroTurn();
                }
                var log = string.Join("\n", state.Log);
                run.Clear();
                return log;
            }
            Assert.AreEqual(Play(99), Play(99));
        }
    }
}
