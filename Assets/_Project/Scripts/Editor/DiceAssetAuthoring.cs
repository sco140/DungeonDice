using DungeonDice.Dice;
using UnityEditor;
using UnityEngine;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Creates or refreshes the prototype's DieDefinition assets from the design proposal tables.
    /// Re-runnable: existing assets are updated in place so their GUIDs (and any references) survive.
    /// </summary>
    public static class DiceAssetAuthoring
    {
        private const string HeroDir = "Assets/_Project/Data/Dice/Heroes";
        private const string TraversalDir = "Assets/_Project/Data/Dice/Traversal";
        private const string MaterialDir = "Assets/_Project/Data/Dice/Materials";
        private const string LegacyPath = "Assets/_Project/Data/Dice/LegacyDie.asset";

        // Die skins from the bundled Dice_6 pack; yellow does not exist there, so it is a tinted copy.
        private const string GlossyRedPath = "Assets/Dice/Dice_6/Materials/Plastic Glossy Pure red.mat";
        private const string GlossyBluePath = "Assets/Dice/Dice_6/Materials/Plastic Glossy Pure blue.mat";
        private const string GlossyWhitePath = "Assets/Dice/Dice_6/Materials/Plastic Glossy Pure write.mat";
        private const string RoughRedPath = "Assets/Dice/Dice_6/Materials/Plastic Rough Scratched red.mat";
        private const string RoughBluePath = "Assets/Dice/Dice_6/Materials/Plastic Rough Scratched blue.mat";

        [MenuItem("DungeonDice/Author Prototype Dice")]
        public static void CreateAll()
        {
            EnsureFolder(HeroDir);
            EnsureFolder(TraversalDir);
            EnsureFolder(MaterialDir);

            var red = AssetDatabase.LoadAssetAtPath<Material>(GlossyRedPath);
            var blue = AssetDatabase.LoadAssetAtPath<Material>(GlossyBluePath);
            var yellow = AuthoringUtil.UpsertTintedCopy($"{MaterialDir}/Die_Yellow.mat", GlossyWhitePath, new Color(1f, 0.8f, 0.15f));
            var traversalSkin = AssetDatabase.LoadAssetAtPath<Material>(RoughBluePath);
            var legacySkin = AssetDatabase.LoadAssetAtPath<Material>(RoughRedPath);

            // Hero dice (one color domain each). Values are placeholders for tuning.
            // Physical face order matters: opposite faces (1-6, 2-5, 3-4) should differ so Flip always
            // changes something. Knight: Strike 3 | Strike 5, Shield 4 | Taunt + Shield 2, Strike 3 | Shield 4.
            Upsert($"{HeroDir}/Knight.asset", "Knight", DieColor.Red, red,
                F("Strike 3", E(FaceEffectKind.Strike, 3)),
                F("Shield 4", E(FaceEffectKind.Shield, 4)),
                F("Strike 3", E(FaceEffectKind.Strike, 3)),
                F("Shield 4", E(FaceEffectKind.Shield, 4)),
                F("Taunt + Shield 2", E(FaceEffectKind.Taunt), E(FaceEffectKind.Shield, 2)),
                F("Strike 5", E(FaceEffectKind.Strike, 5)));

            Upsert($"{HeroDir}/Arcanist.asset", "Arcanist", DieColor.Blue, blue,
                F("Burn 2", E(FaceEffectKind.Burn, 2)),
                F("Burn 2", E(FaceEffectKind.Burn, 2)),
                F("Poison 3", E(FaceEffectKind.Poison, 3)),
                F("Weaken", E(FaceEffectKind.Weaken, 1)),
                F("Hex", E(FaceEffectKind.Hex, 1)),
                F("Fizzle", E(FaceEffectKind.Fizzle)));

            Upsert($"{HeroDir}/Trickster.asset", "Trickster", DieColor.Yellow, yellow,
                F("Nudge ±1", E(FaceEffectKind.Nudge, 1)),
                F("Nudge ±1", E(FaceEffectKind.Nudge, 1)),
                F("Flip", E(FaceEffectKind.Flip)),
                F("Reroll ally", E(FaceEffectKind.RerollAlly)),
                F("Mirror", E(FaceEffectKind.Mirror)),
                F("Jab 2", E(FaceEffectKind.Jab, 2)));

            // Traversal dice: pure numbers, no color, one shared stone-like skin.
            UpsertNumbers($"{TraversalDir}/LowRoad.asset", "Low Road", traversalSkin, 1, 1, 2, 2, 3, 3);
            UpsertNumbers($"{TraversalDir}/Steady.asset", "Steady", traversalSkin, 3, 3, 3, 4, 4, 4);
            UpsertNumbers($"{TraversalDir}/HighRoad.asset", "High Road", traversalSkin, 4, 4, 5, 5, 6, 6);
            UpsertNumbers($"{TraversalDir}/Wild.asset", "Wild", traversalSkin, 1, 1, 1, 6, 6, 6);
            UpsertNumbers($"{TraversalDir}/Pilgrim.asset", "Pilgrim", traversalSkin, 1, 2, 3, 4, 5, 6);

            // The die the legacy SampleScene throws: a plain red d6 so the HUD adapter has a color to bank.
            UpsertNumbers(LegacyPath, "Legacy Red d6", legacySkin, 1, 2, 3, 4, 5, 6, DieColor.Red);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DiceAssetAuthoring: prototype dice authored.");
        }

        private static void UpsertNumbers(string path, string displayName, Material skin,
            int f1, int f2, int f3, int f4, int f5, int f6, DieColor color = DieColor.None)
        {
            Upsert(path, displayName, color, skin,
                DieFace.Number(f1), DieFace.Number(f2), DieFace.Number(f3),
                DieFace.Number(f4), DieFace.Number(f5), DieFace.Number(f6));
        }

        private static DieDefinition Upsert(string path, string displayName, DieColor color, Material skin, params DieFace[] faces)
        {
            var die = AssetDatabase.LoadAssetAtPath<DieDefinition>(path);
            bool created = die == null;
            if (created)
            {
                die = ScriptableObject.CreateInstance<DieDefinition>();
                AssetDatabase.CreateAsset(die, path);
            }

            die.displayName = displayName;
            die.color = color;
            die.visualMaterial = skin;
            die.SetFaces(faces);
            EditorUtility.SetDirty(die);
            Debug.Log($"DiceAssetAuthoring: {(created ? "created" : "updated")} {path}");
            return die;
        }

        private static FaceEffectEntry E(FaceEffectKind kind, int value = 0) => new FaceEffectEntry(kind, value);

        private static DieFace F(string label, params FaceEffectEntry[] effects) => DieFace.Of(label, effects);

        private static void EnsureFolder(string path) => AuthoringUtil.EnsureFolder(path);
    }
}
