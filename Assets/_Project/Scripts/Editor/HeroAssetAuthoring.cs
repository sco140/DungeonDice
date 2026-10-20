using System.Collections.Generic;
using DungeonDice.Combat;
using DungeonDice.Dice;
using DungeonDice.Run;
using UnityEditor;
using UnityEngine;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Creates or refreshes the six <see cref="HeroDefinition"/> assets of the pick screen:
    /// three core classes with three abilities each (costing 1/2/3 of their colour), three
    /// dual-colour classes shown locked. Numbers are placeholders for tuning. Re-runnable and
    /// GUID-preserving.
    /// </summary>
    public static class HeroAssetAuthoring
    {
        public const string HeroDir = "Assets/_Project/Data/Heroes";

        /// <summary>Roster in pick-screen order.</summary>
        public static readonly string[] RosterOrder = { "Knight", "Arcanist", "Trickster", "Rogue", "Berserker", "Cleric" };

        [MenuItem("DungeonDice/Author Heroes")]
        public static void CreateAll()
        {
            AuthoringUtil.EnsureFolder(HeroDir);

            Upsert("Knight", DieColor.Red, 30, true,
                "Consistent and defensive. Red mana: damage and block.",
                A("Strike", "Deal 3 damage to one enemy.", DieColor.Red, 1, AbilityKind.Damage, 3),
                A("Shield", "Shield yourself for 4.", DieColor.Red, 2, AbilityKind.Shield, 4),
                A("Bulwark", "Shield every living hero for 6.", DieColor.Red, 3, AbilityKind.ShieldAll, 6));
            Upsert("Arcanist", DieColor.Blue, 20, true,
                "Statuses. Blue mana: burn, poison, then detonate the stacks.",
                A("Burn", "Burn 2: deals its stacks at the start of the enemy turn, then decays by 1.", DieColor.Blue, 1, AbilityKind.Status, 2, StatusKind.Burn),
                A("Poison", "Poison 3: deals its stacks at the end of the enemy turn; does not decay.", DieColor.Blue, 2, AbilityKind.Status, 3, StatusKind.Poison),
                A("Detonate", "Consume one enemy's status stacks and deal that much damage.", DieColor.Blue, 3, AbilityKind.Detonate));
            Upsert("Trickster", DieColor.Yellow, 24, true,
                "Bends the party's dice. Yellow mana: re-throw or recolour a rolled die.",
                A("Jab", "Deal 2 damage to one enemy.", DieColor.Yellow, 1, AbilityKind.Damage, 2),
                A("Reroll", "Re-throw one rolled die whose mana is unspent; its new colour joins the pool.", DieColor.Yellow, 1, AbilityKind.RerollDie),
                A("Recolour", "Change one rolled die whose mana is unspent to any colour.", DieColor.Yellow, 2, AbilityKind.Recolor));

            Upsert("Rogue", DieColor.Red, 22, false, "Red + Blue. Damage plus poison combos. Coming later.");
            Upsert("Berserker", DieColor.Red, 28, false, "Red + Yellow. Heavy damage, self-modifying Rage. Coming later.");
            Upsert("Cleric", DieColor.Blue, 22, false, "Blue + Yellow. Cleanse and heal, Bless another die. Coming later.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("HeroAssetAuthoring: six heroes authored.");
        }

        public static HeroDefinition Load(string name) =>
            AssetDatabase.LoadAssetAtPath<HeroDefinition>($"{HeroDir}/{name}.asset");

        private static AbilityDefinition A(string name, string description, DieColor color, int cost, AbilityKind kind, int value = 0, StatusKind status = StatusKind.Burn) =>
            new() { name = name, description = description, costColor = color, cost = cost, kind = kind, value = value, status = status };

        private static void Upsert(string name, DieColor color, int maxHealth, bool playable, string description, params AbilityDefinition[] abilities)
        {
            var path = $"{HeroDir}/{name}.asset";
            var hero = AssetDatabase.LoadAssetAtPath<HeroDefinition>(path);
            bool created = hero == null;
            if (created)
            {
                hero = ScriptableObject.CreateInstance<HeroDefinition>();
                AssetDatabase.CreateAsset(hero, path);
            }

            hero.displayName = name;
            hero.color = color;
            hero.maxHealth = maxHealth;
            hero.playableInPrototype = playable;
            hero.description = description;
            hero.abilities = new List<AbilityDefinition>(abilities);
            EditorUtility.SetDirty(hero);
            Debug.Log($"HeroAssetAuthoring: {(created ? "created" : "updated")} {path}");
        }
    }
}
