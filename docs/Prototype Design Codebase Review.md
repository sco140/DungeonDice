# Prototype Design vs. Codebase — Review

_2026-10-19. Compares `docs/Prototype Design Proposal.md` against the project as described in `docs/ARCHITECTURE.md`. No code was changed._

**Short version.** The codebase is a solid *dice-rolling* foundation and a weak *game* foundation. The physical dice, the 6-face ScriptableObject, the event bus, the 3-slot team layout and the run-persistent manager pattern all carry straight into the proposal. Almost everything the proposal calls a "screen", "node", "enemy" or "run" does not exist yet. And the code is currently committed to one design decision the proposal quietly reverses: dice faces produce **mana that is spent on skills**, whereas the proposal says **every face is an action**. That divergence, plus the question of whether physics or a seeded RNG decides a roll, are the two things to settle before building.

---

## 1. What works well with the proposal (keep as-is or nearly)

| Existing piece | Files | Why it fits |
|---|---|---|
| **6-face die as data** | `Assets/Scriptable Objects/Dice/BaseDice.cs`, `BaseDice.asset` | Already a ScriptableObject holding `ResourceFace[6]` of `{faceIndex, type, amount}`. That is the shape of both a traversal die (`type = Number, amount = pips`) and a simple hero face (`Strike 3`, `Shield 4`, `Heal 3`). The `Attack`/`Armor` members already in `DiceResultType` show the code was heading toward combat faces. |
| **Opposite faces sum to 7** | `Assets/Scripts/DiceHandler.cs` (face normal table), `BaseDice.m_diceFaceIndex` 1–6 | Flip is `7 - faceIndex` with no extra data. |
| **Physical roll of N dice** | `DiceManager.SetUpDice(int)`, `DiceHandler.LaunchDice`, `Assets/Dice/D6.prefab`, `DiceRollingArea.prefab`, `DiceCamera.renderTexture` | "Commit 1–3 dice, roll, read the total" is exactly what the dice box does today for 6 dice. Variable count is already a parameter, and dice are pooled and re-enabled between rolls. The render-texture overlay is a ready-made roll screen for both traversal and combat. |
| **Per-die launch** | `DiceHandler.LaunchDice` is public and per-instance | The proposal's "one reroll per turn, player picks which dice" needs a subset re-launch. The per-die entry point already exists; only the aggregation above it assumes all-or-nothing (see §3). |
| **Event bus** | `Assets/Scripts/EventManager.cs` | New systems (Map, Run, Combat, Intents) can be added as listeners without touching dice code. The `BeforeResourceUpdate` / `AfterResourceUpdate` hook pair is the right shape for a modifier pipeline (Bless +1, Rage +2 next turn, Nudge). |
| **Event-keyed passives** | `BaseCharacters.m_dPassiveEffects : Dictionary<string, List<BaseSkill>>` | Unused, but it is precisely the mechanism for the proposal's "Trickster faces also act on traversal rolls" and the later "every class gets one map face": a passive keyed to a `TraversalRoll` event. |
| **Skill resolve contract** | `Assets/Scriptable Objects/Skills/BaseSkill.cs` (`CanTarget(caster, target)`, `Resolve(caster, targets)`) | This is the right signature for a *face effect*. Drop the mana cost and cooldown and it becomes `FaceEffect`. |
| **3-slot party and 3-slot enemy line** | `PlayerTeamManager.m_PlayerUnitController[3]`, scene `PlayArea/PlayerTeam/{Front,Center,Back}`, `PlayArea/EnemyTeam/{Front,Center,Back}` | Matches "3 Hero Dice in combat". The enemy side is already laid out as a mirror, ready for intents and HP. |
| **Click-to-target units** | `Assets/Scripts/PlayerUnitController.cs` (`IPointerClickHandler`), `PhysicsRaycaster` on Main Camera | Target selection for faces that need it (Strike on an enemy, Bless on an ally) can reuse this on both teams. |
| **World-space UI over units** | `PlayArea/PlayerTeam/UI_SkillMenu`, `Assets/UI/WorldUI.mat`, `Assets/UI/RenderForward.shadergraph` | The always-on-top shader is what enemy intent badges and HP bars over units need. |
| **Run-persistent managers** | `DontDestroyOnLoad` in `EventManager.Awake`, `DiceManager.Awake` | The pattern for a `RunState` that survives Hero Pick → Map → Combat scene changes is already in use. |
| **Per-hero health on the data model** | `BaseCharacters.m_nHealth` | Answers the proposal's open question from the code's side: the data model assumed **per-hero HP**, not a shared pool. |
| **Input / rendering stack** | Input System only, URP Forward+, uGUI + TMP | Nothing in the proposal needs anything else. Mouse and touch already work through the UI module. |

