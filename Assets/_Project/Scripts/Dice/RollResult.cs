using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonDice.Dice
{
    /// <summary>
    /// The logical outcome of one die after it has physically settled.
    /// The physical read is <see cref="RolledFaceIndex"/>; modifiers change <see cref="FaceIndex"/>.
    /// </summary>
    public sealed class RollResult
    {
        private readonly List<IDieModifier> m_applied = new();

        public DieDefinition Die { get; }
        public string DieName { get; }
        /// <summary>Slot of the owner (hero index or traversal slot). -1 when unknown.</summary>
        public int OwnerId { get; }
        /// <summary>Face index 1–6 read from the physical die when it settled.</summary>
        public int RolledFaceIndex { get; }
        /// <summary>Face index 1–6 after modifiers. This is what the game resolves.</summary>
        public int FaceIndex { get; private set; }
        /// <summary>A locked die is not re-thrown during a reroll.</summary>
        public bool Locked { get; set; }

        public IReadOnlyList<IDieModifier> Applied => m_applied;
        public DieFace Face => Die.GetFace(FaceIndex);
        public int Pips => Face.Pips;
        public DieColor Color => Die.color;
        public bool WasModified => FaceIndex != RolledFaceIndex;

        public RollResult(DieDefinition die, int rolledFaceIndex, string dieName = null, int ownerId = -1)
        {
            if (die == null) throw new ArgumentNullException(nameof(die));
            if (!DieDefinition.IsValidFaceIndex(rolledFaceIndex))
                throw new ArgumentOutOfRangeException(nameof(rolledFaceIndex), rolledFaceIndex, "Face index must be 1–6.");

            Die = die;
            DieName = string.IsNullOrEmpty(dieName) ? die.name : dieName;
            OwnerId = ownerId;
            RolledFaceIndex = rolledFaceIndex;
            FaceIndex = rolledFaceIndex;
        }

        public void Apply(IDieModifier modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            modifier.Apply(this);
            m_applied.Add(modifier);
        }

        internal void SetFaceIndex(int faceIndex)
        {
            if (!DieDefinition.IsValidFaceIndex(faceIndex))
                throw new ArgumentOutOfRangeException(nameof(faceIndex), faceIndex, "Face index must be 1–6.");
            FaceIndex = faceIndex;
        }

        public override string ToString() =>
            $"{DieName}: face {FaceIndex}{(WasModified ? $" (rolled {RolledFaceIndex})" : "")} = {Face}";
    }

    /// <summary>Payload of <c>GameEvents.RollCompleted</c>: every die of one throw, in owner order.</summary>
    public sealed class RollCompletedEvent
    {
        public IReadOnlyList<RollResult> Results { get; }
        public int TotalPips => Results.Sum(r => r.Pips);

        public RollCompletedEvent(IReadOnlyList<RollResult> results)
        {
            Results = results ?? throw new ArgumentNullException(nameof(results));
        }
    }
}
