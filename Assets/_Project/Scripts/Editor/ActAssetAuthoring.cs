using DungeonDice.Map;
using UnityEditor;
using UnityEngine;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Writes <see cref="PrototypeAct"/> to <c>Assets/_Project/Data/Map/Act1.asset</c> and validates it.
    /// Edit the act in <see cref="PrototypeAct"/> (unit-tested) and re-run; the asset is updated in place.
    /// </summary>
    public static class ActAssetAuthoring
    {
        public const string ActDir = "Assets/_Project/Data/Map";
        public const string Act1Path = ActDir + "/Act1.asset";

        [MenuItem("DungeonDice/Author Act 1")]
        public static void CreateAll()
        {
            AuthoringUtil.EnsureFolder(ActDir);

            var act = AssetDatabase.LoadAssetAtPath<ActDefinition>(Act1Path);
            bool created = act == null;
            if (created)
            {
                act = ScriptableObject.CreateInstance<ActDefinition>();
                AssetDatabase.CreateAsset(act, Act1Path);
            }

            act.displayName = PrototypeAct.DisplayName;
            act.startNode = 0;
            act.nodes = PrototypeAct.BuildNodes();
            EditorUtility.SetDirty(act);

            var errors = ForkValidator.ValidateAct(act);
            foreach (var error in errors) Debug.LogError($"ActAssetAuthoring: {error}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"ActAssetAuthoring: {(created ? "created" : "updated")} {Act1Path} with {act.nodes.Count} nodes, {errors.Count} validation errors.");
        }

        public static ActDefinition Load() => AssetDatabase.LoadAssetAtPath<ActDefinition>(Act1Path);
    }
}
