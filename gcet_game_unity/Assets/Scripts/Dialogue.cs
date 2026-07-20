using System.Collections.Generic;
using System.Text.RegularExpressions;
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

    [Tooltip("Portrait expression key for this line, such as normal, confused, excited, surprised, worried, or stern.")]
    public string expression = "normal";

    [Tooltip("Speaker uses the character frame; GameVoice uses the yellow narrator/instruction frame.")]
    public DialoguePanelStyle panelStyle = DialoguePanelStyle.Speaker;

    [TextArea(2, 4)]
    public string text;

    /// <summary>Action taken when the player advances past this line. None continues the chat; Writing launches the hanzi tracing scene; GoTop tells the player to head upstairs.</summary>
    public DialogueAction action;

    [Tooltip("Optional step to open on normal click-to-advance. Leave at -1 to continue to the next list item.")]
    public int nextStep = -1;

    [Min(1)]
    [Tooltip("How many characters must be completed when this step launches the tracer.")]
    public int requiredTraceCount = 1;

    [Tooltip("Exact CharacterData names to trace, in order. Leave empty to use the tracer's first characters as a legacy fallback.")]
    public List<string> traceCharacters = new List<string>();

    /// <summary>Optional choices presented in place of the default click-to-advance when this step is shown. Each choice jumps to targetStep (and runs its action first, matching the action flow). When empty/null the line advances on click as before.</summary>
    [Tooltip("Player choices for this line. When set (non-empty), the player must pick one instead of clicking to advance. Each choice jumps to its targetStep.")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    [Tooltip("Show choices in the dedicated bottom-right Multiple Choice frame. Leave off for an inline action such as Start Tracing.")]
    public bool useMultipleChoicePanel;
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

    [Tooltip("Exact CharacterData names to trace, in order. Leave empty to use the tracer's legacy count-based selection.")]
    public List<string> traceCharacters = new List<string>();
}

/// <summary>Inspector-authored panel art for one dialogue speaker.</summary>
[System.Serializable]
public class SpeakerDialogueBackground
{
    [Tooltip("Speaker name matched against DialogueStep.speakerName (case-insensitive).")]
    public string speakerName;

    [Tooltip("Complete dialogue-frame artwork for this speaker.")]
    public Sprite background;
}

/// <summary>Inspector-authored portrait for one speaker and expression.</summary>
[System.Serializable]
public class SpeakerDialoguePortrait
{
    public string speakerName;
    public string expression = "normal";
    public Sprite portrait;

    [Tooltip("Optional portrait-position adjustment measured as a fraction of the frame's portrait-slot width and height.")]
    public Vector2 slotOffset;
}

public enum DialoguePanelStyle
{
    Speaker,
    GameVoice
}

public enum DialogueAction
{
    None,
    Writing,
    GoTop,
}

/// <summary>
/// A click-to-advance in-game dialogue, not a popup. Renders speaker-specific dialogue-frame art along the bottom of the
/// screen, with readable text in the frame's content area. Non-diegetic UI (an overlay the player reads) shown through a runtime-built
/// TMP Canvas so it needs no hand-authored Prefab/EventSystem wiring. Advancing reads the mouse or Enter/Space, matching
/// the NPC's existing click-driven interaction.
///
/// The conversation is data-driven: one step's action <see cref="DialogueAction.Writing"/> hands off to the hanzi scene
/// through <see cref="GameProgress"/>. On return from a correct trace the conversation resumes, and a
/// <see cref="DialogueAction.GoTop"/> step tells the player to head to the upper region.
/// </summary>
public class Dialogue : MonoBehaviour
{
    private const string PinyinFontAssetName = "LiberationSans SDF";

    // The main dialogue font is a Chinese typeface whose accented Latin glyphs use full CJK advances. Select the
    // project's Latin TMP font for parenthesized pinyin so tone-marked vowels retain normal Latin spacing.
    private static readonly Regex ParenthesizedPinyin = new Regex(
        @"\((?=[^)]*[āáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜüĀÁǍÀĒÉĚÈĪÍǏÌŌÓǑÒŪÚǓÙǕǗǙǛÜ])[^)]*\)",
        RegexOptions.CultureInvariant);

    [Header("Content")]
    [TextArea(2, 4)]
    [SerializeField] private List<DialogueStep> steps = new List<DialogueStep>();

    [Header("Portrait (optional)")]
    [SerializeField] private Sprite portrait;

