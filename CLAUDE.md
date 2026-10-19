# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

DungeonDice is a Unity 6 (6000.3.5f2) prototype of a dice-driven party RPG. URP 17 (Forward+), Input System only (legacy Input Manager disabled), uGUI + TextMesh Pro. All game code lives in the `DungeonDice` assembly (`Assets/_Project/Scripts/DungeonDice.asmdef`), with `DungeonDice.Editor` for editor tooling and two test assemblies. New code uses `DungeonDice.<System>` namespaces; the older scripts still use `GameUtility` or no namespace until they are touched. Only template leftovers remain in `Assembly-CSharp`.

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
- Comment the code for humans: a short summary on every type and non-obvious method, and a line wherever the *why* is not visible from the code (a Unity quirk, a design decision, an ordering constraint). Descriptive but concise. Other people will build on this project, not only Claude.
- Ask before: changing `Packages/manifest.json`, deleting files (including the dead code listed under Known traps), or changing global `Physics` settings.
- When you fix a Known Trap, remove it from this file in the same commit.

## Prototype conventions (decided 2026-10-19)

- **One scene** for the whole run: `Assets/_Project/Scenes/Prototype.unity`. Hero Pick, Map, Combat and Run End are screens toggled by a run-state controller, not separate scenes. It is Build Settings index 0; `SampleScene` stays listed but disabled as the legacy reference.
- **Layout:** `Assets/_Project/{Scripts/<System>, Scenes, Data, Prefabs, UI, Tests}`. Existing assets under `Assets/Dice`, `Assets/Scripts`, `Assets/Scriptable Objects` are referenced in place, not moved.
- **Assemblies and namespaces:** all game code is in the `DungeonDice` asmdef (test asmdefs cannot reference `Assembly-CSharp`, which is why the legacy scripts were moved in). New code uses `DungeonDice.<System>` (`Core`, `Dice`, `Combat`, `Map`, `Run`); legacy `GameUtility` and global-namespace types move into a namespace when touched. Editor-only code goes in `Assets/_Project/Scripts/Editor` (`DungeonDice.Editor` asmdef). Internals are visible to both test assemblies via `AssemblyInfo.cs`.
- **Tests:** `Assets/_Project/Tests/EditMode` and `Assets/_Project/Tests/PlayMode`, asmdefs referencing `DungeonDice`. Pure logic (roll results, modifiers, fork odds, bracket coverage) gets EditMode tests; anything needing components, physics or scene objects gets PlayMode tests. Run them through the Editor commands listed above; PlayMode runs must be async.
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
- Tests, with the Editor open (the normal case):
  ```bash
  unity command list_tests --format json
  unity command run_tests --mode editor --format json                   # EditMode: synchronous, returns Summary + Results
  unity command run_tests --mode playmode --async_tests true --format json  # PlayMode MUST be async (play mode reloads the domain)
  unity command test_status --format json                               # poll until status == completed
  ```
  `unity test . --mode EditMode|PlayMode` is the headless alternative for CI (exit 8 = tests failed; other non-zero = the run itself failed).
- Authoring tools (menu **DungeonDice ▸ …**, or call the static method via `eval`), all re-runnable and GUID-preserving. Extend these rather than hand-editing `.asset`/`.prefab` files:
  - `DiceBoxProfileAuthoring.CreateAll()` creates the tray materials and the `Combat` / `Traversal` `DiceBoxProfile`s under `Assets/_Project/Data/DiceBox`.
  - `DiceAssetAuthoring.CreateAll()` rewrites the hero/traversal `DieDefinition` assets from the proposal tables, including each die's skin (`Die_Yellow.mat` is a tinted copy of the pack's white die).
  - `DiceBoxPrefabAuthoring.CreateOrUpdate()` rebuilds `Assets/_Project/Prefabs/DiceBox.prefab` (needs the Combat profile to exist first) and pins `EventManager`'s Script Execution Order to −100.
  - `HeroAssetAuthoring.CreateAll()` writes the six `HeroDefinition` assets under `Assets/_Project/Data/Heroes` (needs the hero dice).
  - `ActAssetAuthoring.CreateAll()` writes `PrototypeAct.BuildNodes()` (the act defined in code, unit-tested) to `Assets/_Project/Data/Map/Act1.asset` and runs `ForkValidator` on it. Edit the act in `PrototypeAct.cs`, not in the asset.
  - `PrototypeSceneAuthoring.CreateOrUpdate()` rebuilds `Assets/_Project/Scenes/Prototype.unity` from scratch and sets Build Settings (Prototype enabled at index 0, SampleScene disabled). It **replaces the open scene**, so it refuses to run while the open scene is dirty. Scene changes belong in this tool, not in hand edits to the scene.
  Run them in that order after a clean checkout. `AuthoringUtil` holds the shared folder/material helpers. After an authoring call that creates assets, re-load them by path before reading fields; objects held from before the refresh can read as blank. **Do not enter Play mode or run PlayMode tests in the same breath as creating assets**: a scene reference to an asset whose import has not finished resolves to null in Play mode. Refresh with `ImportAssetOptions.ForceSynchronousImport`, wait for `recompile_status`, then test.
