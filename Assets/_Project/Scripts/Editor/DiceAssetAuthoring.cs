using DungeonDice.Dice;
using UnityEditor;
using UnityEngine;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Creates or refreshes the prototype's DieDefinition assets: the party's resource die, the
    /// traversal dice and the legacy SampleScene die. Re-runnable: existing assets are updated in
    /// place so their GUIDs (and any references) survive.
    /// </summary>
    public static class DiceAssetAuthoring
    {
        public const string ResourcePath = "Assets/_Project/Data/Dice/Resource.asset";
        private const string TraversalDir = "Assets/_Project/Data/Dice/Traversal";
        private const string LegacyPath = "Assets/_Project/Data/Dice/LegacyDie.asset";

        // Die skins from the bundled Dice_6 pack.
        private const string GlossyWhitePath = "Assets/Dice/Dice_6/Materials/Plastic Glossy Pure write.mat";
        private const string RoughRedPath = "Assets/Dice/Dice_6/Materials/Plastic Rough Scratched red.mat";
        private const string RoughBluePath = "Assets/Dice/Dice_6/Materials/Plastic Rough Scratched blue.mat";

        [MenuItem("DungeonDice/Author Prototype Dice")]
        public static void CreateAll()
        {
            EnsureFolder("Assets/_Project/Data/Dice");
            EnsureFolder(TraversalDir);

            var resourceSkin = AssetDatabase.LoadAssetAtPath<Material>(GlossyWhitePath);
            var traversalSkin = AssetDatabase.LoadAssetAtPath<Material>(RoughBluePath);
            var legacySkin = AssetDatabase.LoadAssetAtPath<Material>(RoughRedPath);

            // The resource die (decision 2026-10-20): two faces per colour. The mesh shows pips, so the
            // screen reads 1–2 as Red, 3–4 as Blue, 5–6 as Yellow; a colour-faced skin is a later art task.
            Upsert(ResourcePath, "Resource", DieColor.None, resourceSkin,
                DieFace.Mana(DieColor.Red), DieFace.Mana(DieColor.Red),
                DieFace.Mana(DieColor.Blue), DieFace.Mana(DieColor.Blue),
                DieFace.Mana(DieColor.Yellow), DieFace.Mana(DieColor.Yellow));

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

        public static DieDefinition LoadResourceDie() => AssetDatabase.LoadAssetAtPath<DieDefinition>(ResourcePath);

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

        private static void EnsureFolder(string path) => AuthoringUtil.EnsureFolder(path);
    }
}
