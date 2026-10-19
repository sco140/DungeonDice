using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using DungeonDice.Dice;
using GameUtility;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonDice.Tests
{
    /// <summary>
    /// Integration test of the dice box prefab with real physics: throw, settle, modify, reroll,
    /// and a second setup with fewer dice than the pool (the bug the active list fixes).
    /// </summary>
    public class DiceBoxTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/DiceBox.prefab";
        private const float SettleTimeout = 25f;

        private GameObject m_eventManager;
        private GameObject m_box;
        private readonly List<RollCompletedEvent> m_completed = new();

        [SetUp]
        public void SetUp()
        {
            m_eventManager = new GameObject("EventManager", typeof(EventManager));
            m_completed.Clear();
            EventManager.Register(GameEvents.RollCompleted, new GameUtility.EventHandler<RollCompletedEvent>(OnRollCompleted));

#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#else
            GameObject prefab = null;
#endif
            Assert.IsNotNull(prefab, $"DiceBox prefab missing at {PrefabPath}; run DungeonDice ▸ Author DiceBox Prefab.");
            m_box = Object.Instantiate(prefab, new Vector3(0f, -500f, 0f), Quaternion.identity);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.Unregister<RollCompletedEvent>(GameEvents.RollCompleted, OnRollCompleted);
            // Immediate, not deferred: the next test's SetUp creates fresh singletons in the same frame,
            // and a still-alive old EventManager would make the new one destroy itself.
            Object.DestroyImmediate(m_box);
            Object.DestroyImmediate(m_eventManager);
        }

        private void OnRollCompleted(RollCompletedEvent evt) => m_completed.Add(evt);

        private static DieDefinition Load(string name)
        {
#if UNITY_EDITOR
            var die = UnityEditor.AssetDatabase.LoadAssetAtPath<DieDefinition>($"Assets/_Project/Data/Dice/Heroes/{name}.asset");
#else
            DieDefinition die = null;
#endif
            Assert.IsNotNull(die, $"missing die asset {name}");
            return die;
        }

        private IEnumerator WaitForCompletion(int expectedCount)
        {
            float deadline = Time.time + SettleTimeout;
            while (m_completed.Count < expectedCount && Time.time < deadline) yield return null;
            Assert.AreEqual(expectedCount, m_completed.Count, $"roll did not complete within {SettleTimeout}s (state {DiceManager.Instance.State})");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Throw_Modify_Reroll_ThenSmallerSetup_AllComplete()
        {
            var heroes = new[] { Load("Knight"), Load("Arcanist"), Load("Trickster") };
            var defaultGravity = Physics.gravity;

            // --- first throw: three dice, one per hero -------------------------------------------
            DiceManager.SetUpDice(heroes);
            Assert.AreEqual(DiceBoxState.Idle, DiceManager.Instance.State);
            Assert.AreEqual(3, DiceManager.Instance.ActiveDice.Count);
            yield return null;

            DiceManager.LaunchDice(new Vector3(4f, 0f, 40f));
            Assert.AreEqual(DiceBoxState.Rolling, DiceManager.Instance.State);
            Assert.AreNotEqual(defaultGravity, Physics.gravity, "gravity is overridden in flight");

            yield return WaitForCompletion(1);
            var roll = m_completed[0];
            Assert.AreEqual(DiceBoxState.Settled, DiceManager.Instance.State);
            Assert.AreEqual(defaultGravity, Physics.gravity, "gravity restored on completion");
            Assert.AreEqual(new[] { 0, 1, 2 }, roll.Results.Select(r => r.OwnerId).ToArray());
            Assert.AreEqual(heroes, roll.Results.Select(r => r.Die).ToArray(), "each result carries its hero's die");
            Assert.AreSame(roll, DiceManager.Instance.LastRoll);

            // --- modifier: Flip die 0 and check the physical die shows the new face -------------
            var before = roll.Results[0].FaceIndex;
            var modified = DiceManager.ApplyModifier(0, new FlipModifier());
            Assert.IsNotNull(modified);
            Assert.AreEqual(DieDefinition.OppositeFace(before), modified.FaceIndex);
            var handler0 = DiceManager.Instance.ActiveDice.First(d => d.OwnerId == 0);
            Assert.AreEqual(modified.FaceIndex, DiceHandler.FaceUpFor(handler0.transform), "die re-oriented to the modified face");

            // --- reroll: lock die 1, ask to re-throw 1 and 2; only 2 should fly ------------------
            DiceManager.ApplyModifier(1, new LockModifier());
            var lockedFace = DiceManager.Instance.GetResult(1).FaceIndex;
            DiceManager.RelaunchDice(new[] { 1, 2 }, new Vector3(-3f, 0f, 35f));
            Assert.AreEqual(DiceBoxState.Rolling, DiceManager.Instance.State);
            yield return WaitForCompletion(2);

            var reroll = m_completed[1];
            Assert.AreEqual(3, reroll.Results.Count, "a reroll still reports the full set");
            Assert.AreEqual(lockedFace, reroll.Results[1].FaceIndex, "locked die kept its result");
            Assert.AreEqual(modified.FaceIndex, reroll.Results[0].FaceIndex, "untouched die kept its (modified) result");

            // --- second setup with fewer dice than the pool must still complete -----------------
            DiceManager.SetUpDice(new[] { heroes[0], heroes[2] });
            Assert.AreEqual(2, DiceManager.Instance.ActiveDice.Count);
            Assert.IsNull(DiceManager.Instance.LastRoll, "setup clears the previous roll");
            yield return null;

            DiceManager.LaunchDice(new Vector3(0f, 0f, 30f));
            yield return WaitForCompletion(3);
            Assert.AreEqual(2, m_completed[2].Results.Count);
            Assert.AreEqual(new[] { 0, 1 }, m_completed[2].Results.Select(r => r.OwnerId).ToArray());
        }

        [Test]
        public void Launch_IsRefused_BeforeSetup_AndRelaunch_IsRefused_WhileIdle()
        {
            LogAssert.Expect(LogType.Warning, "DiceManager: no dice have been set up to launch.");
            DiceManager.LaunchDice(Vector3.forward);

            DiceManager.SetUpDice(new[] { Load("Knight") });
            LogAssert.Expect(LogType.Warning, "DiceManager: cannot relaunch while Idle.");
            DiceManager.RelaunchDice(new[] { 0 }, Vector3.forward);
            Assert.AreEqual(DiceBoxState.Idle, DiceManager.Instance.State);
        }
    }
}
