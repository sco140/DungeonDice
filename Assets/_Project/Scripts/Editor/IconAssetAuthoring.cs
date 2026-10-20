using DungeonDice.UI;
using UnityEditor;
using UnityEngine;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Imports the copied Kenney icons as UI sprites and fills <see cref="IconLibrary"/> at
    /// <c>Assets/_Project/Data/UI/Icons.asset</c>. The mapping from concept to file lives here, so
    /// swapping art is a one-line change per icon (and a row in docs/ART_RESOURCES.md).
    /// </summary>
    public static class IconAssetAuthoring
    {
        public const string IconsPath = "Assets/_Project/Data/UI/Icons.asset";
        private const string Board = "Assets/_Project/Art/Kenney/BoardGameIcons";
        private const string Game = "Assets/_Project/Art/Kenney/GameIcons";

        [MenuItem("DungeonDice/Author Icons")]
        public static void CreateAll()
        {
            AuthoringUtil.EnsureFolder("Assets/_Project/Data/UI");
            ImportAsSprites(Board);
            ImportAsSprites(Game);

            var icons = AssetDatabase.LoadAssetAtPath<IconLibrary>(IconsPath);
            if (icons == null)
            {
                icons = ScriptableObject.CreateInstance<IconLibrary>();
                AssetDatabase.CreateAsset(icons, IconsPath);
            }

            icons.nodeStart = B("flag_triangle");
            icons.nodeCombat = B("sword");
            icons.nodeElite = B("skull");
            icons.nodeRest = B("campfire");
            icons.nodeTreasure = B("pouch");
            icons.nodeBoss = B("crown_a");

            icons.strike = B("sword");
            icons.shield = B("shield");
            icons.taunt = B("hand");
            icons.burn = B("fire");
            icons.poison = B("flask_full");
            icons.weaken = B("suit_hearts_broken");
            icons.hex = B("hexagon");
            icons.detonate = B("fire");
            icons.recolor = B("card_flip");
            icons.reroll = B("dice_question");
            icons.number = B("dice");

            icons.intentAttack = B("sword");
            icons.intentAttackAll = B("arrow_cross");
            icons.intentBlock = B("shield");

            icons.heart = B("suit_hearts");
            icons.mana = B("token");
            icons.locked = G("locked");
            icons.check = G("checkmark");
            icons.cross = G("cross");
            icons.warning = G("warning");
            icons.arrowRight = B("arrow_right");
            for (int i = 1; i <= 6; i++) icons.dieFaces[i - 1] = B($"dice_{i}");

            EditorUtility.SetDirty(icons);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("IconAssetAuthoring: icon library authored.");
        }

        public static IconLibrary Load() => AssetDatabase.LoadAssetAtPath<IconLibrary>(IconsPath);

        private static Sprite B(string name) => LoadSprite($"{Board}/{name}.png");
        private static Sprite G(string name) => LoadSprite($"{Game}/{name}.png");

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"IconAssetAuthoring: no sprite at {path}");
            return sprite;
        }

        /// <summary>Set every PNG in the folder to Sprite (2D and UI) import mode.</summary>
        private static void ImportAsSprites(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }
    }
}
