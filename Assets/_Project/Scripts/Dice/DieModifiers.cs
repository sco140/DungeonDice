using System;

namespace DungeonDice.Dice
{
    /// <summary>
    /// Changes a <see cref="RollResult"/> after the physical throw has settled.
    /// Data model only for the prototype; gameplay wiring comes with the combat step.
    /// </summary>
    public interface IDieModifier
    {
        string Name { get; }
        void Apply(RollResult result);
    }

    /// <summary>±N to the face index, clamped to 1–6.</summary>
    public sealed class NudgeModifier : IDieModifier
    {
        public int Delta { get; }
        public string Name => $"Nudge {(Delta >= 0 ? "+" : "")}{Delta}";

        public NudgeModifier(int delta)
        {
            Delta = delta;
        }

        public void Apply(RollResult result)
        {
            result.SetFaceIndex(Math.Clamp(result.FaceIndex + Delta, 1, DieDefinition.FaceCount));
        }
    }

    /// <summary>Turns the die to its opposite face. Opposite faces of a d6 sum to 7.</summary>
    public sealed class FlipModifier : IDieModifier
    {
        public string Name => "Flip";

        public void Apply(RollResult result)
        {
            result.SetFaceIndex(DieDefinition.OppositeFace(result.FaceIndex));
        }
    }

    /// <summary>Sets the die to a chosen face regardless of what was rolled (Loaded Dice).</summary>
    public sealed class ChooseResultModifier : IDieModifier
    {
        public int FaceIndex { get; }
        public string Name => $"Choose {FaceIndex}";

        public ChooseResultModifier(int faceIndex)
        {
            if (!DieDefinition.IsValidFaceIndex(faceIndex))
                throw new ArgumentOutOfRangeException(nameof(faceIndex), faceIndex, "Face index must be 1–6.");
            FaceIndex = faceIndex;
        }

        public void Apply(RollResult result)
        {
            result.SetFaceIndex(FaceIndex);
        }
    }

    /// <summary>Keeps the die's result through a reroll. Does not change the face.</summary>
    public sealed class LockModifier : IDieModifier
    {
        public string Name => "Lock";

        public void Apply(RollResult result)
        {
            result.Locked = true;
        }
    }
}
