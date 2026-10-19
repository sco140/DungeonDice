using System;
using System.Collections.Generic;
using DungeonDice.Run;

namespace DungeonDice.UI
{
    /// <summary>
    /// The rules of the Hero Pick screen without any UI: up to three playable heroes, duplicates
    /// allowed, locked heroes refused. <see cref="HeroPickScreen"/> renders this.
    /// </summary>
    public sealed class HeroPickSelection
    {
        private readonly List<HeroDefinition> m_party = new();

        public int Capacity { get; }
        public IReadOnlyList<HeroDefinition> Party => m_party;
        public bool IsFull => m_party.Count >= Capacity;
        public bool CanConfirm => m_party.Count == Capacity;

        public event Action Changed;

        public HeroPickSelection(int capacity = RunState.PartySize)
        {
            Capacity = capacity;
        }

        public static bool IsPickable(HeroDefinition hero) => hero != null && hero.playableInPrototype && hero.PrimaryDie != null;

        /// <summary>Add a hero to the next free slot. Returns false if locked, null or the party is full.</summary>
        public bool TryAdd(HeroDefinition hero)
        {
            if (!IsPickable(hero) || IsFull) return false;
            m_party.Add(hero);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveAt(int slot)
        {
            if (slot < 0 || slot >= m_party.Count) return false;
            m_party.RemoveAt(slot);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (m_party.Count == 0) return;
            m_party.Clear();
            Changed?.Invoke();
        }
    }
}
