---
name: invisiblewall-task-2026-07-14
description: Context for the 2026-07-14 invisible-wall feature in game1 scene (area grid, unlock pipeline, scripts)
metadata:
  type: project
---

Task (2026-07-14): add a conversation-gated invisible wall to the game1 scene.

Facts:
- game1 scene uses orthographic camera size 5 → viewport H=10, W≈17.78 (16:9). GameArea.FitToCamera derives cell size from `Camera.main.orthographicSize`/`aspect`.
- game1 has 3 Areas in an L-shape: Area_Left (0,0) right-open, Area_Right (1,0), Area_Top (0,1). Cell (1,1) is EMPTY. No NPC currently in the scene (conversation flow exists in code only).
- Area.ClampPlayer gates exits by neighbor-existence + an unlock flag (rightExitUnlocked/topExitUnlocked).
- Unlock pipeline: NPC → Dialogue → Writing step → trace scene → Script1.OnCharacterDone → GameProgress.OnTraceCorrect → reload scene → ApplyForwardUnlock() → UnlockTopExit(targetCol,targetRow).
- User wants: (1) retire area exit limits, (2) invisible wall on TOP edge of cell (1,1) (horizontal), (3) locked → bump shows minimal popup "you cannot go through there", (4) unlocks via the same conversation/trace path.
- New InvisibleWall component: derives world pos from camera viewport, static registry + ClampPlayer, no SpriteRenderer. Popup = minimal overlay Canvas+TMP (no portrait/name), positioned at wall screen pos, clamped to screen.
- Area.ClampPlayer changed to: exit open iff destination cell has a neighbor Area OR is governed by InvisibleWall (GameArea.GetAreaAt || InvisibleWall.GetWallAt).
- GameProgress: bootstrap wall on main-scene load + unlock it in ApplyForwardUnlock (added vestigial wallCol/wallRow). UnlockTopExit still referenced but no longer the active gate.

Note: unlock pipeline is wired, but an NPC/conversation (Writing step) in the scene is what triggers it — none in scene as of this task.

BUG FIX (follow-up): top-right cell (1,1) could not be entered even though the wall should only block exiting it. Root cause: the scene has NO game1 GameObject and my wall-bootstrap lived in GameProgress.OnSceneLoaded — but GameProgress is only created on a conversation's writing step, so it never existed on first load → wall never spawned → GetWallAt(1,1)==null → IsExitOpen(1,1)==false → door *into* the cell was sealed too.
Fix: added [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] AutoCreate() to GameProgress so the inert singleton exists from frame 1, guaranteeing the wall bootstraps on the very first scene-loaded. Same runtime-singleton pattern the project already uses in LaunchWriting.

Confirmation needed: this is gameplay code, can't compile/run outside the Unity Editor. At least one NPC + Conversation with a Writing step must be added to game1 to actually flip the wall open.