---

## 2. Where the code makes a deliberately different design choice

These are not bugs or gaps. They are places where the existing code encodes a design the proposal does not share. Each needs an explicit decision.

### 2.1 Mana economy vs. "every face is an action" (the big one)

The code implements: roll the team's dice → faces yield **Red / Blue / Yellow mana** → mana is pooled at team level (`PlayerTeamManager.m_red/m_blue/m_yellow`) → shown in the HUD (`ResourceManager`) → spent on **skills** chosen from a 2×2 menu (`UI_SkillMenu`, `BaseCharacters.m_lstSkills[4]`, `BaseSkill.m_manaCost`, `m_cooldown`). Dice are a currency generator; skills are the actions.

The proposal implements: roll 3 hero dice → **each face is itself the action** (Strike 3, Shield 4, Charge, Nudge) → resolve faces directly. No pool, no skill list, no costs, no cooldowns. Arcanist's Charge is a per-hero stack, not a shared resource.

Everything in the mana column is affected: `GameUtilities.Mana` / `ManaColor`, `DiceResultType.Red/Blue/Yellow`, `Event_ResourceUpdate`, `PlayerTeamManager.UpdateResources`, `ResourceManager`, the HUD header, `BaseSkill.m_manaCost`, the skill menu. The *shape* of the pipeline (collect results → pre-hooks → apply → UI → post-hooks) is worth keeping as the face-resolution pipeline; the *content* is not in the proposal.

**Decision needed:** Is mana gone, or does the proposal just not mention it? If gone, §4 lists the replacements. If mana stays as a secondary layer (e.g. some faces generate charge), say so in the design doc, because the code will otherwise pull every new feature toward the pool.

### 2.2 Team dice pool vs. one die per hero

The code rolls `PlayerTeamManager.m_diceCount` generic dice (6 in the scene), all sharing one `BaseDice` asset, and `BaseCharacters.m_lstDiceValues` is a **list**, so a character could own several dice. The proposal is strict: **a hero is a die**, 3 heroes → 3 dice, each with its own faces. The code's model is a superset, which is fine, but `DiceManager.SetUpDice` currently assigns one prefab/one face table to every die and will need per-die `BaseDice` injection.

### 2.3 Skills as a separate menu with cooldowns

`UI_SkillMenu` (four buttons that open on unit click) and `BaseSkill.m_cooldown` have no counterpart in the proposal. The closest proposal need is a small per-die action UI: "reroll this die", "pick target for this face", "use Trickster's Nudge on that die". The menu can be repurposed for that, but its current meaning (choose a skill to cast) should be dropped rather than extended.

### 2.4 Physics decides the result, and the throw is player-controlled

Today the face-up side is whatever PhysX says after a drag-launched throw whose force and direction the player controls (`DiceRollerController` → `DiceManager.LaunchDice`, clamped 10–100). That gives the player a *physical* influence channel and makes results non-reproducible.

