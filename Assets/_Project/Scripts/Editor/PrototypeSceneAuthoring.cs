using System.Collections.Generic;
using System.Linq;
using DungeonDice.Run;
using DungeonDice.UI;
using GameUtility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonDice.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/_Project/Scenes/Prototype.unity</c> from scratch: camera, light,
    /// EventManager, EventSystem, the Run object (RunController + ScreenRouter), the UI canvas with
    /// one root per <see cref="RunPhase"/>, and a DiceBox prefab instance far below the camera.
    /// Then makes it Build Settings index 0 with SampleScene kept but disabled.
    /// Re-runnable: the scene file is rewritten in place (its .meta and GUID survive).
    /// </summary>
    public static class PrototypeSceneAuthoring
    {
        public const string ScenePath = "Assets/_Project/Scenes/Prototype.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string DiceBoxPrefabPath = DiceBoxPrefabAuthoring.PrefabPath;

        [MenuItem("DungeonDice/Author Prototype Scene")]
        public static void CreateOrUpdate()
        {
            var open = SceneManager.GetActiveScene();
            if (open.isDirty)
            {
                Debug.LogError("PrototypeSceneAuthoring: the open scene has unsaved changes. Save or discard them first.");
                return;
            }

            var roster = HeroAssetAuthoring.RosterOrder.Select(HeroAssetAuthoring.Load).ToList();
            if (roster.Any(h => h == null))
            {
                Debug.LogError("PrototypeSceneAuthoring: hero assets missing; run DungeonDice ▸ Author Heroes first.");
                return;
            }
            var diceBoxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiceBoxPrefabPath);
            if (diceBoxPrefab == null)
            {
                Debug.LogError("PrototypeSceneAuthoring: DiceBox prefab missing; run DungeonDice ▸ Author DiceBox Prefab first.");
                return;
            }
            var act = ActAssetAuthoring.Load();
            if (act == null)
            {
                Debug.LogError("PrototypeSceneAuthoring: Act1 missing; run DungeonDice ▸ Author Act 1 first.");
                return;
            }
            var traversalPool = new[] { "LowRoad", "Steady", "HighRoad", "Wild" }
                .Select(n => AssetDatabase.LoadAssetAtPath<Dice.DieDefinition>($"Assets/_Project/Data/Dice/Traversal/{n}.asset")).ToList();
            var treasureDie = AssetDatabase.LoadAssetAtPath<Dice.DieDefinition>("Assets/_Project/Data/Dice/Traversal/Pilgrim.asset");
            var rewardPool = traversalPool.Append(treasureDie).ToList(); // Treasure may grant any traversal die, Pilgrim included
            if (traversalPool.Any(d => d == null) || treasureDie == null)
            {
                Debug.LogError("PrototypeSceneAuthoring: traversal dice missing; run DungeonDice ▸ Author Prototype Dice first.");
                return;
            }
            var traversalProfile = AssetDatabase.LoadAssetAtPath<Dice.DiceBoxProfile>(DiceBoxProfileAuthoring.TraversalProfilePath);
            var combatProfile = AssetDatabase.LoadAssetAtPath<Dice.DiceBoxProfile>(DiceBoxProfileAuthoring.CombatProfilePath);
            if (traversalProfile == null || combatProfile == null)
            {
                Debug.LogError("PrototypeSceneAuthoring: dice box profiles missing; run DungeonDice ▸ Author DiceBox Profiles first.");
                return;
            }

            AuthoringUtil.EnsureFolder("Assets/_Project/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera and light. URP adds its additional camera data on first render.
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 1f, -10f);
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);

            var lightGo = new GameObject("Directional Light", typeof(Light));
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;

            // Core singletons. EventManager runs at execution order -100 (set by DiceBoxPrefabAuthoring).
            new GameObject("EventManager", typeof(EventManager));
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Run.
            var runGo = new GameObject("Run", typeof(RunController), typeof(ScreenRouter));
            var runController = runGo.GetComponent<RunController>();
            var router = runGo.GetComponent<ScreenRouter>();

            // UI canvas with one inactive root per phase; the router activates the current one.
            var canvasGo = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var screens = new List<ScreenRouter.ScreenEntry>
            {
                Screen<HeroPickScreen>(canvasGo.transform, RunPhase.HeroPick, null),
                Screen<TraversalPickScreen>(canvasGo.transform, RunPhase.TraversalPick, null),
                Screen<MapScreen>(canvasGo.transform, RunPhase.Map, null),
                Screen<CombatScreen>(canvasGo.transform, RunPhase.Combat, null),
                Screen<RewardScreen>(canvasGo.transform, RunPhase.Reward, null),
                Screen<RunEndScreen>(canvasGo.transform, RunPhase.RunEnd, null),
            };
            router.SetScreens(screens);
            runController.Configure(router, roster, act, traversalPool, rewardPool, traversalProfile, combatProfile);

            var regulars = EnemyAssetAuthoring.Regulars.Select(EnemyAssetAuthoring.Load).ToList();
            var elite = EnemyAssetAuthoring.Load(EnemyAssetAuthoring.Elite);
            var boss = EnemyAssetAuthoring.Load(EnemyAssetAuthoring.Boss);
            if (regulars.Any(e => e == null) || elite == null || boss == null)
                Debug.LogWarning("PrototypeSceneAuthoring: some enemies are missing; run DungeonDice ▸ Author Enemies first.");
            runController.ConfigureEnemies(regulars.Where(e => e != null), elite, boss);

            // The dice box, out of the main camera's view. Its own camera renders to the overlay.
            var diceBox = (GameObject)PrefabUtility.InstantiatePrefab(diceBoxPrefab, scene);
            diceBox.transform.position = new Vector3(0f, -500f, 0f);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"PrototypeSceneAuthoring: failed to save {ScenePath}");
                return;
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(SampleScenePath, false),
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"PrototypeSceneAuthoring: saved {ScenePath} and set it as Build Settings index 0.");
        }

        private static ScreenRouter.ScreenEntry Screen<T>(Transform canvas, RunPhase phase, System.Action<T> configure) where T : MonoBehaviour
        {
            var go = new GameObject($"{phase}Screen", typeof(RectTransform), typeof(T));
            go.transform.SetParent(canvas, false);
            UiFactory.Stretch(go.GetComponent<RectTransform>());
            configure?.Invoke(go.GetComponent<T>());
            go.SetActive(false); // the router shows the current phase
            return new ScreenRouter.ScreenEntry { phase = phase, root = go };
        }
    }
}
