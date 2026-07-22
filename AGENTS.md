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
- Walls continue to use tile material `dcc4a1ac295b22745b75fd4081b68b1`. In the current authoritative background, `Layer 1 – Grass` uses `buildings and stuff/Materials/treesTwo.mat` (`47e0bd82cb3696449b6c5c49c5282fe2`) and `Layer 2 – Grass` uses `character_img/Materials/motherMain.mat` (`2a911dd54431aac4a880e72250030e5f`); `game1` mirrors those assignments.
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

- **`game1.unity`** — the current playable scene. Its visual map is synchronized from 218 of the 222 roots in `background.unity`; the omitted roots are `Area_Left`, `Area_Right`, `Area_Top`, and `PauseController`. 13 game-only roots remain, for **231 scene roots total**. The revised map includes 60 `stoneWall_0*` boundaries, 40 `StoneUP_0*` boundaries, 18 straight-river pieces, 4 river curves, 30 large grass patches, 2 large houses, 2 small houses, the rice farm and border, 4 new fence pieces, 25 red/white flower props, bridges, trees, the market/arch set, and shared character roots. Every non-gameplay source subtree—including authored building and environment colliders—is copied exactly. Gameplay roots retain the newer `game1` wiring and game-only UI, regions, gates, and retained wall roots. The persistent music source is supplied by `StartScene`. `Controls Hint Canvas` is a non-interactive screen-space Canvas at sorting order 90; its 220×220 `Press Tab Hint` Image uses `game-related scenes/Press tab.png`, anchored 24 pixels from the top-right on a 1920×1080 reference layout. `Review Book Hint Canvas` displays a non-interactive 200×200 `misc/bookIcon.png` Image 24 pixels from the bottom-right. `ReviewBookHintVisibility` sits directly on this Canvas and disables its renderer immediately when `Dialogue.OpenStateChanged` reports an open conversation; it restores the renderer when dialogue closes. `ReviewBookController` independently ignores Escape for the same interval. Controls, Dialogue, and Review Book canvases render above both hints. `Opening Location Overlay` is a 1920×1080-responsive screen-space Canvas at sorting order 9000. It dims the game to 55% black and centers a 900×271-aspect `game-related scenes/nanjingText.jpeg` title card for 3 seconds, then fades it for 0.35 seconds while gameplay input is suspended. Character root positions match `background.unity`: Player `(-7.79, 1.19)`, Soldier `(22.87, 10.73)`, Silk Seller `(29.22, 19.01)`, Trader `(50.41, 20.99)`, `guopinyuMain_0` `(52.87, 8.66)`, `motherMain_0` `(28.2, -1.98)`, and `lilguyMain_0` `(26.87, -2.43)`. The `ReviewBookController` root also carries `ControlsOverlayController`, which toggles `ControlsScene` additively with Tab. `Player` keeps movement, collision, `PlayerWalkAnimation`, and `OpeningDialogue` on the root, with a `Player Visual` child containing the `character_img/xiaoyueBook.PNG` holding-book SpriteRenderer. Xiaoyue's serialized dialogue portraits now use the matching holding-book artwork for every authored expression: `xiaoyueBook.PNG` (normal), `bookConfusedXY.PNG`, `bookExcitedXY.PNG`, `bookSleepyXY.PNG` (tired), and `bookWorriedXY.PNG`. This lets the visual flip horizontally and bounce without moving the collider. On the first gameplay arrival, `OpeningDialogue` waits for the Nanjing title card to finish and then presents the one-line player monologue “Where am I? What is this book?” using `CharacterBackground/guoxiaoDialogue.jpeg` and `character_img/bookConfusedXY.PNG`; `GameProgress` prevents the complete opening sequence from replaying after tracing reloads. NPC `InterractE` prompts preserve the source aspect within `1.4×1.45` world units and leave a `0.12`-unit clear gap above the rendered head; the legacy text prompt is fallback-only. Its single wired NPC `Conversation` component maps `player` to `CharacterBackground/guoxiaoDialogue.jpeg` and `soldier` to `CharacterBackground/soldierDialogue.jpeg`, plus expression-keyed portraits (`normal`, `confused`, `excited`, `worried`, `surprised`, `stern`) from `character_img/`. It also wires `gamevoiceDialogue.jpeg` for narrator/instruction prompts and `multichoiceDialogue.jpeg` for answers. `Dialogue` builds 700×211-aspect frames along the bottom: the active speaker or Game Voice prompt on the left and, on choice steps, a separate Multiple Choice frame on the right. The Silk Seller owns the 王喜悦 exchange: 郭小月 introduces herself and 郭品玉, traces 事 before her private thought, then fills “___知道她在哪里吗？” by tracing 你. 王喜悦 identifies 吴叔杨 correctly, switches to her panic portrait for the bridge warning, launches the exact 别→人 tracing task, then 郭小月 answers with her excited portrait, and a final Game Voice line urges the player to hurry. Only the Silk Seller may claim these saved resume points; later E interactions show a short bridge reminder instead of replaying the lesson.
  - **NPC conversation facing:** every `NpcController` in `game1` mirrors only its wired character SpriteRenderer toward the Player as soon as the interaction prompt becomes available, keeps that facing through dialogue and post-tracing resume, then restores the authored `flipX` after the Player leaves interaction range. `artworkFacesRightWhenUnflipped` is Inspector-configured per NPC; the Soldier, Trader, Silk Seller, and Guopinyu use right-facing source art, while `motherMain_0` uses left-facing source art. The root Transform and collider never move.
