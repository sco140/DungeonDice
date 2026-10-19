using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using DungeonDice.Dice;
using DungeonDice.Map;

namespace DungeonDice.Run
{
    /// <summary>The screens of a run, in order. <see cref="ScreenRouter"/> shows one at a time.</summary>
    public enum RunPhase
    {
        HeroPick,
        TraversalPick,
        Map,
        Combat,
        /// <summary>Treasure node: choose one <see cref="RewardOption"/>.</summary>
        Reward,
        RunEnd,
    }

    public enum RunOutcome
    {
        None,
        Victory,
        Defeat,
    }

    /// <summary>One hero's mutable state for the current run: health and their (upgradeable) die copy.</summary>
    public sealed class HeroRunState
    {
        public HeroDefinition Definition { get; }
        /// <summary>Slot in the party, 0–2. Doubles as the owner id of the hero's die in the box.</summary>
        public int Slot { get; }
        public int MaxHealth { get; }
        public int CurrentHealth { get; private set; }
        /// <summary>
        /// Runtime copy of the hero's first die so face upgrades never touch the asset.
        /// The prototype uses one die per hero; <see cref="HeroDefinition.dice"/> keeps a list for later.
        /// </summary>
        public DieDefinition Die { get; private set; }

        /// <summary>Indices into <see cref="HeroDefinition.faceUpgrades"/> already taken this run.</summary>
        public IReadOnlyCollection<int> AppliedUpgrades => m_appliedUpgrades;
        private readonly HashSet<int> m_appliedUpgrades = new();

        public bool IsAlive => CurrentHealth > 0;

        public HeroRunState(HeroDefinition definition, int slot)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Slot = slot;
            MaxHealth = definition.maxHealth;
            CurrentHealth = definition.maxHealth;
            Die = definition.PrimaryDie != null ? UnityEngine.Object.Instantiate(definition.PrimaryDie) : null;
            if (Die != null) Die.name = $"{definition.displayName} die (slot {slot})";
        }

        /// <summary>Clamped damage; returns the amount actually lost.</summary>
        public int TakeDamage(int amount)
        {
            int before = CurrentHealth;
            CurrentHealth = Math.Max(0, CurrentHealth - Math.Max(0, amount));
            return before - CurrentHealth;
        }

        /// <summary>Clamped heal; returns the amount actually restored. Dead heroes stay dead.</summary>
        public int Heal(int amount)
        {
            if (!IsAlive) return 0;
            int before = CurrentHealth;
            CurrentHealth = Math.Min(MaxHealth, CurrentHealth + Math.Max(0, amount));
            return CurrentHealth - before;
        }

        /// <summary>Face-replace upgrade (Treasure reward). Swaps the die copy, never the asset.</summary>
        public void UpgradeFace(int faceIndex, DieFace replacement)
        {
            if (Die == null) throw new InvalidOperationException($"{Definition.displayName} has no die to upgrade.");
            var upgraded = Die.WithFaceReplaced(faceIndex, replacement);
            DestroyRuntimeObject(Die);
            Die = upgraded;
        }

