using System.Collections;
using System.Linq;
using DungeonDice.Dice;
using DungeonDice.Map;
using DungeonDice.Run;
using DungeonDice.UI;
using GameUtility;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DungeonDice.Tests
{
    /// <summary>Real scene: Treasure opens the reward screen and the choice lands on the map; Rest heals; defeat and victory reach the end screen.</summary>
    public class RewardFlowTests
    {
        private const string SceneName = "Prototype";
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

        private static RunController StartRunOnMap()
        {
            var run = RunController.Instance;
            run.StartRun(run.Roster.Where(h => h.playableInPrototype).ToList());
            run.ConfirmTraversalDice(run.StartingTraversalPool.Take(3).ToList());
            return run;
        }

        [UnityTest]
        public IEnumerator Treasure_OpensRewardScreen_ChoiceReturnsToMap()
        {
            var run = StartRunOnMap();
            yield return null;
            run.TravelTo(1); // Forgotten Cache (Treasure)
            Assert.AreEqual(NodeType.Treasure, run.State.CurrentNode.type);

            run.EnterCurrentNode();
            yield return null;
            Assert.AreEqual(RunPhase.Reward, run.Phase);
            Assert.IsNotNull(run.State.PendingReward);
            var screen = Object.FindFirstObjectByType<RewardScreen>();
            Assert.IsNotNull(screen);
            Assert.AreEqual(run.State.PendingReward.Options.Count, screen.OptionButtons.Count);
            Assert.AreEqual(3, screen.OptionButtons.Count, "one die plus two hero upgrades with a fresh party");

            int diceBefore = run.State.TraversalDice.Count;
            int dieOption = run.State.PendingReward.Options.ToList().FindIndex(o => o.Kind == RewardKind.TraversalDie);
            screen.OptionButtons[dieOption].onClick.Invoke();
            yield return null;

            Assert.AreEqual(RunPhase.Map, run.Phase);
            Assert.IsNull(run.State.PendingReward);
            Assert.IsTrue(run.State.CurrentNodeResolved);
            Assert.AreEqual(diceBefore + 1, run.State.TraversalDice.Count);
            Assert.That(run.State.LastNodeSummary, Does.Contain("Treasure"));
            Assert.That(run.RewardDicePool, Does.Contain(run.State.TraversalDice.Last()));
        }

        [UnityTest]
        public IEnumerator Rest_HealsLivingHeroes_AndSummarises()
        {
            var run = StartRunOnMap();
            yield return null;
            run.State.Party[0].TakeDamage(10);
            run.TravelTo(2);            // Combat node: skip its content for this test
            run.State.ResolveCurrentNode();
            run.TravelTo(4);            // Dry Alcove (Rest)
            Assert.AreEqual(NodeType.Rest, run.State.CurrentNode.type);

            int before = run.State.Party[0].CurrentHealth;
            run.EnterCurrentNode();
            Assert.AreEqual(RunPhase.Map, run.Phase);
            Assert.IsTrue(run.State.CurrentNodeResolved);
            Assert.AreEqual(before + Mathf.CeilToInt(run.State.Party[0].MaxHealth * 0.3f), run.State.Party[0].CurrentHealth);
            Assert.That(run.State.LastNodeSummary, Does.StartWith("Rested"));
            Assert.IsTrue(run.State.CurrentNode.HasFork, "the Rest leads to a fork");
        }

        [UnityTest]
        public IEnumerator Defeat_AndVictory_ReachTheEndScreen()
        {
            var run = StartRunOnMap();
            yield return null;

            run.PartyWiped();
            yield return null;
            Assert.AreEqual(RunPhase.RunEnd, run.Phase);
            Assert.AreEqual(RunOutcome.Defeat, run.State.Outcome);
            var end = Object.FindFirstObjectByType<RunEndScreen>();
            Assert.IsNotNull(end);
            Assert.That(end.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Defeat"));

            end.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "NewRun").onClick.Invoke();
            yield return null;
            Assert.AreEqual(RunPhase.HeroPick, run.Phase);
            Assert.IsFalse(run.State.IsActive);

            // Victory: walk to the Boss and complete it.
            run = StartRunOnMap();
            yield return null;
            run.TravelTo(3); run.State.ResolveCurrentNode();
            run.TravelTo(4); run.State.ResolveCurrentNode();
            run.TravelTo(7); run.State.ResolveCurrentNode();
            run.TravelTo(8); run.State.ResolveCurrentNode();
            run.TravelTo(10);
            Assert.AreEqual(NodeType.Boss, run.State.CurrentNode.type);
            run.CompleteNode();
            yield return null;
            Assert.AreEqual(RunPhase.RunEnd, run.Phase);
            Assert.AreEqual(RunOutcome.Victory, run.State.Outcome);
            Assert.That(Object.FindFirstObjectByType<RunEndScreen>().GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Victory!"));
        }
    }
}
