using System.Collections.Generic;
using DungeonDice.Core;
using GameUtility;
using UnityEngine;

namespace DungeonDice.Run
{
    /// <summary>
    /// Owns the <see cref="RunState"/> and drives the run through its phases, showing each phase's
    /// screen via <see cref="ScreenRouter"/>. Screens call back into it (<see cref="StartRun"/>,
    /// <see cref="SetPhase"/>) rather than touching the state directly.
    /// </summary>
    public class RunController : MonoBehaviour
    {
        public static RunController Instance { get; private set; }

        [SerializeField] private ScreenRouter m_router;
        [Tooltip("The six hero slots of the pick screen, in display order. Locked heroes are shown greyed.")]
        [SerializeField] private List<HeroDefinition> m_roster = new();
        [Tooltip("0 = random seed per run. Any other value makes the run reproducible (apart from the dice).")]
        [SerializeField] private int m_seedOverride = 0;

        public RunState State { get; private set; } = new();
        public IReadOnlyList<HeroDefinition> Roster => m_roster;
        public RunPhase Phase => State.Phase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (m_router == null) m_router = GetComponent<ScreenRouter>();
        }

        private void Start()
        {
            SetPhase(RunPhase.HeroPick);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Called by the Hero Pick screen with exactly three playable heroes.</summary>
        public void StartRun(IReadOnlyList<HeroDefinition> party)
        {
            int seed = m_seedOverride != 0 ? m_seedOverride : System.Environment.TickCount;
            State.StartNewRun(party, seed);
            RollService.ResetShared(seed);
            Debug.Log($"RunController: run started, seed {seed}, party {string.Join(", ", party.ConvertAllNames())}.");
            SetPhase(RunPhase.TraversalPick);
        }

        /// <summary>Back to the pick screen, forgetting the current run.</summary>
        public void AbandonRun()
        {
            State.Clear();
            SetPhase(RunPhase.HeroPick);
        }

        public void SetPhase(RunPhase phase)
        {
            State.Phase = phase;
            if (m_router != null) m_router.Show(phase);
            EventManager.TriggerEvent(GameEvents.RunPhaseChanged, phase);
        }

        /// <summary>Authoring-time wiring; the scene tool calls this.</summary>
        public void Configure(ScreenRouter router, IEnumerable<HeroDefinition> roster)
        {
            m_router = router;
            m_roster.Clear();
            m_roster.AddRange(roster);
        }
    }

    internal static class HeroListExtensions
    {
        public static IEnumerable<string> ConvertAllNames(this IReadOnlyList<HeroDefinition> heroes)
        {
            foreach (var hero in heroes) yield return hero != null ? hero.displayName : "null";
        }
    }
}