    [Header("Speaker backgrounds")]
    [Tooltip("Panel artwork selected by DialogueStep.speakerName.")]
    [SerializeField] private List<SpeakerDialogueBackground> speakerBackgrounds = new List<SpeakerDialogueBackground>();

    [Header("Speaker portraits")]
    [Tooltip("Portrait selected by the current line's speakerName and expression.")]
    [SerializeField] private List<SpeakerDialoguePortrait> speakerPortraits = new List<SpeakerDialoguePortrait>();

    [Header("Auxiliary panels")]
    [SerializeField] private Sprite gameVoiceBackground;
    [SerializeField] private Sprite multipleChoiceBackground;

    [Header("Panel look")]
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [Tooltip("Text color used over the light speaker-frame artwork.")]
    [SerializeField] private Color speakerBackgroundTextColor = new Color(0.08f, 0.08f, 0.1f, 1f);
    [Tooltip("Base size used for long dialogue lines.")]
    [SerializeField] private float fontSize = 26f;
    [Tooltip("Largest size used for short prompts and answers.")]
    [SerializeField] private float shortLineFontSize = 32f;
    [SerializeField] private float outlineWidth = 0.14f;
    [Range(0.1f, 0.8f)]
    [SerializeField] private float maxHeightFraction = 0.45f;
    [SerializeField] private float minWidth = 260f;

    public static Dialogue Instance { get; private set; }

    /// <summary>True while the dialogue is showing and the player is interacting with it.</summary>
    public bool IsOpen => open;

    /// <summary>Raised when the conversation ends (the player reached the final step or Close() was invoked externally). Subscribers use it to re-arm click-based triggers such as <see cref="NpcController"/>.</summary>
    public event System.Action OnClosed;

    // The authored dialogue frames are 700x211. Keep that aspect ratio and reserve the left 28% for the frame's
    // built-in portrait/name treatment; dialogue content belongs in the open area to its right.
    private const float BackgroundAspect = 700f / 211f;
    private const float MaxPanelWidth = 1000f;
    private const float ContentLeftFraction = 0.28f;
    private const float Pad = 18f;
    private const float OuterMargin = 24f;
    private const float PortraitSlotLeft = 0.047f;
    private const float PortraitSlotBottom = 0.284f;
    private const float PortraitSlotWidth = 0.216f;
    private const float PortraitSlotHeight = 0.483f;
    private const float PortraitFill = 0.9f;

    private Canvas canvas;
    private RectTransform panel;
    private Image panelImage;
    private RectTransform choicePanel;
    private Image choicePanelImage;
    private Sprite fallbackPanelSprite;
    private Image portraitImage;
    private TextMeshProUGUI nameTag;
    private TextMeshProUGUI body;

    // Currently visible choice rows (TMP Text + their panel-local rects) so Update can hit-test mouse clicks against them. Rebuilt every RenderCurrent pass.
    private readonly List<GameObject> activeChoiceRows = new List<GameObject>();
    private readonly List<Rect> activeChoiceRects = new List<Rect>();
    private readonly Dictionary<Sprite, Sprite> centeredPortraitSprites = new Dictionary<Sprite, Sprite>();

    private int index = 0;
    private bool open;
    private int pendingCol;
    private int pendingRow;
    private int pendingResumeStep = -1;
    private int pendingRequiredTraceCount = 1;
    private readonly List<string> pendingTraceCharacters = new List<string>();
    private string traceOwnerKey;
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
        foreach (Sprite runtimeSprite in centeredPortraitSprites.Values)
        {
            if (runtimeSprite != null)
            {
                Destroy(runtimeSprite);
            }
        }
        centeredPortraitSprites.Clear();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetSteps(List<DialogueStep> newSteps)
    {
        steps = newSteps != null ? new List<DialogueStep>(newSteps) : new List<DialogueStep>();
    }

    /// <summary>Supplies the reusable speaker-to-frame mapping authored on the NPC's Conversation component.</summary>
    public void SetSpeakerBackgrounds(List<SpeakerDialogueBackground> newBackgrounds)
    {
        speakerBackgrounds = newBackgrounds != null
            ? new List<SpeakerDialogueBackground>(newBackgrounds)
            : new List<SpeakerDialogueBackground>();
    }

