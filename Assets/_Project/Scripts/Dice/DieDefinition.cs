using System;
using System.Linq;
using UnityEngine;

namespace DungeonDice.Dice
{
    /// <summary>Color domain of a hero die. Traversal dice are <see cref="None"/>.</summary>
    public enum DieColor
    {
        None,
        Red,    // Damage & Block
        Blue,   // Statuses
        Yellow, // Dice modification
    }

    /// <summary>What a face does when used. <see cref="Number"/> is a traversal pip value.</summary>
    public enum FaceEffectKind
    {
        Number,

        // Red: damage & block
        Strike,
        Shield,
        Taunt,
        Jab,

        // Blue: statuses
        Burn,
        Poison,
        Weaken,
        Hex,
        Fizzle,

        // Yellow: dice modification
        Nudge,
        Flip,
        RerollAlly,
        Mirror,
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

        public override string ToString() => value != 0 ? $"{kind} {value}" : kind.ToString();
    }

    /// <summary>One of the six faces. Compound faces ("Taunt + Shield 2") carry several entries.</summary>
    [Serializable]
    public class DieFace
    {
        public string label;
        public FaceEffectEntry[] effects = Array.Empty<FaceEffectEntry>();

        /// <summary>Sum of <see cref="FaceEffectKind.Number"/> entries; 0 for action faces.</summary>
        public int Pips => effects?.Where(e => e.kind == FaceEffectKind.Number).Sum(e => e.value) ?? 0;

        public bool Has(FaceEffectKind kind) => effects != null && effects.Any(e => e.kind == kind);

        public static DieFace Number(int pips) =>
            new DieFace { label = pips.ToString(), effects = new[] { new FaceEffectEntry(FaceEffectKind.Number, pips) } };

        public static DieFace Of(string label, params FaceEffectEntry[] effects) =>
            new DieFace { label = label, effects = effects ?? Array.Empty<FaceEffectEntry>() };

        public DieFace Clone() => new DieFace { label = label, effects = (FaceEffectEntry[])effects?.Clone() ?? Array.Empty<FaceEffectEntry>() };

        public override string ToString() => string.IsNullOrEmpty(label) ? string.Join(" + ", effects.Select(e => e.ToString())) : label;
    }

    /// <summary>
    /// Shared die model for hero dice and traversal dice: six faces, an optional color domain,
    /// and a visual slot for the dice-box profile. Modifiers act on a <see cref="RollResult"/>,
    /// never on the definition, except <see cref="WithFaceReplaced"/> which returns an upgraded copy.
    /// </summary>
    [CreateAssetMenu(fileName = "DieDefinition", menuName = "DungeonDice/Die Definition")]
    public class DieDefinition : ScriptableObject
    {
        public const int FaceCount = 6;

        public string displayName;
        public DieColor color = DieColor.None;

        [Tooltip("Placeholder for the dice-box visual profile: material applied to this die's mesh.")]
        public Material visualMaterial;

        [SerializeField] private DieFace[] faces = NewBlankFaces();

        /// <summary>Faces indexed 0–5. Prefer <see cref="GetFace"/> which takes the 1–6 face index.</summary>
        public DieFace[] Faces => faces;

        public bool IsTraversal => color == DieColor.None && faces.All(f => f != null && f.Has(FaceEffectKind.Number));

        public static bool IsValidFaceIndex(int faceIndex) => faceIndex >= 1 && faceIndex <= FaceCount;

        /// <summary>Opposite faces of a d6 sum to 7. Used by Flip and by the physical face table.</summary>
        public static int OppositeFace(int faceIndex) => FaceCount + 1 - faceIndex;

        /// <param name="faceIndex">1–6, matching the physical die and <see cref="RollResult.FaceIndex"/>.</param>
        public DieFace GetFace(int faceIndex)
        {
            if (!IsValidFaceIndex(faceIndex))
                throw new ArgumentOutOfRangeException(nameof(faceIndex), faceIndex, "Face index must be 1–6.");
            return faces[faceIndex - 1];
        }

        public int Pips(int faceIndex) => GetFace(faceIndex).Pips;

        public void SetFaces(DieFace[] sixFaces)
        {
            if (sixFaces == null || sixFaces.Length != FaceCount)
                throw new ArgumentException($"A die needs exactly {FaceCount} faces.", nameof(sixFaces));
            faces = sixFaces.Select(f => (f ?? new DieFace()).Clone()).ToArray();
        }

        /// <summary>
        /// Face-replace upgrade: a runtime copy of this definition with one face swapped.
        /// The asset itself is never modified.
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
