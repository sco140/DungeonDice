using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDice.Map
{
    public enum NodeType
    {
        Start,
        Combat,
        Elite,
        Rest,
        Treasure,
        Boss,
    }

    /// <summary>
    /// One exit of a fork: the node it leads to and the roll total needed to unlock it ("9+").
    /// A roll unlocks every path whose threshold it meets, and the player picks one of them.
    /// </summary>
    [Serializable]
    public class ForkPath
    {
        [Min(0)] public int threshold = 1;
        public int targetNode;

        public bool IsUnlockedBy(int total) => total >= threshold;

        /// <summary>"any" when every possible total unlocks it, else "9+".</summary>
        public string Label(Fork fork) => fork != null && threshold <= fork.MinTotal ? "any" : $"{threshold}+";
    }

    /// <summary>
    /// A fork is rolled with between <see cref="minDice"/> and <see cref="maxDice"/> traversal dice;
    /// the summed total unlocks every <see cref="ForkPath"/> at or below it and the player chooses
    /// among the unlocked ones (decision 2026-10-20). At least one path must unlock on the lowest
    /// possible total so a roll can never strand the party (see <c>ForkValidator</c>).
    /// </summary>
    [Serializable]
    public class Fork
    {
        /// <summary>Every traversal die in the prototype has faces between these pip values.</summary>
        public const int MinFace = 1;
        public const int MaxFace = 6;

        [Min(1)] public int minDice = 1;
        [Min(1)] public int maxDice = 3;
        public List<ForkPath> paths = new();

        public bool HasPaths => paths != null && paths.Count > 0;
        public int MinTotal => minDice * MinFace;
        public int MaxTotal => maxDice * MaxFace;
        public bool AllowsDiceCount(int count) => count >= minDice && count <= maxDice;
        public string DiceCountLabel => minDice == maxDice ? $"roll {minDice}" : $"roll {minDice}–{maxDice}";
    }

    /// <summary>A node of the act. Exactly one of <see cref="fork"/> or <see cref="nextNode"/> says where to go next; Boss has neither.</summary>
    [Serializable]
    public class MapNode
    {
        public string name;
        public NodeType type;
        [TextArea] public string description;
        [Tooltip("Single exit when there is no fork. -1 = terminal (Boss).")]
        public int nextNode = -1;
        public Fork fork;

        public bool HasFork => fork != null && fork.HasPaths;
        public bool IsTerminal => !HasFork && nextNode < 0;
        public bool IsCombat => type == NodeType.Combat || type == NodeType.Elite || type == NodeType.Boss;
    }
}
