using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// The bundled Dice_6 pack ships materials on the built-in "Autodesk Interactive" shader, which
    /// URP renders as magenta. This converts them to URP Lit, carrying the albedo, normal, metallic,
    /// occlusion and height maps across. Re-runnable: already-converted materials are skipped.
    /// </summary>
    public static class MaterialUpgradeAuthoring
    {
        private const string PackMaterialDir = "Assets/Dice/Dice_6/Materials";
        private const string UrpLit = "Universal Render Pipeline/Lit";

        [MenuItem("DungeonDice/Upgrade Dice Pack Materials to URP")]
        public static void UpgradeDicePack()
        {
            var lit = Shader.Find(UrpLit);
            if (lit == null)
            {
                Debug.LogError("MaterialUpgradeAuthoring: URP Lit shader not found.");
                return;
            }

            int converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { PackMaterialDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader == lit) continue;
                if (!material.shader.name.Contains("Autodesk") && !material.shader.name.StartsWith("Standard")) continue;

                ConvertToUrpLit(material, lit);
                EditorUtility.SetDirty(material);
                converted++;
                Debug.Log($"MaterialUpgradeAuthoring: converted {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"MaterialUpgradeAuthoring: {converted} material(s) converted to URP Lit.");
        }

        /// <summary>Map Standard / Autodesk Interactive texture slots onto URP Lit and set its keywords.</summary>
        public static void ConvertToUrpLit(Material material, Shader lit)
        {
            Texture Get(string name) => material.HasProperty(name) ? material.GetTexture(name) : null;
            var albedo = Get("_MainTex");
            var normal = Get("_BumpMap");
            var metallic = Get("_MetallicGlossMap");
            var occlusion = Get("_OcclusionMap");
            var height = Get("_ParallaxMap");
            var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

            material.shader = lit;
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", metallic);
            material.SetTexture("_OcclusionMap", occlusion);
            material.SetTexture("_ParallaxMap", height);
            material.SetFloat("_Smoothness", 0.5f);
            material.SetFloat("_Metallic", metallic != null ? 1f : 0f); // URP multiplies the map by this slider

            void Keyword(string keyword, bool on)
            {
                if (on) material.EnableKeyword(keyword);
                else material.DisableKeyword(keyword);
            }
            Keyword("_NORMALMAP", normal != null);
            Keyword("_METALLICSPECGLOSSMAP", metallic != null);
            Keyword("_OCCLUSIONMAP", occlusion != null);
            Keyword("_PARALLAXMAP", height != null);
            // Keywords from the old shader that URP Lit does not know.
            foreach (var stale in material.shaderKeywords.Where(k => k is "_SPECGLOSSMAP" or "_METALLICGLOSSMAP").ToList())
                material.DisableKeyword(stale);
        }
    }
}
