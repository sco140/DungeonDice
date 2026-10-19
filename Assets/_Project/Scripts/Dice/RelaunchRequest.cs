using System.Collections.Generic;
using UnityEngine;

namespace DungeonDice.Dice
{
    /// <summary>Payload of <c>GameEvents.RelaunchDice</c>: which settled dice to re-throw, and how.</summary>
    public sealed class RelaunchRequest
    {
        /// <summary>Drag direction and magnitude, same convention as <c>GameEvents.LaunchDice</c>.</summary>
        public Vector3 Direction { get; }
        /// <summary>Owner ids of the dice to re-throw. Locked dice are skipped by the box.</summary>
        public IReadOnlyList<int> OwnerIds { get; }

        public RelaunchRequest(Vector3 direction, IReadOnlyList<int> ownerIds)
        {
            Direction = direction;
            OwnerIds = ownerIds ?? System.Array.Empty<int>();
        }
    }
}
