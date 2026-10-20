# Dungeon Dice — Prototype Design

Oct 19, 2026 · @Cody Felts

## Prototype goal

One act, start to boss, where choosing traversal dice feels like a real decision. Dungeon Dice is a roguelike: the player brings 3 Hero Dice into combat and steers a branching map by rolling Traversal Dice.

The prototype is done when:

- A full run can be won or lost.
- Playtesters can explain *why* they picked their traversal dice at a fork.
- If players pick traversal dice at random, tune fork brackets and die variety before building anything else.

## Decisions after codebase review

These pivots reconcile the proposal with the existing DungeonDice code. They take priority over anything below that conflicts.

- **Hybrid mana ("use it or bank it").** Every face is an action, but any die can be banked instead for 1 mana in its hero's color. Each hero has one signature skill costing 3 mana of its color. Keeps the existing mana pool, HUD and `BaseSkill`.
- **Three color domains.** Red = Damage & Block, Blue = Statuses, Yellow = Dice modification. Prototype has 3 core classes, one per color. Dual-color classes come later.
- **Physical throw stays.** The player's drag-throw and physics decide the result. Once dice settle, the face read becomes a logical roll result. Modifiers act on that result and the die re-orients to match.
- **One die per hero** for the prototype. The data model keeps a list so "hero gains a second die" can be a future reward.
- **Per-hero HP.** A dead hero's die leaves the roll.
- **One dice box for everything.** Combat and traversal share the same box, with a swappable visual profile per context so they can look different later.

## Run flow

