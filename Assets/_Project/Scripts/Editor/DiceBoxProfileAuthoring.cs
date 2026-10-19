using DungeonDice.Dice;
using UnityEditor;
using UnityEngine;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Creates the tray materials and the two prototype <see cref="DiceBoxProfile"/>s
    /// (Combat, Traversal) under <c>Assets/_Project/Data/DiceBox</c>. Re-runnable and GUID-preserving.
    /// Colours are placeholders for art; the point is that the two contexts look different.
    /// </summary>
    public static class DiceBoxProfileAuthoring
    {
        public const string ProfileDir = "Assets/_Project/Data/DiceBox";
        public const string CombatProfilePath = ProfileDir + "/Combat.asset";
        public const string TraversalProfilePath = ProfileDir + "/Traversal.asset";
        private const string MaterialDir = ProfileDir + "/Materials";

        private const string LegacyFloorPath = "Assets/Dice/DiceMat.mat";
        private const string UrpLitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat";
        private const string RoughRedPath = "Assets/Dice/Dice_6/Materials/Plastic Rough Scratched red.mat";
        private const string RoughBluePath = "Assets/Dice/Dice_6/Materials/Plastic Rough Scratched blue.mat";

        [MenuItem("DungeonDice/Author DiceBox Profiles")]
        public static void CreateAll()
        {
            AuthoringUtil.EnsureFolder(ProfileDir);
            AuthoringUtil.EnsureFolder(MaterialDir);

            // Combat: a dark red felt tray with warm light. (The legacy DiceMat floor renders magenta in
            // the Game view despite reporting URP Lit, so the tray uses authored materials like Traversal.)
            var combat = Upsert(CombatProfilePath, "Combat");
            combat.floorMaterial = AuthoringUtil.UpsertLitMaterial($"{MaterialDir}/Tray_Combat_Floor.mat", new Color(0.30f, 0.09f, 0.09f), 0.2f);
            combat.wallMaterial = AuthoringUtil.UpsertLitMaterial($"{MaterialDir}/Tray_Combat_Wall.mat", new Color(0.14f, 0.07f, 0.07f), 0.15f);
            combat.defaultDieMaterial = AssetDatabase.LoadAssetAtPath<Material>(RoughRedPath);
            combat.cameraLocalPosition = new Vector3(1f, 32.9f, -10.1f);
            combat.cameraLocalEuler = new Vector3(67.35f, 0f, 0f);
            combat.fieldOfView = 64.8f;
            combat.lightColor = new Color(1f, 0.85f, 0.7f);
            combat.lightIntensity = 1.5f;
            combat.lightRange = 60f;
            combat.overlayColor = new Color(0.05f, 0f, 0f, 0.9f); // opaque enough that the covered UI does not compete with the hint
            EditorUtility.SetDirty(combat);

            // Traversal: mossy stone tray, cool light, camera pulled up for a map-table feel.
            var traversal = Upsert(TraversalProfilePath, "Traversal");
            traversal.floorMaterial = AuthoringUtil.UpsertLitMaterial($"{MaterialDir}/Tray_Traversal_Floor.mat", new Color(0.16f, 0.26f, 0.20f), 0.25f);
            traversal.wallMaterial = AuthoringUtil.UpsertLitMaterial($"{MaterialDir}/Tray_Traversal_Wall.mat", new Color(0.12f, 0.16f, 0.14f), 0.15f);
            traversal.defaultDieMaterial = AssetDatabase.LoadAssetAtPath<Material>(RoughBluePath);
            traversal.cameraLocalPosition = new Vector3(0f, 36f, -6f);
            traversal.cameraLocalEuler = new Vector3(75f, 0f, 0f);
            traversal.fieldOfView = 60f;
            traversal.lightColor = new Color(0.7f, 0.9f, 1f);
            traversal.lightIntensity = 1.2f;
            traversal.lightRange = 60f;
            traversal.overlayColor = new Color(0f, 0.03f, 0.08f, 0.9f);
            EditorUtility.SetDirty(traversal);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DiceBoxProfileAuthoring: Combat and Traversal profiles authored.");
        }

        private static DiceBoxProfile Upsert(string path, string displayName)
        {
            var profile = AssetDatabase.LoadAssetAtPath<DiceBoxProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<DiceBoxProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.displayName = displayName;
            return profile;
        }
    }

    /// <summary>Shared helpers for the authoring tools.</summary>
    public static class AuthoringUtil
    {
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Create or update a plain URP Lit material with a base colour and smoothness.</summary>
        public static Material UpsertLitMaterial(string path, Color baseColor, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Create or update a tinted copy of an existing material (keeps its textures and shader).</summary>
        public static Material UpsertTintedCopy(string path, string sourcePath, Color baseColor)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"AuthoringUtil: source material missing at {sourcePath}");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = source.shader; // follow the source if it was upgraded (e.g. to URP Lit)
                material.CopyPropertiesFromMaterial(source);
            }
            material.SetColor("_BaseColor", baseColor);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