The proposal leans the other way: it wants exact odds shown on hover, playtesters reasoning about probability curves, seeded runs for testing, and a pile of result manipulations (choose result, lock, reroll ally, mirror, flip, nudge). All of those are operations on **logical** results. With physics as truth, "Mirror (copy another die)" means physically forcing a die onto a face, "Lock" means not re-throwing one die while the others fly past it, and the displayed odds are approximations of a slightly unfair physical cube.

**Decision needed (recommendation: logical roll, physical presentation).** Introduce a `RollService` that draws the face from a seeded `System.Random`; the dice box still throws the dice for feel, then the die is settled/rotated to show the drawn face. This keeps the entire presentation layer, makes every modifier trivial, makes odds exact, and makes runs seedable. The alternative, physics as truth, keeps the current code untouched but forfeits seeding and makes Mirror/Lock/Choose-result awkward. Either way the choice should be recorded, because it determines where `RollService` sits.

### 2.5 Single persistent scene

Not really deliberate, just early. The proposal needs at minimum Hero Pick, Map, Combat and Run End screens. Nothing in the code opposes this, and the `DontDestroyOnLoad` managers suggest multi-scene was anticipated.

---

## 3. What would need to be built, in run-flow order

Severity key: **missing** = nothing exists; **partial** = pieces exist but need rework.

| # | Run step | Status | What exists | What is needed |
|---|---|---|---|---|
| 1 | Pick Hero Dice (3 of 6, 3 locked) | **missing** | `BaseCharacters` (abstract, unused) with health/attack/dice/skills | `HeroDefinition` SO per class with `faces`, `playableInPrototype`, portrait; a Hero Pick scene; selection UI with locked slots; write selection into `RunState`. |
| 2 | Run state across screens | **missing** | `DontDestroyOnLoad` pattern | `RunState` singleton: chosen heroes, traversal dice owned, current node, seed, HP. |
| 3 | Pick Traversal Dice (3 of 4) | **missing** | `BaseDice` can represent a numeric die | `TraversalDie` assets (Low Road, Steady, High Road, Wild, Pilgrim); pick UI (can share widget with step 1). |
| 4 | Map and forks | **missing** | — | `MapNode` / `Fork` / `PathBracket` data; map generator or hand-authored act; map UI; bracket validator (no gaps/overlaps over min..max sum); odds-on-hover via face-sum convolution. |
| 5 | Fork roll | **partial** | `SetUpDice(count)` + `LaunchDice` + `ProcessDiceValue` already "roll N and sum" | Generalise `ProcessDiceValue` to emit `RollCompleted(results)` instead of summing mana; fix the pooling bug below; pick dice per fork (1–3) from owned traversal dice. |
| 6 | Node: Combat | **partial** | Dice roll, 3v3 layout, click targeting, `GameManager.StartTurn/EndTurn` stubs | Turn state machine; per-hero HP/shield; face → action resolution; one reroll of chosen dice; enemy definitions with telegraphed intents; enemy turn; death/wipe. |
| 7 | Node: Elite | **missing** | — | Same as Combat with an elite enemy set. |
| 8 | Node: Rest | **missing** | — | Heal rule; possibly traversal-die replenish (open question). |
| 9 | Node: Treasure | **missing** | `BaseDice` faces are data | Grant a traversal die to `RunState`; hero face upgrade = **face replace** modifier applied to a hero's `BaseDice` copy. |
| 10 | Boss, win/lose, run end | **missing** | — | Boss enemy set; victory/defeat screens; return to Hero Pick. |
| 11 | Die modifiers (data model only) | **missing** | Hooks: `BeforeResourceUpdate`/`AfterResourceUpdate`; flip = `7 - index` | `RollResult` object per die (`die, faceIndex, locked, modifiers[]`); modifier interface; no gameplay yet. |

**Blocking bug for steps 5 and 6.** `DiceManager.SetUpDice` disables surplus pooled dice but leaves them in `m_spawnedDiceList`, and `ProcessDiceValue` waits until the result count equals `m_spawnedDiceList.Count`. After any roll with fewer dice than the pool, the roll can never complete. `LaunchDice` also calls `StartCoroutine` on the inactive dice, which throws. "Roll 1–3 dice" at forks and "a dead hero's die leaves the roll" both hit this. Track active dice separately (or count only `activeInHierarchy`).

