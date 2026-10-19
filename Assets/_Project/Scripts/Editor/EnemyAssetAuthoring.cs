using System.Collections.Generic;
using DungeonDice.Combat;
using UnityEditor;
using UnityEngine;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Creates the prototype's enemies: three regulars, one elite, one boss, each with telegraphed
    /// intents. Numbers are tuning placeholders. Re-runnable and GUID-preserving.
    /// </summary>
    public static class EnemyAssetAuthoring
    {
        public const string EnemyDir = "Assets/_Project/Data/Enemies";
        public static readonly string[] Regulars = { "SkeletonGuard", "DrownedWretch", "VaultRat" };
        public const string Elite = "VaultWarden";
        public const string Boss = "VaultHeart";

        [MenuItem("DungeonDice/Author Enemies")]
        public static void CreateAll()
        {
            AuthoringUtil.EnsureFolder(EnemyDir);

            Upsert("SkeletonGuard", "Skeleton Guard", 12, "Still holding its post.",
                I(IntentKind.Attack, 4), I(IntentKind.Attack, 3), I(IntentKind.Block, 3));
            Upsert("DrownedWretch", "Drowned Wretch", 10, "Hits hard, dies fast.",
                I(IntentKind.Attack, 5), I(IntentKind.Attack, 2));
            Upsert("VaultRat", "Vault Rat", 8, "Nips at everyone.",
                I(IntentKind.Attack, 3), I(IntentKind.Attack, 3), I(IntentKind.AttackAll, 1));

            Upsert("VaultWarden", "Vault Warden", 25, "Armoured and patient.",
                I(IntentKind.Attack, 7), I(IntentKind.Block, 5), I(IntentKind.AttackAll, 3));

            Upsert("VaultHeart", "The Vault Heart", 40, "What the vault was built to keep in.",
                I(IntentKind.Attack, 8), I(IntentKind.AttackAll, 4), I(IntentKind.Block, 6));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("EnemyAssetAuthoring: five enemies authored.");
        }

        public static EnemyDefinition Load(string name) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{EnemyDir}/{name}.asset");

        private static EnemyIntent I(IntentKind kind, int value) => new() { kind = kind, value = value };

        private static void Upsert(string fileName, string displayName, int maxHealth, string description, params EnemyIntent[] intents)
        {
            var path = $"{EnemyDir}/{fileName}.asset";
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (enemy == null)
            {
                enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(enemy, path);
            }
            enemy.displayName = displayName;
            enemy.maxHealth = maxHealth;
            enemy.description = description;
            enemy.intents = new List<EnemyIntent>(intents);
            EditorUtility.SetDirty(enemy);
        }
    }
}
