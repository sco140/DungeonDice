using System.Collections.Generic;

namespace DungeonDice.Map
{
    /// <summary>
    /// The prototype's single act, defined in code so the validator can unit-test it and the
    /// authoring tool can write it to <c>Assets/_Project/Data/Map/Act1.asset</c>.
    /// Eleven nodes, three forks, one Boss. A roll unlocks every path at or below its total and the
    /// player chooses, so the lowest path is always open and the higher thresholds gate the richer
    /// options. Thresholds are tuning placeholders; tune them against the real traversal dice.
    /// </summary>
    public static class PrototypeAct
    {
        public const string DisplayName = "Act I — The Sunken Vault";

        public static List<MapNode> BuildNodes() => new()
        {
            // 0
            new MapNode
            {
                name = "Vault Gate", type = NodeType.Start,
                description = "The way in. Three doors; the higher you roll, the more of them open.",
                fork = new Fork
                {
                    minDice = 1, maxDice = 3,
                    paths = new List<ForkPath>
                    {
                        new() { threshold = 1, targetNode = 2 },   // always → Combat
                        new() { threshold = 7, targetNode = 3 },   // 7+     → Elite
                        new() { threshold = 12, targetNode = 1 },  // 12+    → Treasure
                    },
                },
            },
            // 1–3
            new MapNode { name = "Forgotten Cache", type = NodeType.Treasure, description = "Something glints under the silt.", nextNode = 4 },
            new MapNode { name = "Drowned Hall", type = NodeType.Combat, description = "Skeletal guards still hold their post.", nextNode = 4 },
            new MapNode { name = "The Warden", type = NodeType.Elite, description = "A vault warden, armoured and patient.", nextNode = 4 },
            // 4
            new MapNode
            {
                name = "Dry Alcove", type = NodeType.Rest,
                description = "A moment to breathe. Then two dice, maybe three.",
                fork = new Fork
                {
                    minDice = 2, maxDice = 3,
                    paths = new List<ForkPath>
                    {
                        new() { threshold = 2, targetNode = 5 },   // always → Combat
                        new() { threshold = 9, targetNode = 6 },   // 9+     → Elite
                        new() { threshold = 14, targetNode = 7 },  // 14+    → Treasure
                    },
                },
            },
            // 5–7
            new MapNode { name = "Flooded Stair", type = NodeType.Combat, description = "Things crawl up from the water.", nextNode = 8 },
            new MapNode { name = "Chained Golem", type = NodeType.Elite, description = "It wakes when you cross the threshold.", nextNode = 8 },
            new MapNode { name = "Sealed Reliquary", type = NodeType.Treasure, description = "Untouched for an age.", nextNode = 8 },
            // 8
            new MapNode
            {
                name = "Last Lantern", type = NodeType.Rest,
                description = "Roll all three. Low means one more fight first; high opens the way straight to the Boss.",
                fork = new Fork
                {
                    minDice = 3, maxDice = 3,
                    paths = new List<ForkPath>
                    {
                        new() { threshold = 3, targetNode = 9 },   // always → one more Combat first
                        new() { threshold = 11, targetNode = 10 }, // 11+    → Boss directly
                    },
                },
            },
            // 9
            new MapNode { name = "Antechamber", type = NodeType.Combat, description = "The Boss's attendants.", nextNode = 10 },
            // 10
            new MapNode { name = "The Vault Heart", type = NodeType.Boss, description = "What the vault was built to keep in." },
        };
    }
}
