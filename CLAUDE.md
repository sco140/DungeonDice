# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

DungeonDice is a Unity 6 (6000.3.5f2) prototype of a dice-driven party RPG. URP 17 (Forward+), Input System only (legacy Input Manager disabled), uGUI + TextMesh Pro. All C# is in the default `Assembly-CSharp` assembly; shared types live in the `GameUtility` namespace.

Deeper references, read before non-trivial changes:

- `docs/ARCHITECTURE.md` — full map of scripts, events, scene hierarchy, known bugs, extension points.
- `docs/Prototype Design Proposal.md` — the target design (roguelike run: hero dice → traversal map → combat).
- `docs/Prototype Design Codebase Review.md` — gap analysis between the two, with keep/adapt/replace/new edits.

## Working with the Editor (Unity CLI)

The project includes `com.unity.pipeline`, so a running Editor can be driven live with the `unity` CLI. The Editor is normally open on this project. Always check first and drive it rather than hand-editing `.unity` / `.prefab` / `.asset` YAML.

```bash
unity status --format json                      # expect state "ready"
unity command --tag scenes                      # discover commands by tag (scenes, scripts/compile, editor/playmode, observability/console, scripts/eval, gameobjects ...)
unity command list_open_scenes --format json
unity command get_scene_hierarchy --format json
unity command console --level error --tail 50 --format json
unity command clear_console

# After editing any .cs file: recompile, then poll until "completed"
unity command recompile --format json
unity command recompile_status --format json    # repeat until status == completed; check compilationFailed/errors

# Play mode round trip
unity command editor_play
unity command eval 'return "isPlaying=" + UnityEngine.Application.isPlaying + " frame=" + UnityEngine.Time.frameCount;'
unity command editor_stop

# Arbitrary C# in the Editor (UnityEditor APIs available)
unity command eval '<c# statements>; return <value>;'
```

Notes:
- `recompile` returns immediately with `status: compiling`; completion must be polled with `recompile_status`.
- `eval` is reliable. `eval_file` has hung indefinitely on a large script once; prefer several short `eval` calls or read serialized values from the YAML.
- If the Editor will not connect, suspect Safe Mode from a compile error (`unity pipeline list`), not a closed Editor.
- Tests: `com.unity.test-framework` is installed but no tests exist yet. Run with `unity test . --mode EditMode` (or `--mode PlayMode`) once there are some.
- Build: `unity build . --target StandaloneWindows64` (nothing is configured for builds yet; only `SampleScene` is in Build Settings).

## Repository hygiene

- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, `*.csproj`, `*.sln`, `*.slnx` are ignored. Never commit them.
- `.gitattributes` forces `*.cs` to CRLF and scene/prefab/asset/meta files to LF. The Editor currently writes `.cs` with LF, so Git warns about normalisation on every C# change; this is expected until the files are renormalised.
- Every file under `Assets/` has a `.meta` sibling. Create or move assets through the Editor so metas stay consistent; if a file is created from the shell, Unity generates the meta on the next refresh, and that meta must be committed too.

## Architecture (big picture)

**One scene.** `Assets/Scenes/SampleScene.unity` holds everything: managers, arena, both 3-slot teams, HUD, and the dice box. The dice box (`DiceRollingArea`, a prefab instance at x = −652) is never in the main camera's view; a second camera renders it to `Assets/Dice/DiceCamera.renderTexture`, shown full-screen via a `RawImage` overlay.

**Everything talks through `EventManager`** (`Assets/Scripts/EventManager.cs`), a string-keyed pub/sub bus living on `/Managers`. Handlers are wrapped in `EventHandler`, `EventHandler<I>`, `<I,J>`, `<I,J,K>` so typed delegates share one list. Payload types are **not** checked at registration; a mismatch between trigger and listener is a runtime `InvalidCastException`. Event names are string literals spread across scripts, so grep for the name before renaming or adding one. Dispatch happens inside a coroutine on the `EventManager` instance, so `TriggerEvent` cannot be called before its `Awake` or from a destroyed object.

**Singleton managers** all follow the same shape: static `Instance`, duplicate-destroying `Awake`, register listeners in `Awake`, unregister in `OnDestroy`. `EventManager` and `DiceManager` are `DontDestroyOnLoad` in anticipation of multi-scene runs.

**The current gameplay loop is a mana roll:**

```
GameManager.Start ─EnableDiceLaunch(count)→ DiceRollerController (shows overlay) ─SetUpDice→ DiceManager (spawns/pools D6 prefabs)
player drags Panel ─LaunchDice(Vector3)→ DiceManager → DiceHandler.LaunchDice per die (physics)
die sleeps → DiceHandler.GetFaceUp → BaseDice face → DiceManager.ProcessDiceValue (static call, waits for all dice)
→ UpdateResources → PlayerTeamManager (Before hook → overwrite red/blue/yellow → ResourceUIUpdate → ResourceManager HUD → After hook)
→ DisableDiceLaunch (hides overlay). Nothing calls StartTurn/EndTurn, so the game stops here.
```

The **face-up result is decided by PhysX**, not by RNG. `UnityEngine.Random` is used only for cosmetic torque. There is no roll service and nothing is seedable. The design proposal pushes toward a seeded logical roll with physics as presentation; see the codebase review before touching rolling.

**Data model.** `BaseDice` (ScriptableObject, `Assets/Scriptable Objects/Dice/`) is six `{faceIndex, DiceResultType, amount}` faces; the single asset maps faces to Red/Blue/Yellow mana. `DiceHandler` hardcodes which local normal is which face index (opposite faces sum to 7). `BaseSkill` and `BaseCharacters` (in `BaseCharacter.cs`) are abstract with no concrete types or assets and are unused at runtime.

**Design tension to be aware of.** The code implements a *mana economy* (faces produce pooled Red/Blue/Yellow, spent on skills from the 2×2 `UI_SkillMenu`). The proposal in `docs/` replaces that with *every face is an action* and one die per hero. Do not extend the mana pipeline without checking which direction the user wants.

## Known traps in the current code

- `DiceManager.m_isIdle` is never reset after a launch, so a second roll in one Play session is refused.
- `DiceManager.SetUpDice` disables surplus pooled dice but keeps them in `m_spawnedDiceList`; `ProcessDiceValue` waits for a result from every list entry, so any roll with fewer dice than the pool never completes, and `LaunchDice` throws on the inactive ones.
- `DiceManager` sets `Physics.gravity` to −12 and solver iterations to 12 globally at runtime and never restores them.
- `DieHandler.cs` and `LaunchDice.cs` are dead, superseded by `DiceManager` + `DiceRollerController`. `GameUtilities.DiceFaceValue` is an unused duplicate of `DiceResultType`. `GameUtilities.Mana` has a constructor bug (Generic takes the blue value) and wraps a `Dictionary`, which Unity cannot serialize.
- `UI_SkillMenu` is active at scene load, never hidden, and its four buttons have no `onClick` handlers.
- The scene's `InputSystemUIInputModule` uses the Input System package's built-in `DefaultInputActions`, not `Assets/InputSystem_Actions.inputactions`. No gameplay code reads the project actions asset; all input is uGUI pointer events (`IPointerDown/Up/ClickHandler`).
- `Assets/TextMesh Pro/Examples & Extras` and `Assets/TutorialInfo` are template leftovers, not game code.
