using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Dice;

namespace DungeonDice.UI
{
    /// <summary>
    /// Rules of the traversal pick without UI: choose exactly <see cref="Capacity"/> distinct dice from
    /// a pool. Toggling a chosen die removes it. The starting pool has four dice; the player takes three.
    /// </summary>
    public sealed class TraversalPickSelection
    {
        private readonly List<DieDefinition> m_chosen = new();

        public int Capacity { get; }
        public IReadOnlyList<DieDefinition> Pool { get; }
        public IReadOnlyList<DieDefinition> Chosen => m_chosen;
        public bool IsFull => m_chosen.Count >= Capacity;
        public bool CanConfirm => m_chosen.Count == Capacity;

        public event Action Changed;

        public TraversalPickSelection(IReadOnlyList<DieDefinition> pool, int capacity = 3)
        {
            Pool = pool ?? Array.Empty<DieDefinition>();
            Capacity = capacity;
        }

        public bool IsChosen(DieDefinition die) => m_chosen.Contains(die);

        /// <summary>Choose or un-choose a pool die. Returns false if the die is not in the pool or the pick is full.</summary>
        public bool Toggle(DieDefinition die)
        {
            if (die == null || !Pool.Contains(die)) return false;
            if (m_chosen.Remove(die))
            {
                Changed?.Invoke();
                return true;
            }
            if (IsFull) return false;
            m_chosen.Add(die);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (m_chosen.Count == 0) return;
            m_chosen.Clear();
            Changed?.Invoke();
        }
    }
}
