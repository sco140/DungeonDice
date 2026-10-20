using System;
using System.Linq;
using UnityEngine;

namespace DungeonDice.Dice
{
    /// <summary>Colour domain of a mana resource. Traversal dice are <see cref="None"/>.</summary>
    public enum DieColor
    {
        None,
        Red,    // Damage & Block
        Blue,   // Statuses
        Yellow, // Dice modification
    }

    /// <summary>What a face yields. <see cref="Number"/> is a traversal pip value; <see cref="Mana"/> is one resource of a colour.</summary>
    public enum FaceEffectKind
    {
        Number,
        /// <summary>One mana of the colour stored in <see cref="FaceEffectEntry.value"/> as a <see cref="DieColor"/>.</summary>
        Mana,
    }

    [Serializable]
    public struct FaceEffectEntry
    {
        public FaceEffectKind kind;
        public int value;

        public FaceEffectEntry(FaceEffectKind kind, int value = 0)
        {
            this.kind = kind;
            this.value = value;
        }

        public override string ToString() => kind == FaceEffectKind.Mana ? ((DieColor)value).ToString() : value != 0 ? $"{kind} {value}" : kind.ToString();
    }

    /// <summary>One of the six faces: a number (traversal) or a mana colour (resource dice).</summary>
    [Serializable]
    public class DieFace
    {
        public string label;
        public FaceEffectEntry[] effects = Array.Empty<FaceEffectEntry>();

        /// <summary>Sum of <see cref="FaceEffectKind.Number"/> entries; 0 for mana faces.</summary>
        public int Pips => effects?.Where(e => e.kind == FaceEffectKind.Number).Sum(e => e.value) ?? 0;

        /// <summary>The mana colour of this face, or None for a number face.</summary>
        public DieColor ManaColor => effects != null && effects.Any(e => e.kind == FaceEffectKind.Mana)
            ? (DieColor)effects.First(e => e.kind == FaceEffectKind.Mana).value
            : DieColor.None;

        public bool Has(FaceEffectKind kind) => effects != null && effects.Any(e => e.kind == kind);

        public static DieFace Number(int pips) =>
            new DieFace { label = pips.ToString(), effects = new[] { new FaceEffectEntry(FaceEffectKind.Number, pips) } };

        public static DieFace Mana(DieColor color) =>
            new DieFace { label = color.ToString(), effects = new[] { new FaceEffectEntry(FaceEffectKind.Mana, (int)color) } };

        public static DieFace Of(string label, params FaceEffectEntry[] effects) =>
            new DieFace { label = label, effects = effects ?? Array.Empty<FaceEffectEntry>() };

        public DieFace Clone() => new DieFace { label = label, effects = (FaceEffectEntry[])effects?.Clone() ?? Array.Empty<FaceEffectEntry>() };

        public override string ToString() => string.IsNullOrEmpty(label) ? string.Join(" + ", effects.Select(e => e.ToString())) : label;
    }

    /// <summary>
    /// Shared die model for the party's resource dice and the traversal dice: six faces, an optional
    /// colour tag, and a visual slot for the dice-box profile. Modifiers act on a <see cref="RollResult"/>,
    /// never on the definition, except <see cref="WithFaceReplaced"/> which returns an upgraded copy.
    /// </summary>
    [CreateAssetMenu(fileName = "DieDefinition", menuName = "DungeonDice/Die Definition")]
    public class DieDefinition : ScriptableObject
    {
        public const int FaceCount = 6;

        public string displayName;
        [Tooltip("Resource dice are mixed-colour; this tag is for single-colour dice and stays None for traversal dice.")]
        public DieColor color = DieColor.None;

        [Tooltip("Placeholder for the dice-box visual profile: material applied to this die's mesh.")]
        public Material visualMaterial;

        [SerializeField] private DieFace[] faces = NewBlankFaces();

        /// <summary>Faces indexed 0–5. Prefer <see cref="GetFace"/> which takes the 1–6 face index.</summary>
        public DieFace[] Faces => faces;

        public bool IsTraversal => faces.All(f => f != null && f.Has(FaceEffectKind.Number));
        public bool IsResource => faces.All(f => f != null && f.Has(FaceEffectKind.Mana));

        public static bool IsValidFaceIndex(int faceIndex) => faceIndex >= 1 && faceIndex <= FaceCount;

        /// <summary>Opposite faces of a d6 sum to 7. Used by the Flip modifier and the physical face table.</summary>
        public static int OppositeFace(int faceIndex) => FaceCount + 1 - faceIndex;

        /// <param name="faceIndex">1–6, matching the physical die and <see cref="RollResult.FaceIndex"/>.</param>
        public DieFace GetFace(int faceIndex)
        {
            if (!IsValidFaceIndex(faceIndex))
                throw new ArgumentOutOfRangeException(nameof(faceIndex), faceIndex, "Face index must be 1–6.");
            return faces[faceIndex - 1];
        }

        public int Pips(int faceIndex) => GetFace(faceIndex).Pips;

        /// <summary>First face index (1–6) that yields <paramref name="color"/>, or 0 if none does.</summary>
        public int FaceIndexOfColor(DieColor color)
        {
            for (int i = 1; i <= FaceCount; i++)
            {
                if (GetFace(i).ManaColor == color) return i;
            }
            return 0;
        }

        public void SetFaces(DieFace[] sixFaces)
        {
            if (sixFaces == null || sixFaces.Length != FaceCount)
                throw new ArgumentException($"A die needs exactly {FaceCount} faces.", nameof(sixFaces));
            faces = sixFaces.Select(f => (f ?? new DieFace()).Clone()).ToArray();
        }

        /// <summary>
        /// Face-replace modifier (future upgrade path): a runtime copy of this definition with one face
        /// swapped. The asset itself is never modified.
        /// </summary>
        public DieDefinition WithFaceReplaced(int faceIndex, DieFace replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (!IsValidFaceIndex(faceIndex))
                throw new ArgumentOutOfRangeException(nameof(faceIndex), faceIndex, "Face index must be 1–6.");

            var copy = Instantiate(this);
            copy.name = $"{name} (upgraded)";
            copy.faces = faces.Select(f => f.Clone()).ToArray();
            copy.faces[faceIndex - 1] = replacement.Clone();
            return copy;
        }

        private static DieFace[] NewBlankFaces() => Enumerable.Range(0, FaceCount).Select(_ => new DieFace()).ToArray();

        private void OnValidate()
        {
            if (faces == null || faces.Length != FaceCount)
            {
                var fixedFaces = NewBlankFaces();
                if (faces != null) Array.Copy(faces, fixedFaces, Math.Min(faces.Length, FaceCount));
                faces = fixedFaces;
            }
            for (int i = 0; i < faces.Length; i++) faces[i] ??= new DieFace();
        }
    }
}
