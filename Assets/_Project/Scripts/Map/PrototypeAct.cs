using System.Collections.Generic;

namespace DungeonDice.Map
{
    /// <summary>
    /// The prototype's single act, defined in code so the validator can unit-test it and the
    /// authoring tool can write it to <c>Assets/_Project/Data/Map/Act1.asset</c>.
    /// Eleven nodes, three forks, one Boss. Bracket thresholds are tuning placeholders; the
    /// proposal's note applies: tune them against the real traversal dice, not plain d6s.
    /// Good paths are deliberately not always the low bracket.
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
                description = "The way in. Three doors; your dice choose.",
                fork = new Fork
                {
                    minDice = 1, maxDice = 3,
                    paths = new List<PathBracket>
                    {
                        new() { minTotal = 1, maxTotal = 5, targetNode = 1 },   // low  → Treasure
                        new() { minTotal = 6, maxTotal = 10, targetNode = 2 },  // mid  → Combat
                        new() { minTotal = 11, maxTotal = 18, targetNode = 3 }, // high → Elite
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
                    paths = new List<PathBracket>
                    {
                        new() { minTotal = 2, maxTotal = 8, targetNode = 5 },   // low  → Combat
                        new() { minTotal = 9, maxTotal = 13, targetNode = 6 },  // mid  → Elite
                        new() { minTotal = 14, maxTotal = 18, targetNode = 7 }, // high → Treasure (the high road pays here)
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
                description = "Roll all three. Low keeps you safe but slow; high goes straight for the Boss.",
                fork = new Fork
                {
                    minDice = 3, maxDice = 3,
                    paths = new List<PathBracket>
                    {
                        new() { minTotal = 3, maxTotal = 10, targetNode = 9 },  // low  → one more Combat first
                        new() { minTotal = 11, maxTotal = 18, targetNode = 10 }, // high → Boss directly
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
