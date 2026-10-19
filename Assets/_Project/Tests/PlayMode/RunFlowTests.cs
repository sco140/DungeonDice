using System.Collections;
using System.Linq;
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
    /// Loads the real Prototype scene and clicks through Hero Pick: six cards, three locked,
    /// pick three, Start Run, land on the next phase with a live run and a dice box present.
    /// </summary>
    public class RunFlowTests
    {
        private const string SceneName = "Prototype";
        private Scene m_scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.IsNotNull(load, $"scene '{SceneName}' is not in Build Settings; run DungeonDice ▸ Author Prototype Scene");
            yield return load;
            m_scene = SceneManager.GetSceneByName(SceneName);
            Assert.IsTrue(m_scene.isLoaded);
            yield return null; // let Start() run (RunController shows HeroPick)
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // These two move to DontDestroyOnLoad on Awake, so unloading the scene would leave them alive
            // and break the next test's singletons.
            if (DiceManager.Instance != null) Object.DestroyImmediate(DiceManager.Instance.gameObject);
            if (EventManager.Instance != null) Object.DestroyImmediate(EventManager.Instance.gameObject);
            if (m_scene.isLoaded) yield return SceneManager.UnloadSceneAsync(m_scene);
        }

        [UnityTest]
        public IEnumerator HeroPick_ToRun_ToAbandon()
        {
            var run = RunController.Instance;
            Assert.IsNotNull(run);
            Assert.AreEqual(RunPhase.HeroPick, run.Phase);
            Assert.AreEqual(6, run.Roster.Count);

            var screen = Object.FindFirstObjectByType<HeroPickScreen>();
            Assert.IsNotNull(screen, "Hero Pick screen is active");
            Assert.AreEqual(6, screen.CardButtons.Count);
            Assert.AreEqual(3, screen.CardButtons.Count(b => !b.interactable), "three locked slots");
            Assert.IsFalse(screen.StartButton.interactable);

            // Click three playable cards (Knight twice, then Trickster: duplicates are allowed).
            var playable = screen.CardButtons.Where(b => b.interactable).ToList();
            playable[0].onClick.Invoke();
            playable[0].onClick.Invoke();
            playable[2].onClick.Invoke();
            Assert.IsTrue(screen.Selection.CanConfirm);
            Assert.IsTrue(screen.StartButton.interactable);
            Assert.IsTrue(screen.CardButtons.All(b => !b.interactable), "cards lock once the party is full");

            screen.StartButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(RunPhase.TraversalPick, run.Phase);
            Assert.IsTrue(run.State.IsActive);
            Assert.AreEqual(3, run.State.Party.Count);
            Assert.AreEqual(run.Roster[0], run.State.Party[0].Definition);
            Assert.AreEqual(run.Roster[0], run.State.Party[1].Definition);
            Assert.AreEqual(run.Roster[2], run.State.Party[2].Definition);
            Assert.IsFalse(screen.gameObject.activeSelf, "Hero Pick is hidden after the run starts");

            Assert.IsNotNull(DiceManager.Instance, "the DiceBox prefab is in the scene");
            Assert.AreEqual(DiceBoxState.Empty, DiceManager.Instance.State);

            run.AbandonRun();
            yield return null;
            Assert.AreEqual(RunPhase.HeroPick, run.Phase);
            Assert.IsFalse(run.State.IsActive);
            Assert.IsTrue(screen.gameObject.activeSelf);
            Assert.AreEqual(0, screen.Selection.Party.Count, "selection resets when the screen reopens");
        }
    }
}
