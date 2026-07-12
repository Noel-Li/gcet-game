# Design Spec: Mouse-Drawn Chinese Character Recognition
**Target:** Unity 6000.3.19f1, Universal 2D project template (URP)
**Author:** design draft for handoff / implementation review
**Status:** Ready for implementation review — see "Open Questions" before coding starts

---

## 1. Goal

Let the player draw an arbitrary Chinese character with the mouse (stroke by stroke, or freeform), and have the game determine whether the drawing is correct — matching both the **shape** and the **stroke order** of a reference character. The system must generalize to *any* character in a dataset, not be hand-authored per character. That means the real deliverable is a **pipeline** that converts a source of character stroke data into a Unity-usable asset, plus a **runtime comparison algorithm** that works generically on that asset format.

Two runtime modes are specified:
- **Practice Mode** (recommended default): reference strokes are shown faintly on screen one at a time; the player draws each stroke and gets instant right/wrong feedback before advancing.
- **Assessment Mode** (harder, optional): no guide shown; player draws the whole character; the system aligns and scores all strokes at the end.

---

## 2. Data Source: turning "images of characters" into reusable game data

Raw pixel images of glyphs are the wrong input to derive stroke order from — stroke order is not recoverable reliably from a bitmap. Two data acquisition paths are specified, in priority order.

### 2.1 Path A (preferred): use an existing structured stroke-order dataset

There are open, machine-readable datasets that already encode, per character: the stroke count, the correct order, and a "median" (a centerline polyline per stroke — essentially the skeleton path a pen would follow), plus filled outline paths for rendering. This is exactly the data model handwriting-practice apps use, and it removes the need to invent stroke segmentation/ordering from scratch.

**Action item for implementer:** locate and vet a dataset of this kind (search terms: "hanzi stroke order data json", "stroke medians dataset", "cjk stroke order svg"). Before shipping, confirm:
- the license permits redistribution inside a commercial/non-commercial game (attribution requirements, share-alike clauses, etc.)
- the data's coordinate system and character coverage (simplified vs. traditional, how many thousand characters)

This spec does not hardcode a specific dataset name/version because licensing terms and availability should be verified directly at implementation time rather than assumed.

### 2.2 Path B (fallback): derive from font glyph images when no dataset entry exists

For a character missing from Path A, or for stylistic/decorative glyphs:
1. Render the glyph from a TTF/OTF font to a high-resolution grayscale bitmap (e.g., 512×512), fully anti-alias-free (binarized black/white).
2. Skeletonize the binary shape (Zhang–Suen thinning or similar) to get a 1-pixel-wide centerline graph.
3. Split the skeleton into stroke segments at branch/junction points and endpoints.
4. **Do not trust automatic stroke ordering** — heuristics (top-before-bottom, left-before-right, horizontal-before-vertical, etc.) get real characters wrong often enough to matter. Feed the extracted segments into a small in-editor review tool where a human drags them into the correct order and can discard bad segments. Save the reviewed result in the same schema as Path A.
5. Tag these assets as `sourced: "generated"` vs `"dataset"` so QA knows which ones need spot-checking.

Path B exists so the pipeline is not blocked on dataset coverage, but Path A should be the primary source whenever the character is available there.

### 2.3 Canonical intermediate schema (source-agnostic JSON)