1. **Pick Hero Dice.** Player chooses 3 of 6 hero dice. In the prototype, 3 are playable and 3 show as locked, but the selection screen and flow must be the real thing.
2. **Start node: pick Traversal Dice.** Player chooses 3 traversal dice from the starting pool.
3. **Traverse the map.** At each fork, the player commits dice (within the fork's allowed count), rolls, and the total decides the path.
4. **Resolve the node.** Combat, Elite, Rest, or Treasure.
5. **Repeat** steps 3–4 until the Boss node.
6. **Boss.** Win ends the run in victory; any hero-party wipe ends it in defeat.

## Hero Dice

Each hero is a d6 with custom faces in one color domain. The prototype ships 3 core classes, one per color. Three dual-color classes fill the remaining slots later, making 6.

| Color | Domain | Mana use |
| --- | --- | --- |
| Red | Damage & Block | Red signature skills |
| Blue | Statuses (Burn, Poison, Weaken, Hex) | Blue signature skills |
| Yellow | Modifying dice (hero and traversal) | Yellow signature skills |

**Core classes (prototype, playable)**

| Class | Color | Faces (6) | Signature skill (proposal) |
| --- | --- | --- | --- |
| Knight | Red | Strike 3, Strike 3, Shield 4, Shield 4, Taunt + Shield 2, Strike 5 | Bulwark: Shield 6 to all allies |
| Arcanist | Blue | Burn 2, Burn 2, Poison 3, Weaken, Hex (target takes +1 from all sources this turn), Fizzle | Detonate: consume a target's statuses, deal damage equal to their stacks |
| Trickster | Yellow | Nudge ±1, Nudge ±1, Flip, Reroll ally, Mirror (copy another die), Jab 2 | Loaded Dice: after dice settle, change any one rolled die (hero or traversal) to any face |

**Dual-color classes (future, shown locked)**

| Class | Colors | Direction |
| --- | --- | --- |
| Rogue | Red + Blue | Damage plus poison combos |
| Berserker | Red + Yellow | Heavy damage, self-modifying Rage |
| Cleric | Blue + Yellow | Cleanse and heal, Bless (+1 to another die) |

The pick screen shows 6 slots: 3 core playable, 3 dual locked. Duplicates are allowed for now, so a party can be e.g. 2 Knights + 1 Trickster.

**Flip** turns a die to its opposite face; on a d6 opposite faces sum to 7. **Trickster** faces and its signature can act on traversal rolls, which ties Yellow to the map.

## Combat

Each turn the player throws all 3 hero dice in the dice box, then decides per die: use its face, or bank it for mana. References: Slice & Dice, Dicey Dungeons.

1. **Throw** all living heroes' dice (physical drag-throw).
2. **Reroll once:** the player picks which dice to re-throw; the rest stay put.
3. **Use or bank** each die: resolve its face (attack, block, status, modify), or bank it for 1 mana of its hero's color.
4. **Signature skills:** any hero can cast its one signature skill if the team mana pool covers its cost. Signatures are cast after the dice settle and before faces are used or banked, so effects that change dice (like Loaded Dice) work on the current roll.
5. **Enemy turn:** enemies act on the intents they telegraphed (Slay the Spire style).

HP is per hero. A dead hero's die leaves the roll, and its signature skill is unavailable.

## Traversal Dice

Traversal dice have different face sets, so choosing dice means choosing a probability curve. Player picks 3 at the start node; Treasure nodes can grant more.

| Die | Faces | Role | Prototype |
| --- | --- | --- | --- |
| Low Road | 1, 1, 2, 2, 3, 3 | Chase low brackets | Starting pool |
| Steady | 3, 3, 3, 4, 4, 4 | Hit middle ranges reliably | Starting pool |
| High Road | 4, 4, 5, 5, 6, 6 | Chase high brackets | Starting pool |
| Wild | 1, 1, 1, 6, 6, 6 | All-or-nothing | Starting pool |
| Pilgrim | 1–6 (standard d6) | Baseline die; target for future modifiers | Treasure reward |

Pilgrim is a plain d6 on purpose. "Choose your result" behavior belongs to the die-modifier system, not the die itself.

Traversal dice are kept after a roll and can be used at every fork. A specific die can override this (e.g. a future one-use die), but none in the prototype do.

## Map and forks

A Slay the Spire–style node map where every fork is resolved by a traversal roll. Prototype map: 8–10 nodes, 3–4 forks, one Boss at the end.

**Fork definition**

- Dice count: a fixed number ("roll 3") or an allowed range ("roll 1–3").
- Paths: each path declares a result range for the summed total.
- Ranges must cover every possible total, with no gaps or overlaps. The player never fails a roll; they always land on some path.
- Good paths aren't always low. Sometimes the high bracket is the treasure path, so no single die is strictly best.

**Example fork:** Roll 2–3 dice. ≤5 → Treasure, 6–10 → Combat, 11+ → Elite. Two Low Road dice nearly guarantee Treasure; adding a third widens options but risks the middle.

**Balance note:** with plain d6s, 3 dice totaling ≤3 is a 1-in-216 chance. Bracket thresholds must be tuned against the actual traversal dice in the pool.

**UI:** show each path's nominal odds (fair-die math) as a percentage on hover. Physics decides the actual result, so real outcomes may drift slightly from the shown odds.

**Node types:** Combat, Elite, Rest, Treasure (new traversal die or hero face upgrade), Boss.

## Die modifiers (future)

Not built for the prototype, but the die data model should leave room for them. Hero and traversal dice should share one die/face model so modifiers can apply to either.

Because the physical throw decides results, every modifier works on the **logical result after dice settle**: the face read becomes a roll result, the modifier changes it, and the die visually re-orients to the new face. Lock means the die isn't re-thrown.

Example modifiers to design toward:

- **Choose result:** set a die to a chosen face instead of rolling.
- **Nudge:** ±1 to a rolled result.
- **Lock:** keep a die's result through a reroll.
- **Face replace:** permanently swap one face for another (upgrade path).

## Prototype scope

| Area | In scope |
| --- | --- |
| Hero pick | Full pick screen; 6 slots, 3 core playable (Knight, Arcanist, Trickster), 3 dual-color locked; duplicates allowed |
| Mana | Red/Blue/Yellow team pool from banked dice; one signature skill per hero |
| Traversal pick | Pick 3 of 4 (Low Road, Steady, High Road, Wild) |
| Map | 1 act, 8–10 nodes, 3–4 forks |
| Dice box | One box for combat and traversal, visual profile swappable per context |
| Enemies | 3 regular, 1 elite, 1 boss, all with telegraphed intents |
| Rewards | Treasure grants a traversal die (incl. Pilgrim) or a hero face upgrade |
| Modifiers | Data model only, no gameplay (Trickster's faces excepted) |

## Build order

Order adapted from the codebase review to the decisions above.

1. **Die data model:** `DieDefinition` with `FaceEffect` faces, a roll result per die, modifier interface (data only). Fix the `DiceManager` pooled-count and `m_isIdle` bugs; delete dead dice scripts.
2. **Dice box:** per-die `DieDefinition` injection; a `RollCompleted` event replaces mana summing; a post-settle re-orient so modifiers can change a die's shown face.
3. **Box visual profile:** materials, lighting, tray, camera framing and sounds grouped into a swappable profile (combat vs. traversal), plus a per-die visual on `DieDefinition`.
4. **Run scaffolding:** `RunState`, scene router, Hero Pick with locked slots.
5. **Map:** fork/bracket data, coverage validator, nominal odds, one authored act.
6. **Combat:** turn phases, use-or-bank, keep the color mana pool and HUD, trim `BaseSkill` to one signature per hero, per-hero HP, enemy intents.
7. **Nodes and run end:** Treasure, Rest, Boss, victory and defeat.

Randomness outside the throw (enemy intents, rewards, map) goes through one seeded service so runs are reproducible apart from the dice themselves.
