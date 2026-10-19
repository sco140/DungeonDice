using DungeonDice.Dice;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/_Project/Prefabs/DiceBox.prefab</c>: the self-contained dice box the
    /// prototype scene drops in. It nests the legacy <c>DiceRollingArea</c> prefab (walls, camera,
    /// spawn point), adds <see cref="DiceManager"/>, and carries its own screen-space overlay
    /// (dim panel with <see cref="DiceRollerController"/> and the dice-camera RawImage).
    /// Re-runnable: saving over the existing path keeps the prefab's GUID.
    /// </summary>
    public static class DiceBoxPrefabAuthoring
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/DiceBox.prefab";
        private const string AreaPrefabPath = "Assets/Dice/DiceRollingArea.prefab";
        private const string DiePrefabPath = "Assets/Dice/D6.prefab";
        private const string RenderTexturePath = "Assets/Dice/DiceCamera.renderTexture";

        [MenuItem("DungeonDice/Author DiceBox Prefab")]
        public static void CreateOrUpdate()
        {
            var area = AssetDatabase.LoadAssetAtPath<GameObject>(AreaPrefabPath);
            var die = AssetDatabase.LoadAssetAtPath<GameObject>(DiePrefabPath);
            var renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (area == null || die == null || renderTexture == null)
            {
                Debug.LogError("DiceBoxPrefabAuthoring: a source asset is missing (DiceRollingArea, D6 or DiceCamera).");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            // Build in the open scene, save, then remove the temporary instance.
            var root = new GameObject("DiceBox");
            try
            {
                var areaInstance = (GameObject)PrefabUtility.InstantiatePrefab(area);
                areaInstance.name = "Arena";
                areaInstance.transform.SetParent(root.transform, false);
                areaInstance.transform.localPosition = Vector3.zero;

                var spawnPoint = areaInstance.transform.Find("DiceHolder");
                if (spawnPoint == null)
                {
                    Debug.LogError("DiceBoxPrefabAuthoring: DiceRollingArea has no DiceHolder child.");
                    return;
                }

                var manager = root.AddComponent<DiceManager>();
                var so = new SerializedObject(manager);
                so.FindProperty("diePrefab").objectReferenceValue = die;
                so.FindProperty("diceSpawnPoint").objectReferenceValue = spawnPoint.gameObject;
                // Tuning copied from the SampleScene instance.
                so.FindProperty("m_idleTorqueStrength").intValue = 20;
                so.FindProperty("m_launchTorqueStrength").intValue = 10;
                so.FindProperty("m_spawnPadding").floatValue = 3f;
                so.FindProperty("m_downwardForce").floatValue = 0.5f;
                so.FindProperty("m_minForce").floatValue = 10f;
                so.FindProperty("m_maxForce").floatValue = 100f;
                so.ApplyModifiedPropertiesWithoutUndo();

                BuildOverlay(root.transform, renderTexture);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                Debug.Log(success
                    ? $"DiceBoxPrefabAuthoring: saved {PrefabPath}"
                    : $"DiceBoxPrefabAuthoring: FAILED to save {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            // EventManager must Awake before anything that registers listeners in Awake
            // (DiceManager, DiceRollerController, the legacy managers). Script Execution Order
            // guarantees it instead of relying on scene order.
            var eventManagerScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Scripts/Core/EventManager.cs");
            if (eventManagerScript == null)
                Debug.LogWarning("DiceBoxPrefabAuthoring: EventManager.cs not found; execution order not set.");
            else if (MonoImporter.GetExecutionOrder(eventManagerScript) != -100)
            {
                MonoImporter.SetExecutionOrder(eventManagerScript, -100);
                Debug.Log("DiceBoxPrefabAuthoring: EventManager execution order set to -100.");
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Screen-space overlay: dim panel (drag surface) with the dice camera image inside it.</summary>
        private static void BuildOverlay(Transform parent, RenderTexture renderTexture)
        {
            var overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlay.transform.SetParent(parent, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above the HUD
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(DiceRollerController));
            panel.transform.SetParent(overlay.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            var dim = panel.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true;
            dim.enabled = false; // hidden until EnableDiceLaunch

            var view = new GameObject("DiceRollCamera", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
            view.transform.SetParent(panel.transform, false);
            Stretch(view.GetComponent<RectTransform>());
            view.GetComponent<RawImage>().texture = renderTexture;
            var fitter = view.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f; // the render texture is square
            view.SetActive(false);

            var controller = panel.GetComponent<DiceRollerController>();
            controller.m_objDiceCameraPanel = view;
            controller.m_cBackgroundPanel = dim;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

    }
}
