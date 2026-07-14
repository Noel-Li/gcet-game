using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A single line of in-game dialogue: who speaks, what they say, and an optional action taken when the line is reached.
/// The "speaker" is shown as a name tag; the portrait comes from that speaker's character art.
/// </summary>
[System.Serializable]
public class DialogueStep
{
    [Tooltip("Who says this line. The speaker name shown in the name-tag; defaults to the global NPC/Player name when empty.")]
    public string speakerName;

    [TextArea(2, 4)]
    public string text;

    /// <summary>Action taken when the player advances past this line. None continues the chat; Writing launches the hanzi tracing scene; GoTop tells the player to head upstairs.</summary>
    public DialogueAction action;

    [Min(1)]
    [Tooltip("How many characters must be completed when this step launches the tracer.")]
    public int requiredTraceCount = 1;

    /// <summary>Optional choices presented in place of the default click-to-advance when this step is shown. Each choice jumps to targetStep (and runs its action first, matching the action flow). When empty/null the line advances on click as before.</summary>
    [Tooltip("Player choices for this line. When set (non-empty), the player must pick one instead of clicking to advance. Each choice jumps to its targetStep.")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();
}

/// <summary>
/// One choice shown to the player on a dialogue step. Selecting it runs the choice's action (Writing/GoTop/None) and jumps the conversation to <see cref="targetStep"/>. The label is what the player sees; branch the conversation differently per response.</summary>
[System.Serializable]
public class DialogueChoice
{
    [TextArea(1, 2)]
    [Tooltip("The button text the player reads.")]
    public string label;

    [Tooltip("Step index to jump to when this choice is picked (0-based, indexing the Conversation's steps list).")]
    public int targetStep;

    [Tooltip("Action run when the player picks this choice. None jumps straight to targetStep; Writing/GoTop run their normal action before jumping.")]
    public DialogueAction action;

    [Min(1)]
    [Tooltip("How many characters must be completed when this choice launches the tracer.")]
    public int requiredTraceCount = 1;
}

public enum DialogueAction
{
    None,
    Writing,
    GoTop,
}

/// <summary>
/// A click-to-advance in-game dialogue, not a popup. Renders as a black square panel pinned to the top of the screen: a
/// square character portrait on the left, a speaker name tag, and large white text with a black outline that grows to fit
/// its content, all reading purely horizontal. Non-diegetic UI (an overlay the player reads) shown through a runtime-built
/// TMP Canvas so it needs no hand-authored Prefab/EventSystem wiring. Advancing reads the mouse or Enter/Space, matching
/// the NPC's existing click-driven interaction.
///
/// The conversation is data-driven: one step's action <see cref="DialogueAction.Writing"/> hands off to the hanzi scene
/// through <see cref="GameProgress"/>. On return from a correct trace the conversation resumes, and a
/// <see cref="DialogueAction.GoTop"/> step tells the player to head to the upper region.
/// </summary>
public class Dialogue : MonoBehaviour
{
    [Header("Content")]
    [TextArea(2, 4)]
    [SerializeField] private List<DialogueStep> steps = new List<DialogueStep>();

    [Header("Portrait (optional)")]
    [SerializeField] private Sprite portrait;

    [Header("Panel look")]
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float fontSize = 24f;
    [SerializeField] private float outlineWidth = 0.14f;
    [Range(0.1f, 0.8f)]
    [SerializeField] private float maxHeightFraction = 0.45f;
    [SerializeField] private float minWidth = 260f;

    public static Dialogue Instance { get; private set; }

    /// <summary>True while the dialogue is showing and the player is interacting with it.</summary>
    public bool IsOpen => open;

    /// <summary>Raised when the conversation ends (the player reached the final step or Close() was invoked externally). Subscribers use it to re-arm click-based triggers such as <see cref="NpcController"/>.</summary>
    public event System.Action OnClosed;

    // Layout constants in panel-local pixels (the panel is positioned in anchored screen space; children use fixed local rects).
    private const float PortraitSize = 160f;
    private const float NameHeight = 52f;
    private const float Pad = 24f;
    private const float OuterMargin = 24f;

