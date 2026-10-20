using System;
using UnityEngine;

namespace DungeonDice.Combat
{
    /// <summary>
    /// The knobs of a fight that are design decisions rather than data: how many resource dice the
    /// party rolls, whether unspent mana survives the turn, and whether a reroll step exists.
    /// Serialized on <c>RunController</c> so they can be flipped in the Inspector.
    /// </summary>
    [Serializable]
    public class CombatRules
    {
        [Tooltip("Resource dice thrown each turn. Treasure can add more up to RunState.MaxResourceDice.")]
        [Min(1)] public int resourceDiceCount = 6;

        [Tooltip("Decision (2026-10-20): mana carries between turns within a fight. Set false to wipe the pool each turn.")]
        public bool manaCarriesBetweenTurns = true;

        [Tooltip("Decision (2026-10-20): no reroll for now. The Reroll phase and RelaunchDice path stay in place; set 1 to restore a once-per-turn re-throw, or raise it through an item later.")]
        [Min(0)] public int rerollsPerTurn = 0;

        public CombatRules Clone() => (CombatRules)MemberwiseClone();
    }
}