- **`game1_test.unity`** — the legacy region-access test scene. It has 198 scene roots, including its authored R1–R3 regions, three `InvisibleWall` gates, and test NPCs. Its YAML contains one unique object block per fileID and no dangling internal references; duplicate map/NPC blocks accidentally spliced in by the July merge were removed.
  - **Mother interaction:** `motherMain_0` carries `NpcController` and a one-line `Conversation`. Within 2.5 world units it shows the same aspect-preserving `interactE` prompt used by the Soldier, with a `0.12`-unit gap above the rendered head. Pressing E opens `CharacterBackground/motherDialogue.jpeg`, centers `character_img/motherMain.PNG` in the frame's higher white portrait slot using the portrait entry's normalized `(0, 0.15)` slot offset, and displays “Respect your elders! 尊重老人！” using the default dynamic Chinese TMP font. Closing the line re-arms the interaction; the empty `repeatSteps` list intentionally reuses the same authored line on every later conversation. Its `resumeAfterTracing` flag is off; only the Soldier enables that Inspector flag, preventing simple NPCs from claiming the Soldier's saved post-tracing exchange.
  - **Trader interaction:** `Trader` owns a 12-step Inspector-authored exchange with 吴叔杨. 郭小月 uses tired and normal portraits; 吴叔杨 uses stern, normal, and slight-shock portraits, all on their dedicated character frames. The conversation traces exact 的 before his introduction and exact 喜→欢 after he explains that he likes 郭品玉’s materials, then continues through his suggestion to inspect the book. After Xiaoyue says thanks, a centered Game Voice card shows `不要问别人你不喜欢的事` and “Don't ask others about things you don't like.”; advancing the card automatically launches the exact 11-character trace `不→要→问→别→人→你→不→喜→欢→的→事`. Trader has `resumeAfterTracing` enabled with owner key `Trader`, so only this NPC can claim any of its three saved trace points. Later E interactions use the one-line reminder “Maybe look in that book. I recognize 郭品玉’s inscription on it.” instead of replaying the full exchange.
  - **Guopinyu interaction:** `guopinyuMain_0` in the lower-right flower garden now carries `NpcController` and a 14-step Inspector-authored `Conversation`. Its aspect-preserving E prompt appears above her head within 2.5 world units. 郭小月 uses tired, normal, and excited portraits; 郭品玉 uses normal and confused portraits on `CharacterBackground/guopinDialogue.jpeg`. The authored dialogue omits the planned comic panels, launches an exact 水 tracing task from a Game Voice instruction, and resumes at “It works! Thank you, thank you!” using owner key `Guopinyu`. Later E interactions use the short reminder “Take care of the book—and your new charm.”
  - **Dialogue input and glyph coverage:** the Silk Seller’s 事, 你, and 别→人 prompts are one-option writing choices, so number-pad `1` launches the exact trace and resumes at steps 4, 8, and 12 respectively. `Dialogue.MakeText` enables multi-atlas expansion on the dynamic Chinese TMP font before creating runtime text; dialogue can add 可, 呀, 怎, and later Chinese glyphs without falling back to square placeholders when its first 1024×1024 atlas fills.
  - **Soldier interaction:** the Soldier now owns a 24-step Inspector-authored first conversation. It asks whether the player is 张三, traces 不 as the reply, quizzes the translation of 你叫什么名字 with both wrong answers looping back, introduces 李沅诺, traces 对 before the 郭品玉 family reveal, quizzes 她做最好的 with a Game Voice retry line, and finishes by tracing the exact 不→要→问 sequence before 郭小月 says 谢谢. Completed Hanzi are added to the review book as soon as their traces finish. After the first conversation closes, the E prompt reappears; later E presses use the serialized one-line `repeatSteps` exchange “Did you find 王喜悦？” with `soldierDialogue.jpeg` and the normal Soldier portrait, then close without replaying the lesson. `GameProgress` stores completed NPC keys for the current play session, so this repeat state survives tracing reloads of `game1`.
  - **Portrait placement:** each serialized `SpeakerDialoguePortrait` may use a normalized `slotOffset` without changing the source bitmap. Xiaoyue's referenced `bookExcitedXY_0` sprite slice spans the book-holding body and both exclamation-mark pieces (`x=10`, width `452`); `bookSleepyXY_0` similarly includes its detached thought mark (`x=10`, width `406`). Guopinyu's referenced `guopinyuConfused_0` slice likewise spans her body and both question-mark pieces (`x=59`, width `429`).
  - **Latest background resync:** `game1` contains 218 authoritative roots from the current 222-root `background.unity`, omitting `Area_Left`, `Area_Right`, `Area_Top`, and `PauseController`. The revised stone-boundary, river, house, rice-farm, grass, fence, flower, bridge, tree, market, and character roots match the source transforms, sprites, and colliders. The Soldier is at `(22.87, 10.73)` and the rice farmer at `(-2.53, 12.55)`. The Player visual child, NPC controllers/conversations, review-book/controls/opening UI, R1–R4 region gates, tracing state, and two game-only wall roots remain intact. `tools/sync_background_to_game1.py` reproduces and validates the merge.
