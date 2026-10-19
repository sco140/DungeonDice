# DungeonDice — Architecture

_Snapshot taken 2026-10-19 from branch `ROBOCODY` (Unity 6000.3.5f2), **before prototype build-order step 1**. Written by reading every script, the scene, prefabs, and project settings, and by inspecting the live Editor._

> **Status note (after step 1, same day):** the live scripts now sit under `Assets/_Project/Scripts/` in the `DungeonDice` assembly, `BaseDice` became `DieDefinition`, `DieHandler.cs`/`LaunchDice.cs` were deleted, and `DiceManager` emits `RollCompleted` instead of summing mana. Items 1, 2, 7 (partly), 9, 12 and 17 (for `DiceManager`) in §9 are resolved. `CLAUDE.md` describes the current layout; this document remains the reference for the SampleScene wiring and the original event flow.

---

## 1. What the game is right now

DungeonDice is an early prototype of a turn-based, dice-driven party RPG. One scene exists. In it:

- A **player team** of three placeholder units (Front / Center / Back) faces an **enemy team** of three placeholder capsules on a flat arena.
- At the start of the game a **dice roll overlay** appears: six physical D6 dice spin weightlessly inside a walled box that is rendered by a second camera into a render texture and shown full-screen.
- The player **drags across the screen** to launch the dice. Drag direction becomes launch direction, drag length becomes force.
- When every die comes to rest, the face-up side of each is read. Faces map to **Red / Blue / Yellow mana** (two faces each on the default die). The totals are summed, written into the player team's resource pool, and shown in the HUD header.
- The overlay hides. Clicking a player unit opens a world-space **skill menu** with four unlabelled buttons that do nothing.

That is the complete current loop. There is no enemy behaviour, no skill resolution, no damage, no win/lose, and no scene transitions.

---

## 2. Scenes

| Scene | Path | Role |
|---|---|---|
| SampleScene | `Assets/Scenes/SampleScene.unity` | The only scene. Listed first and alone in Build Settings. Contains managers, arena, both teams, HUD, dice box and camera. |

Because there is one scene, the `DontDestroyOnLoad` calls on `EventManager` and `DiceManager` currently have no effect, but they signal the intent for those two to persist across future scene loads.

### Scene hierarchy (live, as of this snapshot)

```
Managers                         GameManager, EventManager
Directional Light
Main Camera                      Camera (depth -1, culls everything), AudioListener, PhysicsRaycaster
Canvas                           Screen Space Overlay
  Panel                          Image (full-screen dim) + DiceRollerController  <- drag surface
    DiceRollCamera  [inactive]   RawImage showing the DiceCamera render texture
  HUD                            ResourceManager
    Header/ResouceHolder         Red / Blue / Yellow  (Image + TextMeshProUGUI each)
PlayArea
  Arena                          MeshFilter/Renderer/MeshCollider (floor)
  PlayerTeam                     PlayerTeamManager
    UI_SkillMenu                 World Space Canvas (camera = Main Camera), 2x2 Buttons, WorldUI material
    Front / Center / Back        BoxCollider + PlayerUnitController, each with SkillLocation + Character mesh
  EnemyTeam
    Front / Center / Back        BoxCollider + Capsule mesh only (no scripts)
EventSystem                      EventSystem + InputSystemUIInputModule
DiceRollingArea (prefab inst.)   DiceManager (added in-scene, not in the prefab)
  Camera                         renders to DiceCamera.renderTexture; culls Default|UI|Border|Dice
  DiceHolder                     spawn point for dice
  Walls                          Floor, Ceiling, 4 Walls, 4 Corners (BoxColliders)
```

`DiceRollingArea` is positioned at x = -652, far outside the main camera's view, so the dice box is only ever seen through its own camera's render texture.

---

## 3. Core scripts and how they connect

All gameplay code is in `Assets/Scripts/` (11 files, ~1,100 lines) plus three ScriptableObject definitions in `Assets/Scriptable Objects/`. Everything is in the default `Assembly-CSharp` assembly. Shared types live in the `GameUtility` namespace.

### 3.1 Managers (singletons)

Every manager follows the same pattern: a static `Instance`, an `Awake` that destroys duplicates, and registration of string-keyed event handlers.

