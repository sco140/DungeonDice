using System.Collections.Generic;
using DungeonDice.Dice;
using DungeonDice.Run;
using UnityEditor;
using UnityEngine;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Creates or refreshes the six <see cref="HeroDefinition"/> assets of the pick screen:
    /// three core classes with dice, three dual-colour classes shown locked. Numbers are
    /// placeholders for tuning. Re-runnable and GUID-preserving.
    /// </summary>
    public static class HeroAssetAuthoring
    {
        public const string HeroDir = "Assets/_Project/Data/Heroes";
        private const string DiceDir = "Assets/_Project/Data/Dice/Heroes";

        /// <summary>Roster in pick-screen order.</summary>
        public static readonly string[] RosterOrder = { "Knight", "Arcanist", "Trickster", "Rogue", "Berserker", "Cleric" };

        [MenuItem("DungeonDice/Author Heroes")]
        public static void CreateAll()
        {
            AuthoringUtil.EnsureFolder(HeroDir);

            Upsert("Knight", DieColor.Red, 30, true,
                "Consistent and defensive. No bad faces.",
                "Bulwark", "Shield 6 to all allies.", SignatureKind.Bulwark, Die("Knight"));
            Upsert("Arcanist", DieColor.Blue, 20, true,
                "Statuses: burn, poison, weaken, hex.",
                "Detonate", "Consume a target's statuses and deal damage equal to their stacks.", SignatureKind.Detonate, Die("Arcanist"));
            Upsert("Trickster", DieColor.Yellow, 24, true,
                "Bends dice: nudge, flip, reroll, mirror. Works on traversal rolls too.",
                "Loaded Dice", "Set any one die, hero or traversal, to a chosen face.", SignatureKind.LoadedDice, Die("Trickster"));

            Upsert("Rogue", DieColor.Red, 22, false,
                "Red + Blue. Damage plus poison combos. Coming later.",
                "", "", SignatureKind.None, null);
            Upsert("Berserker", DieColor.Red, 28, false,
                "Red + Yellow. Heavy damage, self-modifying Rage. Coming later.",
                "", "", SignatureKind.None, null);
            Upsert("Cleric", DieColor.Blue, 22, false,
                "Blue + Yellow. Cleanse and heal, Bless another die. Coming later.",
                "", "", SignatureKind.None, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("HeroAssetAuthoring: six heroes authored.");
        }

        /// <summary>Treasure face upgrades per class. Face indices match the dice authored in DiceAssetAuthoring.</summary>
        private static List<FaceUpgrade> UpgradesFor(string hero)
        {
            FaceEffectEntry E(FaceEffectKind k, int v = 0) => new(k, v);
            FaceUpgrade U(int face, string label, params FaceEffectEntry[] effects) =>
                new() { faceIndex = face, replacement = DieFace.Of(label, effects) };

            return hero switch
            {
                "Knight" => new List<FaceUpgrade>
                {
                    U(1, "Strike 4", E(FaceEffectKind.Strike, 4)),
                    U(3, "Shield 5", E(FaceEffectKind.Shield, 5)),
                    U(5, "Taunt + Shield 4", E(FaceEffectKind.Taunt), E(FaceEffectKind.Shield, 4)),
                },
                "Arcanist" => new List<FaceUpgrade>
                {
                    U(1, "Burn 3", E(FaceEffectKind.Burn, 3)),
                    U(3, "Poison 4", E(FaceEffectKind.Poison, 4)),
                    U(6, "Burn 1", E(FaceEffectKind.Burn, 1)), // Fizzle becomes a weak face instead of a dead one
                },
                "Trickster" => new List<FaceUpgrade>
                {
                    U(6, "Jab 4", E(FaceEffectKind.Jab, 4)),
                    U(1, "Nudge ±2", E(FaceEffectKind.Nudge, 2)),
                },
                _ => new List<FaceUpgrade>(),
            };
        }

        public static HeroDefinition Load(string name) =>
            AssetDatabase.LoadAssetAtPath<HeroDefinition>($"{HeroDir}/{name}.asset");

        private static DieDefinition Die(string name)
        {
            var die = AssetDatabase.LoadAssetAtPath<DieDefinition>($"{DiceDir}/{name}.asset");
            if (die == null) Debug.LogWarning($"HeroAssetAuthoring: die {name} missing; run DungeonDice ▸ Author Prototype Dice first.");
            return die;
        }

        private static void Upsert(string name, DieColor color, int maxHealth, bool playable,
            string description, string signatureName, string signatureDescription, SignatureKind signatureKind, DieDefinition die)
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
            hero.signatureName = signatureName;
            hero.signatureDescription = signatureDescription;
            hero.signatureKind = signatureKind;
            hero.dice.Clear();
            if (die != null) hero.dice.Add(die);
            hero.faceUpgrades = UpgradesFor(name);
            EditorUtility.SetDirty(hero);
            Debug.Log($"HeroAssetAuthoring: {(created ? "created" : "updated")} {path}");
        }
    }
}