- `eval` gotcha: the C# is passed through the shell in single quotes, so a `' '` char literal (or any apostrophe) splits the argument and the CLI reports bogus `--timeout` errors. Use `string.Empty.PadLeft(n)` or `" "` instead.
- Build: `unity build . --target StandaloneWindows64` (nothing is configured for builds yet; only `SampleScene` is in Build Settings).

## Repository hygiene

- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, `*.csproj`, `*.sln`, `*.slnx` are ignored. Never commit them.
- `.gitattributes` normalises `*.cs` and scene/prefab/asset/meta files to LF, matching what the Editor writes. Don't change line-ending rules without asking.
- **One ScriptableObject or MonoBehaviour class per file, file named after the class.** Unity binds assets and scene components to scripts by file name; a class declared in another file saves fine (`CreateInstance` works) but its asset loads from disk with `m_Script: {fileID: 0}`, i.e. null. Plain `[Serializable]` classes and enums can share a file.
- Every file under `Assets/` has a `.meta` sibling. Create or move assets through the Editor so metas stay consistent; if a file is created from the shell, Unity generates the meta on the next refresh, and that meta must be committed too.

## Architecture (big picture)

**Legacy scene vs. prototype.** `Assets/Scenes/SampleScene.unity` is the pre-prototype scene and holds everything built so far: managers, arena, both 3-slot teams, HUD, and the dice box. The prototype is being built in a **new scene** (see the proposal's Build order); SampleScene stays as the working reference for the dice box and the mana HUD until those are lifted out. The dice box (`DiceRollingArea`, a prefab instance at x = −652) is never in the main camera's view; a second camera renders it to `Assets/Dice/DiceCamera.renderTexture`, shown full-screen via a `RawImage` overlay. The proposal keeps **one dice box for combat and traversal**, with a swappable visual profile per context.

**Everything talks through `EventManager`** (`Assets/Scripts/EventManager.cs`), a string-keyed pub/sub bus living on `/Managers`. Handlers are wrapped in `EventHandler`, `EventHandler<I>`, `<I,J>`, `<I,J,K>` so typed delegates share one list. Payload types are **not** checked at registration; a mismatch between trigger and listener is a runtime `InvalidCastException`. Event names are string literals spread across scripts, so grep for the name before renaming or adding one. **New events get a `const string` in a single `GameEvents` static class; don't add new raw string literals.** Existing literals can migrate there opportunistically when a file is already being touched. Dispatch happens inside a coroutine on the `EventManager` instance, so `TriggerEvent` cannot be called before its `Awake` or from a destroyed object.

**Singleton managers** all follow the same shape: static `Instance`, duplicate-destroying `Awake`, register listeners in `Awake`, unregister in `OnDestroy`. `EventManager` and `DiceManager` are `DontDestroyOnLoad` in anticipation of multi-scene runs.

**The dice box loop** (`Assets/_Project/Scripts/Dice/`, all event names in `GameEvents`):

```
GameManager.Start ─EnableDiceLaunch(count)→ DiceRollerController (shows overlay) ─SetUpDice→ DiceManager (pools D6 prefabs, builds the active list)
player drags Panel ─LaunchDice(Vector3)→ DiceManager (overrides gravity) → DiceHandler.LaunchDice per active die (physics)
die sleeps → DiceHandler.GetFaceUp → DiceManager.ProcessDiceValue(die, faceIndex) → RollResult per die
all active dice settled → DiceManager.CompleteRoll: restore gravity → RollCompleted(RollCompletedEvent) → DisableDiceLaunch
```

`DiceManager` is a small state machine, `DiceBoxState { Empty, Idle, Rolling, Settled }`, and knows nothing about mana or combat:

- `SetUpDice(IReadOnlyList<DieDefinition>)` injects one definition per die (owner id = index); `SetUpDice(int)` keeps whatever the prefab carries. Either resets to Idle and clears `LastRoll`.
- `LaunchDice(Vector3)` is only valid in Idle. `RelaunchDice(ownerIds, Vector3)` (event `RelaunchDice` with a `RelaunchRequest`) is only valid in Settled, skips Locked results, leaves the other dice where they lie, and fires `RollCompleted` again with the full set.
- `ApplyModifier(ownerId, IDieModifier)` in Settled applies the modifier to that `RollResult`, calls `DiceHandler.ShowFace` so the physical die matches, and fires `RollResultModified`.
- Gravity is overridden only while Rolling and restored on completion. Solver iterations are set per die rigidbody, never globally.
- The box never hides the overlay. Consumers do (combat needs it open for reroll and use-or-bank).

Consumers subscribe to `RollCompleted`. In the legacy SampleScene the consumer is `GameManager`, which previews the "bank it" rule by granting 1 mana of each die's color to `PlayerTeamManager` (Before hook → overwrite pool → `ResourceUIUpdate` → HUD → After hook) and then fires `DisableDiceLaunch`. Nothing calls `StartTurn`/`EndTurn` yet.

**`DiceBox.prefab`** (`Assets/_Project/Prefabs/`) is the self-contained box for the prototype scene: `DiceManager` on the root, the legacy `DiceRollingArea` nested as `Arena` (walls, camera rendering to `DiceCamera.renderTexture`, `DiceHolder` spawn point) and an `Overlay` canvas (sort order 10) whose `Panel` carries `DiceRollerController` and the `DiceRollCamera` RawImage. Place it far from the main camera (the integration test uses y = −500). It needs an `EventManager` alive before it is instantiated; `EventManager` runs at execution order −100 so scene order does not matter. `DiceRollerController` throws on drag while Idle and re-throws its `RerollSelection` while Settled.

**Run scaffolding** (`DungeonDice.Run`, `DungeonDice.UI`). The Prototype scene has one `Run` object with `RunController` + `ScreenRouter`, a `UI` canvas with one inactive root per `RunPhase` (`HeroPick`, `TraversalPick`, `Map`, `Combat`, `RunEnd`), `EventManager`, `EventSystem`, camera, light and a `DiceBox` instance at y = −500. `RunController` owns a plain-C# `RunState` (party of three `HeroRunState`s with per-hero HP and a runtime copy of their die, traversal dice, node index, seed and a seeded `RollService`) and drives phases with `SetPhase`, which the router turns into "activate that root" and which fires `RunPhaseChanged`. `StartRun(party)` validates three playable heroes (duplicates allowed) and resets `RollService.Shared` to the run seed; `AbandonRun` clears back to Hero Pick. `HeroDefinition` (replaces the old `BaseCharacters`) is the hero as data: colour, die list (one used), max HP, playable flag, signature text. Screens build their own uGUI in code through `UiFactory` (placeholder panels + TMP text): `HeroPickScreen` renders a pure `HeroPickSelection` (six cards, three locked, duplicates allowed, Start Run when full); the other four phases are `PlaceholderScreen`s with Continue/Abandon so the flow can be clicked end to end. Nothing listens to `RunPhaseChanged` yet, so `EventManager` warns about it once per phase change; expected. `RunFlowTests` loads the scene additively and must `DestroyImmediate` the `DontDestroyOnLoad` singletons (`EventManager`, `DiceManager`) in teardown or later tests' singletons self-destruct.

**Map** (`DungeonDice.Map`). `ActDefinition` is a list of `MapNode`s (`NodeType` Start/Combat/Elite/Rest/Treasure/Boss, description, and exactly one exit: a `Fork` or a `nextNode`; Boss has neither). A `Fork` is `minDice..maxDice` traversal dice plus `PathBracket`s (inclusive total ranges → target node) that must cover `minDice×1 .. maxDice×6` with no gaps or overlaps; `ForkValidator` enforces that, `ForkResolver` picks the bracket for a total, `ForkOdds` convolves the committed dice's pip faces for the nominal percentages the UI shows. The only act is `PrototypeAct` (11 nodes, 3 forks, 1 Boss; bracket thresholds are tuning placeholders). `RunState` tracks `Act`, `CurrentNodeIndex`, `CurrentNodeResolved`, `VisitedNodes` and `Outcome`. The map flow lives in `RunController`: `ConfirmTraversalDice` enters the act; `EnterCurrentNode` sends Combat/Elite/Boss to the Combat phase and resolves Rest (heal 30%) and Treasure (placeholder: grants the Pilgrim die) in place; `CompleteNode` is the combat screen's "won" callback (Boss → RunEnd Victory, otherwise back to Map); `TravelTo` moves along a resolved node's exit; `PartyWiped` → RunEnd Defeat. `MapScreen` renders the node list and the current node's actions; for a fork it toggles owned dice, shows odds, and `StartForkRoll` swaps the box to the Traversal profile, `SetUpDice(committed)` and fires `OpenDiceBox` (overlay only; `EnableDiceLaunch(int)` is the legacy path that also spawns dice). On `RollCompleted` it resolves the bracket, hides the overlay and offers Continue. `TraversalPickScreen` renders `TraversalPickSelection` (3 distinct of the 4-die starting pool).

**Combat** (`DungeonDice.Combat`). `CombatState` is the whole rule set as plain C#, driven by a seeded `RollService`: phases `Throw → Reroll → Act → EnemyTurn` (then `Throw` again, or `Won`/`Lost`). It holds `CombatHero`s (run HP plus combat-only Shield and `StatusSet`), `CombatEnemy`s (HP, Shield, statuses, telegraphed `EnemyIntent`), the colour mana pool (`Red/Blue/Yellow`, reset per fight) and this turn's `CombatDie`s. Dice come in via `ReceiveRoll(slot, RollResult)`: in Throw it opens the reroll decision, in Reroll it closes it (`RerollUsed`), in Act it completes a Reroll-ally re-throw (`PendingAllyReroll`). `UseDie(slot, target, nudgeDelta)` resolves the face's `FaceEffectEntry`s (target is an enemy index, hero slot or die slot per `TargetFor`); Mirror leaves the die in hand as a copy of another die's face; Reroll-ally spends the die and fires `AllyRerollRequested`. `BankDie` gives 1 mana of the hero's colour. `CastSignature` costs 3 of the colour, once per hero per turn: Bulwark (Shield 6 all), Detonate (consume a target's stacks as damage), Loaded Dice (`ChooseResult` on an unspent hero die). `EndHeroTurn` runs the enemy turn: Burn ticks then decays, each enemy drops its Block and executes its intent (Attack → taunter else seeded random hero, minus 1 if Weakened; AttackAll; Block), Poison ticks without decay, Weaken/Hex/Taunt and hero shields expire, win/loss is checked, new intents are drawn. Hex adds +1 to damage the enemy takes. `DieModified(slot, modifier)` tells the renderer to turn the physical die (the `RollResult` is shared, so never re-apply the modifier through `DiceManager`). `EnemyDefinition` is health plus an intent list; `RunController.EnemiesFor(NodeType)` spawns two seeded regulars, the elite or the boss. `CombatScreen` maps box owner ids to party slots (`OwnerToSlot`), opens the box for throws and re-throws (real drags; the overlay closes once dice settle), renders cards with Use/Bank/signature buttons and a targeting mode, and on Won/Lost calls `RunController.CompleteNode` / `PartyWiped`. Legacy `PlayerTeamManager`/`ResourceManager`/`BaseSkill` are SampleScene-only and unused by the prototype.

**Visual profiles.** `DiceBoxVisuals` (on the prefab root, beside `DiceManager`) applies a `DiceBoxProfile`: floor and wall materials, camera local position/rotation/FOV, the `BoxLight` point light, the overlay dim colour, optional throw/settle clips, and the fallback die material. Swap with `ApplyProfile` or the `SetDiceBoxProfile` event; the Combat profile is applied on Awake. Die skins resolve in order: `DieDefinition.visualMaterial`, then the profile's `defaultDieMaterial`, then the prefab's original material (`DiceHandler.ApplyVisual`). `DiceThrown` fires on every throw for presentation only. The legacy SampleScene has no `DiceBoxVisuals`, so `EventManager` logs "Event 'DiceThrown' was never registered" there; that warning is expected.

The **face-up result is decided by PhysX**, not by RNG, and the proposal keeps it that way: the player's drag-throw stays. Once dice settle, the face read becomes a **logical roll result**; modifiers (Nudge, Flip, Mirror, Loaded Dice) change that result and the die re-orients to show the new face. Lock means the die is not re-thrown. Randomness **outside** the throw (enemy intents, rewards, map) must go through one seeded service so runs are reproducible apart from the dice. `UnityEngine.Random` in `DiceManager` is cosmetic torque only and may stay unseeded.

**Die data model** (`DungeonDice.Dice`). `DieDefinition` (ScriptableObject) is the shared model for hero and traversal dice: `displayName`, `DieColor` (None for traversal), six `DieFace`s, a `visualMaterial` slot. A `DieFace` is a label plus `FaceEffectEntry[]` of `{FaceEffectKind, value}`; compound faces have several entries and traversal faces have one `Number` entry (`Pips`). Assets live in `Assets/_Project/Data/Dice/{Heroes,Traversal}` and are authored by `DiceAssetAuthoring`. `RollResult` is the logical outcome of one settled die (`RolledFaceIndex` physical, `FaceIndex` after `IDieModifier`s: Nudge, Flip, ChooseResult, Lock); `RollCompletedEvent` carries one per die. `DieDefinition.WithFaceReplaced` returns an upgraded runtime copy; assets are never mutated at runtime. `DiceHandler` owns the physical face table (+Z=1, +Y=2, −X=3, +X=4, −Y=5, −Z=6) and `ShowFace(i)` re-orients a die to match a modified result. `RollService` (`DungeonDice.Core`) is the seeded RNG for everything except the throw. `BaseSkill` and `BaseCharacters` (in `BaseCharacter.cs`) are still abstract with no concrete types or assets.

**Decided direction (from the proposal's "Decisions after codebase review").** The legacy mana economy is kept, but reshaped into **hybrid mana, "use it or bank it"**: every hero-die face is an action, and any die can instead be banked for 1 mana in its hero's color. Each hero has **one signature skill** costing 3 mana of its color, so `BaseSkill` survives as the signature-skill type and the Red/Blue/Yellow team pool and HUD stay. Colors are domains: **Red** = damage and block, **Blue** = statuses (Burn, Poison, Weaken, Hex), **Yellow** = dice modification (hero and traversal). One die per hero in the prototype, but `HeroDefinition` keeps a *list* of dice for future rewards. **Per-hero HP**; a dead hero's die leaves the roll and its signature is unavailable. Hero and traversal dice share one die/face model so modifiers can target either.

## Known traps in the current code

- `GameUtilities.Mana` wraps a `Dictionary`, which Unity cannot serialize, so `BaseSkill.m_manaCost` neither shows in the Inspector nor persists. Replace it when `BaseSkill` is trimmed to signature skills.
- `PlayerTeamManager.UpdateResources` overwrites the mana pool with the incoming values rather than adding to them.
- `PlayerTeamManager` and `ResourceManager` still register events with raw string literals; migrate to `GameEvents` when touched.
- `UI_SkillMenu` is active at scene load, never hidden, and its four buttons have no `onClick` handlers.
- The scene's `InputSystemUIInputModule` uses the Input System package's built-in `DefaultInputActions`, not `Assets/InputSystem_Actions.inputactions`. No gameplay code reads the project actions asset; all input is uGUI pointer events (`IPointerDown/Up/ClickHandler`).
- `Assets/TextMesh Pro/Examples & Extras` and `Assets/TutorialInfo` are template leftovers, not game code.