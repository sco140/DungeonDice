# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

DungeonDice is a Unity 6 (6000.3.5f2) prototype of a dice-driven party RPG. URP 17 (Forward+), Input System only (legacy Input Manager disabled), uGUI + TextMesh Pro. All C# is in the default `Assembly-CSharp` assembly; shared types live in the `GameUtility` namespace.

Deeper references, read before non-trivial changes:

- `docs/ARCHITECTURE.md` — full map of the legacy scripts, events, scene hierarchy, known bugs, extension points.
- `docs/Prototype Design Proposal.md` — the target design (roguelike run: hero dice → traversal map → combat). Its **"Decisions after codebase review"** section and **"Build order"** are authoritative and override anything that conflicts in the other two docs.
- `docs/Prototype Design Codebase Review.md` — gap analysis with keep/adapt/replace/new edits. Written before the decisions; where it recommends a logical/seeded roll or dropping mana, the proposal's decisions win.

## Working rules

- Branch: work on `ROBOCODY` or `feature/*` branches. Never commit to `master`. Never push (no write access to origin yet).
- Commit (or tell me to) before any multi-file change.
- Definition of done: `recompile_status` shows no errors, the console has no new errors, the Play mode round trip works, and tests pass once they exist.
- New gameplay code goes in `Assets/_Project/Scripts/<System>/`; leave existing scripts in place unless the task is a refactor.
- Plan first for anything touching `EventManager` event names, `DiceManager`, or rolling.
- Ask before: changing `Packages/manifest.json`, deleting files (including the dead code listed under Known traps), or changing global `Physics` settings.
- When you fix a Known Trap, remove it from this file in the same commit.

## Prototype conventions (decided 2026-10-19)

- **One scene** for the whole run: `Assets/_Project/Scenes/Prototype.unity`. Hero Pick, Map, Combat and Run End are screens toggled by a run-state controller, not separate scenes. It is Build Settings index 0; `SampleScene` stays listed but disabled as the legacy reference.
- **Layout:** `Assets/_Project/{Scripts/<System>, Scenes, Data, Prefabs, UI, Tests}`. Existing assets under `Assets/Dice`, `Assets/Scripts`, `Assets/Scriptable Objects` are referenced in place, not moved.
- **Namespaces:** new code in `DungeonDice.<System>` (e.g. `DungeonDice.Dice`, `DungeonDice.Map`, `DungeonDice.Combat`, `DungeonDice.Run`), still in Assembly-CSharp. Legacy `GameUtility` types stay until touched.
- **Tests:** `Assets/_Project/Tests/EditMode` and `Assets/_Project/Tests/PlayMode`, each with its own asmdef referencing `Assembly-CSharp`. Pure logic (roll results, modifiers, fork odds, bracket coverage) gets EditMode tests; anything needing the dice box or scene objects gets PlayMode tests. Run with `unity test . --mode EditMode` / `--mode PlayMode`.
- **Numbers are placeholders.** HP, damage, enemy stats, heal amounts and bracket thresholds live in ScriptableObjects and are first guesses for tuning: heroes ~20–30 HP, regular enemies 10–15, elite 25, boss 40; Rest heals 30% max HP; Combat nodes have 2 regulars, Elite and Boss nodes have 1.
- **Mana pool** resets to 0 at the start of each combat, carries across turns within it, no cap. Banking a die = 1 mana of its hero's color; a signature skill costs 3 of its color.
- **Status semantics (first pass):** Burn N deals N at the start of the target's turn and decays by 1. Poison N deals N at end of turn and does not decay. Weaken = −1 to the target's damage for one turn. Hex = target takes +1 from all sources this turn. Taunt redirects enemy single-target intents to the taunter this turn. Shield absorbs damage and expires at the end of the enemy turn.
- **Treasure:** grants a traversal die (incl. Pilgrim) or a hero face upgrade chosen from 2–3 options listed on that `HeroDefinition`.
- **Hero Pick fidelity:** flow and layout are final; art is TMP text and colored placeholder panels, locked slots greyed with a lock label.
- **Commits:** one per build-order step, smaller commits inside a step when a sub-piece compiles and runs clean. Dead dice scripts (`DieHandler.cs`, `LaunchDice.cs`, `DiceFaceValue`) are approved for deletion in step 1.

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
- Tests: `unity test . --mode EditMode` / `unity test . --mode PlayMode` (see Prototype conventions for where tests live). Exit 8 = tests failed; any other non-zero = the run itself failed.
- Build: `unity build . --target StandaloneWindows64` (nothing is configured for builds yet; only `SampleScene` is in Build Settings).

## Repository hygiene

- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, `*.csproj`, `*.sln`, `*.slnx` are ignored. Never commit them.
- `.gitattributes` normalises `*.cs` and scene/prefab/asset/meta files to LF, matching what the Editor writes. Don't change line-ending rules without asking.
- Every file under `Assets/` has a `.meta` sibling. Create or move assets through the Editor so metas stay consistent; if a file is created from the shell, Unity generates the meta on the next refresh, and that meta must be committed too.

## Architecture (big picture)