**Also required before combat:** `DiceManager.m_isIdle` is never reset after a launch, so a second roll in the same session is refused.

---

## 4. Suggested edits, tagged

Tags: **keep** = use as-is · **adapt** = modify in place · **replace** = remove and build the new thing in its place · **new** = does not exist.

### Die data model (proposal check 1)

| Tag | Edit | Files |
|---|---|---|
| **adapt** | Rename `BaseDice` → `DieDefinition`; make a face `{ faceIndex, FaceEffect effect, int value }` where `FaceEffect` is a ScriptableObject (see next row). Traversal dice use a single `NumberFace` effect with `value = pips`. Hero dice use `Strike`, `Shield`, `Charge`, `Nudge`, etc. Compound faces (Taunt + Shield 2, Frenzy 6 take 2) become a face with a **list** of effects. | `Assets/Scriptable Objects/Dice/BaseDice.cs`, `BaseDice.asset` |
| **adapt** | Turn `BaseSkill` into `FaceEffect`: keep `CanTarget` / `Resolve(caster, targets)`, add `Resolve` overloads that receive the `RollResult` set so Trickster faces (Nudge, Flip, Reroll ally, Mirror) can act on other dice. Remove `m_manaCost`, `m_cooldown`. | `Assets/Scriptable Objects/Skills/BaseSkill.cs` |
| **new** | `RollResult` class: `DieDefinition die, int faceIndex, bool locked, List<Modifier> applied`. Lives between `DiceHandler` and whatever consumes the roll. Flip = `7 - faceIndex`; Nudge = `faceIndex ± 1` clamped (or `value ± 1` for traversal). | new `Assets/Scripts/Dice/RollResult.cs` |
| **new** | `IDieModifier` interface + no-op data-only implementations (`ChooseResult`, `Nudge`, `Lock`, `FaceReplace`) so the model has room without gameplay. | new `Assets/Scripts/Dice/Modifiers/` |
| **replace** | Delete `DiceFaceValue` enum (unused duplicate) and the `Mana` / `ManaColor` types (mana is not in the proposal; the `Mana` struct also has a constructor bug and cannot serialize). | `Assets/Scripts/GameUtilities.cs` |
| **replace** | `DiceResultType` → either delete in favour of `FaceEffect` subclasses, or shrink to `{ Number, Action }`. | `Assets/Scriptable Objects/Dice/BaseDice.cs` |
| **keep** | `DiceHandler` face-normal table as the **physical** layer that maps a resting orientation to 1–6 (or, under a logical roll, rotates the die to show a given index). | `Assets/Scripts/DiceHandler.cs` |

### Selection flow (check 2)

| Tag | Edit | Files |
|---|---|---|
| **adapt** | `BaseCharacters` → `HeroDefinition`: one `DieDefinition` (not a list), `maxHealth`, `playableInPrototype`, `displayName`, `portrait`. Drop `m_lstSkills[4]`. Keep `m_dPassiveEffects` but make it serializable (list of `{eventName, FaceEffect}`) for map-face hooks. Fix class/file name mismatch (`BaseCharacters` in `BaseCharacter.cs`). | `Assets/Scriptable Objects/Characters/BaseCharacter.cs` |
| **new** | Six `HeroDefinition` assets (Knight, Arcanist, Trickster playable; Berserker, Rogue, Cleric locked) with faces from the proposal table. | new `Assets/Scriptable Objects/Characters/*.asset` |
| **new** | `HeroPick` scene + `DicePickController` (generic "pick N of M with locked slots"; reused for traversal pick). | new `Assets/Scenes/HeroPick.unity`, new `Assets/Scripts/UI/DicePickController.cs` |
| **new** | `RunState` (DontDestroyOnLoad): chosen heroes, owned traversal dice, HP, current node, RNG seed. | new `Assets/Scripts/Run/RunState.cs` |
| **adapt** | `PlayerTeamManager`: populate `Front/Center/Back` from `RunState` heroes at combat start instead of serialized scene refs; remove mana fields. | `Assets/Scripts/PlayerTeamManager.cs` |

