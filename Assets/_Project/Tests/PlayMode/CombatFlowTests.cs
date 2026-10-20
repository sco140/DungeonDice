using System.Collections;
using System.Linq;
using DungeonDice.Combat;
using DungeonDice.Dice;
using DungeonDice.Run;
using DungeonDice.UI;
using GameUtility;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DungeonDice.Tests
{
    /// <summary>
    /// Real scene, real physics: enter a Combat node, throw the party's six resource dice in the box,
    /// watch the pool fill, cast what the colours allow (including a Trickster Recolour through the
    /// screen's targeting flow), end the turn, and get a fresh throw set up.
    /// </summary>
    public class CombatFlowTests
    {
        private const string SceneName = "Prototype";
        private const float SettleTimeout = 25f;
        private Scene m_scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.IsNotNull(load, $"scene '{SceneName}' is not in Build Settings");
            yield return load;
            m_scene = SceneManager.GetSceneByName(SceneName);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (DiceManager.Instance != null) Object.DestroyImmediate(DiceManager.Instance.gameObject);
            if (EventManager.Instance != null) Object.DestroyImmediate(EventManager.Instance.gameObject);
            if (m_scene.isLoaded) yield return SceneManager.UnloadSceneAsync(m_scene);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Combat_Throw_PoolFills_Cast_EndTurn()
        {
            var run = RunController.Instance;
            Assert.IsNotNull(run.ResourceDie, "the scene must reference the resource die");
            run.StartRun(run.Roster.Where(h => h.playableInPrototype).ToList());
            run.ConfirmTraversalDice(run.StartingTraversalPool.Take(3).ToList());
            yield return null;
            run.TravelTo(2); // Drowned Hall, a Combat node reachable from the resolved start node
            Assert.AreEqual(DungeonDice.Map.NodeType.Combat, run.State.CurrentNode.type);
            run.EnterCurrentNode();
            yield return null;
            Assert.AreEqual(RunPhase.Combat, run.Phase);

            var screen = Object.FindFirstObjectByType<CombatScreen>();
            Assert.IsNotNull(screen);
            var state = screen.State;
            Assert.IsNotNull(state);
            Assert.AreEqual(CombatPhase.Throw, state.Phase);
            Assert.AreEqual(2, state.Enemies.Count, "a Combat node spawns two regulars");
            Assert.IsTrue(state.Enemies.All(e => e.Intent != null), "intents are telegraphed");
            Assert.IsTrue(screen.WaitingForThrow);
            Assert.AreEqual(DiceBoxState.Idle, DiceManager.Instance.State);
            Assert.AreEqual(6, DiceManager.Instance.ActiveDice.Count, "six resource dice");
            Assert.IsTrue(DiceManager.Instance.ActiveDice.All(d => d.Definition == run.ResourceDie));
            Assert.AreSame(run.CombatProfile, DiceManager.Instance.GetComponent<DiceBoxVisuals>().Profile, "combat uses the Combat profile");

            DiceManager.LaunchDice(new Vector3(2f, 0f, 40f));
            float deadline = Time.time + SettleTimeout;
            while (state.Phase == CombatPhase.Throw && Time.time < deadline) yield return null;
            Assert.AreEqual(CombatPhase.Act, state.Phase, "dice settled; no reroll step by default");
            Assert.AreEqual(6, state.Dice.Count);
            Assert.AreEqual(6, state.Red + state.Blue + state.Yellow, "every die put one mana in the pool");
            Assert.IsFalse(screen.WaitingForThrow);
            foreach (var die in state.Dice)
                Assert.AreEqual(DiceManager.Instance.GetResult(die.Index).ManaColor, die.Color, "die colours follow the settled faces");

            // Recolour through the screen: Trickster needs 2 Yellow plus a non-Yellow die to change.
            int tricksterSlot = state.Heroes.First(h => h.Color == DieColor.Yellow).Slot;
            int recolour = state.Hero(tricksterSlot).Abilities.ToList().FindIndex(a => a.kind == AbilityKind.Recolor);
            state.GrantManaForTests(DieColor.Yellow, 2);
            var target = state.Dice.FirstOrDefault(d => d.Color != DieColor.Yellow && state.Mana(d.Color) >= 1);
            if (target != null)
            {
                int yellowBefore = state.Yellow;
                screen.Cast(tricksterSlot, recolour);
                Assert.IsTrue(screen.IsTargeting);
                screen.ChooseDie(target.Index);
                screen.ChooseColor(DieColor.Yellow);
                Assert.IsFalse(screen.IsTargeting);
                Assert.AreEqual(DieColor.Yellow, target.Color);
                Assert.AreEqual(yellowBefore - 2 + 1, state.Yellow, "paid 2, the recoloured die added 1");
                Assert.AreEqual(DieColor.Yellow, DiceManager.Instance.GetResult(target.Index).ManaColor, "the physical die was turned to a Yellow face");
            }

            // Spend some Red on Strike if there is any.
            int knightSlot = state.Heroes.First(h => h.Color == DieColor.Red).Slot;
            if (state.Red >= 1)
            {
                int hp = state.Enemies[0].Health;
                Assert.IsTrue(state.Cast(knightSlot, 0, 0));
                Assert.That(state.Enemies[0].Health + state.Enemies[0].Shield, Is.LessThanOrEqualTo(hp), "Strike landed (or was blocked)");
            }

            int heroHp = state.Heroes.Sum(h => h.Health);
            screen.EndTurn();
            yield return null;
            Assert.IsFalse(state.IsOver, "a single Strike cannot win, and two regulars cannot wipe a fresh party in one turn");
            Assert.AreEqual(CombatPhase.Throw, state.Phase);
            Assert.AreEqual(2, state.Turn);
            Assert.That(state.Heroes.Sum(h => h.Health), Is.LessThanOrEqualTo(heroHp), "enemies acted on their intents");
            Assert.IsTrue(screen.WaitingForThrow, "a new throw is set up");
            Assert.AreEqual(DiceBoxState.Idle, DiceManager.Instance.State);
            Assert.AreEqual(6, DiceManager.Instance.ActiveDice.Count, "still six dice, dead or alive");
        }
    }
}
