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
    /// <summary>
    /// Real scene, real physics: start a run, pick traversal dice, commit two at the first fork,
    /// throw, see which paths the total unlocked, choose one, enter it, and come back to the map.
    /// </summary>
    public class MapFlowTests
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
        public IEnumerator TraversalPick_ForkRoll_Choose_Enter_Return()
        {
            var run = RunController.Instance;
            run.StartRun(run.Roster.Where(h => h.playableInPrototype).ToList());
            yield return null;
            Assert.AreEqual(RunPhase.TraversalPick, run.Phase);

            // Traversal pick: toggle three cards, confirm.
            var pick = Object.FindFirstObjectByType<TraversalPickScreen>();
            Assert.IsNotNull(pick);
            Assert.AreEqual(4, pick.CardButtons.Count);
            Assert.IsFalse(pick.ConfirmButton.interactable);
            pick.CardButtons[0].onClick.Invoke(); // Low Road
            pick.CardButtons[1].onClick.Invoke(); // Steady
            pick.CardButtons[2].onClick.Invoke(); // High Road
            Assert.IsTrue(pick.ConfirmButton.interactable);
            pick.ConfirmButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(RunPhase.Map, run.Phase);
            Assert.AreEqual(3, run.State.TraversalDice.Count);
            Assert.AreEqual(0, run.State.CurrentNodeIndex);
            Assert.IsTrue(run.State.CurrentNodeResolved, "start node is resolved");

            // Fork roll with two dice.
            var map = Object.FindFirstObjectByType<MapScreen>();
            Assert.IsNotNull(map);
            map.ToggleDie(0);
            map.ToggleDie(1);
            Assert.AreEqual(2, map.CommittedIndices.Count);
            map.StartForkRoll();
            Assert.IsTrue(map.AwaitingRoll);
            Assert.AreEqual(DiceBoxState.Idle, DiceManager.Instance.State);
            Assert.AreEqual(2, DiceManager.Instance.ActiveDice.Count);
            Assert.AreSame(run.TraversalProfile, DiceManager.Instance.GetComponent<DiceBoxVisuals>().Profile, "map rolls use the Traversal profile");
            yield return null;

            DiceManager.LaunchDice(new Vector3(3f, 0f, 40f));
            float deadline = Time.time + SettleTimeout;
            while (map.AwaitingRoll && Time.time < deadline) yield return null;
            Assert.IsFalse(map.AwaitingRoll, "fork roll settled");

            var fork = run.Act.nodes[0].fork;
            var expected = ForkResolver.UnlockedTargets(fork, map.LastTotal);
            Assert.IsTrue(map.HasPendingChoice);
            Assert.AreEqual(expected, map.UnlockedTargets.ToList());
            Assert.That(map.LastTotal, Is.InRange(2, 12), "two dice from Low Road + Steady");
            Assert.That(map.UnlockedTargets, Does.Contain(2), "the always-open Combat path is unlocked whatever the total");
            Assert.IsTrue(map.UnlockedTargets.All(i => map.NodeButtons[i].interactable), "unlocked nodes are clickable");
            Assert.IsTrue(fork.paths.Select(p => p.targetNode).Except(map.UnlockedTargets).All(i => !map.NodeButtons[i].interactable), "locked nodes are not");

            // Choose the highest unlocked path by clicking its node.
            int chosen = map.UnlockedTargets.Last();
            map.NodeButtons[chosen].onClick.Invoke();
            Assert.AreEqual(chosen, run.State.CurrentNodeIndex);
            Assert.IsFalse(run.State.CurrentNodeResolved);
            Assert.IsFalse(map.HasPendingChoice);

            // Enter it. Combat-type nodes go to the Combat phase and come back via CompleteNode;
            // Treasure opens the reward screen.
            var node = run.State.CurrentNode;
            map.EnterNode();
            yield return null;
            if (node.IsCombat)
            {
                Assert.AreEqual(RunPhase.Combat, run.Phase);
                run.CompleteNode();
                yield return null;
                Assert.AreEqual(RunPhase.Map, run.Phase);
            }
            else if (node.type == NodeType.Treasure)
            {
                Assert.AreEqual(RunPhase.Reward, run.Phase);
                Assert.IsNotNull(run.State.PendingReward);
                run.ChooseReward(0);
                yield return null;
                Assert.AreEqual(RunPhase.Map, run.Phase);
            }
            else
            {
                Assert.AreEqual(RunPhase.Map, run.Phase);
            }
            Assert.IsTrue(run.State.CurrentNodeResolved);
            Assert.AreEqual(4, node.nextNode, "every first-ring node leads to the Rest at node 4");
        }
    }
}