### Map (check 3)

| Tag | Edit | Files |
|---|---|---|
| **new** | `MapNode` (type: Combat/Elite/Rest/Treasure/Boss, outgoing fork), `Fork` (`minDice, maxDice, List<PathBracket>`), `PathBracket` (`minTotal, maxTotal, targetNode`). `OnValidate` asserts brackets cover `[minDice × lowestFace, maxDice × highestFace]` with no gaps/overlaps, using the pattern already in `BaseCharacters.OnValidate`. | new `Assets/Scripts/Map/` |
| **new** | `ForkOdds`: convolve the chosen dice's face multisets to get P(total) and sum per bracket. Pure C#, unit-testable with the already-installed test framework. | new `Assets/Scripts/Map/ForkOdds.cs`, new `Assets/Tests/EditMode/ForkOddsTests.cs` |
| **new** | One hand-authored act (8–10 nodes, 3–4 forks) as a ScriptableObject, plus `Map` scene and node UI with hover odds. | new `Assets/Scriptable Objects/Map/Act1.asset`, new `Assets/Scenes/Map.unity` |
| **new** | Traversal die assets: Low Road, Steady, High Road, Wild, Pilgrim. | new `Assets/Scriptable Objects/Dice/Traversal/*.asset` |
| **new** | Scene flow: `SceneRouter` or similar driven by `RunState` (HeroPick → Map → Combat ↔ Map → RunEnd). Add scenes to Build Settings. | new `Assets/Scripts/Run/SceneRouter.cs`, `ProjectSettings/EditorBuildSettings.asset` |

### Combat (check 4)

| Tag | Edit | Files |
|---|---|---|
| **adapt** | `GameManager` → `CombatController` with an explicit `TurnPhase { Roll, Reroll, Resolve, EnemyTurn, CheckEnd }`. `StartTurn`/`EndTurn` stubs become real transitions. Reset `DiceManager.m_isIdle` on each roll setup. | `Assets/Scripts/GameManager.cs`, `Assets/Scripts/DiceManager.cs` |
| **adapt** | `DiceManager.SetUpDice` takes `List<DieDefinition>` (one per die) and assigns `DiceHandler.m_diceFaceInfo` per instance. Track active dice in a separate list; fix the pooled-count bug in `ProcessDiceValue`. Add `RelaunchDice(IEnumerable<int> indices)` for the one-reroll rule. | `Assets/Scripts/DiceManager.cs`, `Assets/Scripts/DiceHandler.cs` |
| **adapt** | `ProcessDiceValue` stops summing mana. It builds `List<RollResult>` and fires `RollCompleted`. Traversal and combat each subscribe and interpret. | `Assets/Scripts/DiceManager.cs` |
| **adapt** | `PlayerUnitController` → `UnitController` used on both teams: `HeroDefinition` or `EnemyDefinition`, current HP, shield, status (Charge stacks, Rage, Poison), `IPointerClickHandler` for targeting. | `Assets/Scripts/PlayerUnitController.cs` |
| **new** | `EnemyDefinition` SO (HP, intent list) and `IntentController` that picks and telegraphs next action; 3 regular + 1 elite + 1 boss assets. | new `Assets/Scripts/Combat/`, new `Assets/Scriptable Objects/Enemies/` |
| **new** | Face resolution pipeline: `BeforeResolve(results)` → apply each face's `FaceEffect` → `AfterResolve`. Reuse the Before/After hook shape from `PlayerTeamManager.UpdateResources`. | new `Assets/Scripts/Combat/FaceResolver.cs` |
| **replace** | HUD: `ResourceManager` + `Canvas/HUD/Header/ResouceHolder` (Red/Blue/Yellow) → per-unit HP/shield bars and enemy intent badges using the `WorldUI` material. | `Assets/Scripts/ResourceManager.cs`, scene `Canvas/HUD`, `Assets/UI/WorldUI.mat` |
| **replace** | `UI_SkillMenu` (2×2 skill buttons) → a per-die action strip on the roll overlay: Keep / Reroll toggle, target prompt when a face needs one. | scene `PlayArea/PlayerTeam/UI_SkillMenu` |
| **keep** | `DiceRollerController` drag-to-launch, `DiceRollingArea` box and camera, `DiceRollCamera` overlay. | `Assets/Scripts/DiceRollerController.cs`, `Assets/Dice/DiceRollingArea.prefab`, scene `Canvas/Panel` |
| **replace** | Delete dead `DieHandler.cs`, `LaunchDice.cs`. | `Assets/Scripts/DieHandler.cs`, `Assets/Scripts/LaunchDice.cs` |