    public void SetSpeakerPortraits(List<SpeakerDialoguePortrait> newPortraits)
    {
        speakerPortraits = newPortraits != null
            ? new List<SpeakerDialoguePortrait>(newPortraits)
            : new List<SpeakerDialoguePortrait>();
    }

    public void SetAuxiliaryPanelBackgrounds(Sprite gameVoice, Sprite multipleChoice)
    {
        gameVoiceBackground = gameVoice;
        multipleChoiceBackground = multipleChoice;
    }

    /// <summary>Identifies the NPC that owns a tracing handoff so only that NPC restores the saved conversation.</summary>
    public void SetTraceOwner(string ownerKey)
    {
        traceOwnerKey = ownerKey;
    }

    /// <summary>Sets the current speaker art. Kept separate from the dialogue lines so expressions can be swapped later.</summary>
    public void SetPortrait(Sprite newPortrait)
    {
        portrait = newPortrait;
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
        if (PauseController.IsPaused || ControlsOverlayController.IsOpen || !open || !waitingInput)
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
                // Pressing the top-row or numeric-keypad keys 1..N selects that choice directly.
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
            case 1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case 2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case 3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case 4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            case 5: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
            case 6: return kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame;
            case 7: return kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame;
            case 8: return kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame;
            case 9: return kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame;
            default: return false;
        }
    }

    private void Advance()
    {
        Advance(toIndex: null);
    }

    /// <summary>Advance (or, when skipping action handling, directly jump) to a concrete step index.</summary>
    private void Advance(int? toIndex)
    {
        DialogueStep current = steps[index];

        // Writing / GoTop fire their action exactly once — when leaving the line — no matter how we reach the next step. This keeps the "hands off to the hanzi scene then resume" flow correct whether the player clicked through normally or a Writing choice redirected them.
        if (current.action == DialogueAction.Writing)
        {
            int resumeAt = toIndex ?? (current.nextStep >= 0 ? current.nextStep : index + 1);
            pendingResumeStep = resumeAt;
            pendingRequiredTraceCount = Mathf.Max(1, current.requiredTraceCount);
            SetPendingTraceCharacters(current.traceCharacters);
            Close();
            LaunchWriting();
            return;
        }

        int target = toIndex ?? (current.nextStep >= 0 ? current.nextStep : index + 1);
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
            SetPendingTraceCharacters(choice.traceCharacters);
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
            pendingRequiredTraceCount,
            traceOwnerKey,
            pendingTraceCharacters);
    }

    private void SetPendingTraceCharacters(List<string> characterNames)
    {
        pendingTraceCharacters.Clear();
        if (characterNames != null)
        {
            pendingTraceCharacters.AddRange(characterNames);
        }
    }

    /// <summary>Re-enter the dialogue after the hanzi scene resolves, at the step set when the Writing line was read.</summary>
    public void ResumeAfterWriting()
    {
        if (GameProgress.Instance == null)
        {
            return;
        }
        int resume = GameProgress.Instance.ResumeStep;
        if (resume == steps.Count)
        {
            Close();
            return;
        }
        if (resume < 0 || resume > steps.Count)
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

        bool isGameVoice = step.panelStyle == DialoguePanelStyle.GameVoice;
        bool hasChoices = step.choices != null && step.choices.Count > 0;
        bool usesSeparateChoicePanel = hasChoices && step.useMultipleChoicePanel;
        Sprite background = isGameVoice ? ResolveGameVoiceBackground() : FindBackground(speakerName);
        SpeakerDialoguePortrait portraitPresentation = FindPortraitPresentation(speakerName, step.expression);
        Sprite currentPortrait = portraitPresentation != null
            ? portraitPresentation.portrait
            : FindPortrait(speakerName, step.expression);
        bool hasPanelArtwork = background != null;
        bool hasSpeakerBackground = !isGameVoice && hasPanelArtwork;
        bool hasPortrait = !isGameVoice && currentPortrait != null;

        nameTag.text = speakerName;
        body.text = FormatPinyin(step.text);
        body.color = hasPanelArtwork ? speakerBackgroundTextColor : textColor;
        nameTag.gameObject.SetActive(!isGameVoice && !hasSpeakerBackground);
        body.gameObject.SetActive(true);

        float screenW = Screen.width > 0 ? Screen.width : 1280f;
        float screenH = Screen.height > 0 ? Screen.height : 720f;
        float availableWidth = usesSeparateChoicePanel
            ? (screenW - OuterMargin * 3f) * 0.5f
            : screenW - OuterMargin * 2f;
        float panelWidth = Mathf.Max(minWidth, Mathf.Min(MaxPanelWidth, availableWidth));
        float panelHeight = panelWidth / BackgroundAspect;
        float maxPanelHeight = screenH * maxHeightFraction;
        if (panelHeight > maxPanelHeight)
        {
            panelHeight = maxPanelHeight;
            panelWidth = panelHeight * BackgroundAspect;
        }

        panel.sizeDelta = new Vector2(panelWidth, panelHeight);
        panel.anchoredPosition = new Vector2(OuterMargin, OuterMargin);

        if (panelImage != null)
        {
            panelImage.sprite = hasPanelArtwork ? background : fallbackPanelSprite;
            panelImage.color = hasPanelArtwork ? Color.white : panelColor;
            panelImage.SetAllDirty();
            panelImage.SetVerticesDirty();
        }

        // The supplied artwork includes a portrait window. Keep optional portrait art inside that window instead of
        // reserving a separate box beside the panel.
        if (portraitImage != null)
        {
            portraitImage.sprite = CenteredPortrait(currentPortrait);
            portraitImage.enabled = hasPortrait;
            var pr = portraitImage.GetComponent<RectTransform>();

            float slotWidth = panelWidth * PortraitSlotWidth;
            float slotHeight = panelHeight * PortraitSlotHeight;
            float portraitWidth = slotWidth * PortraitFill;
            float portraitHeight = slotHeight * PortraitFill;

            if (currentPortrait != null && currentPortrait.rect.height > 0f)
            {
                float aspect = currentPortrait.rect.width / currentPortrait.rect.height;
                portraitWidth = portraitHeight * aspect;
                float maxWidth = slotWidth * PortraitFill;
                if (portraitWidth > maxWidth)
                {
                    portraitWidth = maxWidth;
                    portraitHeight = portraitWidth / aspect;
                }
            }

            pr.pivot = new Vector2(0.5f, 0.5f);
            Vector2 slotOffset = portraitPresentation != null
                ? portraitPresentation.slotOffset
                : Vector2.zero;
            pr.anchoredPosition = panel.anchoredPosition + new Vector2(
                panelWidth * (PortraitSlotLeft + PortraitSlotWidth * 0.5f),
                panelHeight * (PortraitSlotBottom + PortraitSlotHeight * 0.5f))
                + new Vector2(slotWidth * slotOffset.x, slotHeight * slotOffset.y);
            pr.sizeDelta = new Vector2(portraitWidth, portraitHeight);
        }

        float contentLeft = hasSpeakerBackground ? panelWidth * ContentLeftFraction : Pad;
        float contentWidth = Mathf.Max(120f, panelWidth - contentLeft - Pad);
        float bodyOffsetY = Pad;

        var nr = nameTag.GetComponent<RectTransform>();
        if (!hasSpeakerBackground)
        {
            const float fallbackNameHeight = 36f;
            nr.sizeDelta = new Vector2(contentWidth, fallbackNameHeight);
            nr.anchoredPosition = new Vector2(contentLeft, -Pad);
            bodyOffsetY += fallbackNameHeight + 6f;
            nameTag.ForceMeshUpdate(true);
        }

        // Give TMP a fixed width before measuring so dialogue wraps inside the artwork's open right-hand area.
        var br = body.GetComponent<RectTransform>();
        body.fontSize = ResponsiveFontSize(step.text);
        body.alignment = TextAlignmentOptions.Center;
        br.sizeDelta = new Vector2(contentWidth, 0f);
        br.anchoredPosition = new Vector2(contentLeft, -bodyOffsetY);
        body.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        float bodyH = Mathf.Max(body.preferredHeight, 1f);
        br.sizeDelta = new Vector2(contentWidth, bodyH);
        br.anchoredPosition = new Vector2(
            contentLeft,
            -Mathf.Max(Pad, (panelHeight - bodyH) * 0.5f));

        TearDownChoices();
        if (choicePanel != null)
        {
            choicePanel.gameObject.SetActive(usesSeparateChoicePanel);
        }

        if (usesSeparateChoicePanel && choicePanel != null)
        {
            choicePanel.sizeDelta = new Vector2(panelWidth, panelHeight);
            choicePanel.anchoredPosition = new Vector2(screenW - OuterMargin - panelWidth, OuterMargin);

            Sprite choiceBackground = ResolveMultipleChoiceBackground();
            choicePanelImage.sprite = choiceBackground != null ? choiceBackground : fallbackPanelSprite;
            choicePanelImage.color = choiceBackground != null ? Color.white : panelColor;
            choicePanelImage.SetAllDirty();

            float choiceHeight = LayoutChoices(
                step,
                choicePanel,
                Pad,
                Mathf.Max(120f, panelWidth - Pad * 2f),
                0f,
                choiceBackground != null
                    ? speakerBackgroundTextColor
                    : new Color(0.95f, 0.95f, 1f, 1f));
            PositionChoiceRows(Mathf.Max(Pad, (panelHeight - choiceHeight) * 0.5f));
            RefreshChoiceRects();
        }
        else if (hasChoices)
        {
            float choiceHeight = LayoutChoices(
                step,
                panel,
                contentLeft,
                contentWidth,
                0f,
                hasPanelArtwork
                    ? speakerBackgroundTextColor
                    : new Color(0.95f, 0.95f, 1f, 1f));
            float groupHeight = bodyH + 8f + choiceHeight;
            float groupTop = Mathf.Max(Pad, (panelHeight - groupHeight) * 0.5f);
            br.anchoredPosition = new Vector2(contentLeft, -groupTop);
            PositionChoiceRows(groupTop + bodyH + 8f);
            RefreshChoiceRects();
        }
        Canvas.ForceUpdateCanvases();

        // First frame may report ~0 mesh sizes; re-measure next frame so the box never collapses to nothing.
        if (bodyH < 1f && open)
        {
            StopCoroutine("ReflowNextFrame");
            StartCoroutine(ReflowNextFrame());
        }
    }

    private Sprite FindBackground(string speakerName)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            return null;
        }

        for (int i = 0; speakerBackgrounds != null && i < speakerBackgrounds.Count; i++)
        {
            SpeakerDialogueBackground entry = speakerBackgrounds[i];
            if (entry != null &&
                entry.background != null &&
                string.Equals(entry.speakerName, speakerName, System.StringComparison.OrdinalIgnoreCase))
            {
                return entry.background;
            }
        }

