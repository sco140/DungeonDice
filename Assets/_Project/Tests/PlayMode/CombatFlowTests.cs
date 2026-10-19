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
    /// Real scene, real physics: enter a Combat node, throw the three hero dice in the box, keep them,
    /// bank all three for mana, end the turn, and get a fresh throw set up.
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
        public IEnumerator Combat_Throw_Keep_BankAll_EndTurn()
        {
            var run = RunController.Instance;
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
            Assert.AreEqual(3, DiceManager.Instance.ActiveDice.Count);
            Assert.AreSame(run.CombatProfile, DiceManager.Instance.GetComponent<DiceBoxVisuals>().Profile, "combat uses the Combat profile");
            Assert.AreEqual(new[] { 0, 1, 2 }, screen.OwnerToSlot.ToArray());

            DiceManager.LaunchDice(new Vector3(2f, 0f, 40f));
            float deadline = Time.time + SettleTimeout;
            while (state.Phase == CombatPhase.Throw && Time.time < deadline) yield return null;
            Assert.AreEqual(CombatPhase.Reroll, state.Phase, "dice settled and the reroll decision is open");
            Assert.AreEqual(3, state.Dice.Count);
            Assert.IsFalse(screen.WaitingForThrow);

            screen.KeepAll();
            Assert.AreEqual(CombatPhase.Act, state.Phase);

            screen.Bank(0);
            screen.Bank(1);
            screen.Bank(2);
            Assert.AreEqual((1, 1, 1), (state.Red, state.Blue, state.Yellow), "Knight, Arcanist, Trickster each bank their colour");

            int hpBefore = state.Heroes.Sum(h => h.Health);
            screen.EndTurn();
            yield return null;
            Assert.IsFalse(state.IsOver, "banking cannot win, and two regulars cannot wipe a fresh party in one turn");
            Assert.AreEqual(CombatPhase.Throw, state.Phase);
            Assert.AreEqual(2, state.Turn);
            Assert.That(state.Heroes.Sum(h => h.Health), Is.LessThanOrEqualTo(hpBefore), "enemies acted on their intents");
            Assert.IsTrue(screen.WaitingForThrow, "a new throw is set up");
            Assert.AreEqual(DiceBoxState.Idle, DiceManager.Instance.State);
            Assert.AreEqual(state.LivingHeroes.Count(), DiceManager.Instance.ActiveDice.Count);
        }

    }
}
