using System.Collections.Generic;
using DungeonDice.Dice;
using UnityEngine;

namespace DungeonDice.Run
{
    /// <summary>
    /// A hero class as data: its colour domain, die (or dice), health and signature skill text.
    /// Six exist for the pick screen; the three with <see cref="playableInPrototype"/> can be chosen.
    /// Replaces the old abstract <c>BaseCharacters</c>. The signature skill itself is wired in the
    /// combat step; until then it is described here as text.
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

        [Header("Dice")]
        [Tooltip("One die per hero in the prototype. A list so 'hero gains a second die' can be a future reward.")]
        public List<DieDefinition> dice = new();

        [Header("Signature skill (3 mana of this hero's colour)")]
        public string signatureName;
        [TextArea] public string signatureDescription;
        [Tooltip("Resolved in the combat step; may be empty until then.")]
        public BaseSkill signatureSkill;

        [Header("Presentation")]
        public Sprite portrait;

        /// <summary>The die that goes in the box for this hero, or null if none is set.</summary>
        public DieDefinition PrimaryDie => dice != null && dice.Count > 0 ? dice[0] : null;

        public string ColorLabel => color switch
        {
            DieColor.Red => "Red — Damage & Block",
            DieColor.Blue => "Blue — Statuses",
            DieColor.Yellow => "Yellow — Dice modification",
            _ => "Uncoloured",
        };
    }
}