| Script | Lives on | Responsibility |
|---|---|---|
| `GameManager` | `/Managers` | Kicks off the game by firing `EnableDiceLaunch` in `Start`. Has `StartTurn` / `EndTurn` stubs. |
| `EventManager` | `/Managers` | Global string-keyed pub/sub bus (see 3.2). The spine of the architecture. |
| `DiceManager` | `/DiceRollingArea` | Spawns, idles, launches dice; aggregates results into a resource event. |
| `PlayerTeamManager` | `/PlayArea/PlayerTeam` | Owns red/blue/yellow mana, dice count, the three `PlayerUnitController`s and the skill menu reference. |
| `ResourceManager` | `/Canvas/HUD` | Writes mana totals into the three HUD text fields. |

### 3.2 The event bus (`EventManager`)

`EventManager` is a `Dictionary<string, List<EventHandlerBase>>`. Handlers are wrapped in `EventHandler`, `EventHandler<I>`, `EventHandler<I,J>` or `EventHandler<I,J,K>` so that typed delegates can share one list. Key properties:

- **Dispatch runs inside a coroutine.** `TriggerEvent` starts a coroutine; the coroutine invokes listeners synchronously and then yields once. Listeners therefore run in the same frame as the trigger, but `TriggerEvent` cannot be used before `EventManager.Awake` has run or from an object that is being destroyed.
- **Type safety is by convention.** A listener registered for `"Foo"` as `EventHandler<int>` will throw an `InvalidCastException` if someone triggers `"Foo"` with a `Vector3`. Event names are raw string literals scattered across scripts; there is no central registry.
- `Unregister` matches by delegate equality, so a listener must unregister with the exact same method it registered.
- Triggering an event with no listeners logs a warning rather than failing.

### Event catalogue

| Event | Payload | Fired by | Handled by |
|---|---|---|---|
| `EnableDiceLaunch` | `int` dice count | `GameManager.Start`, `GameManager.StartTurn` | `DiceRollerController` → shows overlay, fires `SetUpDice` |
| `SetUpDice` | `int` dice count | `DiceRollerController.EnableDiceLaunch` | `DiceManager.SetUpDice` |
| `LaunchDice` | `Vector3` drag vector | `DiceRollerController.OnPointerUp` | `DiceManager.LaunchDice` |
| `UpdateResources` | `Event_ResourceUpdate` | `DiceManager.ProcessDiceValue` | `PlayerTeamManager.UpdateResources` |
| `BeforeResourceUpdate` | `Event_ResourceUpdate` | `PlayerTeamManager.UpdateResources` | **nobody** (hook for modifiers) |
| `ResourceUIUpdate` | `Event_ResourceUpdate` | `PlayerTeamManager.UpdateResources` | `ResourceManager.UpdateUI` |
| `AfterResourceUpdate` | `Event_ResourceUpdate` | `PlayerTeamManager.UpdateResources` | **nobody** (hook for reactions) |
| `DisableDiceLaunch` | none | `DiceManager.ProcessDiceValue` | `DiceRollerController` → hides overlay |

`Event_ResourceUpdate` (defined at the top of `PlayerTeamManager.cs`) is a mutable class with `red/blue/yellow` ints, passed by reference so `BeforeResourceUpdate` listeners could alter it before it is applied.

### 3.3 Dice scripts

| Script | Used? | Notes |
|---|---|---|
| `DiceManager` | Yes | Scene-level controller. |
| `DiceHandler` | Yes (on `D6.prefab`) | Per-die physics launch and face-up detection. |
| `DiceRollerController` | Yes (on `/Canvas/Panel`) | Drag input + overlay toggling. |
| `DieHandler` | **No** | Older, self-contained version of `DiceManager` + `LaunchDice`. Not referenced by any scene or prefab. Dead code. |
| `LaunchDice` | **No** | Older drag handler that calls `DiceManager.LaunchDice` directly. Superseded by `DiceRollerController`. Dead code. |

### 3.4 Data definitions (ScriptableObjects)

| Script | Status |
|---|---|
| `BaseDice` (`GameUtility`) | Concrete. Holds `ResourceFace[6]` of `{faceIndex, DiceResultType, amount}`. One asset exists: `Assets/Scriptable Objects/Dice/BaseDice.asset`, referenced by `D6.prefab`. |
| `BaseSkill` | Abstract. `name`, `cooldown`, `Mana` cost, `CanTarget`, `Resolve`. No concrete subclass, no assets. |
| `BaseCharacters` (file `BaseCharacter.cs`) | Abstract. Health, attack, `List<BaseDice>`, `BaseSkill[4]`, a private passive-effects dictionary. No subclass, no assets, not referenced anywhere. |

