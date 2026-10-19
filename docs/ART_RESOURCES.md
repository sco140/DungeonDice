# Art & Asset Resources

Free sources to pull from when the prototype needs art, audio or 3D, and the register of what is
actually in the project so every piece can be swapped later. **Rule:** anything that enters
`Assets/` from outside is logged in the "In use" table below with its source, license and path.

## Sources

| Source | What | License notes | Best for us |
| --- | --- | --- | --- |
| [Kenney](https://kenney.nl/assets) | 2D/3D packs, UI, icons, audio | CC0 (public domain). No attribution required; Kenney asks for a mention. | UI icons, board-game icons, dice, UI panels, sound effects. Consistent style, tiny files. |
| [KayKit](https://kaylousberg.itch.io/) | Low-poly 3D character, dungeon and prop kits | CC0 on the free kits (check each page). | Dungeon tiles and props for the box tray, hero and enemy figures. |
| [Quaternius](https://quaternius.com/) | Low-poly 3D characters, monsters, environments | CC0. | Enemy and hero models in one coherent style. |
| [OpenGameArt](https://opengameart.org/) | Everything, mixed quality | Per-asset: CC0, CC-BY, CC-BY-SA, GPL. **Check and record the license per asset.** CC-BY needs attribution in-game or in credits; SA/GPL may be unsuitable. | Icons, tilesets, SFX, music. Filter by license on the site. |
| [Sketchfab](https://sketchfab.com/) | 3D models | Per-model: CC0, CC-BY, CC-BY-NC… Many are NC (non-commercial) or ND. **Record the exact license.** | Hero dice, props, a boss model. Prefer CC0/CC-BY. |
| [Poly Haven](https://polyhaven.com/) | HDRIs, PBR textures, models | CC0. | Lighting HDRI for the dice box, tray felt/stone/wood textures. |
| [ambientCG](https://ambientcg.com/) | PBR textures, HDRIs | CC0. | Tray surfaces, dungeon walls, ground. |
| [cgbookcase](https://www.cgbookcase.com/) (and friends: [3dtextures.me](https://3dtextures.me/), [ShareTextures](https://www.sharetextures.com/)) | PBR textures | cgbookcase: CC0. Check the others per site. | Tray and map surfaces. |

Attribution lines for CC-BY assets go in `docs/CREDITS.md` (create it when the first CC-BY asset lands).

## How to bring an asset in

1. Download to the session scratchpad, inspect, and copy **only the files used** into
   `Assets/_Project/Art/<Source>/<Pack>/`. Keep the pack's license file beside them.
2. Reference assets from code or authoring tools by path (icons: `IconLibrary` loads from
   `Assets/_Project/Art/.../` via `AssetDatabase` in authoring, or from a `Resources`-free sprite
   table authored into a ScriptableObject).
3. Add a row to the table below. If it replaces a placeholder, say which.
4. For textures and models: note the import settings that matter (sprite mode, pixels-per-unit,
   scale) so a replacement can match.

## In use

| Asset | Source / pack | License | Project path | Used for | Replace later? |
| --- | --- | --- | --- | --- | --- |
| Dice d6 game ready PBR (mesh + 5 plastic materials, 2k/512 textures) | Unknown pack bundled before this project started (`Assets/Dice/Dice_6/Documentation/Readmy.txt`) | **Unverified** — find the source and record it | `Assets/Dice/Dice_6/` | The physical die mesh and hero/traversal skins | Yes: swap for a CC0 die or own model once provenance is known |
| Game Icons (1-bit white, 1x) | Kenney — [Game Icons](https://kenney.nl/assets/game-icons) | CC0 | `Assets/_Project/Art/Kenney/GameIcons/` | Node types, intents, statuses, mana, face effects | Fine to keep; style-swap if the UI gets its own look |
| Board Game Icons (1x) | Kenney — [Board Game Icons](https://kenney.nl/assets/board-game-icons) | CC0 | `Assets/_Project/Art/Kenney/BoardGameIcons/` | Dice faces, die/pip glyphs, cards | Same |

Placeholders that are **not** external assets and can be replaced freely: all `UiFactory` panels
(flat colours), the tray materials under `Assets/_Project/Data/DiceBox/Materials/` (plain URP Lit
colours), `Die_Yellow.mat` (a tint of the pack's white die).