    private Canvas canvas;
    private RectTransform panel;
    private Image panelImage;
    private Image portraitImage;
    private TextMeshProUGUI nameTag;
    private TextMeshProUGUI body;

    // Currently visible choice rows (TMP Text + their panel-local rects) so Update can hit-test mouse clicks against them. Rebuilt every RenderCurrent pass.
    private readonly List<GameObject> activeChoiceRows = new List<GameObject>();
    private readonly List<Rect> activeChoiceRects = new List<Rect>();

    private int index = 0;
    private bool open;
    private int pendingCol;
    private int pendingRow;
    private int pendingResumeStep = -1;
    private int pendingRequiredTraceCount = 1;
    private bool waitingInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Build();
        Close();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetSteps(List<DialogueStep> newSteps)
    {
        steps = newSteps != null ? new List<DialogueStep>(newSteps) : new List<DialogueStep>();
    }

    /// <summary>Open the dialogue over the room (col,row) that gates progress, starting from the first line.</summary>
    public void Open(int areaCol, int areaRow)
    {
        pendingCol = areaCol;
        pendingRow = areaRow;
        index = 0;
        open = true;
        canvas.gameObject.SetActive(true);
        RenderCurrent();
        ArmInput();
        Debug.Log($"[Dialogue] Open steps={steps.Count} index={index} step0text='{(steps.Count > 0 ? steps[0].text : "(none)")}' fontAssetIsNull={TMP_Settings.defaultFontAsset == null} canvasActive={canvas.gameObject.activeInHierarchy}");
    }

    public void Close()
    {
        open = false;
        waitingInput = false;
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }
        // Give the opportunity for subscribers to react to the conversation ending — e.g. NpcController uses this to flip `activated` back to false so the NPC can be clicked again.
        OnClosed?.Invoke();
    }

    private void Update()
    {
        if (PauseController.IsPaused || !open || !waitingInput)
        {
            return;
        }

        // A choice step suspends plain click-to-advance: the player must pick one of the listed choices. Choice selection is handled before the global click because the click might land on a choice button.
        DialogueStep currentStep = steps[index];
        if (currentStep.choices != null && currentStep.choices.Count > 0)
        {
            int chosen = -1;

            var kb = Keyboard.current;
            if (kb != null)
            {
                // Pressing the digit keys 1..N selects that choice directly.
                for (int i = 0; i < currentStep.choices.Count && i < 9; i++)
                {
                    if (KeyForDigit(i + 1, kb))
                    {
                        chosen = i;
                        break;
                    }
                }
            }

            // Mouse click: hit-test against each choice rectangle (manual raycast, no EventSystem dependency).
            if (chosen < 0)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 mousePos = mouse.position.ReadValue();
                    for (int i = 0; i < activeChoiceRects.Count; i++)
                    {
                        if (activeChoiceRects[i].Contains(mousePos))
                        {
                            chosen = i;
                            break;
                        }
                    }
                }
            }