### Rolling and RNG (check 5)

| Tag | Edit | Files |
|---|---|---|
| **new** | `RollService` with a `System.Random` seeded from `RunState.seed`. `Roll(DieDefinition) → faceIndex`. All gameplay randomness (face draws, enemy intent choice, map generation if any) goes through it. | new `Assets/Scripts/Dice/RollService.cs` |
| **adapt** | Under the recommended "logical roll, physical show" model: `DiceHandler.LaunchDice` receives the target face, throws physically, then on settle rotates/snaps to the target face (or on `IsSleeping` timeout). Under "physics is truth": leave as-is and accept no seeding for rolls. | `Assets/Scripts/DiceHandler.cs` |
| **adapt** | The three `UnityEngine.Random` calls in `DiceManager` (idle torque, idle angular velocity, launch torque) are cosmetic and may stay unseeded, but should be the *only* unseeded randomness. | `Assets/Scripts/DiceManager.cs` |
| **adapt** | Restore `Physics.gravity` and solver iterations after a roll, or scope them to the dice scene, so combat/map physics are not affected. | `Assets/Scripts/DiceManager.cs` |

### Event bus hygiene (cross-cutting)

| Tag | Edit | Files |
|---|---|---|
| **keep** | `EventManager` as the bus. | `Assets/Scripts/EventManager.cs` |
| **adapt** | Add a `static class GameEvents` of `const string` names (`RollCompleted`, `TurnPhaseChanged`, `TraversalRoll`, `UnitDamaged`, `NodeEntered`, …) to stop payload-type mismatches at string literals. | new `Assets/Scripts/GameEvents.cs` |

---

## 5. Answers the code gives to the open questions

- **Traversal dice reusable or spent?** No bearing in code. Pooled dice in `DiceManager` make either trivial.
- **Party HP shared or per-hero?** `BaseCharacters.m_nHealth` is per-hero. The 3-slot layout with individual colliders also leans per-hero. Shared pool would need a new place to live.
- **Does a dead hero's die leave the roll?** `SetUpDice` can already shrink the active set, so yes is cheap, **once the pooled-count bug in §3 is fixed**.

---

## 6. Suggested order

1. Decide §2.1 (mana) and §2.4 (logical vs. physical roll). Both change the shape of everything below.
2. Die data model: `DieDefinition`, `FaceEffect`, `RollResult`, `RollService`. Fix the two `DiceManager` bugs. Delete dead code. This can be done entirely inside the existing scene and verified with the existing overlay.
3. `RunState` + `SceneRouter` + Hero Pick with locked slots (the proposal insists the pick flow be "the real thing").
4. Map data + fork odds + one authored act. Reuse the dice overlay for fork rolls.
5. Combat: turn machine, HP, intents, face resolution, reroll.
6. Treasure / Rest / Boss / run end.
