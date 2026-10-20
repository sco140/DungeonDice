using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using DungeonDice.Dice;
using DungeonDice.Map;
using GameUtility;
using UnityEngine;

namespace DungeonDice.Run
{
    /// <summary>
    /// Owns the <see cref="RunState"/> and drives the run through its phases, showing each phase's
    /// screen via <see cref="ScreenRouter"/>. Screens call back into it (<see cref="StartRun"/>,
    /// <see cref="ConfirmTraversalDice"/>, <see cref="EnterCurrentNode"/>, <see cref="TravelTo"/>,
    /// <see cref="CompleteNode"/>) rather than touching the state directly. Also the one place that
    /// knows which assets the run uses: the act, the dice box profiles, the starting traversal pool.
    /// </summary>
    public class RunController : MonoBehaviour
    {
        public static RunController Instance { get; private set; }

        [SerializeField] private ScreenRouter m_router;
        [Tooltip("The six hero slots of the pick screen, in display order. Locked heroes are shown greyed.")]
        [SerializeField] private List<HeroDefinition> m_roster = new();
        [Tooltip("0 = random seed per run. Any other value makes the run reproducible (apart from the dice).")]
        [SerializeField] private int m_seedOverride = 0;

        [Header("Map")]
        [SerializeField] private ActDefinition m_act;
        [Tooltip("The dice offered at the start node; the player takes RunState.TraversalPickCount of them.")]
        [SerializeField] private List<DieDefinition> m_startingTraversalPool = new();
        [Tooltip("Treasure can grant one of these traversal dice (drawn with the run RNG).")]
        [SerializeField] private List<DieDefinition> m_rewardDicePool = new();
        [Range(0f, 1f)] [SerializeField] private float m_restHealFraction = 0.3f;

        [Header("UI")]
        [SerializeField] private UI.IconLibrary m_icons;

        [Header("Dice box profiles")]
        [SerializeField] private DiceBoxProfile m_traversalProfile;
        [SerializeField] private DiceBoxProfile m_combatProfile;

        [Header("Enemies")]
        [Tooltip("Combat nodes spawn two of these, picked with the run's seeded RNG.")]
        [SerializeField] private List<Combat.EnemyDefinition> m_regularEnemies = new();
        [SerializeField] private Combat.EnemyDefinition m_eliteEnemy;
        [SerializeField] private Combat.EnemyDefinition m_bossEnemy;
        [Min(1)] [SerializeField] private int m_regularsPerCombat = 2;

        public RunState State { get; private set; } = new();
        public IReadOnlyList<HeroDefinition> Roster => m_roster;
        public IReadOnlyList<DieDefinition> StartingTraversalPool => m_startingTraversalPool;
        public IReadOnlyList<DieDefinition> RewardDicePool => m_rewardDicePool;
        public ActDefinition Act => m_act;
        public DiceBoxProfile TraversalProfile => m_traversalProfile;
        public DiceBoxProfile CombatProfile => m_combatProfile;
        /// <summary>Icon set for every screen; may be null in tests (screens fall back to text).</summary>
        public UI.IconLibrary Icons => m_icons;
        public static UI.IconLibrary CurrentIcons => Instance != null ? Instance.m_icons : null;

        /// <summary>Authoring-time wiring; the scene tool calls this.</summary>
        public void ConfigureIcons(UI.IconLibrary icons) => m_icons = icons;
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

        // ---------------------------------------------------------------- run lifecycle

        /// <summary>Called by the Hero Pick screen with exactly three playable heroes.</summary>
        public void StartRun(IReadOnlyList<HeroDefinition> party)
        {
            int seed = m_seedOverride != 0 ? m_seedOverride : Environment.TickCount;
            State.StartNewRun(party, seed);
            RollService.ResetShared(seed);
            Debug.Log($"RunController: run started, seed {seed}, party {string.Join(", ", party.Select(h => h.displayName))}.");
            SetPhase(RunPhase.TraversalPick);
        }

