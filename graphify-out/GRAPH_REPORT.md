# Graph Report - DungeonDice  (2026-11-04)

## Corpus Check
- Corpus is ~47,683 words - fits in a single context window. You may not need a graph.

## Summary
- 1345 nodes · 3562 edges · 102 communities (54 shown, 48 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 297 edges (avg confidence: 0.86)
- Token cost: 633,392 input · 0 output

## Community Hubs (Navigation)
- Combat Screen & Targeting
- Combat Module Files
- CombatState Mana & Dice
- DiceManager State Machine
- EventManager Handler Wrappers
- Hero Pick Selection
- Run State & Rewards
- Legacy Architecture Snapshot
- Ability Definitions
- Combat Hero Unit
- Dice Box Visual Profiles
- Hero Pick Screen
- CombatState Unit Tests
- Enemy Turn & Statuses
- Legacy Mana HUD
- Physical Die Handler
- Project Guide & Pivot
- Act Entry & Test Teardown
- Seeded RollService
- Die Modifier Application
- Fork Odds & Tests
- Traversal Pick Screen
- Art Resources & Icons
- Die Faces & Mana Colors
- Map Graph Layout
- PlayMode Flow Tests
- Legacy Team & Skills
- Dice Roller Overlay
- RunController Config
- Dice Box Loop (docs)
- Act Asset Authoring
- RollResult Tests
- Prefab & Scene Authoring
- Combat Events
- Die Definition & Recolor
- Run Phases & Routing
- Combat Act Screenshot
- Run Start & Seeding
- Combat Throw Screenshot
- Unity Module Packages
- Package Dependencies
- Die Modifier Types
- Fork Resolver
- Run Outcome Flow
- Codebase Review Findings
- Enemy Intents & Log
- Combat Enemy Unit
- Dice & Material Authoring
- Die Data Model (docs)
- Map Fork Screenshot
- Combat Targeting Screenshot
- Roll Completed Listeners
- Reward Options
- Hero Classes & Colors
- Fork Rules (docs)
- Node Travel & Reward Tests
- Traversal Pick Screenshot
- Map After Roll Screenshot
- Enemy Intent Authoring
- Map Node Types
- IconLibrary Lookups
- Combat Phases
- Die Material Resolution
- Authoring Utilities
- Legacy Mana Colors
- Icon Import Authoring
- Run End Screenshot
- Package: ide.rider
- Package: ide.visualstudio
- Package: inputsystem
- Package: modules.ai
- Package: modules.androidjni
- Package: modules.animation
- Package: modules.cloth
- Package: modules.director
- Package: modules.imageconversion
- Package: modules.jsonserialize
- Package: modules.particlesystem
- Package: modules.physics
- Package: modules.physics2d
- Package: modules.screencapture
- Package: modules.terrainphysics
- Package: modules.tilemap
- Package: modules.ui
- Package: modules.umbra
- Package: modules.unityanalytics
- Package: modules.unitywebrequest
- Package: modules.unitywebrequestaudio
- Package: modules.unitywebrequesttexture
- Package: modules.unitywebrequestwww
- Package: modules.vectorgraphics
- Package: modules.vehicles
- Package: modules.video
- Package: modules.vr
- Package: modules.wind
- Package: modules.xr
- Package: pipeline
- Package: test-framework
- Package: ugui
- Package: visualscripting

## God Nodes (most connected - your core abstractions)
1. `CombatState` - 82 edges
2. `RunState` - 61 edges
3. `DieDefinition` - 59 edges
4. `CombatScreen` - 55 edges
5. `RollResult` - 50 edges
6. `RunController` - 47 edges
7. `DungeonDice.Dice` - 44 edges
8. `MapScreen` - 44 edges
9. `DiceManager` - 38 edges
10. `CombatHero` - 33 edges

## Surprising Connections (you probably didn't know these)
- `MapLayout` --references--> `MapLayout`  [INFERRED]
  CLAUDE.md → Assets/_Project/Scripts/Map/MapLayout.cs
- `Screenshot: Traversal Pick screen` --references--> `TraversalPickScreen`  [INFERRED]
  docs/screenshots/02_traversal_pick.png → Assets/_Project/Scripts/UI/TraversalPickScreen.cs
- `AbilityDefinition` --references--> `AbilityDefinition`  [INFERRED]
  CLAUDE.md → Assets/_Project/Scripts/Combat/AbilityDefinition.cs
- `BaseSkill` --references--> `BaseSkill`  [INFERRED]
  docs/ARCHITECTURE.md → Assets/_Project/Scripts/Combat/BaseSkill.cs
- `CombatRules` --references--> `CombatRules`  [INFERRED]
  CLAUDE.md → Assets/_Project/Scripts/Combat/CombatRules.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Dice box roll pipeline** — claude_dicerollercontroller, claude_dicemanager, claude_dicehandler, claude_rollresult, claude_eventmanager [EXTRACTED 1.00]
- **Fork traversal: validate, resolve, odds, display** — claude_actdefinition, claude_forkvalidator, claude_forkresolver, claude_forkodds, claude_mapscreen, claude_prototypeact [EXTRACTED 1.00]
- **Legacy mana pipeline (SampleScene)** — docs_architecture_gamemanager, claude_dicemanager, docs_architecture_playerteammanager, docs_architecture_resourcemanager, docs_architecture_event_resourceupdate [EXTRACTED 1.00]
- **Playable hero roster, one per colour domain** — docs_screenshots_01_hero_pick_knight_card, docs_screenshots_01_hero_pick_arcanist_card, docs_screenshots_01_hero_pick_trickster_card, docs_screenshots_01_hero_pick_colour_domains [EXTRACTED 1.00]
- **Starting traversal dice pool (4 dice trading mean vs variance)** — docs_screenshots_02_traversal_pick_low_road_die, docs_screenshots_02_traversal_pick_steady_die, docs_screenshots_02_traversal_pick_high_road_die, docs_screenshots_02_traversal_pick_wild_die [EXTRACTED 1.00]
- **Fork roll flow: commit dice, see odds, roll, choose unlocked path** — docs_screenshots_03_map_fork_traversal_dice_toggles, docs_screenshots_03_map_fork_fork_edge_labels, docs_screenshots_03_map_fork_roll_button, docs_screenshots_03_map_fork_roll_to_unlock_rule [INFERRED 0.85]
- **Roll-to-unlock-then-choose traversal flow as shown on the map** — docs_screenshots_05_map_after_roll_fork_threshold_labels, docs_screenshots_05_map_after_roll_roll_result_panel, docs_screenshots_05_map_after_roll_travel_button, docs_screenshots_05_map_after_roll_map_graph_view [INFERRED 0.85]
- **Turn 1 throw: hint, physics dice, pip-to-mana mapping feeding hero ability costs** — docs_screenshots_06_combat_throw_throw_hint_banner, docs_screenshots_06_combat_throw_six_resource_dice_in_flight, docs_screenshots_06_combat_throw_pip_to_colour_mapping, docs_screenshots_06_combat_throw_ability_cost_labels [INFERRED 0.85]
- **Combat screen unit cards (enemies with intents, heroes with abilities)** — docs_screenshots_06_combat_throw_enemy_drowned_wretch, docs_screenshots_06_combat_throw_enemy_vault_rat, docs_screenshots_06_combat_throw_hero_knight_card, docs_screenshots_06_combat_throw_hero_trickster_card, docs_screenshots_06_combat_throw_health_bars [EXTRACTED 1.00]
- **Party of three heroes, each spending one colour domain of the shared pool** — docs_screenshots_07_combat_act_knight, docs_screenshots_07_combat_act_arcanist, docs_screenshots_07_combat_act_trickster, docs_screenshots_07_combat_act_mana_pool_display [INFERRED 0.85]
- **Roll -> mana pool -> ability spend flow** — docs_screenshots_07_combat_act_resource_dice_chips, docs_screenshots_07_combat_act_mana_pool_display, docs_screenshots_07_combat_act_hero_ability_cards, docs_screenshots_07_combat_act_combat_log [INFERRED 0.85]
- **Combat screen layout: enemies, mana pool and dice, heroes, log** — docs_screenshots_07b_combat_targeting_enemy_cards, docs_screenshots_07b_combat_targeting_mana_pool_hud, docs_screenshots_07b_combat_targeting_die_chips, docs_screenshots_07b_combat_targeting_hero_cards, docs_screenshots_07b_combat_targeting_combat_log [EXTRACTED 1.00]

## Communities (102 total, 48 thin omitted)

### Community 0 - "Combat Screen & Targeting"
Cohesion: 0.06
Nodes (25): TargetKind, Die, Enemy, None, BarView, CardFlash, CombatScreen, Icons (+17 more)

### Community 1 - "Combat Module Files"
Cohesion: 0.09
Nodes (9): DungeonDice.UI, DungeonDice.EditorTools, DungeonDice.Combat, DungeonDice.Core, GameUtility, DungeonDice.Map, DungeonDice.Run, DungeonDice.Tests (+1 more)

### Community 2 - "CombatState Mana & Dice"
Cohesion: 0.07
Nodes (24): CombatState, AnyDiePending, Blue, CanAct, CanReroll, Dice, Enemies, Heroes (+16 more)

### Community 3 - "DiceManager State Machine"
Cohesion: 0.10
Nodes (15): DiceBoxState, Empty, Idle, Rolling, Settled, DiceManager, ActiveDice, Instance (+7 more)

### Community 4 - "EventManager Handler Wrappers"
Cohesion: 0.10
Nodes (7): EventHandler, EventHandler, EventHandler, EventHandler, EventHandlerBase, EventManager, Instance

### Community 5 - "Hero Pick Selection"
Cohesion: 0.18
Nodes (9): HeroDefinition, ColorLabel, HeroPickSelection, CanConfirm, Capacity, IsFull, Party, HeroPickSelectionTests (+1 more)

### Community 6 - "Run State & Rewards"
Cohesion: 0.09
Nodes (21): RewardOffer, Options, RunState, Act, CanAddResourceDie, CurrentNode, CurrentNodeIndex, CurrentNodeResolved (+13 more)

### Community 7 - "Legacy Architecture Snapshot"
Cohesion: 0.11
Nodes (25): Known traps in the current code, ScreenRouter, DungeonDice Architecture (legacy snapshot), BaseSkill, Dead code: DieHandler and LaunchDice, DiceRollingArea prefab and DiceCamera render texture, Legacy event catalogue, Event_ResourceUpdate (+17 more)

### Community 8 - "Ability Definitions"
Cohesion: 0.10
Nodes (18): AbilityDefinition, CostLabel, Target, AbilityKind, Damage, Detonate, Guard, Recolor (+10 more)

### Community 9 - "Combat Hero Unit"
Cohesion: 0.10
Nodes (18): CombatHero, Abilities, ActionsThisTurn, Color, Health, IsAlive, MaxHealth, Name (+10 more)

### Community 10 - "Dice Box Visual Profiles"
Cohesion: 0.19
Nodes (4): DiceBoxProfile, DiceBoxVisuals, Profile, DiceBoxVisualsTests

### Community 11 - "Hero Pick Screen"
Cohesion: 0.12
Nodes (14): HeroPickScreen, CardButtons, Icons, Roster, Selection, StartButton, Hero Pick screenshot (01_hero_pick.png), Arcanist card (Blue, HP 20: Burn 1B, Poison 2B, Detonate 3B) (+6 more)

### Community 13 - "Enemy Turn & Statuses"
Cohesion: 0.13
Nodes (9): StatusKind, Burn, Hex, Poison, Taunt, Weaken, StatusSet, All (+1 more)

### Community 14 - "Legacy Mana HUD"
Cohesion: 0.12
Nodes (5): Event_ResourceUpdate, ResourceManager, Instance, GameManager, Instance

### Community 15 - "Physical Die Handler"
Cohesion: 0.15
Nodes (6): DiceHandler, Definition, IsSettling, OwnerId, FaceNormal, DiceHandlerFaceTests

### Community 16 - "Project Guide & Pivot"
Cohesion: 0.13
Nodes (16): PrototypeSceneAuthoring, CLAUDE.md project guide, Authoring tools (re-runnable, GUID-preserving), CombatRules, CombatScreen, CombatState, DungeonDice assembly (asmdef), Prototype.unity single-scene run (+8 more)

### Community 18 - "Seeded RollService"
Cohesion: 0.21
Nodes (5): RollService, Seed, Shared, RewardTests, TestDice

### Community 19 - "Die Modifier Application"
Cohesion: 0.14
Nodes (13): RollResult, Applied, Color, Die, DieName, Face, FaceIndex, Locked (+5 more)

### Community 21 - "Traversal Pick Screen"
Cohesion: 0.13
Nodes (10): TraversalPickScreen, CardButtons, ConfirmButton, Selection, TraversalPickSelection, CanConfirm, Capacity, Chosen (+2 more)

### Community 22 - "Art Resources & Icons"
Cohesion: 0.14
Nodes (17): Dice d6 Pack Readme, Dice d6 game ready PBR pack, IconAssetAuthoring, IconLibrary, Art & Asset Resources, ambientCG, cgbookcase, docs/CREDITS.md (CC-BY attribution) (+9 more)

### Community 23 - "Die Faces & Mana Colors"
Cohesion: 0.16
Nodes (7): DieFace, ManaColor, Pips, FaceEffectEntry, FaceEffectKind, Mana, Number

### Community 24 - "Map Graph Layout"
Cohesion: 0.16
Nodes (10): Cell, Column, Node, Row, MapLayout, Result, Cells, Columns (+2 more)

### Community 25 - "PlayMode Flow Tests"
Cohesion: 0.19
Nodes (3): CombatFlowTests, MapFlowTests, RunFlowTests

### Community 26 - "Legacy Team & Skills"
Cohesion: 0.16
Nodes (5): BaseSkill, PlayerTeamManager, DiceCount, Instance, PlayerUnitController

### Community 28 - "RunController Config"
Cohesion: 0.12
Nodes (14): RunController, Act, CombatProfile, CurrentIcons, Icons, Instance, Phase, ResourceDie (+6 more)

### Community 30 - "Dice Box Loop (docs)"
Cohesion: 0.16
Nodes (15): GameEvents, Dice box loop, DiceBox.prefab, DiceBoxVisuals / DiceBoxProfile, DiceHandler, DiceManager (DiceBoxState machine), DiceRollerController, EventManager (string-keyed pub/sub bus) (+7 more)

### Community 31 - "Act Asset Authoring"
Cohesion: 0.18
Nodes (6): ActAssetAuthoring, ActDefinition, MapNode, HasFork, IsCombat, IsTerminal

### Community 34 - "Combat Events"
Cohesion: 0.14
Nodes (12): CombatEvent, Amount, Detail, Kind, Target, TargetIsEnemy, CombatEventKind, Damage (+4 more)

### Community 35 - "Die Definition & Recolor"
Cohesion: 0.18
Nodes (4): DieDefinition, Faces, IsResource, IsTraversal

### Community 36 - "Run Phases & Routing"
Cohesion: 0.17
Nodes (10): RunPhase, Combat, HeroPick, Map, Reward, RunEnd, TraversalPick, ScreenEntry (+2 more)

### Community 37 - "Combat Act Screenshot"
Cohesion: 0.19
Nodes (14): Combat Screen Screenshot (Act phase, Drowned Hall), Arcanist (20 HP; Burn 1B, Poison 2B, Detonate 3B), Combat Log Panel, Combat Screen Layout (enemies top, mana+dice middle, heroes bottom, log footer), Drowned Hall (Combat map node), Drowned Wretch (enemy, 10 HP, Attack 5), Enemy Cards with Telegraphed Intents, Hero Cards with Ability Buttons (+6 more)

### Community 39 - "Combat Throw Screenshot"
Cohesion: 0.21
Nodes (13): Combat Throw Screenshot (Turn 1), Abandon Run Button, Ability Buttons with Colour Cost Labels (1/2/3), Combat Log Line ('Combat begins: Drowned Wretch, Vault Rat.'), Dice Box Render Overlay (Combat Profile), Drowned Wretch Enemy Card (10/10, Next: Attack 5), Vault Rat Enemy Card (8/8, Next: Attack 3), Unit Health Bars (red enemies, green heroes) (+5 more)

### Community 40 - "Unity Module Packages"
Cohesion: 0.14
Nodes (13): com.unity.modules.accessibility@1.0.0, com.unity.modules.adaptiveperformance@1.0.0, com.unity.modules.assetbundle@1.0.0, com.unity.modules.audio@1.0.0, com.unity.modules.imgui@1.0.0, com.unity.modules.unitywebrequestassetbundle@1.0.0, com.unity.ai.navigation, com.unity.modules.accessibility (+5 more)

### Community 41 - "Package Dependencies"
Cohesion: 0.14
Nodes (14): dependencies, com.unity.ai.navigation@2.0.9, com.unity.collab-proxy@2.11.2, com.unity.modules.terrain@1.0.0, com.unity.modules.uielements@1.0.0, com.unity.multiplayer.center@1.0.1, com.unity.render-pipelines.universal@17.3.0, com.unity.timeline@1.8.10 (+6 more)

### Community 42 - "Die Modifier Types"
Cohesion: 0.21
Nodes (12): ChooseResultModifier, FaceIndex, Name, FlipModifier, Name, IDieModifier, Name, LockModifier (+4 more)

### Community 43 - "Fork Resolver"
Cohesion: 0.22
Nodes (7): ForkResolver, Fork, DiceCountLabel, HasPaths, MaxTotal, MinTotal, ForkPath

### Community 44 - "Run Outcome Flow"
Cohesion: 0.18
Nodes (4): RunOutcome, Defeat, None, Victory

### Community 45 - "Codebase Review Findings"
Cohesion: 0.21
Nodes (8): RollService (seeded RNG), Prototype Design vs. Codebase Review, DiceManager pooled-count and m_isIdle bugs, What needs to be built, in run-flow order, Suggested build order (review), Dungeon Dice Prototype Design Proposal, Build order (7 steps), Prototype scope

### Community 46 - "Enemy Intents & Log"
Cohesion: 0.32
Nodes (3): EnemyDefinition, EnemyIntent, CombatFeedbackTests

### Community 47 - "Combat Enemy Unit"
Cohesion: 0.18
Nodes (10): CombatEnemy, Definition, Health, Index, Intent, IsAlive, MaxHealth, Name (+2 more)

### Community 49 - "Die Data Model (docs)"
Cohesion: 0.18
Nodes (9): DieDefinition, HeroDefinition, IDieModifier (Nudge, Flip, ChooseResult, Lock), RewardOffer, RollResult, BaseCharacters, BaseDice (legacy die ScriptableObject), Die modifiers (future) (+1 more)

### Community 50 - "Map Fork Screenshot"
Cohesion: 0.24
Nodes (10): Map Fork Screenshot (03_map_fork.png), Abandon Run Button, Fork Edge Labels (threshold + unlock chance), UI Issue: Edge Label Overlaps Antechamber Card, Map Screen Act Graph (left-to-right columns), Node Detail Panel (Vault Gate, Start), Map Header: Party HP, Resource Dice, Traversal Dice, Prototype Act Nodes (Vault Gate to The Vault Heart) (+2 more)

### Community 51 - "Combat Targeting Screenshot"
Cohesion: 0.25
Nodes (10): Combat Targeting Screenshot (Drowned Hall), Arcanist Abilities (Burn 1B, Poison 2B, Detonate 3B), Combat Log Panel, Resource Die Chips (pips + colour letter), Enemy Cards (Drowned Wretch 10/10, Vault Rat 8/8), Enemy Targeting Mode (Strike: click an enemy), Hero Cards with Ability Buttons (Knight, Arcanist, Trickster), Knight Abilities (Strike 1R, Shield 2R, Bulwark 3R) (+2 more)

### Community 52 - "Roll Completed Listeners"
Cohesion: 0.29
Nodes (3): RollCompletedEvent, Results, TotalPips

### Community 53 - "Reward Options"
Cohesion: 0.20
Nodes (8): RewardKind, ResourceDie, TraversalDie, RewardOption, Description, Die, Kind, Title

### Community 54 - "Hero Classes & Colors"
Cohesion: 0.33
Nodes (8): Hero abilities (Knight/Arcanist/Trickster), AbilityDefinition, Arcanist (Blue class), Dual-color classes (Rogue, Berserker, Cleric), Hero Dice, Knight (Red class), Three color domains (Red/Blue/Yellow), Trickster (Yellow class)

### Community 55 - "Fork Rules (docs)"
Cohesion: 0.28
Nodes (7): ForkValidator, PrototypeAct, ActDefinition / MapNode / Fork, ForkOdds, ForkValidator, PrototypeAct, PathBracket coverage validator (proposed)

### Community 57 - "Traversal Pick Screenshot"
Cohesion: 0.33
Nodes (8): Screenshot: Traversal Pick screen, Enter the dungeon button, Fork roll explainer text, High Road die (4 4 5 5 6 6, mean 5.0), Low Road die (1 1 2 2 3 3, mean 2.0), Steady die (3 3 3 4 4 4, mean 3.5), Traversal Pick screen layout, Wild die (1 1 1 6 6 6, mean 3.5)

### Community 58 - "Map After Roll Screenshot"
Cohesion: 0.25
Nodes (9): Screenshot: Map after fork roll, Abandon run button, Act 1 nodes shown (Vault Gate, Forgotten Cache, Drowned Hall, The Warden, Dry Alcove, Flooded Stair, Chained Golem, Sealed Reliquary, Last Lantern, Antechamber, The Vault Heart), Fork edge threshold labels (any / 7+ / 9+ / 12+ / 14+, open/locked), Overlapping label at Antechamber ('11+' over node card), Map graph view (left-to-right node columns with edges), Party status header (hero HP, resource dice, traversal dice), Node detail panel with roll result (Rolled 5: one path open) (+1 more)

### Community 59 - "Enemy Intent Authoring"
Cohesion: 0.32
Nodes (5): IntentKind, Attack, AttackAll, Block, EnemyAssetAuthoring

### Community 60 - "Map Node Types"
Cohesion: 0.25
Nodes (7): NodeType, Boss, Combat, Elite, Rest, Start, Treasure

### Community 62 - "Combat Phases"
Cohesion: 0.29
Nodes (7): CombatPhase, Act, EnemyTurn, Lost, Reroll, Throw, Won

### Community 65 - "Legacy Mana Colors"
Cohesion: 0.33
Nodes (6): Mana, ManaColor, Blue, Generic, Red, Yellow

### Community 67 - "Run End Screenshot"
Cohesion: 0.40
Nodes (4): Full-HP Party on Defeat (possible forced/eval-driven defeat), New Run Button, Run End Party Summary (nodes visited, per-hero HP, traversal dice, seed), Run End Screen (Defeat) screenshot

## Ambiguous Edges - Review These
- `Fork edge threshold labels (any / 7+ / 9+ / 12+ / 14+, open/locked)` → `Overlapping label at Antechamber ('11+' over node card)`  [AMBIGUOUS]
  docs/screenshots/05_map_after_roll.png · relation: conceptually_related_to
- `Hero Cards with Ability Buttons` → `Act-phase Hint: spend pool as often as it lasts, unspent mana carries over`  [AMBIGUOUS]
  docs/screenshots/07_combat_act.png · relation: conceptually_related_to
- `Run End Party Summary (nodes visited, per-hero HP, traversal dice, seed)` → `Full-HP Party on Defeat (possible forced/eval-driven defeat)`  [AMBIGUOUS]
  docs/screenshots/09_run_end.png · relation: conceptually_related_to

## Knowledge Gaps
- **276 isolated node(s):** `Damage`, `Shield`, `Guard`, `ShieldAll`, `Status` (+271 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 338 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **48 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CombatScreen` connect `Combat Screen & Targeting` to `Prefab & Scene Authoring`, `CombatState Mana & Dice`, `Combat Module Files`, `Die Definition & Recolor`, `Combat Events`, `DiceManager State Machine`, `Combat Throw Screenshot`, `Combat Act Screenshot`, `Enemy Intents & Log`, `Project Guide & Pivot`, `Combat Targeting Screenshot`, `Roll Completed Listeners`, `Fork Odds & Tests`, `PlayMode Flow Tests`, `Legacy Team & Skills`, `Dice Roller Overlay`, `IconLibrary Lookups`?**
  _High betweenness centrality (0.150) - this node is a cross-community bridge._
- **Are the 4 inferred relationships involving `CombatState` (e.g. with `CombatRules.Clone()` and `CombatScreen.StartCombat()`) actually correct?**
  _`CombatState` has 4 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Damage`, `Shield`, `Guard` to the rest of the system?**
  _276 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Combat Screen & Targeting` be split into smaller, more focused modules?**
  _Cohesion score 0.057715756672537595 - nodes in this community are weakly interconnected._
- **Why does `CombatState` connect `CombatState Mana & Dice` to `Combat Screen & Targeting`, `Combat Module Files`, `Die Definition & Recolor`, `DiceManager State Machine`, `Hero Pick Selection`, `Run Start & Seeding`, `Ability Definitions`, `Combat Hero Unit`, `CombatState Unit Tests`, `Enemy Turn & Statuses`, `Enemy Intents & Log`, `Combat Enemy Unit`, `Project Guide & Pivot`, `Seeded RollService`, `Fork Odds & Tests`, `Ability Resolution`, `Combat Phases`?**
  _High betweenness centrality (0.128) - this node is a cross-community bridge._

### Low-confidence Hints
_AMBIGUOUS edges — the extractor was unsure. Verify before acting on these._

- **What is the exact relationship between `Fork edge threshold labels (any / 7+ / 9+ / 12+ / 14+, open/locked)` and `Overlapping label at Antechamber ('11+' over node card)`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Hero Cards with Ability Buttons` and `Act-phase Hint: spend pool as often as it lasts, unspent mana carries over`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._