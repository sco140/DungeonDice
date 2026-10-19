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

        [Header("Face effects")]
        public Sprite strike;
        public Sprite shield;
        public Sprite taunt;
        public Sprite burn;
        public Sprite poison;
        public Sprite weaken;
        public Sprite hex;
        public Sprite nudge;
        public Sprite flip;
        public Sprite rerollAlly;
        public Sprite mirror;
        public Sprite fizzle;
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

        public Sprite ForEffect(FaceEffectKind kind) => kind switch
        {
            FaceEffectKind.Strike or FaceEffectKind.Jab => strike,
            FaceEffectKind.Shield => shield,
            FaceEffectKind.Taunt => taunt,
            FaceEffectKind.Burn => burn,
            FaceEffectKind.Poison => poison,
            FaceEffectKind.Weaken => weaken,
            FaceEffectKind.Hex => hex,
            FaceEffectKind.Nudge => nudge,
            FaceEffectKind.Flip => flip,
            FaceEffectKind.RerollAlly => rerollAlly,
            FaceEffectKind.Mirror => mirror,
            FaceEffectKind.Fizzle => fizzle,
            FaceEffectKind.Number => number,
            _ => null,
        };

        /// <summary>The icon for a face: its first effect's icon, or a die pip sprite for numbers.</summary>
        public Sprite ForFace(DieFace face)
        {
            if (face == null || face.effects == null || face.effects.Length == 0) return fizzle;
            var first = face.effects[0];
            if (first.kind == FaceEffectKind.Number) return ForPips(first.value);
            return ForEffect(first.kind);
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