        /// <summary>Upgrades on the definition that have not been taken yet (as indices).</summary>
        public IEnumerable<int> AvailableUpgrades()
        {
            var list = Definition.faceUpgrades;
            if (list == null || Die == null) yield break;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && !m_appliedUpgrades.Contains(i)) yield return i;
            }
        }

        /// <summary>Take an upgrade from the definition's list (Treasure reward).</summary>
        public void ApplyUpgrade(int upgradeIndex)
        {
            var list = Definition.faceUpgrades;
            if (list == null || upgradeIndex < 0 || upgradeIndex >= list.Count)
                throw new ArgumentOutOfRangeException(nameof(upgradeIndex));
            if (!m_appliedUpgrades.Add(upgradeIndex))
                throw new InvalidOperationException("Upgrade already applied.");
            var upgrade = list[upgradeIndex];
            UpgradeFace(upgrade.faceIndex, upgrade.replacement);
        }

        /// <summary>Destroy a runtime-created object in Play mode or in the Editor (tests run in edit mode).</summary>
        internal static void DestroyRuntimeObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    /// <summary>
    /// Everything that persists across the screens of one run: the party, the traversal dice owned,
    /// the act and position in it, the phase, the outcome and the seed. Plain C# so it can be
    /// unit-tested; owned by <see cref="RunController"/>. Combat-scoped state (mana pool, statuses)
    /// lives in combat, not here.
    /// </summary>
    public sealed class RunState
    {
        public const int PartySize = 3;
        public const int TraversalPickCount = 3;

        private readonly List<HeroRunState> m_party = new();
        private readonly List<DieDefinition> m_traversalDice = new();
        private readonly HashSet<int> m_visited = new();

        public RunPhase Phase { get; set; } = RunPhase.HeroPick;
        public RunOutcome Outcome { get; private set; } = RunOutcome.None;
        public int Seed { get; private set; }
        /// <summary>Seeded RNG for everything except the physical throw. Reset per run.</summary>
        public RollService Rng { get; private set; }
        public IReadOnlyList<HeroRunState> Party => m_party;
        public IReadOnlyList<DieDefinition> TraversalDice => m_traversalDice;
        public bool IsActive { get; private set; }

        // --- map progress ---
        public ActDefinition Act { get; private set; }
        public int CurrentNodeIndex { get; private set; }
        /// <summary>True once the current node's content (combat, rest, treasure) is done and the exits open.</summary>
        public bool CurrentNodeResolved { get; private set; }
        public IReadOnlyCollection<int> VisitedNodes => m_visited;
        public MapNode CurrentNode => Act != null ? Act.Node(CurrentNodeIndex) : null;
        /// <summary>The Treasure offer being chosen from, while <see cref="Phase"/> is Reward.</summary>
        public RewardOffer PendingReward { get; set; }
        /// <summary>One line about what just happened at this node (rest, treasure, victory), shown on the map.</summary>
        public string LastNodeSummary { get; set; }

        public IEnumerable<HeroRunState> LivingHeroes => m_party.Where(h => h.IsAlive);
        public bool PartyWiped => IsActive && m_party.All(h => !h.IsAlive);

        /// <summary>Begin a run with exactly <see cref="PartySize"/> playable heroes. Duplicates are allowed.</summary>
        public void StartNewRun(IReadOnlyList<HeroDefinition> heroes, int seed)
        {
            if (heroes == null || heroes.Count != PartySize)
                throw new ArgumentException($"A party needs exactly {PartySize} heroes.", nameof(heroes));
            var locked = heroes.FirstOrDefault(h => h == null || !h.playableInPrototype);
            if (locked != null || heroes.Any(h => h == null))
                throw new ArgumentException($"'{locked?.displayName ?? "null"}' is not playable in the prototype.", nameof(heroes));

            Clear();
            Seed = seed;
            Rng = new RollService(seed);
            for (int i = 0; i < heroes.Count; i++) m_party.Add(new HeroRunState(heroes[i], i));
            Phase = RunPhase.TraversalPick;
            IsActive = true;
        }

        public void SetTraversalDice(IEnumerable<DieDefinition> dice)
        {
            m_traversalDice.Clear();
            m_traversalDice.AddRange(dice.Where(d => d != null));
        }

        public void AddTraversalDie(DieDefinition die)
        {
            if (die != null) m_traversalDice.Add(die);
        }

        /// <summary>Place the party at the act's start node. The Start node has no content, so it is already resolved.</summary>
        public void EnterAct(ActDefinition act)
        {
            Act = act ?? throw new ArgumentNullException(nameof(act));
            if (act.Node(act.startNode) == null) throw new ArgumentException($"{act.name} has no start node.", nameof(act));
            m_visited.Clear();
            CurrentNodeIndex = act.startNode;
            m_visited.Add(CurrentNodeIndex);
            CurrentNodeResolved = CurrentNode.type == NodeType.Start;
            Phase = RunPhase.Map;
        }

        /// <summary>Travel to a node. Its content is unresolved until <see cref="ResolveCurrentNode"/>.</summary>
        public void MoveTo(int nodeIndex)
        {
            if (Act == null) throw new InvalidOperationException("No act entered.");
            if (Act.Node(nodeIndex) == null) throw new ArgumentOutOfRangeException(nameof(nodeIndex), nodeIndex, "no such node");
            CurrentNodeIndex = nodeIndex;
            m_visited.Add(nodeIndex);
            CurrentNodeResolved = Act.Node(nodeIndex).type == NodeType.Start;
            LastNodeSummary = null;
        }

        public void ResolveCurrentNode() => CurrentNodeResolved = true;

        public void EndRun(RunOutcome outcome)
        {
            Outcome = outcome;
            Phase = RunPhase.RunEnd;
        }

        /// <summary>Forget the run. Destroys the heroes' runtime die copies.</summary>
        public void Clear()
        {
            foreach (var hero in m_party) HeroRunState.DestroyRuntimeObject(hero.Die);
            m_party.Clear();
            m_traversalDice.Clear();
            m_visited.Clear();
            Act = null;
            CurrentNodeIndex = 0;
            CurrentNodeResolved = false;
            PendingReward = null;
            LastNodeSummary = null;
            Outcome = RunOutcome.None;
            IsActive = false;
            Phase = RunPhase.HeroPick;
        }
    }
}