`DiceResultType` already enumerates `Red, Blue, Yellow, Nothing, Attack, Armor`, so the die data model anticipates combat faces that the roll processor does not yet handle.

### 3.5 Dependency sketch

```
GameManager --EnableDiceLaunch--> DiceRollerController --SetUpDice / LaunchDice--> DiceManager
                                        ^                                             |
                                        | DisableDiceLaunch                           | spawns / launches
                                        |                                             v
                                        |                                    D6.prefab (DiceHandler)
                                        |                                             |
                                        |                      ProcessDiceValue (direct static call)
                                        +--------------------- DiceManager <----------+
                                                                   | UpdateResources
                                                                   v
                                                         PlayerTeamManager --ResourceUIUpdate--> ResourceManager (HUD)
                                                                   ^
                                    PlayerUnitController --GetSkillMenu()--+   (direct static call, opens UI_SkillMenu)
```

Almost everything crosses module boundaries through `EventManager`. The two exceptions are direct static calls: `DiceHandler → DiceManager.ProcessDiceValue` and `PlayerUnitController → PlayerTeamManager.GetSkillMenu`.

---

## 4. Game loop and state flow

There is no explicit state machine. State is implicit in which objects are active and in two booleans (`DiceManager.m_isIdle`, overlay active). The actual sequence on Play:

1. **Awake phase.** All managers claim their singletons and register listeners. `DiceManager` raises `Physics.defaultSolverIterations` and `defaultSolverVelocityIterations` to 12 globally.
2. **`GameManager.Start`** fires `EnableDiceLaunch(PlayerTeamManager.DiceCount)`. `DiceCount` is 6 in the scene.
3. **`DiceRollerController.EnableDiceLaunch`** fires `SetUpDice(6)`, activates `DiceRollCamera` (the RawImage) and enables the dimming `Panel` image.
4. **`DiceManager.SetUpDice`** instantiates (or re-positions/re-enables) six `D6` prefabs in a row under `DiceHolder`, spaced by `m_spawnPadding` (3 units), gravity off, random angular velocity. While `m_isIdle` is true, `Update` keeps nudging each die with random torque so they tumble in place.
5. **Player drags on the Panel.** `OnPointerDown` stores the pointer position; `OnPointerUp` fires `LaunchDice(new Vector3(dx, 0, dy))`.
6. **`DiceManager.LaunchDice`** normalises the vector, adds a downward bias, clamps force to [10, 100], sets `Physics.gravity` to (0, -12, 0) **globally and permanently**, sets `m_isIdle = false`, and calls `DiceHandler.LaunchDice` on every die with the force plus random torque.
7. **`DiceHandler.LaunchDice`** enables gravity, applies the impulse, torque, and an extra 12-unit downward impulse, then starts `WaitForDiceToSettle`, which polls `Rigidbody.IsSleeping()` every fixed update.
8. **Face read.** `GetFaceUp` picks the local face normal most aligned with world up and maps it 1–6, then looks up `BaseDice.m_lstDieFaces[result-1]` and calls `DiceManager.ProcessDiceValue(name, face)`.
9. **`ProcessDiceValue`** accumulates faces until the count equals the number of spawned dice, sums Red/Blue/Yellow, fires `UpdateResources`, fires `DisableDiceLaunch`, clears the accumulator.
10. **`PlayerTeamManager.UpdateResources`** fires `BeforeResourceUpdate` (no listeners), **overwrites** (does not add to) its red/blue/yellow fields, fires `ResourceUIUpdate` → HUD text updates to `": N"`, fires `AfterResourceUpdate` (no listeners).
11. **Overlay hides.** The player can now click a unit to show the skill menu. Nothing else happens. `GameManager.EndTurn` / `StartTurn` exist but nothing calls them, so the roll cannot be repeated without restarting Play mode.

Note on step 11: even if `StartTurn` were called, `DiceManager.m_isIdle` is never reset to true after a launch, so a second `LaunchDice` would be rejected with "Dice have been launched already."

