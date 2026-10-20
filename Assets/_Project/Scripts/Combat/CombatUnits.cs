using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;
using DungeonDice.Run;

namespace DungeonDice.Combat
{
    public enum StatusKind
    {
        /// <summary>Deals its stacks at the start of the target's turn, then decays by 1.</summary>
        Burn,
        /// <summary>Deals its stacks at the end of the target's turn; does not decay.</summary>
        Poison,
        /// <summary>−1 to the target's damage this turn.</summary>
        Weaken,
        /// <summary>Target takes +1 from every source this turn.</summary>
        Hex,
        /// <summary>Enemy single-target attacks go to this hero this turn.</summary>
        Taunt,
    }

    /// <summary>Stacks per status. Zero stacks means absent.</summary>
    public sealed class StatusSet
    {
        private readonly Dictionary<StatusKind, int> m_stacks = new();

        public int Get(StatusKind kind) => m_stacks.TryGetValue(kind, out var v) ? v : 0;
        public bool Has(StatusKind kind) => Get(kind) > 0;
        public int Total => m_stacks.Values.Sum();
        public IEnumerable<KeyValuePair<StatusKind, int>> All => m_stacks.Where(kv => kv.Value > 0);

        public void Add(StatusKind kind, int stacks) => Set(kind, Get(kind) + stacks);

        public void Set(StatusKind kind, int stacks)
        {
            if (stacks <= 0) m_stacks.Remove(kind);
            else m_stacks[kind] = stacks;
        }

        public void Remove(StatusKind kind) => m_stacks.Remove(kind);
        public void Clear() => m_stacks.Clear();

        public override string ToString() => All.Any() ? string.Join(", ", All.Select(kv => $"{kv.Key} {kv.Value}")) : "";
    }

    /// <summary>A hero in this fight: the run's HP plus combat-only shield and statuses.</summary>
    public sealed class CombatHero
    {
        public HeroRunState Run { get; }
        public StatusSet Statuses { get; } = new();
        /// <summary>Absorbs damage before HP. Expires at the end of the enemy turn.</summary>
        public int Shield { get; set; }
        /// <summary>Abilities cast this turn; checked against <see cref="CombatRules.actionsPerHeroPerTurn"/>, reset each turn.</summary>
        public int ActionsThisTurn { get; internal set; }

        public int Slot => Run.Slot;
        public string Name => Run.Definition.displayName;
        public DieColor Color => Run.Definition.color;
        public int Health => Run.CurrentHealth;
        public int MaxHealth => Run.MaxHealth;
        public bool IsAlive => Run.IsAlive;
        public IReadOnlyList<AbilityDefinition> Abilities => Run.Definition.abilities;

        public CombatHero(HeroRunState run)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
        }

        /// <summary>Shield first, then HP. Returns HP actually lost.</summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0) return 0;
            int absorbed = Math.Min(Shield, amount);
            Shield -= absorbed;
            return Run.TakeDamage(amount - absorbed);
        }
    }

    /// <summary>An enemy in this fight.</summary>
    public sealed class CombatEnemy
    {
        public EnemyDefinition Definition { get; }
        public int Index { get; }
        public int Health { get; private set; }
        public int MaxHealth { get; }
        /// <summary>Gained by Block; dropped when this enemy next acts.</summary>
        public int Shield { get; set; }
        public StatusSet Statuses { get; } = new();
        /// <summary>What this enemy will do on its next turn. Shown to the player.</summary>
        public EnemyIntent Intent { get; set; }

        public string Name => Definition.displayName;
        public bool IsAlive => Health > 0;

        public CombatEnemy(EnemyDefinition definition, int index)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Index = index;
            MaxHealth = definition.maxHealth;
            Health = definition.maxHealth;
        }

        /// <summary>Shield first, then HP. Returns HP actually lost.</summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0) return 0;
            int absorbed = Math.Min(Shield, amount);
            Shield -= absorbed;
            return LoseHealth(amount - absorbed);
        }

        /// <summary>Status damage ignores shield.</summary>
        public int LoseHealth(int amount)
        {
            int before = Health;
            Health = Math.Max(0, Health - Math.Max(0, amount));
            return before - Health;
        }
    }

    /// <summary>
    /// One of the party's resource dice this turn. Its colour fed the pool when it settled; Recolour
    /// and Reroll abilities can change it afterwards while that mana is still unspent.
    /// </summary>
    public sealed class CombatDie
    {
        /// <summary>Position in the throw; also the box owner id.</summary>
        public int Index { get; }
        public RollResult Result { get; internal set; }
        /// <summary>Current colour (may differ from the settled face after Recolour).</summary>
        public DieColor Color { get; internal set; }
        /// <summary>True while a Reroll-die re-throw is in flight; its mana has been pulled from the pool.</summary>
        public bool Pending { get; internal set; }

        public CombatDie(int index, RollResult result)
        {
            Index = index;
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Color = result.Face.ManaColor;
        }

        public override string ToString() => $"die {Index + 1}: {Color}{(Pending ? " (re-throwing)" : "")}";
    }
}