#if UNITY_EDITOR
        // If game1 was already open when its serialized Conversation mapping changed on disk, Play mode may still see
        // the older in-memory list. Resolve the two shipped defaults directly in the Editor so the player never falls
        // back to the black box; builds continue to use the serialized mapping above.
        string assetPath = null;
        if (string.Equals(speakerName, "player", System.StringComparison.OrdinalIgnoreCase))
        {
            assetPath = "Assets/CharacterBackground/guoxiaoDialogue.jpeg";
        }
        else if (string.Equals(speakerName, "soldier", System.StringComparison.OrdinalIgnoreCase))
        {
            assetPath = "Assets/CharacterBackground/soldierDialogue.jpeg";
        }

        if (!string.IsNullOrEmpty(assetPath))
        {
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    return sprite;
                }
            }
        }
#endif

        return null;
    }

    private Sprite ResolveGameVoiceBackground()
    {
        if (gameVoiceBackground != null)
        {
            return gameVoiceBackground;
        }

#if UNITY_EDITOR
        return LoadFirstEditorSprite("Assets/CharacterBackground/gamevoiceDialogue.jpeg");
#else
        return null;
#endif
    }

    private Sprite ResolveMultipleChoiceBackground()
    {
        if (multipleChoiceBackground != null)
        {
            return multipleChoiceBackground;
        }

#if UNITY_EDITOR
        return LoadFirstEditorSprite("Assets/CharacterBackground/multichoiceDialogue.jpeg");
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    private static Sprite LoadFirstEditorSprite(string assetPath)
    {
        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                return sprite;
            }
        }

        return null;
    }
