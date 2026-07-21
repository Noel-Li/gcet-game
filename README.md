# gcet-game

A 2D top-down game built in **Unity Engine (6000.3.19f1)** using the Universal 2D (URP2D) render pipeline. The current build is a region-access-control + conversation demo: the player walks between rectangular regions, each separated by a conversation-gated invisible wall that only opens after the player talks to that region's gate NPC (which launches a hanzi-tracing task).

Design doc: https://docs.google.com/document/d/1kAJAHLvWIgtqb_OMR9vzJOddMddIAvhJqlZOiyhzepEw/edit?tab=t.sfvi1brsrj8m

> Commit and push to this repo to save progress for version control.

---

## Table of contents

- [Repo layout](#repo-layout)
- [How the project is wired](#how-the-project-is-wired)
  - [Scenes \& flow](#scenes--flow)
  - [The Player](#the-player)
  - [Regions \& gates](#regions--gates)
  - [The NPC \& conversation](#the-npc--conversation)
  - [Dialogue \& tracing](#dialogue--tracing)
  - [The Camera](#the-camera)
  - [Input](#input)
  - [How the scripts talk to each-other](#how-the-scripts-talk-to-each-other)
- [Controls](#controls)
- [How to add new stuff](#how-to-add-new-stuff)
  - [Add a new region](#add-a-new-region)
  - [Add a new gate NPC](#add-a-new-gate-npc)
  - [Add a new conversation step / tracing task](#add-a-new-conversation-step--tracing-task)
  - [Add a new mechanic to the player](#add-a-new-mechanic-to-the-player)
  - [Add a new input action](#add-a-new-input-action)
  - [Make a brand-new scene](#make-a-brand-new-scene)
  - [Add a new script](#add-a-new-script)
- [Unity MCP server (optional)](#unity-mcp-server-optional)
- [Requirements](#requirements)

---

## Repo layout

```
gcet-game/
├── README.md                  # you are here
├── CLAUDE.md                  # scene-structure & design-philosophy notes (for Editor/AI reference)
├── AGENTS.md
├── gcet_game_unity/           # the Unity project root (open THIS in the Editor)
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   ├── StartScene.unity          # start screen → ComicScene
│   │   │   ├── ComicScene.unity          # opening comic panels (Space) → ControlsScene
│   │   │   ├── ControlsScene.unity       # controls reference overlay (Tab) → game1
│   │   │   ├── game1.unity               # the gameplay scene (regions + NPCs + dialogue + tracing)
│   │   │   ├── game1_test.unity          # test variant of the gameplay scene
│   │   │   ├── hanzi tracing base.unity  # the hanzi tracing scene (loaded additively on a Writing step)
│   │   │   ├── PauseScene.unity          # pause overlay (loaded additively on Escape)
│   │   │   ├── background.unity          # reusable background geometry (tilemap walls + grass)
│   │   │   └── template.unity            # authoring template for the tilemap background
│   │   ├── Scripts/
│   │   │   ├── PlayerMovement.cs         # WASD movement + region + wall clamping
│   │   │   ├── PlayerWalkAnimation.cs    # movement-driven player facing + bounce
│   │   │   ├── FollowCamera.cs           # smooth-follow camera, gate-aware region framing
│   │   │   ├── GameArea.cs               # a region: world bounds + player/camera clamp
│   │   │   ├── InvisibleWall.cs          # conversation-gated barrier between regions
│   │   │   ├── NpcController.cs          # proximity + E-to-talk NPC; unlocks its gate
│   │   │   ├── Conversation.cs           # per-NPC reusable dialogue + speaker presentation data
│   │   │   ├── Dialogue.cs               # the click-to-advance dialogue renderer (runtime TMP canvas)
│   │   │   ├── GameProgress.cs           # survives scene loads: trace state, wall unlocks, resume
│   │   │   ├── Script1.cs                # the hanzi stroke tracer (fires OnCharacterDone)
│   │   │   ├── CharacterData.cs          # ScriptableObject: one Hanzi's strokes + pinyin + meaning
│   │   │   ├── TracingCharacterHeader.cs # keeps the tracing header synced to the active character
│   │   │   ├── TracingMeaningOverlay.cs  # "you traced X = Y" card shown before returning
│   │   │   ├── ColorSprite.cs            # generates a solid-color square sprite at runtime
│   │   │   ├── VisibleBox.cs             # runtime visible test square (collider + tinted sprite)
│   │   │   ├── ComicSequence.cs          # click-through opening comic panels
│   │   │   ├── ControlsSceneEntry.cs     # starts gameplay from the controls screen
│   │   │   ├── ControlsOverlayController.cs # Tab-toggled additive controls reference overlay
│   │   │   ├── OpeningOverlay.cs         # centered title card shown before gameplay
│   │   │   ├── OpeningDialogue.cs        # one opening player monologue, first time only
│   │   │   ├── StartGame.cs              # start-screen Begin → ComicScene (+ music)
│   │   │   ├── BackgroundMusic.cs        # single looping music source across scene changes
│   │   │   ├── PauseController.cs        # Escape ↔ PauseScene toggle + timeScale
│   │   │   ├── PauseResume.cs            # resume hook on the pause screen's Resume button
│   │   │   ├── CameraFillSprite.cs       # stretches a sprite to fill the ortho camera viewport
│   │   │   ├── RainBackground.cs         # animated raindrop overlay
│   │   │   ├── IntroVideoPlayer.cs       # plays Intro Video.mov once at startup, then reveals menu
│   │   │   └── DialogueSceneChanger.cs   # small helper that loads the tracing scene by name
│   │   ├── CharacterBackground/          # speaker dialogue-frame artwork (player/soldier/gamevoice/multichoice)
│   │   ├── character_img/                # character portraits (xiaoyue* / soldier*)
│   │   ├── Settings/                     # URP2D render pipeline + renderer settings
│   │   │   ├── UniversalRP.asset
│   │   │   ├── Renderer2D.asset
│   │   │   └── Scenes/URP2DSceneTemplate.unity
│   │   ├── gcet_game_unity.inputactions  # Input System action asset (WASD etc.)
│   │   ├── misc/                         # misc editor/runtime assets (incl. the interact-E prompt sprite)
│   │   └── TextMesh Pro/                 # TMP resources, fonts (incl. LiberationSans SDF for pinyin)
│   ├── Packages/                         # manifest.json lists package dependencies
│   ├── ProjectSettings/                  # EditorBuildSettings.asset registers scenes
│   └── README_MCP.md                     # how to start the Unity MCP server
├── obs/                                  # Obsidian notes (game_loop canvas / diagram)
└── tmp-assets/                           # scratch folder for incoming art/assets
```

Key facts:

- **Unity version is pinned** to `6000.3.19f1` (`ProjectSettings/ProjectVersion.txt`). Use that exact version to avoid serialization upgrades.
- The project uses the **Input System** package (`com.unity.inputsystem`), *not* the old `Input` class. The action asset is `Assets/gcet_game_unity.inputactions`.
- Rendering is **URP 2D** (`com.unity.render-pipelines.universal` + `com.unity.feature.2d`).
- There is **no Assembly Definition** (`.asmdef`) in `Assets/` yet, so every script auto-compiles into `Assembly-CSharp`.
- `CLAUDE.md` captures the *scene structure* (object hierarchy, fileIDs, tilemap layout) and the design philosophy — read it alongside this file when editing scenes.

---

## How the project is wired

The gameplay lives in **`Assets/Scenes/game1.unity`** (with `game1_test.unity` as a test variant). The opening flow — start screen → comic → controls → game — lives in its own scenes and is covered under [Scenes & flow](#scenes--flow) below.

### Scenes & flow

The scenes registered in **File → Build Settings** form one opening flow plus the additive overlays the gameplay scene loads on demand:

| Scene | Role | How it's reached |
|-------|------|------------------|
| `StartScene` | Start screen with a **Begin** button | application entry |
| `ComicScene` | Opening comic panels; **Space** advances, then loads the next scene | from StartScene |
| `ControlsScene` | Controls reference; **Tab** starts gameplay | from ComicScene |
| `game1` | The gameplay scene — regions, NPCs, dialogue, tracing | from ControlsScene |
| `hanzi tracing base` | The hanzi tracing mini-game | additively from a dialogue Writing step |
| `PauseScene` | Pause menu | additively from the gameplay scene on **Escape** |
| `ControlsScene` (again) | Tab-toggled controls overlay during gameplay | additively from the gameplay scene |

`ControlsScene` serves double duty: opened once by the opening flow (single mode) and then toggled open/closed with **Tab** during gameplay (additive mode). `ControlsOverlayController` owns the Tab toggle during gameplay; `ControlsSceneEntry` owns it when the scene is opened standalone from the opening flow.

Two scenes are **not** in the build and are authoring/support only: `template.unity` (the tilemap background authoring template) and `background.unity` (reusable background geometry). `SampleScene.unity` is no longer the gameplay scene.

### The Player

The `Player` GameObject carries `SpriteRenderer` + `PlayerMovement` (+ optional `PlayerWalkAnimation`). `PlayerMovement` exposes one property other scripts rely on:

- **`MoveInput`** (Vector2) — the latest movement input after the device is read; drives the walk animation without re-polling the device.

The player is moved by `PlayerMovement` and then clamped in two layers each frame:

1. the current **region** (`GameArea.ClampPlayer`) keeps it inside that region's bounds, leaving a gap only on edges a wall covers;
2. every locked **wall** (`InvisibleWall.ClampPlayer`) then blocks or permits passage based on its lock state.

The player is frozen while a dialogue is open so they can't walk away mid-conversation. `PlayerWalkAnimation` (optional) adds a movement-driven horizontal flip + vertical bounce to a child visual without touching the collider.

### Regions & gates

The world is a set of rectangular **regions** (`GameArea`), each a trigger `BoxCollider2D` whose world-space bounds (honoring transform scale) define the area. There is one `GameArea` per region — the old `GameRegion` type no longer exists. `GameArea` keeps a static registry and answers `GetAreaContaining(point)`.

A **gate** (`InvisibleWall`) is a conversation-gated barrier on a shared edge between two regions. Pure logic — no sprite, renderer, or physical collider of its own (its `BoxCollider2D` is a trigger used as an editor handle). Its position and span are read straight from the GameObject: `WallX`/`WallY` = its `Transform.position`, span = its collider's long dimension in **world** space (accounts for transform scale). While locked, a player whose extent overlaps the wall is blocked from crossing in either direction and an on-screen popup ("you cannot go through there") shows; `Unlock()` flips `Locked` to false so the player passes.

Gating math:

- `GameArea.ClampPlayer` leaves a gap on any edge a locked wall covers (via `InvisibleWall.CoversHorizontalEdge` / `CoversVerticalEdge`).
- `InvisibleWall.ClampPlayer` pins each player's side to their **previous-frame** position so a fast mover can't tunnel through a thin wall in one frame.
- A wall counts as the gate for a region edge only when its **center** lies inside the edge's run — this stops an overlap-neighbour's gate from registering as a second wall.

Walls are authored by hand in the Scene and are **never moved or resized at runtime.** `SnapToSharedEdge` is an opt-in Editor-only `[ContextMenu]` action for placing a wall, not something that runs on every reload. No wall is spawned at runtime.

### The NPC & conversation

Each gate NPC is a GameObject with a `Collider2D`, `SpriteRenderer`, `NpcController`, and a sibling **`Conversation`** component that holds that NPC's reusable dialogue + speaker presentation. `NpcController` drives proximity + **E-to-talk**:

- a floating "Press E" prompt (a sprite, or a TMP text fallback) hovers above the NPC while the player is in range;
- the prompt is shown only until the player talks to that NPC once, then is suppressed for good (tracked by `hasSpokenBefore`, persisted across scene reloads by `GameProgress`);
- pressing **E** in range unlocks that NPC's gate wall and opens its conversation.

`Conversation` is data-driven: it holds the first-time `steps`, optional `repeatSteps`, per-speaker dialogue-frame backgrounds, per-speaker-expression portraits, auxiliary panel art, and the gate-unlock assignment (`wallToUnlock`, `regionToUnlock`). If a scene `Conversation` has empty steps it falls back to a full default conversation. The NPC's *lines* live on `Conversation`; `NpcController` only manages proximity / open / advance — so a new NPC gets a new `Conversation` in the scene, not a new script.

### Dialogue & tracing

**`Dialogue`** is the click-to-advance renderer. It builds a runtime TMP `Canvas` (no hand-authored prefab) and renders speaker-specific dialogue-frame art along the bottom of the screen, with a name tag, portrait, and clickable choices. Advancing reads the mouse or **Enter**/**Space**; choice steps read the mouse or number keys **1–9**. It is a singleton (`Dialogue.Instance`); the conversation's content is injected into it by the NPC.

The conversation is data-driven. One step's action `DialogueAction.Writing` hands off to the hanzi scene through `GameProgress`:

1. `Dialogue` calls `GameProgress.BeginTrace(...)`, which records the step to resume at, the trace owner (which NPC), and the player's current position, then loads `hanzi tracing base` (the tracing scene).
2. **`Script1`** (in the tracing scene) is the stroke tracer. Each time a character is completed correctly it fires `OnCharacterDone`. `GameProgress` listens and counts completions toward the requested count.
3. Once every requested character is traced, `GameProgress.OnTraceCorrect` reloads the main scene. The reload re-creates every wall locked and re-spawns the player, so `GameProgress` (which survives via `DontDestroyOnLoad`) restores the saved player position and re-applies every previously-unlocked wall.
4. The NPC whose `Conversation` owns the trace re-opens its dialogue at the saved step (`NpcController.RefreshFromProgress` → `Dialogue.ResumeAfterWriting`), and a `DialogueAction.GoTop` step tells the player to head to the next region.

`CharacterData` is a `ScriptableObject` holding one Hanzi's strokes, pinyin, and meaning; `TracingCharacterHeader` and `TracingMeaningOverlay` show the active character and the post-trace meaning card in the tracing scene.

**`GameProgress`** is the cross-scene contract. It survives scene loads and owns: tracing state, the set of opened walls (by stable key derived from each wall's authored position), completed-conversation keys, the one-time opening-dialogue flag, and the saved player position. It is auto-created before the first scene loads (`RuntimeInitializeOnLoadMethod`).

### The Camera

`FollowCamera` sits on the Main Camera. Every frame (`LateUpdate`) it smooth-follows the player and clamps the result to the current region's bounds (`GameArea.ClampCamera`). The region is resolved from the player's position each frame, and the transition is **gated**: the camera only snaps to a new region once the player has actually crossed the shared boundary (i.e. no locked wall still blocks it) — so the player is never caught half off-screen on the far side of a gate. If the player is outside every region (in a gap) the camera clamps to the nearest region so void is never shown.

### Input

Control scheme comes from `Assets/gcet_game_unity.inputactions`:

- **`Player` action map → `Move` (Vector2)**, bound to the **WASD** composite (also arrow keys, gamepad stick, etc.).
- There are also `Look` and `Fire` actions and a `UI` map, unused by the current scripts.

`PlayerMovement` uses the `Player`/`Move` action from the asset if the asset is wired into the Inspector's `inputActions` field; if that field is empty it **automatically builds the same WASD action in code**, so movement works even with a blank field. Other input (E, Space, Enter, Tab, Escape, number keys, mouse) is read directly through the Input System `Keyboard`/`Mouse` singletons, not through the action asset.

### How the scripts talk to each other

```
[Opening flow]
StartScene --Begin--> ComicScene --Space--> ControlsScene --Tab--> game1

[game1 gameplay]
[WASD / Input System]
        │
        ▼
PlayerMovement ─── asks GameArea.GetAreaContaining() for the current region
   │                └─ GameArea.ClampPlayer leaves gaps where InvisibleWall gates an edge
   │                └─ InvisibleWall.ClampPlayer blocks locked walls (prev-pos anti-tunnel)
   └─ player position ──► FollowCamera
                            ├─ smooth-follows player
                            └─ clamps to current region (gated region transition)

[NPC interaction]
player in range + E ──► NpcController
                          ├─ unlocks Conversation.WallToUnlock (the gate)
                          ├─ injects Conversation's steps into Dialogue.Instance
                          └─ Dialogue.Open()

[Dialogue]
click / Space / Enter ──► advance / pick a choice
   └─ a Writing step ──► GameProgress.BeginTrace()
                            ├─ saves resume step + player position
                            └─ loads "hanzi tracing base"

[hanzi tracing base]
Script1 traces strokes ──► OnCharacterDone ──► GameProgress.OnTraceCorrect()
   └─ all characters done ──► reload main scene
                               ├─ GameProgress restores player position
                               ├─ GameProgress re-applies unlocked walls
                               └─ owning NPC resumes Dialogue at the saved step
```

- **Coordinate/gameplay logic is split by responsibility, never duplicated:**
  - `GameArea` owns region bounds, the region registry, and the player/camera clamp.
  - `InvisibleWall` owns gate state + the geometric gating queries `GameArea` asks.
  - `PlayerMovement` owns movement + asks the region and walls whether they block; it hard-codes no coordinates.
  - `NpcController` owns proximity/talk and unlocks the gate; `Conversation` owns the per-NPC dialogue data.
  - `Dialogue` owns rendering; `GameProgress` owns cross-scene persistence.
  - `FollowCamera` resolves the region from the player and clamps to it, gated by the walls.

---

## Controls

| Key | Action |
|-----|--------|
| `W` / `↑` | move up |
| `S` / `↓` | move down |
| `A` / `←` | move left |
| `D` / `→` | move right |
| `E` | talk to a nearby NPC (shows a "Press E" prompt while in range) |
| `Space` / `Enter` / mouse click | advance dialogue; advance comic panels; dismiss the trace meaning card |
| `1`–`9` | pick a dialogue choice directly |
| `Tab` | toggle the controls reference overlay |
| `Escape` | toggle pause |

Gamepad left stick is bound too via the Input System asset (but only takes effect once the asset is wired into the `inputActions` field on the Player, since the in-code fallback is keyboard-only).

---

## How to add new stuff

### Add a new region

Regions are self-contained: each `GameArea` registers itself and owns its bounds + clamps. To add a region:

1. Add a GameObject in `game1` with a `GameArea` + a trigger `BoxCollider2D`; size and position the collider to the region's bounds. (The collider is the editor handle — gating reads `col.bounds`, which honors transform scale, so always size in world units.)
2. The camera, player, and wall-gating pick it up automatically via `GameArea.GetAreaContaining` / `GetRegistered`. No other code changes needed.
3. Give it a gate NPC (see below) and an `InvisibleWall` on the shared edge to the region the player arrives from.

### Add a new gate NPC

1. Add a GameObject with a `Collider2D`, `SpriteRenderer`, `NpcController`, and a sibling `Conversation`. Author the `Conversation`'s steps / backgrounds / portraits / auxiliary panels in the Inspector (or leave steps empty to use the default conversation).
2. Assign the `Conversation.wallToUnlock` to the `InvisibleWall` that genuinely spans that boundary (assign by the wall's fileID, explicitly — a `wallToUnlock` left empty falls back to an edge scan that can miss). The wall's center must lie within the edge's run.
3. Set the NPC's `traceOwnerKey` and enable `resumeAfterTracing` only on the NPC that launches/resumes the tracing task.
4. Verify the wall clears *all* adjoining regions' bounds (a wall's effective reach = `col.size × lossyScale`), not just the two it intends to gate.

### Add a new conversation step / tracing task

- Add a `DialogueStep` to the `Conversation.steps` list. Use `action = DialogueAction.Writing` with `traceCharacters` (exact `CharacterData` names) for a tracing task, `DialogueAction.GoTop` to point the player onward, and `choices` with `useMultipleChoicePanel` for multiple-choice steps.
- Make sure a `CharacterData` asset exists for each Hanzi you request (Create → Tracing → Character Data) and is added to the `Script1.availableCharacters` list in the tracing scene so the tracer can find it.

### Add a new mechanic to the player

- Edit `Assets/Scripts/PlayerMovement.cs`. Public serialized fields show up in the Inspector immediately.
- The two-layer clamp runs every frame from `Update()` — it queries the current region and all walls, so new regions/walls you add in the scene are enforced automatically.
- Keep the single-source-of-truth rule: **region/gate data lives on the `GameArea`/`InvisibleWall` components**; `PlayerMovement` only asks them, and `FollowCamera` only asks `GameArea`.

### Add a new input action

1. Open `Assets/gcet_game_unity.inputactions` (double-click in Unity, or edit the JSON).
2. Add an action under the `Player` map (e.g. `Interact`, type `Button`).
3. In code: `var interact = inputActions.FindActionMap("Player").FindAction("Interact");` then `interact.performed += ctx => …`. The `PlayerMovement.Awake` already shows the `FindActionMap`/`FindAction` pattern.
4. For analog/2D actions use a composite like the existing `Move`, or build one in code as the WASD fallback does.

### Make a brand-new scene

1. **File → New Scene** → choose the **URP2D** template (keeps the correct camera + light setup).
2. **File → Save As** under `Assets/Scenes/`.
3. Add it to the build via **File → Build Settings → Add Open Scenes** (so `SceneManager.LoadScene` / the additive overlays work).
4. Copy the `Player` object (and its components) across with **Ctrl+D** in the Hierarchy, or better, make it a prefab: drag it from the Hierarchy into `Assets/`, then drop the prefab into every new scene.
5. Also copy `FollowCamera` onto the camera (or prefab the camera), and add a `ControlsOverlayController` + `PauseController`/pause wiring if the scene is a gameplay scene. New scenes then add their own region/NPC objects following the patterns above.

### Add a new script

1. Create `Assets/Scripts/YourScript.cs`. Because there's no `.asmdef`, it compiles into the main assembly automatically — no references to add.
2. Add `[RequireComponent(typeof(…))]` if it depends on another component; Unity will add it automatically.
3. Use the existing scripts as a pattern for how to find other objects:
   - objects it owns → `GetComponent<T>()`
   - the player or camera → `FindObjectOfType<PlayerMovement>()` / `Camera.main`
   - the current region → `GameArea.GetAreaContaining(point)`
4. Expose tunable knobs as `[SerializeField] private` fields so they show in the Inspector.

---

## Unity MCP server (optional)

Unity's official MCP package turns the **running Unity Editor into an MCP server** so an AI client can query/create/edit scenes and assets directly. It's bundled in this project inside `com.unity.ai.assistant` — no install needed.

To use it: open the project in Unity, then **Window → Unity AI → Open MCP Console**, and point Claude Desktop / Cursor / Windsurf at the running server. Full steps are in `gcet_game_unity/README_MCP.md`.

https://unity.com/blog/unity-ai-mcp-how-to-get-started

---

## Requirements

- **Unity Hub** + **Unity Editor 6000.3.19f1** with the following modules for your platform:
  - Windows/macOS/Linux Build Support
- Recommended IDE: **JetBrains Rider** or **Visual Studio 2022** (both configured in `ProjectSettings`).
- To open the project: clone the repo, open Unity Hub, **Add** the `gcet_game_unity` folder, and launch with version `6000.3.19f1`.
