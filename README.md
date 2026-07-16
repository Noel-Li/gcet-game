# gcet-game

A 2D top-down game built in **Unity Engine (6000.3.19f1)** using the Universal 2D (URP2D) render pipeline.

Design doc: https://docs.google.com/document/d/1kAJAHLvWIgtqb_OMR9vzJOyzMddIAvhJqlZOyhzepEw/edit?tab=t.sfvi1brsrj8m

> Commit and push to this repo to save progress for version control.

---

## Table of contents

- [Repo layout](#repo-layout)
- [How the current scene is wired](#how-the-current-scene-is-wired)
  - [Scenes \& rendering](#scenes--rendering)
  - [The Player](#the-player)
  - [The Areas \& Regions](#the-areas--regions)
  - [The NPC](#the-npc)
  - [The Camera](#the-camera)
  - [Input](#input)
  - [How the scripts talk to each other](#how-the-scripts-talk-to-each-other)
- [Controls](#controls)
- [How to add new stuff](#how-to-add-new-stuff)
  - [Add a new Area / level area](#add-a-new-area--level-area)
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
├── gcet_game_unity/           # the Unity project root (open this in the Editor)
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   └── SampleScene.unity   # the only scene right now — everything lives here
│   │   ├── Scripts/
│   │   │   ├── PlayerMovement.cs   # WASD movement + keeps the player out of gated regions
│   │   │   ├── PlayerWalkAnimation.cs # movement-driven player flip + bounce
│   │   │   ├── ControlsOverlayController.cs # Tab-toggled additive controls reference
│   │   │   ├── GameArea.cs          # an Area on the world grid: bounds + camera clamp + region registry
│   │   │   ├── GameRegion.cs       # a gated sub-area inside an Area (blocked until unlocked)
│   │   │   ├── NpcController.cs     # clickable NPC: changes color + unlocks a region
│   │   │   ├── FollowCamera.cs      # smooth-follow camera clamped to the current Area
│   │   │   └── ColorSprite.cs      # generates a solid-colored square sprite at runtime
│   │   ├── Settings/               # URP2D render pipeline + renderer settings
│   │   │   ├── UniversalRP.asset
│   │   │   ├── Renderer2D.asset
│   │   │   └── Scenes/URP2DSceneTemplate.unity
│   │   ├── gcet_game_unity.inputactions  # Input System action asset (WASD etc.)
│   │   └── Square.png              # a plain white square texture (sprite)
│   ├── Packages/                   # manifest.json lists package dependencies
│   ├── ProjectSettings/            # EditorBuildSettings.asset registers scenes
│   └── README_MCP.md               # how to start the Unity MCP server
├── obs/                            # Obsidian notes (game_loop canvas / diagram)
└── tmp-assets/                     # scratch folder for incoming art/assets
```

Key facts:

- **Unity version is pinned** to `6000.3.19f1` (`ProjectSettings/ProjectVersion.txt`). Use that exact version to avoid serialization upgrades.
- The project uses the **Input System** package (`com.unity.inputsystem`), *not* the old `Input` class. The action asset is `Assets/gcet_game_unity.inputactions`.
- Rendering is **URP 2D** (`com.unity.render-pipelines.universal` + `com.unity.feature.2d`).
- There is **noAssembly Definition** (`.asmdef`) in `Assets/` yet, so every script auto-compiles into `Assembly-CSharp`.

---

## How the current scene is wired

The whole game so far lives in one scene — `Assets/Scenes/SampleScene.unity` — built around the default URP2D template (orthographic camera + a global 2D light).

### Scenes & rendering

- One scene is registered in **File → Build Settings**: `Assets/Scenes/SampleScene.unity`.
- **Main Camera** is orthographic, size `5` (so the visible world is 10 units tall). It has the `UniversalAdditionalCameraData` component expected by URP.
- **Global Light 2D** (type = `Global`) lights everything with no shadows — flat colored shapes.
- Each zone/player is a **`SpriteRenderer` using Unity's built-in sprite material** (`Default Sprite`), drawn with a solid color and no texture detail. `ColorSprite.cs` generates the square sprite itself at runtime so the scene has **no external sprite/material dependencies**.

### The Player

A white square (`Player` GameObject) at world position `(0, -5, 0)`. Components:

| Component | Purpose |
|-----------|---------|
| `SpriteRenderer` | draws the white square (sort order 1, above the zones) |
| `ColorSprite` | generates a 1×1 white sprite at runtime and tints it |
| `PlayerMovement` | WASD movement + keeps the screen inside the current zone |

`PlayerMovement` exposes one important toggle in the Inspector:

- **`canPassThroughBoundary` (bool, default `true`)** — when on, the player may walk up across y = 0 into the top zone and the camera follows. When off, the player is locked to whichever zone they started in.

Hard rule the script enforces: **the player never leaves the camera view.** Vertically it's clamped to the active zone (one camera-height tall); horizontally it's clamped to the current aspect ratio.

### The Areas & Regions

The world is a grid of **Areas** (`GameArea`), each a 24×24 world-unit block (bigger than the 10-tall camera view, so the camera can pan inside it). Areas are named `Area_<col>_<row>`:

| GameObject | World pos | Notes |
|------------|-----------|-------|
| `Area_1_1` | `(0, 0)` | starting Area, holds two Regions |
| `Area_2_1` | `(24, 0)` | open Area to the right (no regions) |

Each Area has a base-color background (a child named `Background`). Areas tile edge-to-edge, so the camera never shows void: at a seam it pans smoothly across valid content, and at an outer edge it **holds** while the player walks right up to the boundary. Use `GameArea.areaSize` to resize and the grid coords to place new Areas.

A **Region** (`GameRegion`) is a gated sub-area inside an Area. In `Area_1_1`:

| GameObject | Size | Color | Gate |
|------------|------|-------|------|
| `Region_Bottom` | 24×12 at `(0,-6)` | blue | open |
| `Region_Top` | 24×12 at `(0,6)` | orange | **gated (locked)** |

A Region whose `isGated` is on and `IsUnlocked` is off blocks the player (`GameRegion.IsBlocking`). The blocker is enforced by `PlayerMovement`, which pushes the player out of any locked Region. An NPC unlocks a Region — see below. Both type and state live on the `GameRegion` component, so gating is data-driven, not hard-coded.

### The NPC

`NPC` is a square with a `BoxCollider2D` + `NpcController`. Clicking it (requires the collider) changes its color to `activatedColor` and calls `Unlock()` on its `linkedRegion` (wired in the Inspector → `Region_Top`). Click again does nothing. To add more, duplicate the NPC, point `linkedRegion` at a different gated Region, and set its colors.

### The Camera

`FollowCamera` sits on the Main Camera. Every frame (`LateUpdate`) it smooth-follows the player (`Mathf.SmoothDamp`) and then **clamps** the result to the current Area's bounds via `GameArea.ClampCameraToArea`. So the camera follows but never shows beyond the Area — near an edge the camera stops while the player keeps walking to the boundary. If the player leaves every Area (e.g. into the void) the camera keeps using the last Area so void is never shown. The current Area is resolved from the player's position each frame via `GameArea.GetAreaContaining`.

### Input

Control scheme comes from `Assets/gcet_game_unity.inputactions`:

- **`Player` action map → `Move` (Vector2)**, bound to the **WASD** composite (also arrow keys, gamepad stick, etc.).
- There are also `Look` and `Fire` actions and a `UI` map, unused by the current scripts.

`PlayerMovement` tries to use the `Player` / `Move` action from the asset if the asset is wired into the Inspector's `inputActions` field; if that field is empty it **automatically builds the same WASD action in code**, so movement works even with a blank field. (The field is currently left blank on purpose — see `README_MCP.md` note; drag the asset in if you also want gamepad bindings.)

### How the scripts talk to each other

```
[WASD / Input System]
        │
        ▼
PlayerMovement ─── asks GameArea.GetAreaContaining() for current Area
   │                └─ asks each GameRegion.IsBlocking → pushes player out of locked ones
   │
   └─ player position ──► FollowCamera
                            ├─ smooth-follows player
                            └─ clamps to current Area bounds via GameArea.ClampCameraToArea

[NPC click] ──────► NpcController.OnMouseDown
                       ├─ recolor NPC
                       └─ linkedRegion.Unlock()
```

- **Coordinate/gameplay logic is split by responsibility, never duplicated:**
  - `GameArea` owns world bounds, the region registry, and the camera clamp. It keeps a static list of all Areas and answers `GetAreaContaining(point)`.
  - `GameRegion` owns its own size/color and gate state (`IsGated`, `IsUnlocked` → `IsBlocking`).
  - `PlayerMovement` owns movement + asks the current area's regions whether they block; it hard-codes no coordinates.
  - `FollowCamera` resolves the current Area from the player and clamps to it.
- The Area/Region GameObjects are purely visual; all gameplay reads the `GameArea`/`GameRegion` components.

---

## Controls

| Key | Action |
|-----|--------|
| `W` / `↑` | move up |
| `S` / `↓` | move down |
| `A` / `←` | move left |
| `D` / `→` | move right |

Gamepad left stick is bound too via the Input System asset (but only takes effect once the asset is wired into the `inputActions` field on the Player, since the in-code fallback is keyboard-only).

---

## How to add new stuff

### Add a new Area / level area

Areas are self-contained: each `GameArea` registers itself and owns its bounds + camera clamp. To add an Area to the right of the start:

1. Duplicate `Area_2_1`, set its `areaCol`/`areaRow` (e.g. `3, 1`), and move its Transform to the new center (e.g. `(48, 0)`). Set `areaSize` and `areaColor` as you like.
2. The camera and player automatically pick it up — `GameArea.GetAreaContaining` finds it by bounds, and `FollowCamera` clamps to it. No other code changes needed.
3. To add a Region inside it, add a child GameObject with a `GameRegion` + a `Background` child (SpriteRenderer + `ColorSprite`), set its `regionSize`/color, and toggle `isGated`. Wire any NPC's `linkedRegion` to it.

To add a gated Region to an existing Area: add a `GameRegion` child with a `Background`, set `isGated = true` and `startsUnlocked = false`, and point an NPC's `linkedRegion` at it.

### Add a new mechanic to the player

- Edit `Assets/Scripts/PlayerMovement.cs`. Public serialized fields show up in the Inspector immediately (e.g. add a `runSpeed`, a `jump`, or collectible counters).
- Gate clamping runs every frame from `Update()` through `ClampToGates` — it queries the current Area's regions, so new Areas/Regions you add in the scene are enforced automatically.
- Keep the single source of truth rule: **Area/Region data lives on the `GameArea`/`GameRegion` components**; `PlayerMovement` only asks them (`IsBlocking`), and `FollowCamera` only asks `GameArea` for the clamp.

### Add a new input action

1. Open `Assets/gcet_game_unity.inputactions` (double-click in Unity, or edit the JSON).
2. Add an action under the `Player` map (e.g. `Interact`, type `Button`).
3. In code: `var interact = inputActions.FindActionMap("Player").FindAction("Interact");` then `interact.performed += ctx => …`. The `PlayerMovement.Awake` already shows the `FindActionMap` / `FindAction` pattern.
4. For analog/2D actions use a composite like the existing `Move`. To build composites in code, see the WASD fallback in `PlayerMovement.Awake` (uses `AddCompositeBinding("2DVector").With(…)`).

### Make a brand-new scene

1. **File → New Scene** → choose the **URP2D** template (keeps the correct camera + light setup).
2. **File → Save As** under `Assets/Scenes/`.
3. Add it to the build via **File → Build Settings → Add Open Scenes** (so `SceneManager.LoadScene` / application entry works).
4. Copy the `Player` object (and its components) across with **Ctrl+D** in the Hierarchy, or better, make it a prefab: drag it from the Hierarchy into `Assets/`, then drop the prefab into every new scene.
5. Also copy `FollowCamera` onto the camera (or prefab the camera). New scenes then add their own Area/Region/NPC objects following the patterns above, or prefab `Area_1_1` / the NPC and reuse them.

### Add a new script

1. Create `Assets/Scripts/YourScript.cs`. Because there's no `.asmdef`, it compiles into the main assembly automatically — no references to add.
2. Add `[RequireComponent(typeof(…))]` if it depends on another component; Unity will add it automatically.
3. Use the existing scripts as a pattern for how to find other objects:
   - objects it owns → ` GetComponent<T>()`
   - the player or camera → `FindObjectOfType<PlayerMovement>()` / `Camera.main` (see `ZoneCamera.Reset` / `PlayerMovement.Awake`)
4. Expose tunabl$e knobs as `[SerializeField] private` fields so they show in the Inspector.

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
