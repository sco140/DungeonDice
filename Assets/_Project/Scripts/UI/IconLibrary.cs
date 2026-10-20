using DungeonDice.Combat;
using DungeonDice.Dice;
using DungeonDice.Map;
using UnityEngine;

namespace DungeonDice.UI
{
    /// <summary>
    /// Every icon the prototype shows, resolved by game concept so the art can be swapped in one
    /// place. Authored by <c>IconAssetAuthoring</c> from the Kenney packs under
    /// <c>Assets/_Project/Art/Kenney</c> (CC0; see docs/ART_RESOURCES.md). Null sprites are fine:
    /// callers fall back to text.
    /// </summary>
    [CreateAssetMenu(fileName = "Icons", menuName = "DungeonDice/Icon Library")]
    public class IconLibrary : ScriptableObject
    {
        [Header("Map nodes")]
        public Sprite nodeStart;
        public Sprite nodeCombat;
        public Sprite nodeElite;
        public Sprite nodeRest;
        public Sprite nodeTreasure;
        public Sprite nodeBoss;

        [Header("Abilities and statuses")]
        public Sprite strike;
        public Sprite shield;
        public Sprite taunt;
        public Sprite burn;
        public Sprite poison;
        public Sprite weaken;
        public Sprite hex;
        public Sprite detonate;
        public Sprite recolor;
        public Sprite reroll;
        public Sprite number;

        [Header("Intents")]
        public Sprite intentAttack;
        public Sprite intentAttackAll;
        public Sprite intentBlock;

        [Header("Misc")]
        public Sprite heart;
        public Sprite mana;
        public Sprite locked;
        public Sprite check;
        public Sprite cross;
        public Sprite warning;
        public Sprite arrowRight;
        public Sprite[] dieFaces = new Sprite[DieDefinition.FaceCount];

        public Sprite ForNode(NodeType type) => type switch
        {
            NodeType.Start => nodeStart,
            NodeType.Combat => nodeCombat,
            NodeType.Elite => nodeElite,
            NodeType.Rest => nodeRest,
            NodeType.Treasure => nodeTreasure,
            NodeType.Boss => nodeBoss,
            _ => null,
        };

        public Sprite ForAbility(AbilityDefinition ability)
        {
            if (ability == null) return null;
            return ability.kind switch
            {
                AbilityKind.Damage => strike,
                AbilityKind.Shield or AbilityKind.ShieldAll => shield,
                AbilityKind.Guard => taunt,
                AbilityKind.Status => ForStatus(ability.status),
                AbilityKind.Detonate => detonate,
                AbilityKind.Recolor => recolor,
                AbilityKind.RerollDie => reroll,
                _ => null,
            };
        }

        /// <summary>The icon for a die face: a pip sprite for numbers, the mana token for colours.</summary>
        public Sprite ForFace(DieFace face)
        {
            if (face == null || face.effects == null || face.effects.Length == 0) return null;
            var first = face.effects[0];
            return first.kind == FaceEffectKind.Number ? ForPips(first.value) : mana;
        }

        public Sprite ForPips(int pips) =>
            pips >= 1 && pips <= dieFaces.Length && dieFaces[pips - 1] != null ? dieFaces[pips - 1] : number;

        public Sprite ForStatus(StatusKind kind) => kind switch
        {
            StatusKind.Burn => burn,
            StatusKind.Poison => poison,
            StatusKind.Weaken => weaken,
            StatusKind.Hex => hex,
            StatusKind.Taunt => taunt,
            _ => null,
        };

        public Sprite ForIntent(IntentKind kind) => kind switch
        {
            IntentKind.Attack => intentAttack,
            IntentKind.AttackAll => intentAttackAll,
            IntentKind.Block => intentBlock,
            _ => null,
        };

        public static Color ManaColor(DieColor color) => color switch
        {
            DieColor.Red => new Color(0.85f, 0.25f, 0.25f),
            DieColor.Blue => new Color(0.3f, 0.5f, 0.95f),
            DieColor.Yellow => new Color(0.9f, 0.75f, 0.25f),
            _ => Color.white,
        };
    }
}
