using System.Collections.Generic;
using DungeonDice.Combat;
using DungeonDice.Dice;
using UnityEngine;

namespace DungeonDice.Run
{
    /// <summary>
    /// A hero class as data: colour domain, health, and the three abilities it spends the party's
    /// mana on. Six exist for the pick screen; the three with <see cref="playableInPrototype"/> can be
    /// chosen. Replaces the old abstract <c>BaseCharacters</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Hero", menuName = "DungeonDice/Hero Definition")]
    public class HeroDefinition : ScriptableObject
    {
        public string displayName;
        [Tooltip("Colour domain. Dual-colour classes are listed under their primary colour for now.")]
        public DieColor color = DieColor.None;
        [Min(1)] public int maxHealth = 20;
        [Tooltip("Shown as LOCKED on the pick screen when false.")]
        public bool playableInPrototype = true;
        [TextArea] public string description;

        [Header("Abilities (paid from the party's mana pool)")]
        public List<AbilityDefinition> abilities = new();

        [Header("Presentation")]
        public Sprite portrait;

        public string ColorLabel => color switch
        {
            DieColor.Red => "Red — Damage & Block",
            DieColor.Blue => "Blue — Statuses",
            DieColor.Yellow => "Yellow — Dice modification",
            _ => "Uncoloured",
        };
    }
}