        /// <summary>Called by the Traversal Pick screen; enters the act at its start node.</summary>
        public void ConfirmTraversalDice(IReadOnlyList<DieDefinition> dice)
        {
            if (dice == null || dice.Count != RunState.TraversalPickCount)
                throw new ArgumentException($"Pick exactly {RunState.TraversalPickCount} traversal dice.", nameof(dice));
            if (m_act == null)
            {
                Debug.LogError("RunController: no act assigned.");
                return;
            }
            State.SetTraversalDice(dice);
            State.EnterAct(m_act);
            Debug.Log($"RunController: traversal dice {string.Join(", ", dice.Select(d => d.displayName))}; entering {m_act.displayName}.");
            SetPhase(RunPhase.Map);
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

        // ---------------------------------------------------------------- map flow

        /// <summary>
        /// Play the current node's content. Combat-type nodes hand off to the Combat phase and come
        /// back through <see cref="CompleteNode"/>; Rest and Treasure resolve immediately.
        /// </summary>
        public void EnterCurrentNode()
        {
            var node = State.CurrentNode;
            if (node == null || State.CurrentNodeResolved) return;

            switch (node.type)
            {
                case NodeType.Combat:
                case NodeType.Elite:
                case NodeType.Boss:
                    SetPhase(RunPhase.Combat);
                    break;

                case NodeType.Rest:
                {
                    var parts = new List<string>();
                    foreach (var hero in State.LivingHeroes)
                    {
                        int healed = hero.Heal(Mathf.CeilToInt(hero.MaxHealth * m_restHealFraction));
                        parts.Add($"{hero.Definition.displayName} +{healed}");
                    }
                    State.LastNodeSummary = $"Rested: {string.Join(", ", parts)}.";
                    Debug.Log($"RunController: {State.LastNodeSummary}");
                    State.ResolveCurrentNode();
                    break;
                }

                case NodeType.Treasure:
                {
                    var offer = RewardOffer.Generate(State, m_rewardDicePool, State.Rng);
                    if (offer.Options.Count == 0)
                    {
                        State.LastNodeSummary = "The treasure was empty.";
                        State.ResolveCurrentNode();
                        break;
                    }
                    State.PendingReward = offer;
                    SetPhase(RunPhase.Reward);
                    break;
                }

                default:
                    State.ResolveCurrentNode();
                    break;
            }
        }

        /// <summary>The Reward screen's choice. Applies it, resolves the Treasure node and returns to the map.</summary>
        public void ChooseReward(int optionIndex)
        {
            var offer = State.PendingReward;
            if (offer == null)
            {
                Debug.LogWarning("RunController: no reward pending.");
                return;
            }
            State.LastNodeSummary = offer.Apply(optionIndex, State);
            Debug.Log($"RunController: {State.LastNodeSummary}");
            State.PendingReward = null;
            State.ResolveCurrentNode();
            SetPhase(RunPhase.Map);
        }

        /// <summary>Called when the current node's combat is over (won). The Boss ends the run in victory.</summary>
        public void CompleteNode()
        {
            var node = State.CurrentNode;
            if (node == null) return;
            State.ResolveCurrentNode();
            State.LastNodeSummary = $"Victory at {node.name}.";
            if (node.type == NodeType.Boss)
            {
                State.EndRun(RunOutcome.Victory);
                SetPhase(RunPhase.RunEnd);
            }
            else
            {
                SetPhase(RunPhase.Map);
            }
        }

        /// <summary>The party is dead. Ends the run in defeat.</summary>
        public void PartyWiped()
        {
            State.EndRun(RunOutcome.Defeat);
            SetPhase(RunPhase.RunEnd);
        }

        // ---------------------------------------------------------------- Loaded Dice on the map

        /// <summary>The living hero whose signature is Loaded Dice, or null.</summary>
        public HeroRunState LoadedDiceCaster =>
            State.LivingHeroes.FirstOrDefault(h => h.Definition.signatureKind == SignatureKind.LoadedDice);

        /// <summary>
        /// Loaded Dice works on traversal rolls too: it needs a living Trickster and 3 Yellow mana carried
        /// out of the last fight (see <see cref="RunState.SetCarriedMana"/>).
        /// </summary>
        public bool CanCastLoadedDiceOnMap =>
            State.IsActive && LoadedDiceCaster != null && State.CarriedMana(DieColor.Yellow) >= Combat.CombatState.SignatureCost;

        /// <summary>Pay the Loaded Dice cost from carried mana. False if it cannot be afforded.</summary>
        public bool PayLoadedDiceOnMap()
        {
            if (!CanCastLoadedDiceOnMap) return false;
            State.SpendCarriedMana(DieColor.Yellow, Combat.CombatState.SignatureCost);
            Debug.Log($"RunController: {LoadedDiceCaster.Definition.displayName} casts Loaded Dice on the map; Yellow left {State.CarriedYellow}.");
            return true;
        }

        /// <summary>Move along a resolved node's exit (single exit or a path chosen by a fork roll).</summary>
        public void TravelTo(int nodeIndex)
        {
            if (!State.CurrentNodeResolved)
            {
                Debug.LogWarning("RunController: resolve the current node before travelling.");
                return;
            }
            State.MoveTo(nodeIndex);
            Debug.Log($"RunController: travelled to node {nodeIndex} ({State.CurrentNode.name}).");
        }

        /// <summary>
        /// The enemies a node spawns. Combat: two regulars chosen with the run RNG (repeats allowed);
        /// Elite and Boss: their single enemy. Empty if nothing is configured.
        /// </summary>
        public List<Combat.EnemyDefinition> EnemiesFor(NodeType type)
        {
            var result = new List<Combat.EnemyDefinition>();
            switch (type)
            {
                case NodeType.Elite:
                    if (m_eliteEnemy != null) result.Add(m_eliteEnemy);
                    break;
                case NodeType.Boss:
                    if (m_bossEnemy != null) result.Add(m_bossEnemy);
                    break;
                default:
                    if (m_regularEnemies.Count == 0) break;
                    var rng = State.Rng ?? RollService.Shared;
                    for (int i = 0; i < m_regularsPerCombat; i++) result.Add(rng.Pick(m_regularEnemies));
                    break;
            }
            return result;
        }

        /// <summary>Authoring-time wiring; the scene tool calls this.</summary>
        public void ConfigureEnemies(IEnumerable<Combat.EnemyDefinition> regulars, Combat.EnemyDefinition elite, Combat.EnemyDefinition boss)
        {
            m_regularEnemies.Clear();
            m_regularEnemies.AddRange(regulars);
            m_eliteEnemy = elite;
            m_bossEnemy = boss;
        }

        /// <summary>Authoring-time wiring; the scene tool calls this.</summary>
        public void Configure(ScreenRouter router, IEnumerable<HeroDefinition> roster, ActDefinition act,
            IEnumerable<DieDefinition> startingTraversalPool, IEnumerable<DieDefinition> rewardDicePool,
            DiceBoxProfile traversalProfile, DiceBoxProfile combatProfile)
        {
            m_router = router;
            m_roster.Clear();
            m_roster.AddRange(roster);
            m_act = act;
            m_startingTraversalPool.Clear();
            m_startingTraversalPool.AddRange(startingTraversalPool);
            m_rewardDicePool.Clear();
            m_rewardDicePool.AddRange(rewardDicePool);
            m_traversalProfile = traversalProfile;
            m_combatProfile = combatProfile;
        }
    }
}