            if (chosen >= 0)
            {
                SelectChoice(chosen);
            }
            return;
        }

        // No choices — the existing click / Space / Enter advances to the next line.
        var m = Mouse.current;
        var k = Keyboard.current;
        bool pressed = (m != null && m.leftButton.wasPressedThisFrame)
            || (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame));

        if (pressed)
        {
            Advance();
        }
    }

    private static bool KeyForDigit(int digit, Keyboard kb)
    {
        switch (digit)
        {
            case 1: return kb.digit1Key.wasPressedThisFrame;
            case 2: return kb.digit2Key.wasPressedThisFrame;
            case 3: return kb.digit3Key.wasPressedThisFrame;
            case 4: return kb.digit4Key.wasPressedThisFrame;
            case 5: return kb.digit5Key.wasPressedThisFrame;
            case 6: return kb.digit6Key.wasPressedThisFrame;
            case 7: return kb.digit7Key.wasPressedThisFrame;
            case 8: return kb.digit8Key.wasPressedThisFrame;
            case 9: return kb.digit9Key.wasPressedThisFrame;
            default: return false;
        }
    }

    private void Advance()
    {
        Advance(toIndex: index + 1);
    }

    /// <summary>Advance (or, when skipping action handling, directly jump) to a concrete step index.</summary>
    private void Advance(int? toIndex)
    {
        DialogueStep current = steps[index];

        // Writing / GoTop fire their action exactly once — when leaving the line — no matter how we reach the next step. This keeps the "hands off to the hanzi scene then resume" flow correct whether the player clicked through normally or a Writing choice redirected them.
        if (current.action == DialogueAction.Writing)
        {
            int resumeAt = toIndex ?? index + 1;
            pendingResumeStep = resumeAt;
            pendingRequiredTraceCount = Mathf.Max(1, current.requiredTraceCount);
            Close();
            LaunchWriting();
            return;
        }

        int target = toIndex ?? (index + 1);
        if (target >= steps.Count)
        {
            Close();
            return;
        }

        index = target;
        RenderCurrent();
        ArmInput();
    }

    /// <summary>Select a choice on the current step: honor its own action, then jump to its target step.</summary>
    private void SelectChoice(int choiceIndex)
    {
        DialogueStep current = steps[index];
        if (current.choices == null || choiceIndex < 0 || choiceIndex >= current.choices.Count)
        {
            return;
        }
        DialogueChoice choice = current.choices[choiceIndex];

        // A choice can require the player to trace/proceed (Writing) before advancing to its target step. Defer the actual jump until the action resolves so any Writing action re-enters the main scene and the Conversation resumes at choice.targetStep.
        if (choice.action == DialogueAction.Writing)
        {
            pendingResumeStep = choice.targetStep;
            pendingRequiredTraceCount = Mathf.Max(1, choice.requiredTraceCount);
            Close();
            LaunchWriting();
            return;
        }

        // None / GoTop: jump straight to the target step.
        if (choice.targetStep < 0 || choice.targetStep >= steps.Count)
        {
            Close();
            return;
        }
        index = choice.targetStep;
        RenderCurrent();
        ArmInput();
    }

    /// <summary>Jump to an arbitrary step directly (used to implement choice selection uniformly). Exposed for tests / external scripts.</summary>
    public void JumpTo(int targetStep)
    {
        if (targetStep < 0 || targetStep >= steps.Count || !open)
        {
            return;
        }
        index = targetStep;
        RenderCurrent();
        ArmInput();
    }

    private void LaunchWriting()
    {
        if (GameProgress.Instance == null)
        {
            var carrier = new GameObject("GameProgress");
            carrier.AddComponent<GameProgress>();
        }
        GameProgress.Instance.BeginTrace(
            pendingCol,
            pendingRow,
            pendingResumeStep,
            pendingRequiredTraceCount);
    }

    /// <summary>Re-enter the dialogue after the hanzi scene resolves, at the step set when the Writing line was read.</summary>
    public void ResumeAfterWriting()
    {
        if (GameProgress.Instance == null)
        {
            return;
        }
        int resume = GameProgress.Instance.ResumeStep;
        if (resume < 0 || resume >= steps.Count)
        {
            return;
        }

        index = resume;
        open = true;
        canvas.gameObject.SetActive(true);
        RenderCurrent();
        ArmInput();
    }

    /// <summary>Resume only if the player actually passed the trace.</summary>
    public bool CanResume()
    {
        return GameProgress.Instance != null && GameProgress.Instance.tracePassed;
    }

    private void RenderCurrent()
    {
        if (steps.Count == 0)
        {
            Close();
            return;
        }
        DialogueStep step = steps[index];

        // The speaker name is supplied per line, so a single reusable Dialogue can carry any cast of speakers. Empty lines
        // fall back to a neutral name rather than rendering a blank tag.
        string speakerName = !string.IsNullOrEmpty(step.speakerName) ? step.speakerName : "???";

        nameTag.text = speakerName;
        body.text = step.text;

        // Decide layout mode: with a portrait reserve a square on the left; without one use the full width.
        bool hasPortrait = portrait != null;
        nameTag.gameObject.SetActive(true);
        body.gameObject.SetActive(true);
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = hasPortrait;
        }

        // Repaint the existing texts and rebuild meshes so layout is current before we measure.
        if (panelImage != null)
        {
            panelImage.SetAllDirty();
            panelImage.SetVerticesDirty();
        }
        nameTag.ForceMeshUpdate(true);
        body.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();

        // Choices (if any). This is done first because adding rows changes the space the body can occupy, so we measure the body area *after* laying out the choices above it. Each choice is one clickable TMP row; we record its screen-space raycast rect for the Update hit-test.
        TearDownChoices();
        float choicesHeight = LayoutChoices(step);

        // TMP only wraps text once the field has a FIXED width. So instead of measuring the full unwrapped line and
        // letting the panel grow off-screen (the original overflow bug), we choose a content width first, force TMP to
        // wrap to it, then read the wrapped height and size the panel to fit -- constrained to the screen.
        float screenW = Screen.width > 0 ? Screen.width : 1280f;
        float maxWidthUnits = screenW - OuterMargin * 2f;
        float maxContentWidth = Mathf.Max(120f, maxWidthUnits - Pad * 2f - (hasPortrait ? PortraitSize + Pad : 0f));

        // Give body a fixed width first; TMP then reports the height needed to wrap the text to that width.
        float contentLeft = hasPortrait ? PortraitSize + Pad : 0f;
        var br = body.GetComponent<RectTransform>();
        br.sizeDelta = new Vector2(maxContentWidth, 0f);
        body.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        float bodyW = maxContentWidth;
        float bodyH = UnityEngine.Mathf.Max(body.preferredHeight, 1f);
        // Drive the rect height from the measurement so TMP glyphs never render with a stale/zero-sized rect (the original
        // overflow "text outside the box" symptom). The panel mask clips anything that still exceeds the panel's own clamp.
        br.sizeDelta = new Vector2(maxContentWidth, bodyH);

        // Name tag width matches content too (it wraps within the same bounded width).
        var nr = nameTag.GetComponent<RectTransform>();
        nr.sizeDelta = new Vector2(maxContentWidth, NameHeight);
        nameTag.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();

        float panelWidth = bodyW + Pad * 2f + (hasPortrait ? PortraitSize + Pad : 0f);
        panelWidth = UnityEngine.Mathf.Max(panelWidth, minWidth);
        panelWidth = UnityEngine.Mathf.Min(panelWidth, maxWidthUnits);

        // Reserve room at the top of the panel for the choice rows, then the standard name+body layout beneath them. Panel height still clamps to the configured fraction of the screen (the choices simply share that budget).
        float panelHeight = Pad + choicesHeight + Pad + NameHeight + Pad + bodyH + Pad;
        panelHeight = UnityEngine.Mathf.Max(panelHeight, PortraitSize + Pad * 2f);
        panelHeight = UnityEngine.Mathf.Min(panelHeight, Screen.height > 0 ? Screen.height * maxHeightFraction : 400f);

        if (panel != null)
        {
            panel.sizeDelta = new Vector2(panelWidth, panelHeight);
        }

        if (portraitImage != null)
        {
            // Top-left anchored: the rectangle occupies down/right from its anchored point.
            var pr = portraitImage.GetComponent<RectTransform>();
            pr.anchoredPosition = new Vector2(Pad, -Pad);
            pr.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        }

        // Choices are laid first from the panel top (closest to the screen edge). Name tag sits just below them, body below the name tag.
        float choiceBlockOffset = Pad;
        float nameOffsetY = Pad + choicesHeight + Pad;
        float bodyOffsetY = Pad + choicesHeight + Pad + NameHeight + Pad;

        // Position each choice row inside the panel (stacking downward). We already sized each row w  RF measuring its preferredHeight, so re-position them in a pass now that the panel height is final.
        PositionChoiceRows(choiceBlockOffset);

        nr.anchoredPosition = new Vector2(contentLeft + Pad, -nameOffsetY);
        br.anchoredPosition = new Vector2(contentLeft + Pad, -bodyOffsetY);

        // First frame may report ~0 mesh sizes; re-measure next frame so the box never collapses to nothing.
        if (bodyH < 1f && open)
        {
            StopCoroutine("ReflowNextFrame");
            StartCoroutine(ReflowNextFrame());
        }
    }

    private void TearDownChoices()
    {
        for (int i = 0; i < activeChoiceRows.Count; i++)
        {
            if (activeChoiceRows[i] != null)
            {
                Destroy(activeChoiceRows[i]);
            }
        }
        activeChoiceRows.Clear();
        activeChoiceRects.Clear();
    }

    /// <summary>Lays out the current step's choices inside the panel and returns the total vertical space they occupy. Populates <see cref="activeChoiceRows"/> for cleanup and <see cref="activeChoiceRects"/> for click hit-testing.</summary>
    private float LayoutChoices(DialogueStep step)
    {
        activeChoiceRects.Clear();
        activeChoiceRows.Clear();
        if (step.choices == null || step.choices.Count <= 0 || panel == null)
        {
            return 0f;
        }

        // The space available for choices is the wider panel-width minus horizontal padding. Sit choices inside the same column as the name tag/body so they line up.
        bool hasPortrait = portrait != null;
        float screenW = Screen.width > 0 ? Screen.width : 1280f;
        float maxWidthUnits = screenW - OuterMargin * 2f;
        float contentW = UnityEngine.Mathf.Max(120f, maxWidthUnits - Pad * 2f - (hasPortrait ? PortraitSize + Pad : 0f));
        float contentLeft = hasPortrait ? PortraitSize + Pad : 0f;

        float maxChoiceWidth = contentW;
        float padBetween = 8f;
        float offsetY = Pad; // first choice flush under the panel top

        for (int i = 0; i < step.choices.Count; i++)
        {
            string text = string.IsNullOrEmpty(step.choices[i].label) ? "(...)" : $"{i + 1}. {step.choices[i].label}";
            var rowObj = new GameObject($"Choice_{i}");
            rowObj.transform.SetParent(panel.transform, false);
            var tmp = MakeText(rowObj, new Color(0.95f, 0.95f, 1f, 1f), Mathf.Max(14, fontSize - 6), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            tmp.text = text;

            var rowRt = rowObj.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(0f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);

            // Size first, then measure TMP's preferred height so each row fits its label.
            rowRt.sizeDelta = new Vector2(maxChoiceWidth, 0f);
            tmp.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();
            float rowH = UnityEngine.Mathf.Max(tmp.preferredHeight, 1f);
            rowRt.sizeDelta = new Vector2(maxChoiceWidth, rowH);
            rowRt.anchoredPosition = new Vector2(contentLeft + Pad, -offsetY);

            tmp.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();

            activeChoiceRows.Add(rowObj);

            // Record the screen-space bounding rectangle of this row for mouse hit-testing.
            Rect screenRect = RectTransformToScreenRect(rowRt);
            activeChoiceRects.Add(screenRect);

            offsetY += rowH + padBetween;
        }

        float totalHeight = 0f;
        for (int i = 0; i < activeChoiceRows.Count; i++)
        {
            totalHeight += activeChoiceRows[i].GetComponent<RectTransform>().sizeDelta.y;
            if (i > 0)
            {
                totalHeight += padBetween;
            }
        }
        return totalHeight;
    }

    private void PositionChoiceRows(float choiceBlockOffset)
    {
        // LayoutChoices has already positioned the rows; this hook exists so future layout tweaks have a home. Kept no-op here to keep flow clear.
    }

    /// <summary>Convert a panel-space RectTransform to its screen-space bounding Rect (for manual click hit-testing). The panel itself is screen-space override anchored, so its children map directly to screen pixels.</summary>
    private Rect RectTransformToScreenRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // Screen space: y inverted vs world in Canvas (but Dialogue canvas is ScreenSpaceOverlay at identity camera, so world y == already screen y). Bottom-left - top-right read directly.
        float minX = corners[0].x, minY = corners[0].y;
        float maxX = corners[2].x, maxY = corners[2].y;
        // Clamp / guard against degenerate/NaN when corners are not yet valid (~first frame).
        if (float.IsNaN(minX) || float.IsNaN(minY) || float.IsNaN(maxX) || float.IsNaN(maxY) || maxX < minX || maxY < minY)
        {
            return new Rect(0f, 0f, 0f, 0f);
        }
        return new Rect(minX, Screen.height - maxY, maxX - minX, maxY - minY);
    }

    private System.Collections.IEnumerator ReflowNextFrame()
    {
        yield return null;
        if (open)
        {
            RenderCurrent();
        }
    }

    /// <summary>Delay accepting input for one frame so the same click that opened the dialogue doesn't advance it.</summary>
    private void ArmInput()
    {
        waitingInput = false;
        StartCoroutine(ArmNextFrame());
    }

    private System.Collections.IEnumerator ArmNextFrame()
    {
        yield return null;
        if (open)
        {
            waitingInput = true;
        }
    }

    private TextMeshProUGUI MakeText(GameObject host, Color color, float size, FontStyles style, TextAlignmentOptions align)
    {
        var tmp = host.AddComponent<TextMeshProUGUI>();

        // TMP needs a font asset to emit any glyphs at all; resolve the project-wide default explicitly so runtime-created
        // text renders even when TMP hasn't auto-assigned one (otherwise the canvas exists but draws nothing). The
        // material must come from the font asset (its SDF atlas) or TMP draws with no glyph material.
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            Material mat = tmp.font.material;
            if (mat != null)
            {
                tmp.material = mat;
            }
            else
            {
                mat = new Material(Shader.Find("TextMeshPro/Mobile/Distance Field"));
                if (mat != null)
                {
                    tmp.material = mat;
                }
            }
        }

        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;   // we size the rect to fit; never let TMP clip glyphs itself
        tmp.enableAutoSizing = false;                    // font stays the readable size set by fontSize
        tmp.outlineWidth = outlineWidth;
        tmp.outlineColor = Color.black;
        tmp.richText = true;
        tmp.raycastTarget = false;

        // The panel is top-anchored, so its children are top-anchored too and stack downward: each rect's anchoredPosition is a
        // downward distance from the panel's top edge, and a positive sizeDelta.y extends *down*. If a child were instead
        // bottom-anchored, the negative-y positions below would land it under the panel and the mask would crop it (only the
        // bottom sliver of the name-tag shows). Start them at a neutral rect; RenderCurrent sets size/position each step.
        var rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return tmp;
    }

    private void Build()
    {
        var canvasObj = new GameObject("DialogueCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>();

        // The Canvas has to cover the whole screen or anchored children land off-screen. An overlay Canvas created at
        // runtime does NOT auto-fill, so set the root rect explicitly.
        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        // Panel: a fixed-position black square pinned to the top-left of the screen. Its children are positioned in its
        // local space inside RenderCurrent().
        var panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        panel = panelObj.AddComponent<RectTransform>();
        panelImage = panelObj.AddComponent<Image>();
        panelImage.sprite = SolidSprite();
        panelImage.color = panelColor;
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(OuterMargin, -OuterMargin);
        panel.sizeDelta = Vector2.zero;
        panelImage.raycastTarget = false;

        // Clip children (name tag, body, portrait) to the panel's own rectangle. Without this, a line taller than the
        // panel's height clamp would render outside the black box. The panel carries an Image, which RectMask2D needs.
        panelObj.AddComponent<RectMask2D>();

        // Portrait is created whether or not one is set; hidden per-step when the active speaker has none. Top-left anchored
        // like the rest so its panel-local positions match the others.
        var portraitObj = new GameObject("Portrait");
        portraitObj.transform.SetParent(panelObj.transform, false);
        portraitImage = portraitObj.AddComponent<Image>();
        portraitImage.raycastTarget = false;
        var prt = portraitImage.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 1f);
        prt.anchorMax = new Vector2(0f, 1f);
        prt.pivot = new Vector2(0f, 1f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = Vector2.zero;

        var nameObj = new GameObject("NameTag");
        nameObj.transform.SetParent(panelObj.transform, false);
        nameTag = MakeText(nameObj, new Color(1f, 0.85f, 0.35f, 1f), Mathf.Max(18, fontSize - 8), FontStyles.Bold, TextAlignmentOptions.Left);

        var bodyObj = new GameObject("Body");
        bodyObj.transform.SetParent(panelObj.transform, false);
        body = MakeText(bodyObj, textColor, fontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft);
    }

    private static Sprite SolidSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        s.name = "DialogueSolid";
        return s;
    }
}