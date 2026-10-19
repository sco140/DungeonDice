using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using DungeonDice.Dice;

namespace DungeonDice.Run
{
    /// <summary>The screens of a run, in order. <see cref="ScreenRouter"/> shows one at a time.</summary>
    public enum RunPhase
    {
        HeroPick,
        TraversalPick,
        Map,
        Combat,
        RunEnd,
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

        /// <summary>Clamped heal; returns the amount actually restored.</summary>
        public int Heal(int amount)
        {
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
    /// the map position, the phase and the seed. Plain C# so it can be unit-tested; owned by
    /// <see cref="RunController"/>. Combat-scoped state (mana pool, statuses) lives in combat, not here.
    /// </summary>
    public sealed class RunState
    {
        public const int PartySize = 3;

        private readonly List<HeroRunState> m_party = new();
        private readonly List<DieDefinition> m_traversalDice = new();

        public RunPhase Phase { get; set; } = RunPhase.HeroPick;
        public int Seed { get; private set; }
        /// <summary>Seeded RNG for everything except the physical throw. Reset per run.</summary>
        public RollService Rng { get; private set; }
        public IReadOnlyList<HeroRunState> Party => m_party;
        public IReadOnlyList<DieDefinition> TraversalDice => m_traversalDice;
        public int CurrentNodeIndex { get; set; }
        public bool IsActive { get; private set; }

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

        /// <summary>Forget the run. Destroys the heroes' runtime die copies.</summary>
        public void Clear()
        {
            foreach (var hero in m_party) HeroRunState.DestroyRuntimeObject(hero.Die);
            m_party.Clear();
            m_traversalDice.Clear();
            CurrentNodeIndex = 0;
            IsActive = false;
            Phase = RunPhase.HeroPick;
        }
    }
}