Both paths converge on one JSON shape before import into Unity. Coordinates are normalized to a 0–1 square (origin top-left, matching typical font/SVG convention; convert to Unity's bottom-left convention during import).

```json
{
  "character": "水",
  "unicode": "6C34",
  "strokeCount": 4,
  "source": "dataset",         // "dataset" | "generated"
  "strokes": [
    {
      "index": 0,
      "medianPoints": [[0.51, 0.05], [0.50, 0.30], [0.49, 0.55]],
      "outlinePath": "M ... Z"   // optional, for reference-stroke rendering fill
    }
  ]
}
```

- `medianPoints`: ordered polyline sampled along the stroke's centerline, in drawing direction (start point first). This is the array actually used for comparison.
- `outlinePath`: optional SVG path string used only to render a solid/pretty reference glyph on screen; not used in scoring.

### 2.4 Unity import: ScriptableObject + importer tool

Define a runtime/editor-shared asset:

```csharp
[CreateAssetMenu(menuName = "HanziWriting/CharacterStrokeData")]
public class CharacterStrokeData : ScriptableObject
{
    public string character;
    public int unicodeCodepoint;
    public StrokeData[] strokes;
    public string sourceType; // "dataset" or "generated"
}

[System.Serializable]
public class StrokeData
{
    public Vector2[] medianPoints; // normalized 0..1, Unity coord convention
}
```

Build an editor-only tool, `HanziStrokeImporter`, that:
- Accepts a list of Unicode codepoints (or "import all") plus a path to the source JSON/dataset file(s).
- For each codepoint, looks up the matching entry, converts coordinate convention, and creates/updates a `CharacterStrokeData` asset under `Assets/Resources/CharacterData/{codepoint}.asset` (Resources folder so it can also be loaded by codepoint at runtime via `Resources.Load`).
- Is idempotent/re-runnable — running it again for the same character overwrites cleanly (satisfies "can be reapplied" requirement: the same tool works for 1 character or 20,000).
- Logs any codepoint it couldn't find so a human can route it to Path B.

At runtime, a `CharacterDatabase` singleton/service maps a requested Unicode codepoint to a loaded `CharacterStrokeData`, lazily via `Resources.Load($"CharacterData/{codepoint}")`, so new characters can be added to the project later without touching game code.

---

## 3. Runtime Architecture

### 3.1 Input capture

`StrokeInputRecorder` (MonoBehaviour on the drawing canvas):
- On `mouse down` inside the drawing area: start a new stroke, clear point buffer.
- On `mouse drag`: append the current pointer position (converted to the canvas's local normalized 0–1 space) to the buffer, throttled by minimum distance-since-last-point (e.g., 0.01 in normalized units) to avoid oversampling and to keep noise down.
- On `mouse up`: close the stroke, hand the finished point list off to the validator, and fire an event (`OnStrokeCompleted(List<Vector2> points)`).

Use `Camera.ScreenToViewportPoint` or a `RectTransformUtility.ScreenPointToLocalPointInRectangle` against a fixed-size drawing `RectTransform`, then remap to 0–1, so the drawing area's fixed size *is* the shared coordinate frame with the reference data (this sidesteps needing to re-normalize scale/position per stroke later — see §4.1).

### 3.2 Visual feedback

- Render the reference character faintly (e.g., 20% opacity outline, built from `outlinePath` triangulated to a mesh, or simply the stroke medians drawn as thick faded lines) as a guide layer, toggle-able per mode.
- Render the player's live stroke with a `LineRenderer` (world space, positioned to match the drawing canvas) or a procedurally extruded mesh for a "brush" look.
- On a stroke's validation result, flash the guide stroke green (correct) or red (incorrect, please retry) in Practice Mode.

### 3.3 Session state machine

`CharacterWritingSession` component per attempt:
```
Idle -> Showing(strokeIndex=0)
  Showing(i) --draw+validate ok--> Showing(i+1)  [or Complete if i+1 == strokeCount]
  Showing(i) --draw+validate fail--> Showing(i)  [retry same stroke, feedback shown]
Complete -> report(score, perStrokeResults)
```
Assessment Mode is the same machine minus the per-stroke gating: all strokes are collected first (player signals "done," or stroke count reaches the reference count), then §4.3's alignment routine runs once over the whole set.

---

## 4. Verification Algorithm

### 4.1 Normalization

Because the drawing canvas is defined as the same fixed rectangle the reference data was normalized against, position and scale are already comparable — **do not** rescale/recenter the player's stroke to fit the reference stroke's own bounding box. Proportion and placement are part of what makes a character "correct" (e.g., relative size of radicals), so over-normalizing would hide real mistakes.
Allow only a small centroid-shift tolerance (e.g. up to 3–5% of canvas size) to absorb ordinary mouse imprecision, not gross repositioning.

### 4.2 Resampling

Both the reference `medianPoints` and the player's captured stroke are resampled to a fixed number of points (e.g., N = 32) via arc-length interpolation (equal spacing along the path length, not equal time/index spacing), so comparison isn't biased by drawing speed or point density.

### 4.3 Per-stroke scoring (Dynamic Time Warping)

Compare the two resampled point sequences with DTW rather than a rigid index-to-index Euclidean distance, so minor differences in relative spacing along the stroke (e.g., a slightly longer straight segment before a turn) don't get over-penalized while still requiring the same *sequence* of shape features:

```
function DTWDistance(refPoints[N], userPoints[M]):
    cost[0][0] = 0
    for i in 1..N: cost[i][0] = infinity
    for j in 1..M: cost[0][j] = infinity
    for i in 1..N:
        for j in 1..M:
            d = euclidean(refPoints[i], userPoints[j])
            cost[i][j] = d + min(cost[i-1][j], cost[i][j-1], cost[i-1][j-1])
    return cost[N][M] / (N + M)   // normalize by path length to get avg per-point cost
```

Because sequences are compared in original recorded order (start → end) with no reversal test, a stroke drawn backwards naturally scores poorly — this implicitly enforces stroke **direction**, which matters for correct handwriting, without a separate check.

Also compare **stroke length ratio** (`len(user) / len(ref)`, using summed segment lengths pre-resample) and reject strokes where the ratio is outside a tolerance band (e.g., 0.5–1.8×) even if DTW cost looks acceptable — this catches strokes that are the wrong scale but happen to align well pointwise (e.g., a tiny scribble in the right spot).

A stroke passes if:
```
avgDTWCost <= shapeTolerance   AND   lengthRatio in [minRatio, maxRatio]   AND   centroidShift <= positionTolerance
```
All three thresholds should be exposed as tunable `[SerializeField]` values on a `CharacterValidationSettings` asset, not hardcoded, so difficulty can be adjusted per age group / game difficulty setting without code changes.

### 4.4 Stroke count / order handling

- **Practice Mode:** trivial — the game only ever asks for stroke `i`, so count/order mismatches can't occur by construction; only shape correctness of the current stroke is being judged.
- **Assessment Mode:** if the player's stroke count differs from the reference:
  - If player count == reference count: compare index-for-index (order is exactly what's being tested).
  - If player count != reference count: run a Needleman–Wunsch-style sequence alignment where the "substitution cost" between reference stroke *i* and player stroke *j* is the DTW cost from §4.3, and insertion/deletion has a fixed penalty (representing a missing or extra stroke). This produces a best-effort alignment and a clear diagnostic ("expected 4 strokes, you drew 5 — stroke 3 looks extra") rather than a flat failure. Treat this alignment step as a stretch goal; an MVP can simply reject on count mismatch with a "wrong number of strokes" message and let the player retry.

### 4.5 Overall result

`CharacterAttemptResult`:
```csharp
public class CharacterAttemptResult
{
    public bool overallPass;
    public List<StrokeResult> perStroke; // pass/fail + numeric score per stroke
    public string diagnosticMessage;      // human-readable summary for UI
}
```

---

## 5. Component / Class List (Unity side)

| Class | Responsibility |
|---|---|
| `CharacterStrokeData` (ScriptableObject) | Per-character reference data (§2.4) |
| `CharacterDatabase` | Loads/caches `CharacterStrokeData` by Unicode codepoint |
| `HanziStrokeImporter` (Editor tool) | Converts source JSON → `CharacterStrokeData` assets; reapplyable to any character/batch |
| `StrokeInputRecorder` | Captures mouse input into normalized point lists per stroke |
| `StrokeRenderer` | Draws the player's live stroke and the faded reference guide |
| `StrokeValidator` (static utility) | Resampling, DTW, scoring (§4) |
| `CharacterValidationSettings` (ScriptableObject) | Tunable thresholds |
| `CharacterWritingSession` | State machine driving Practice/Assessment flow, emits `CharacterAttemptResult` |
| `CharacterWritingUI` | Displays prompts, per-stroke feedback color, final result |

---

## 6. Edge Cases & Failure Modes to Handle

- Player releases mouse mid-canvas accidentally (very short stroke, few points) — reject strokes below a minimum point count / minimum length before they even reach scoring, with a neutral "too short, try again" message rather than a hard fail.
- Player draws outside the canvas bounds — clamp captured points to the canvas rect rather than letting them go to (∞) or crash normalization.
- Extremely fast mouse movement causing gaps between sampled points (browser/engine frame drops) — consider interpolating between consecutive raw samples if the gap exceeds a distance threshold, so resampling still gets a smooth path.
- Character not found in `CharacterDatabase` at runtime — fail gracefully (log + skip to next character in a queue) rather than throwing.
- Left-handed / trackpad users producing shakier lines — this is why thresholds are tunable rather than hardcoded; recommend an accessibility-friendly default tolerance and let it be loosened further in settings.

---

## 7. Testing / Verification Plan (for the reviewing agent)

To confirm this design is sound before/while implementing:
1. **Schema round-trip test:** import 5–10 known simple characters (e.g., 一, 十, 人, 水, 中) through `HanziStrokeImporter`, confirm resulting `CharacterStrokeData` assets have plausible stroke counts (e.g., 一 = 1, 十 = 2, 人 = 2, 水 = 4, 中 = 4).
2. **Self-match test:** feed each reference stroke's own `medianPoints` back into `StrokeValidator` as if it were "player input" (identity case) — this must score as a clean pass with near-zero DTW cost, proving the scoring pipeline isn't systematically biased against valid input.
3. **Negative test:** feed a reversed point order for a known stroke and confirm it fails (validates direction-sensitivity from §4.3).
4. **Wrong-order test (Assessment Mode):** draw a 2-stroke character's strokes in swapped order and confirm the index-for-index comparison flags it (since count matches but shape-at-index won't align well for strokes with different start/end orientation, e.g. 十 drawn horizontal-then-vertical vs vertical-then-horizontal).
5. **Tolerance sweep:** verify that adjusting `CharacterValidationSettings` thresholds changes pass/fail outcomes for a held-out set of borderline "good enough" and "clearly wrong" sample recordings, without code changes.
6. **Load test:** confirm `CharacterDatabase` can resolve an arbitrary requested codepoint at runtime without the character having been referenced anywhere in code beforehand (proves the "not limited to one character" requirement).

---

## 8. Open Questions Requiring a Decision Before/During Implementation

- **Dataset choice & license**: which stroke-order dataset will actually be used, and has its license been confirmed compatible with the project's distribution model? (Flagged in §2.1 — do not assume any specific dataset without checking.)
- **Simplified vs. Traditional** character forms — does the game need both, or one?
- Is **Assessment Mode** actually required for v1, or is Practice Mode (which avoids the hardest alignment problem entirely) sufficient for the initial release?
- Target **input devices**: mouse only, or should touch/stylus support be designed in from the start (recommend capturing input generically enough that touch is a drop-in later — `StrokeInputRecorder` should not assume `Input.mousePosition` exclusively; wrap it behind a small input-source interface).
- Desired **difficulty tiers** (e.g., "lenient for kids," "strict for calligraphy mode") — confirms whether `CharacterValidationSettings` needs multiple preset assets.

---

## 9. Summary

The core idea: separate the problem into (a) a **reusable, re-runnable import pipeline** that turns a structured stroke-order source (or, as fallback, a skeletonized+human-reviewed font glyph) into a small per-character Unity asset containing ordered stroke centerlines, and (b) a **generic runtime comparator** (resample → DTW → threshold, direction-sensitive by construction) that works identically regardless of which character is loaded. Practice Mode sidesteps the hardest part of the problem (stroke segmentation/ordering ambiguity) by only ever asking the player to match one known stroke at a time; Assessment Mode is an optional harder layer built on top once the core loop is proven.
