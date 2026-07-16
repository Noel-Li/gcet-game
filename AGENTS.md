# GCET Game — Project Notes

A 2D top-down game in **Unity 6000.3.19f1**, URP2D render pipeline. Full project layout, scripts and wiring live in `README.md`; this file captures *how the scene is actually built* (object hierarchy, IDs, tilemap layout) so you don't have to re-derive it from the YAML.

---

## Design philosophy — reuse, clarity, Editor-first

This game is one piece of a larger design. Treat every change as a chance to make the project more **reusable**, more **readable**, and more **editable from inside the Unity Editor** (Inspector / Prefab / context menu) rather than by editing C# source.

- **Prefer data over code.** Ship content and tuning as serialized fields on components, `[RequireComponent]`-wired, or asset files (`ScriptableObject`) — not as literals buried in methods. If a designer would want to tweak it without asking a programmer, expose it in the Inspector.
- **Components are reusable building blocks.** Give each `MonoBehaviour` one clear responsibility and decouple it from its data where reasonable (e.g. the NPC's *lines* sit on a sibling `Conversation` component; `NpcController` only manages click/open/advance). New NPCs get a new `Conversation` in the scene, not a new script.
- **Keep scripts readable.** Short, single-purpose files and methods; XML comments that say *why*; no cleverness that can't be explained in a sentence.
- **Editor affordances matter.** Use `[Header]`, `[Tooltip]`, `[TextArea]`, `[Range]`, `[ContextMenu]`, `[CreateAssetMenu]`, and `OnValidate` where they make a component self-documenting and safe to edit from the Inspector.
- **Aim for real content.** Don't let default/filler text ("placeholder", untranslated strings) become the shipped state — replace with the intended dialogue/text wherever you touch content.

When a change touches a script, prefab, or asset, apply this philosophy by default.

---

## Convention — keep this file in sync

**Every time you edit scene structure** (add/remove/reparent GameObjects, change a Grid cell size or tilemap contents, wire new components, or move art between layers), **update the relevant section below** — especially the per-scene hierarchy and the tilemap layout tables. This file is the single source of truth for what the hierarchy *currently* looks like, not what it was when first written. If something here contradicts the scene, this file is wrong; fix it.

---

## Environment

- **Unity version pinned** to `6000.3.19f1` — open `gcet_game_unity/ProjectSettings/ProjectVersion.txt`. Do not upgrade.
- **Input System** package (`com.unity.inputsystem`), *not* the old `Input` class. Action asset: `gcet_game_unity/Assets/gcet_game_unity.inputactions`.
- **Rendering** is URP 2D (`com.unity.render-pipelines.universal` + `com.unity.feature.2d`).
- **No `.asmdef`** in `Assets/` yet → all scripts compile into `Assembly-CSharp`.
- Design doc: https://docs.google.com/document/d/1kAJAHLvWIgtqb_OMR9vzJOddMddIAvhJqlZOiyhzepEw

---

## Art asset

- **Cainos Pixel Art Top Down - Basic** at `Assets/Cainos/Pixel Art Top Down - Basic/`. Wall/grass props use sprite fileIDs `21300000`–`21300059`, guid `9f8f114589864b6489859a58fbb6baf9`.
  - Relevant wall sprites used in `template.unity`: **index 38** (`TX Tileset Wall_19`, horizontal edges), **index 16** (`TX Tileset Wall_8`, vertical edges), **index 40** (`TX Tileset Wall_20`, corner).
  - Tile material: `dcc4a1ac295b22745b75fd4081b68b1` (the 2D sprite material the grass and walls tilemaps both use).
- All scene tilemaps sort on **SortingLayer `-1500971143` ("layer 1")**; draw order is controlled by each renderer's `m_SortingOrder`.

---

## Scenes

```
Assets/Scenes/
  SampleScene.unity        the original/playable scene (see README)
  template.unity           the authoring template — ALL background geometry lives here
  background.unity
  hanzi tracing base.unity
  Skyden_Games/...         (Cozy Farm demo art)
```

### `template.unity` — background hierarchy

The `background` GameObject (fileID `625347797`; Transform `625347799`) sits at **world position `(15.6, 1.4, 0)`** and carries a `SortingGroup` (fileID `625347798`, SortingLayer 1, order 0). Its children (listed on the Transform's `m_Children`):

| Child (fileID) | Role | Grid | Contents |
|---|---|---|---|
| `3000000001` → `3000000002` | **Walls** Grid | **cell 4×4** | 31 wall tiles (see layout table below) |
| `304655127` → LAYER 2 | Grass scaffold | cell 1×1 | child `1467464659` Grid → `1919175851` "Layer 2 – Grass" (206 tiles, SortingOrder 1) |
| `832442544` | standalone Tilemap | cell 1×1 | **empty** — safe to delete if you want it gone |
| `1223041772` → LAYER 1 | Grass scaffold | cell 1×1 | child `564326181` Grid → `1004411643` "Layer 1 – Grass" (408 tiles, SortingOrder 0) |

Each LAYER scaffold Transform is at local `(-21.46119, -2.7633393, ~-0.43)` with **scale `(2.7568076, 2.0110736, 1)`** — the grass renders at this enlarged scale, which is why the grass uses **1×1** cells while the walls use **4×4** (the two layers are on incompatible scales; a single Grid cannot hold both densities). All existing grass tilemap blocks are **byte-identical** to the original authored file — do not regenerate them by hand.

#### Walls Tilemap layout (cell coords → sprite index)

Grid cell size `4×4`; Tilemap Transform local position `(3.5, 1.5)` (the L∞-optimal snap origin). 31 tiles forming a rectangular frame:

```
y= 2  (-11..1, spr38)            top edge
y= 1  x=-11 (spr16)              left vertical
y= 0  x=-11 (spr16)              left vertical
y=-1  x=-11 (spr16)              left vertical
y=-2  (1,spr16)x=-11 ^ ; x=-1 spr40 corner ; x=1 spr16 right vert
y=-3  (-11..0, spr38)            bottom edge
x=1   y=-2..2 (spr16)            right edge
```

Explicit cell list `{(cx,cy,index)}`:
```
(-11,-3,38)(-11,-2,16)(-11,-1,16)(-11,0,16)(-11,1,16)(-11,2,38)(-10,-3,38)(-10,2,38)
(-9,-3,38)(-9,2,38)(-8,-3,38)(-8,2,38)(-7,2,38)(-6,-3,38)(-6,2,38)(-5,-3,38)(-5,2,38)
(-4,2,38)(-3,-3,38)(-3,2,38)(-2,-3,38)(-2,2,38)(-1,-3,38)(-1,-2,40)(-1,2,38)(0,-3,38)
(1,-2,16)(1,-1,16)(1,0,16)(1,1,16)(1,2,16)
```

**How this was made** — the walls started as ~40 hand-placed `SpriteRenderer` props (scale ~4, not on any grid). They were **baked** into this 4×4 tilemap: each prop's center was snapped to the nearest 4-cell (worst case ~0.39 cell ≈ 1.57 units off; 6 overlapping props collapsed into shared cells). The result is a clean rectangular frame. If you re-bake, expect similar snap error — the props weren't authored to a grid.

**Fixtures for editing** — fileIDs `3000000000`–`3000000006` are the new Walls objects (Walls GO + Grid, Walls Tilemap GO + Transform + Renderer + Tilemap). Background children must cite `3000000000` (the Walls Grid transform) in its `m_Children`.

### Other scenes

- **`game1.unity`** — the current playable scene. Its full visual map is synchronized from `background.unity`: 147 scene roots, including the 47-object `background` hierarchy plus the expanded standalone houses, trees, bridges, water, wall, grass/flower, pavement, and fence props. Gameplay roots remain in the same scene (`PauseController`, `Player`, three Areas, `Soldier`, `Silk Seller`, `Main Camera`, `EventSystem`, and `Audio Source`); the Soldier is positioned at `(21.8, 10.1)`, just below the fence's left side. Its single wired NPC `Conversation` component maps `player` to `CharacterBackground/guoxiaoDialogue.jpeg` and `soldier` to `CharacterBackground/soldierDialogue.jpeg`, plus expression-keyed portraits (`normal`, `confused`, `excited`, `worried`, `surprised`, `stern`) from `character_img/`. It also wires `gamevoiceDialogue.jpeg` for narrator/instruction prompts and `multichoiceDialogue.jpeg` for answers. `Dialogue` builds 700×211-aspect frames along the bottom: the active speaker or Game Voice prompt on the left and, on choice steps, a separate Multiple Choice frame on the right.
- **`StartScene.unity`** — build index 0 and the game's intro/menu scene. Its screen-space Canvas stretches `game-related scenes/startScreen.png` across a 1920×1080 reference layout. A transparent 820×170 `BeginButton` hit area sits over the painted **Begin** control and calls `StartGame.LoadGame()` with `gameSceneName: game1`. The former title, sprite backdrop, and video host remain inactive so the supplied template is shown immediately.
- **`PauseScene.unity`** — loaded additively by `game1`'s `PauseController` when Escape is pressed, preserving the live game scene underneath while `Time.timeScale` is zero. Its screen-space Canvas stretches `game-related scenes/pauseScreen.png`; a transparent 820×170 `UnpauseButton` hit area overlays the painted **Unpause** control and calls `PauseResume.Resume()`. Its local camera, AudioListener, EventSystem, rain, and legacy text are disabled because the additive game scene supplies input/rendering.
- **`SampleScene.unity`** — the playable scene: `Player` (`PlayerMovement` + `ColorSprite`), Areas/Regions (`GameArea`/`GameRegion`), `NPC`, `FollowCamera`. See README for wiring. Has its **own copy** of the wall sprite reused on the Player/Area/Camera sprite renderers (guards in the detection scripts filter on `TX Tileset Wall` name prefix, not just the sprite).
- **`background.unity`**, **`hanzi tracing base.unity`** — separate; see their own hierarchy.

---

## Reading / editing scene YAML safely

The scene files are Unity YAML. Key rules when editing by hand or script:

- Every `GameObject`, `Transform`, `Component` etc. is a top-level object starting with `--- !u!NNN &<fileID>`. Objects reference each other **by `fileID`** — never by name.
- To *remove* a GameObject you must delete **all** of its components (`!u!1`, `!u!4` Transform, `!u!212` SpriteRenderer, …) **and** remove its fileID from its parent's `m_Children` list — otherwise Unity logs missing-reference errors. Deleting a wall prop = 3 objects (GO + Transform + SpriteRenderer).
- `m_Children` is written **multi-line** in these files (one `{fileID: N}` per line under `m_Children:`), not inline `m_Children: [{…}]` — patching code must handle that.
- Sprite references like `21300038` and script references like `11500000` are **external asset IDs** (resolve against `.meta`/asset files, not the scene) — an "unresolved fileID" check that flags only those is healthy and expected.
- Real scene `fileID`s currently max out around `2.1 × 10⁹`; new objects should be allocated in the `3 × 10⁹`+ range to avoid collisions.

### Detection gotcha
Several non-wall objects (Player, Area_Left, Main Camera, the Tilemap GO, LAYER 1/2, and the grass tilemaps) **reuse the wall sprite** (`guid 9f8f1145…21300…`). To identify actual wall props, filter on **object name** `TX Tileset Wall*` AND parent fileID `625347799` (background), *not* on the sprite GUID alone.

---

## Scripts (Assets/Scripts)

`PlayerMovement.cs`, `GameArea.cs`, `GameRegion.cs`, `NpcController.cs`, `FollowCamera.cs`, `ColorSprite.cs`. All in `Assembly-CSharp` (no `.asmdef`). See README for the message flow.

Editor-only utility: `Assets/misc/Editor/PlayModeStartScene.cs` assigns `StartScene.unity` to `EditorSceneManager.playModeStartScene` after script reloads. Therefore the Unity Play button always enters through the intro even while a gameplay scene is open; the `Tools > GCET > Set Intro as Play Mode Start Scene` menu item can reapply it manually.

Dialogue pinyin is authored with standard tone marks in `Conversation.cs`. `Dialogue.FormatPinyin` wraps every tone-marked parenthetical pronunciation in the dynamic `LiberationSans SDF` font, keeping Latin glyph spacing normal while the surrounding Chinese text continues to use the default Chinese TMP font.

---

## Adding a new mechanic / Area / input / script

Follow the recipes in README.md § "How to add new stuff". For scene *structure* changes, remember to update this file (Convention section, above).
