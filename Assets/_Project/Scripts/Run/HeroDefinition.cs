using System;
using System.Collections.Generic;
using DungeonDice.Dice;
using UnityEngine;

namespace DungeonDice.Run
{
    /// <summary>The three built-in signature skills. Resolved in <c>CombatState.CastSignature</c>.</summary>
    public enum SignatureKind
    {
        None,
        /// <summary>Knight: Shield 6 to all allies.</summary>
        Bulwark,
        /// <summary>Arcanist: consume a target's statuses, deal damage equal to the stacks.</summary>
        Detonate,
        /// <summary>Trickster: set any one unspent hero die to a chosen face.</summary>
        LoadedDice,
    }

    /// <summary>A Treasure reward option: replace one face of the hero's die with a better one.</summary>
    [Serializable]
    public class FaceUpgrade
    {
        [Range(1, DieDefinition.FaceCount)] public int faceIndex = 1;
        public DieFace replacement = new();

        /// <summary>"Strike 3 → Strike 4", read against the hero's current die.</summary>
        public string Describe(DieDefinition currentDie)
        {
            var before = currentDie != null && DieDefinition.IsValidFaceIndex(faceIndex) ? currentDie.GetFace(faceIndex).ToString() : "?";
            return $"{before} → {replacement}";
        }
    }

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
        [Tooltip("Which built-in signature CombatState resolves. None for locked heroes.")]
        public SignatureKind signatureKind = SignatureKind.None;

        [Header("Treasure: face upgrades (2–3), each offered once per run")]
        public List<FaceUpgrade> faceUpgrades = new();

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
