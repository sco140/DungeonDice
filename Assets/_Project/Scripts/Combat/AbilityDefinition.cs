using System;
using DungeonDice.Dice;
using UnityEngine;

namespace DungeonDice.Combat
{
    public enum AbilityKind
    {
        /// <summary>Deal <c>value</c> damage to one enemy.</summary>
        Damage,
        /// <summary>Shield self for <c>value</c>.</summary>
        Shield,
        /// <summary>Shield self for <c>value</c> and taunt: enemy single-target attacks come here this turn.</summary>
        Guard,
        /// <summary>Shield every living hero for <c>value</c>.</summary>
        ShieldAll,
        /// <summary>Apply <c>status</c> with <c>value</c> stacks to one enemy.</summary>
        Status,
        /// <summary>Consume one enemy's status stacks and deal that much damage.</summary>
        Detonate,
        /// <summary>Change one rolled resource die (whose mana is unspent) to a colour of choice.</summary>
        Recolor,
        /// <summary>Re-throw one rolled resource die (whose mana is unspent); its new colour joins the pool.</summary>
        RerollDie,
    }

    /// <summary>
    /// One hero action, paid from the party's mana pool. Heroes carry three of these; costs are in the
    /// hero's colour. Numbers are tuning placeholders.
    /// </summary>
    [Serializable]
    public class AbilityDefinition
    {
        public string name;
        [TextArea] public string description;
        public DieColor costColor = DieColor.Red;
        [Min(0)] public int cost = 1;
        public AbilityKind kind = AbilityKind.Damage;
        public int value;
        [Tooltip("For Status abilities.")]
        public StatusKind status = StatusKind.Burn;

        public TargetKind Target => kind switch
        {
            AbilityKind.Damage or AbilityKind.Status or AbilityKind.Detonate => TargetKind.Enemy,
            AbilityKind.Recolor or AbilityKind.RerollDie => TargetKind.Die,
            _ => TargetKind.None,
        };

        public string CostLabel => $"{cost} {costColor}";

        public override string ToString() => $"{name} ({CostLabel})";
    }
}