#endif

    private Sprite FindPortrait(string speakerName, string expression)
    {
        string requestedExpression = string.IsNullOrWhiteSpace(expression) ? "normal" : expression;
        SpeakerDialoguePortrait normalFallback = null;

        for (int i = 0; speakerPortraits != null && i < speakerPortraits.Count; i++)
        {
            SpeakerDialoguePortrait entry = speakerPortraits[i];
            if (entry == null || entry.portrait == null ||
                !string.Equals(entry.speakerName, speakerName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(entry.expression, requestedExpression, System.StringComparison.OrdinalIgnoreCase))
            {
                return entry.portrait;
            }

            if (string.Equals(entry.expression, "normal", System.StringComparison.OrdinalIgnoreCase))
            {
                normalFallback = entry;
            }
        }

        if (normalFallback != null)
        {
            return normalFallback.portrait;
        }

#if UNITY_EDITOR
        string assetPath = EditorPortraitPath(speakerName, requestedExpression);
        if (!string.IsNullOrEmpty(assetPath))
        {
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    return sprite;
                }
            }
        }
#endif

        return portrait;
    }

    /// <summary>
    /// Finds the serialized portrait entry as well as its Inspector-authored placement adjustment.
    /// Exact expressions win; a normal portrait remains the fallback for other expressions.
    /// </summary>
    private SpeakerDialoguePortrait FindPortraitPresentation(string speakerName, string expression)
    {
        string requestedExpression = string.IsNullOrWhiteSpace(expression) ? "normal" : expression;
        SpeakerDialoguePortrait normalFallback = null;

        for (int i = 0; speakerPortraits != null && i < speakerPortraits.Count; i++)
        {
            SpeakerDialoguePortrait entry = speakerPortraits[i];
            if (entry == null || entry.portrait == null ||
                !string.Equals(entry.speakerName, speakerName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(entry.expression, requestedExpression, System.StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }

            if (string.Equals(entry.expression, "normal", System.StringComparison.OrdinalIgnoreCase))
            {
                normalFallback = entry;
            }
        }

        return normalFallback;
    }

    /// <summary>
    /// Portrait imports currently use a bottom-left pivot. UI Image respects that pivot when building its geometry,
    /// so create and cache a centered-pivot view of the same texture rectangle for predictable placement.
    /// </summary>
    private Sprite CenteredPortrait(Sprite source)
    {
        if (source == null)
        {
            return null;
        }

        if (centeredPortraitSprites.TryGetValue(source, out Sprite centered))
        {
            return centered;
        }

        centered = Sprite.Create(
            source.texture,
            source.rect,
            new Vector2(0.5f, 0.5f),
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            source.border);
        centered.name = source.name + "_DialogueCentered";
        centered.hideFlags = HideFlags.HideAndDontSave;
        centeredPortraitSprites.Add(source, centered);
        return centered;
    }

#if UNITY_EDITOR
    private static string EditorPortraitPath(string speakerName, string expression)
    {
        string speaker = speakerName.ToLowerInvariant();
        string mood = expression.ToLowerInvariant();

        if (speaker == "player")
        {
            if (mood == "confused") return "Assets/character_img/xiaoyueConfused.PNG";
            if (mood == "excited") return "Assets/character_img/xiaoyueExcited.PNG";
            if (mood == "worried") return "Assets/character_img/xiaoyueWorries.PNG";
            return "Assets/character_img/xiaoyueMain.PNG";
        }

        if (speaker == "soldier")
        {
            if (mood == "confused") return "Assets/character_img/soldierConfused.PNG";
            if (mood == "surprised") return "Assets/character_img/soldierSurprised.PNG";
            return "Assets/character_img/soldierMain.PNG";
        }

        return null;
    }
#endif

    private float ResponsiveFontSize(string text)
    {
        int length = string.IsNullOrWhiteSpace(text) ? 0 : text.Length;
        if (length <= 48)
        {
            return Mathf.Max(fontSize, shortLineFontSize);
        }

        if (length <= 100)
        {
            return Mathf.Lerp(fontSize, Mathf.Max(fontSize, shortLineFontSize), 0.5f);
        }

        return fontSize;
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
    private float LayoutChoices(
        DialogueStep step,
        RectTransform hostPanel,
        float contentLeft,
        float contentWidth,
        float startOffsetY,
        Color choiceTextColor)
    {
        activeChoiceRects.Clear();
        activeChoiceRows.Clear();
        if (step.choices == null || step.choices.Count <= 0 || hostPanel == null)
        {
            return 0f;
        }

        float maxChoiceWidth = contentWidth;
        float padBetween = 8f;
        float offsetY = startOffsetY;

        for (int i = 0; i < step.choices.Count; i++)
        {
            string text = string.IsNullOrEmpty(step.choices[i].label)
                ? "(...)"
                : $"{i + 1}. {FormatPinyin(step.choices[i].label)}";
            var rowObj = new GameObject($"Choice_{i}");
            rowObj.transform.SetParent(hostPanel.transform, false);
            var tmp = MakeText(
                rowObj,
                choiceTextColor,
                ResponsiveFontSize(step.choices[i].label),
                FontStyles.Normal,
                TextAlignmentOptions.Center);
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
            rowRt.anchoredPosition = new Vector2(contentLeft, -offsetY);

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
        const float padBetween = 8f;
        float offsetY = choiceBlockOffset;

        for (int i = 0; i < activeChoiceRows.Count; i++)
        {
            RectTransform row = activeChoiceRows[i].GetComponent<RectTransform>();
            row.anchoredPosition = new Vector2(row.anchoredPosition.x, -offsetY);
            offsetY += row.sizeDelta.y + padBetween;
        }
    }

    private void RefreshChoiceRects()
    {
        Canvas.ForceUpdateCanvases();
        activeChoiceRects.Clear();
        for (int i = 0; i < activeChoiceRows.Count; i++)
        {
            activeChoiceRects.Add(
                RectTransformToScreenRect(activeChoiceRows[i].GetComponent<RectTransform>()));
        }
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
        return new Rect(minX, minY, maxX - minX, maxY - minY);
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
        // material must come from the font asset (its SDF atlas) or TMP draws with no glyph material. Chinese dialogue
        // can exceed one atlas page over a full playthrough, so keep dynamic multi-atlas expansion enabled at runtime.
        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont != null)
        {
            defaultFont.isMultiAtlasTexturesEnabled = true;
            tmp.font = defaultFont;
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

    /// <summary>
    /// Wraps tone-marked pinyin in a TMP font tag. Chinese characters stay in the project's Chinese font while the
    /// pronunciation uses Latin glyph metrics, preventing the wide gaps previously visible around accented vowels.
    /// </summary>
    private static string FormatPinyin(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return ParenthesizedPinyin.Replace(
            text,
            match => $"<font=\"{PinyinFontAssetName}\">{match.Value}</font>");
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

        // Panel: a fixed-position speaker frame along the bottom. Its children are positioned in its
        // local space inside RenderCurrent(). A solid sprite remains as a safe fallback for unmapped speakers.
        var panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        panel = panelObj.AddComponent<RectTransform>();
        panelImage = panelObj.AddComponent<Image>();
        fallbackPanelSprite = SolidSprite();
        panelImage.sprite = fallbackPanelSprite;
        panelImage.color = panelColor;
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 0f);
        panel.pivot = new Vector2(0f, 0f);
        panel.anchoredPosition = new Vector2(OuterMargin, OuterMargin);
        panel.sizeDelta = Vector2.zero;
        panelImage.raycastTarget = false;

        // Clip children (name tag, body, portrait) to the panel's own rectangle. Without this, a line taller than the
        // panel's height clamp would render outside the black box. The panel carries an Image, which RectMask2D needs.
        panelObj.AddComponent<RectMask2D>();

        // Choices use their own artwork and stay at the bottom-right, independent of the prompt/speaker frame.
        var choicePanelObj = new GameObject("MultipleChoicePanel");
        choicePanelObj.transform.SetParent(canvasObj.transform, false);
        choicePanel = choicePanelObj.AddComponent<RectTransform>();
        choicePanelImage = choicePanelObj.AddComponent<Image>();
        choicePanelImage.sprite = fallbackPanelSprite;
        choicePanelImage.color = panelColor;
        choicePanelImage.raycastTarget = false;
        choicePanel.anchorMin = new Vector2(0f, 0f);
        choicePanel.anchorMax = new Vector2(0f, 0f);
        choicePanel.pivot = new Vector2(0f, 0f);
        choicePanel.anchoredPosition = new Vector2(OuterMargin, OuterMargin);
        choicePanel.sizeDelta = Vector2.zero;
        choicePanelObj.AddComponent<RectMask2D>();
        choicePanelObj.SetActive(false);

        // Portrait is a Canvas sibling, not a panel child: the black background never covers it and the art is not clipped.
        var portraitObj = new GameObject("Portrait");
        portraitObj.transform.SetParent(canvasObj.transform, false);
        portraitImage = portraitObj.AddComponent<Image>();
        portraitImage.raycastTarget = false;
        portraitImage.preserveAspect = true;
        var prt = portraitImage.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 0f);
        prt.anchorMax = new Vector2(0f, 0f);
        prt.pivot = new Vector2(0f, 0f);
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