- **`StartScene.unity`** — build index 0 and the game's intro/menu scene. Its screen-space Canvas stretches `game-related scenes/newStartscreen.png` across a 1920×1080 reference layout. A transparent 820×170 `BeginButton` hit area sits over the painted **Begin** control and calls `StartGame.LoadGame()` with `gameSceneName: ComicScene`. `StartGame` first calls the root `Background Music` object's `BackgroundMusic.Play()`, starting the Inspector-wired “Journey Through Time” clip at volume `0.727`; that source loops and survives every subsequent scene load. The former title, sprite backdrop, and video host remain inactive so the supplied template is shown immediately.
- **`ComicScene.unity`** — build index 1 and the opening comic sequence. Its 1920×1080-responsive screen-space Canvas contains a black backdrop and a stretch-to-fill `Comic Panel` Image, eliminating side bars while keeping the complete panel visible at any Game-view aspect. `ComicSequence` owns the six Inspector-wired sprites `game-related scenes/1.png` through `6.png`; Space advances every panel, and pressing Space after panel 6 loads `ControlsScene`. A non-interactive 250×150 `Press Space Prompt` Image uses `game-related scenes/Space.png`, preserves its aspect, and sits 24 pixels inside the bottom-right corner above all six panels. A 0.2-second initial input delay prevents transition input from skipping panel 1. A lightweight root `Comic Camera` (AudioListener enabled, culling mask 0) clears Display 1 and keeps the persistent background music audible without showing Unity's "No cameras rendering" warning; the overlay Canvas renders on top.
- **`ReviewBookScene.unity`** — build index 4 and the additive review overlay opened by Escape from gameplay. Its 1920×1080-responsive screen-space Canvas (sorting order 9600) stretches `game-related scenes/reviewBookScreen.png` across the display and blocks underlying UI input. `ReviewBookController` freezes gameplay while the scene is open; pressing Escape again unloads it and returns to the exact prior game state. `ReviewBookView` aligns twelve responsive, transparent buttons over the painted white boxes in top-left reading order. It activates only the distinct Hanzi recorded by `GameProgress`, showing each character and its tone-marked pinyin; clicking an entry launches an exact one-character review trace. Review traces return to the same gameplay scene and replace the normal meaning result with that character's Inspector-authored Chinese/English example sentence in the Game Voice panel. Review availability is conversation-gated: the gameplay hint is hidden and Escape is disabled until the active dialogue closes, after which reopening the scene rebuilds all slots from the latest `GameProgress` collection.
- **`ControlsScene.unity`** — used in two modes. After `ComicScene`, it is loaded alone and its `ControlsSceneEntry` component loads `game1` when Space is pressed (Tab remains a compatibility shortcut). During gameplay, `game1`'s `ControlsOverlayController` opens it additively with Tab and closes it with Space or Tab. Its 1920×1080-responsive screen-space Canvas (sorting order 9500) stretches `game-related scenes/newgamecontrolsactually.png` across the full-screen `Controls Background` Image, which remains raycast-enabled as the input blocker. The former generated `Controls Frame`/panel/text subtree is inactive. Gameplay time and dialogue/NPC keyboard input remain suspended while it is open additively; Escape closes it before opening `ReviewBookScene`. Its root `Controls Camera` and AudioListener render and play audio in standalone mode; `ControlsSceneEntry` disables both whenever `game1` is already loaded additively, preventing camera and listener conflicts.
- **`SampleScene.unity`** — the playable scene: `Player` (`PlayerMovement` + `ColorSprite`), Areas/Regions (`GameArea`/`GameRegion`), `NPC`, `FollowCamera`. See README for wiring. Has its **own copy** of the wall sprite reused on the Player/Area/Camera sprite renderers (guards in the detection scripts filter on `TX Tileset Wall` name prefix, not just the sprite).
- **`background.unity`** — the authoritative 222-root map source synchronized into `game1`. It contains the revised stone-boundary frame, river network, houses, rice farm, grass grid, fences, flower garden, bridges, trees, market/arch props, shared player/NPC roots, and four support roots omitted from gameplay synchronization (`Area_Left`, `Area_Right`, `Area_Top`, and `PauseController`).
- **`hanzi tracing base.unity`** — eight roots: `Main Camera`, `Global Light 2D`, `TraceManager`, `GuideStroke`, `UserStroke`, `StrokeArrow`, `Tracing Background`, and `Tracing Header Canvas`. `Tracing Background` is a SpriteRenderer using `game-related scenes/tracingBG.png` (`tracingBG_0`, guid `8d31ff95d1419e24a9a208e617b58ddb`) at world `(0, 0, 1)` and SortingOrder `-100`. Its `CameraFillSprite` component is wired to `Main Camera` and independently fits the sprite's X/Y scale to the live orthographic viewport, eliminating clear-color bars at non-16:9 Game view sizes without changing tracing coordinates. `Tracing Header Canvas` is a non-interactive 1920×1080-responsive overlay at sorting order 1000; its compact white 560×132 top-centered panel sits 64 pixels below the top edge and shows the active Hanzi plus tone-marked pinyin in black without covering the world-space stroke area. `TracingCharacterHeader` updates both labels whenever `TraceManager` advances through a multi-character request. When the full tracing task is complete, a lower-center 900×271 completion card uses `CharacterBackground/gamevoiceDialogue.jpeg`. Story traces show `Hanzi → meaning` or `phrase → meaning`; a review-launched trace instead shows the selected Hanzi's Chinese example sentence and English translation, centered across the same Game Voice panel. It accepts click, Space, or Enter after a 0.25-second guard, and only then returns to gameplay. `TraceManager.userStrokeColor` is Inspector-configured as black; correct/incorrect release feedback remains green/red. `availableCharacters` is ordered 不, 要, 我, 知, 道, 别, 人, 可, 以, 对, 事, 你, 水, 喜, 欢, 的, 问: legacy count-only tasks still consume the initial entries, while newer dialogue tasks select exact `CharacterData.characterName` values in their authored order. Every `CharacterData` stores its Inspector-editable tone-marked `pinyin` and single-character `meaning` alongside `characterName`; `TraceManager.phraseMeanings` defines 不要, 知道, 别人, 可以, 喜欢, 不要问, and the full sentence 不要问别人你不喜欢的事.
  - `Assets/Character vectors/` contains `CharacterData` assets for 不 (4 strokes), 对 (5), 要 (9), 事 (8), 你 (7), 别 (7), 人 (2), 水 (4), 喜 (12), 欢 (6), 的 (8), 问 (6), 以 (4), 可 (5), 我 (7), 知 (8), and 道 (12). Every guide polyline uses the full ordered Make Me a Hanzi/Hanzi Writer median data, normalized into the tracing scene's approximately 4×3 coordinate area with `x = (sourceX - 512) / 200` and `y = (sourceY - 387) / 280`.

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