---

## 5. How dice work in detail

**Prefab.** `Assets/Dice/D6.prefab`: 44-poly `Dice_d6.fbx` from the bundled "Dice d6 game ready PBR" pack, `Plastic Rough Scratched red` material, `BoxCollider` 1.4³ with `DicePhysicMaterial` (friction 0.6, bounce 0.5), `Rigidbody` (mass 1, gravity off, continuous collision), `DiceHandler` referencing `BaseDice.asset`. Layer: `Dice` (7).

**Face mapping.** `DiceHandler.faces` hardcodes six local normals: +Z=1, +Y=2, −X=3, +X=4, −Y=5, −Z=6. Opposite faces sum to 7, matching the model's documentation. This mapping is in code, not on the `BaseDice` asset, so a different die mesh with a different face orientation would need a code change.

**Face values.** `BaseDice.asset` (and the C# defaults) map faces 1 & 6 → Red, 2 & 5 → Blue, 3 & 4 → Yellow, each amount 1. There is no `Nothing`, `Attack` or `Armor` face in use yet.

**The box.** `DiceRollingArea.prefab` is a 30×15×30 closed box (floor, ceiling, four walls, four corner wedges) of `BoxCollider`s with no physic material, plus a perspective camera at (1, 32.9, −10.1) looking down into it, writing to `DiceCamera.renderTexture` (2048²). The ceiling keeps hard throws inside the frame.

**Physics tuning that leaks.** Two settings are changed at runtime and never restored: solver iterations (6/1 → 12/12) and gravity (−9.81 → −12). Both are process-wide and will affect any later gameplay physics.

**Settling detection** relies on `Rigidbody.IsSleeping()`. A die that comes to rest on an edge against a wall, or that keeps micro-jittering, will delay or block result processing, and because `ProcessDiceValue` waits for all six, one stuck die blocks the whole turn.

---

## 6. UI setup

- **UI framework:** Unity UI (uGUI 2.0.0) with TextMesh Pro. UI Toolkit is not used (there is an unused `using UnityEngine.UIElements` in `EventManager.cs`).
- **Main `Canvas`:** Screen Space Overlay. `Panel` is a full-screen `Image` that doubles as the drag surface (`DiceRollerController` implements `IPointerDownHandler`/`IPointerUpHandler`). Its only child, `DiceRollCamera`, is a `RawImage` bound to `DiceCamera.renderTexture` and starts inactive.
- **HUD:** `Canvas/HUD/Header/ResouceHolder` (sic) holds three `HorizontalLayoutGroup` rows, each an icon `Image` plus a `TextMeshProUGUI`. `ResourceManager` holds references to the three text fields and writes `": N"` into them.
- **Skill menu:** `PlayArea/PlayerTeam/UI_SkillMenu` is a **World Space** canvas with `Main Camera` as its event camera. It has a 2×2 grid of `Button`s (`Top/LH_Button`, `Top/RH_Button`, `Bottom/LH_Button`, `Bottom/RH_Button`), all labelled "Button", all with **zero** `onClick` listeners. The six `Image`s in the menu use `Assets/UI/WorldUI.mat`, driven by `Assets/UI/RenderForward.shadergraph`, a URP shader graph that draws on top of world geometry (commit "New shader: Render on top of everything").
- **Opening the menu:** `PlayerUnitController.OnPointerClick` (via `PhysicsRaycaster` on Main Camera hitting the unit's `BoxCollider`) sets the menu active and moves it to that unit's `SkillLocation` transform. Nothing ever hides the menu, and it is **active at scene load**, so it is visible before any unit is clicked.
- **Pointer events** reach the uGUI canvases through `EventSystem` + `InputSystemUIInputModule`, and reach 3D colliders through `PhysicsRaycaster` (event mask: everything).

---

## 7. Packages and render pipeline

**Editor:** 6000.3.5f2. **Template origin:** URP Empty Template (per `Assets/Readme.asset` and `Assets/TutorialInfo`).

| Package | Version | Used? |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.3.0 | Yes. Active pipeline. |
| `com.unity.inputsystem` | 1.17.0 | Yes. `activeInputHandler: 1` = Input System only (legacy Input Manager disabled). |
| `com.unity.ugui` | 2.0.0 | Yes. All UI, including TextMesh Pro. |
| `com.unity.pipeline` | 0.8.0-exp.1 | Yes. Unity CLI / agent bridge (`unity command`, `eval`). Dev tooling, not gameplay. |
| `com.unity.ai.navigation` | 2.0.9 | No. Template default. |
| `com.unity.timeline` | 1.8.10 | No. Template default. |
| `com.unity.visualscripting` | 1.9.9 | No. Template default. |
| `com.unity.multiplayer.center` | 1.0.1 | No. Template default. |
| `com.unity.test-framework` | 1.6.0 | No tests exist. |
| `com.unity.collab-proxy`, `ide.rider`, `ide.visualstudio` | — | Tooling. |

TextMesh Pro ships inside uGUI 2.0; its "Examples & Extras" are imported under `Assets/TextMesh Pro/` (34 sample scripts, none used by the game).

**URP configuration** (`Assets/Settings/`):

- Two quality tiers, `PC` → `PC_RPAsset` + `PC_Renderer`, `Mobile` → `Mobile_RPAsset` + `Mobile_Renderer`. `PC_RPAsset` is the default pipeline in Graphics Settings.
- `PC_Renderer`: **Forward+** rendering (`m_RenderingMode: 2`), one renderer feature: Screen Space Ambient Occlusion. HDR on, MSAA off, render scale 1, main-light shadows on, per-pixel additional lights.
- `DefaultVolumeProfile.asset` and `SampleSceneProfile.asset` for post-processing volumes.
- Render texture `Assets/Dice/DiceCamera.renderTexture` (2048×2048) bridges the dice camera to the overlay `RawImage`.

**Layers:** `Border` (6) and `Dice` (7) are custom. `WorldUI` is a custom Rendering Layer. No custom tags, no custom sorting layers used.

---

## 8. Input system

- Project-wide actions asset: `Assets/InputSystem_Actions.inputactions` (the Unity default template: `Player` map with Move/Look/Attack/Interact/Crouch/Jump/Previous/Next/Sprint, `UI` map with Navigate/Submit/Cancel/Point/Click/etc., control schemes Keyboard&Mouse, Gamepad, Touch, Joystick, XR). **No gameplay code reads any of these actions.**
- The scene's `InputSystemUIInputModule` is bound to the Input System package's built-in `DefaultInputActions`, not to the project's asset. This works, but means UI pointer bindings and the project asset can drift.
- All actual gameplay input is **pointer-based through uGUI event interfaces**: `IPointerDownHandler`/`IPointerUpHandler` on the dice panel and `IPointerClickHandler` on player units. This already works with mouse and touch via the UI module's Point/Click actions.
- No keyboard or gamepad input is wired anywhere.

---

## 9. Broken, half-finished, or risky

**Functional gaps**

1. **Turn loop never advances.** `GameManager.EndTurn`/`StartTurn` are never called. After one roll the game is inert until Play is restarted.
2. **Dice cannot be re-launched.** `DiceManager.m_isIdle` is never set back to `true`, so a second `LaunchDice` is refused even if the overlay is re-shown.
3. **Resources are overwritten, not accumulated.** `PlayerTeamManager.UpdateResources` assigns the roll totals directly, so mana does not carry over between rolls. Intentional or not, it is undocumented.
4. **Skill menu is inert and always visible.** Four "Button" buttons with no `onClick`, no skill data, no close logic; active from frame one.
5. **No skills or characters exist.** `BaseSkill` and `BaseCharacters` are abstract with no concrete subclasses or assets. `BaseCharacters` is referenced by nothing.
6. **`DiceResultType.Attack` / `Armor` / `Nothing`** are enumerated but ignored by `ProcessDiceValue`'s switch (they are silently dropped).
7. **`Mana` constructor bug** in `GameUtilities.cs`: `ManaColor.Generic` is initialised from the `blue` parameter instead of `generic`. The struct also wraps a `Dictionary` and is marked `[Serializable]`, but Unity cannot serialize dictionaries, so `BaseSkill.m_manaCost` will not show in the Inspector or persist.
8. **Enemy team has no scripts** and no data. Three capsules with colliders.

**Dead code**

9. `DieHandler.cs` and `LaunchDice.cs` are superseded and unreferenced.
10. 34 TextMesh Pro example scripts and `Assets/TutorialInfo` (template readme) are shipped in `Assets/`.
11. Empty `Start()` methods in `ResourceManager` and `PlayerTeamManager`; unused `using` directives in several files.

**Fragility / design smells**

12. **Global physics mutation.** Solver iterations and `Physics.gravity` are changed at runtime and never restored (see §5).
13. **Stringly-typed events with unchecked casts.** A payload type mismatch is a runtime `InvalidCastException`. There is no `const` registry of event names.
14. **Coroutine dispatch** means `EventManager.TriggerEvent` cannot be called before `EventManager.Awake` or from a destroyed object. `GameManager.Start` works only because `Start` runs after every `Awake`.
15. **`Rigidbody.IsSleeping` settling** can stall indefinitely on a jittering die, blocking the whole turn.
16. **Hardcoded face normals** in `DiceHandler` instead of on the `BaseDice` asset; a different mesh orientation breaks results silently.
17. **Singleton `Instance` access without null checks** in static methods (`PlayerTeamManager.DiceCount`, `DiceManager.SetUpDice`, etc.) will `NullReferenceException` if called before the owning object exists.
18. **Naming drift**: `BaseCharacters` class in `BaseCharacter.cs`; `ResouceHolder` typo in the hierarchy; `DiceFaceValue` enum in `GameUtilities.cs` (RED/BLUE/YELLOW/BLANK) duplicates `DiceResultType` and is unused.
19. **Line endings**: `.gitattributes` (untracked at time of writing) forces `.cs` to CRLF while scene/prefab/asset files are LF. The Editor currently writes `.cs` with LF, so Git reports normalisation warnings on every C# change until the files are renormalised.

---

## 10. Five most natural extension points

1. **Turn state machine in `GameManager`.** `StartTurn`/`EndTurn` already exist and the dice overlay already toggles on `EnableDiceLaunch`/`DisableDiceLaunch`. Add an explicit `enum TurnPhase { Roll, Act, EnemyTurn, Resolve }`, have `DiceManager` reset `m_isIdle` on `SetUpDice`, and call `EndTurn` from a UI button or after skills resolve. This unlocks repeated play without touching the dice code.

2. **Skill system via `BaseSkill` + the skill menu buttons.** `BaseSkill` already defines `CanTarget`/`Resolve`/`Mana` cost, `BaseCharacters.m_lstSkills` is sized 4, and `UI_SkillMenu` has exactly four buttons. Concrete `BaseSkill` subclasses as ScriptableObject assets, a small `SkillMenuController` that binds a unit's four skills to the four buttons (the TODO in `PlayerTeamManager` asks for exactly this), and spending from `PlayerTeamManager`'s red/blue/yellow pool is a straight line. Fix the `Mana` struct serialisation first.

3. **Resource modifiers through `BeforeResourceUpdate` / `AfterResourceUpdate`.** These events already fire with a mutable `Event_ResourceUpdate` and have no listeners. Passive character effects ("+1 Red whenever you roll Blue"), buffs, or an accumulation policy (add instead of overwrite) can be implemented as listeners with no changes to `PlayerTeamManager` or `DiceManager`.

4. **Per-character dice via `BaseCharacters.m_lstDiceValues` and `BaseDice`.** `DiceManager.SetUpDice` currently spawns one prefab with one `BaseDice`. Extending `SetUpDice` to take a `List<BaseDice>` (one per die, sourced from the party's characters) and assigning `DiceHandler.m_diceFaceInfo` per instance gives each hero their own die faces. Handling `Attack`/`Armor`/`Nothing` in `ProcessDiceValue` (and extending `Event_ResourceUpdate`) falls out naturally.

5. **Enemy team behaviour on the existing `EnemyTeam` slots.** `Front/Center/Back` mirror the player layout and already have colliders. Adding an `EnemyUnitController` + `EnemyTeamManager` (same singleton/event pattern as `PlayerTeamManager`), driven by `BaseCharacters` assets for health/attack, and hooking into the turn machine from point 1 gives the first real combat loop. `PlayerUnitController` is the template for targeting via `IPointerClickHandler`.

Secondary candidates: scene flow (a `MainMenu` / `Battle` split, which is why the managers already use `DontDestroyOnLoad`), and replacing `IsSleeping` settling with a velocity-threshold-plus-timeout so one stuck die cannot freeze a turn.