**Legacy scene vs. prototype.** `Assets/Scenes/SampleScene.unity` is the pre-prototype scene and holds everything built so far: managers, arena, both 3-slot teams, HUD, and the dice box. The prototype is being built in a **new scene** (see the proposal's Build order); SampleScene stays as the working reference for the dice box and the mana HUD until those are lifted out. The dice box (`DiceRollingArea`, a prefab instance at x = −652) is never in the main camera's view; a second camera renders it to `Assets/Dice/DiceCamera.renderTexture`, shown full-screen via a `RawImage` overlay. The proposal keeps **one dice box for combat and traversal**, with a swappable visual profile per context.

**Everything talks through `EventManager`** (`Assets/Scripts/EventManager.cs`), a string-keyed pub/sub bus living on `/Managers`. Handlers are wrapped in `EventHandler`, `EventHandler<I>`, `<I,J>`, `<I,J,K>` so typed delegates share one list. Payload types are **not** checked at registration; a mismatch between trigger and listener is a runtime `InvalidCastException`. Event names are string literals spread across scripts, so grep for the name before renaming or adding one. **New events get a `const string` in a single `GameEvents` static class; don't add new raw string literals.** Existing literals can migrate there opportunistically when a file is already being touched. Dispatch happens inside a coroutine on the `EventManager` instance, so `TriggerEvent` cannot be called before its `Awake` or from a destroyed object.

**Singleton managers** all follow the same shape: static `Instance`, duplicate-destroying `Awake`, register listeners in `Awake`, unregister in `OnDestroy`. `EventManager` and `DiceManager` are `DontDestroyOnLoad` in anticipation of multi-scene runs.

**The current gameplay loop is a mana roll:**

```
GameManager.Start ─EnableDiceLaunch(count)→ DiceRollerController (shows overlay) ─SetUpDice→ DiceManager (spawns/pools D6 prefabs)
player drags Panel ─LaunchDice(Vector3)→ DiceManager → DiceHandler.LaunchDice per die (physics)
die sleeps → DiceHandler.GetFaceUp → BaseDice face → DiceManager.ProcessDiceValue (static call, waits for all dice)
→ UpdateResources → PlayerTeamManager (Before hook → overwrite red/blue/yellow → ResourceUIUpdate → ResourceManager HUD → After hook)
→ DisableDiceLaunch (hides overlay). Nothing calls StartTurn/EndTurn, so the game stops here.
```

The **face-up result is decided by PhysX**, not by RNG, and the proposal keeps it that way: the player's drag-throw stays. Once dice settle, the face read becomes a **logical roll result**; modifiers (Nudge, Flip, Mirror, Loaded Dice) change that result and the die re-orients to show the new face. Lock means the die is not re-thrown. Randomness **outside** the throw (enemy intents, rewards, map) must go through one seeded service so runs are reproducible apart from the dice. `UnityEngine.Random` in `DiceManager` is cosmetic torque only and may stay unseeded.

**Data model.** `BaseDice` (ScriptableObject, `Assets/Scriptable Objects/Dice/`) is six `{faceIndex, DiceResultType, amount}` faces; the single asset maps faces to Red/Blue/Yellow mana. `DiceHandler` hardcodes which local normal is which face index (opposite faces sum to 7). `BaseSkill` and `BaseCharacters` (in `BaseCharacter.cs`) are abstract with no concrete types or assets and are unused at runtime.

**Decided direction (from the proposal's "Decisions after codebase review").** The legacy mana economy is kept, but reshaped into **hybrid mana, "use it or bank it"**: every hero-die face is an action, and any die can instead be banked for 1 mana in its hero's color. Each hero has **one signature skill** costing 3 mana of its color, so `BaseSkill` survives as the signature-skill type and the Red/Blue/Yellow team pool and HUD stay. Colors are domains: **Red** = damage and block, **Blue** = statuses (Burn, Poison, Weaken, Hex), **Yellow** = dice modification (hero and traversal). One die per hero in the prototype, but `HeroDefinition` keeps a *list* of dice for future rewards. **Per-hero HP**; a dead hero's die leaves the roll and its signature is unavailable. Hero and traversal dice share one die/face model so modifiers can target either.

## Known traps in the current code

- `DiceManager.m_isIdle` is never reset after a launch, so a second roll in one Play session is refused.
- `DiceManager.SetUpDice` disables surplus pooled dice but keeps them in `m_spawnedDiceList`; `ProcessDiceValue` waits for a result from every list entry, so any roll with fewer dice than the pool never completes, and `LaunchDice` throws on the inactive ones.
- `DiceManager` sets `Physics.gravity` to −12 and solver iterations to 12 globally at runtime and never restores them.
- `DieHandler.cs` and `LaunchDice.cs` are dead, superseded by `DiceManager` + `DiceRollerController`. `GameUtilities.DiceFaceValue` is an unused duplicate of `DiceResultType`. `GameUtilities.Mana` has a constructor bug (Generic takes the blue value) and wraps a `Dictionary`, which Unity cannot serialize.
- `UI_SkillMenu` is active at scene load, never hidden, and its four buttons have no `onClick` handlers.
- The scene's `InputSystemUIInputModule` uses the Input System package's built-in `DefaultInputActions`, not `Assets/InputSystem_Actions.inputactions`. No gameplay code reads the project actions asset; all input is uGUI pointer events (`IPointerDown/Up/ClickHandler`).
- `Assets/TextMesh Pro/Examples & Extras` and `Assets/TutorialInfo` are template leftovers, not game code.