`PlayerMovement.cs`, `PlayerWalkAnimation.cs`, `OpeningDialogue.cs`, `StartGame.cs`, `BackgroundMusic.cs`, `ComicSequence.cs`, `ControlsSceneEntry.cs`, `ControlsOverlayController.cs`, `ReviewBookController.cs`, `ReviewBookHintVisibility.cs`, `ReviewBookView.cs`, `GameArea.cs`, `GameRegion.cs`, `NpcController.cs`, `FollowCamera.cs`, `ColorSprite.cs`. All in `Assembly-CSharp` (no `.asmdef`). See README for the message flow. `NpcController` exposes conversation-facing fields in the Inspector, mirrors its assigned SpriteRenderer while the Player is in interaction range and throughout dialogue/resume, then restores the original facing after the Player leaves or the controller is disabled.

Editor-only utility: `Assets/misc/Editor/PlayModeStartScene.cs` assigns `StartScene.unity` to `EditorSceneManager.playModeStartScene` after script reloads. Therefore the Unity Play button always enters through the intro even while a gameplay scene is open; the `Tools > GCET > Set Intro as Play Mode Start Scene` menu item can reapply it manually.

Dialogue pinyin is authored with standard tone marks in `Conversation.cs`. `Dialogue.FormatDialogueText` renders every English and pinyin run in the dynamic, multi-atlas `LiberationSans SDF - Fallback` asset at an Inspector-tunable 133% scale, making both use the same Latin typeface and visually matching their lowercase height to the surrounding Chinese glyphs. Chinese text continues to use the default dynamic Chinese TMP font; the readable Latin fallback atlas adds tone-marked vowels at runtime without missing-glyph warnings.
`GameProgress` records each successfully completed Hanzi once, in first-completed order, and caps the review collection at the artwork's twelve slots. Every `CharacterData` asset exposes `reviewExampleChinese` and `reviewExampleEnglish`; normal story tracing continues to show meanings, while review-launched tracing uses these bilingual examples.
`DialogueStep` and `DialogueChoice` may serialize exact `traceCharacters`; `GameProgress` persists that ordered request plus an NPC owner key across scene loads, and `NpcController` atomically claims only its own resume point.

---

## Adding a new mechanic / Area / input / script

Follow the recipes in README.md § "How to add new stuff". For scene *structure* changes, remember to update this file (Convention section, above).
