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

    private int index = 0;
    private bool open;
    private int pendingCol;
    private int pendingRow;
    private int pendingResumeStep = -1;
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
    }

    private void Update()
    {
        if (!open || !waitingInput)
        {
            return;
        }

        var mouse = Mouse.current;
        var kb = Keyboard.current;
        bool pressed = (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));

        if (pressed)
        {
            Advance();
        }
    }

    private void Advance()
    {
        DialogueStep current = steps[index];
        int nextIndex = index + 1;

        if (current.action == DialogueAction.Writing)
        {
            // Resume the conversation AFTER this writing step once the trace is resolved.
            pendingResumeStep = nextIndex;
            Close();
            LaunchWriting();
            return;
        }

        if (nextIndex >= steps.Count)
        {
            Close();
            return;
        }

        index = nextIndex;
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
        GameProgress.Instance.BeginTrace(pendingCol, pendingRow, pendingResumeStep);
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

        // TMP only assigns its layout/material/font in the frame the component is created and recomputes glyph meshes on a
        // later pass, so force an immediate mesh rebuild on both texts plus a canvas layout pass. The panel Image also only
        // renders its quad when dirty, so mark it each step. Without these, the objects are present in the hierarchy but
        // emit no vertices (render nothing).
        if (panelImage != null)
        {
            panelImage.SetAllDirty();
            panelImage.SetVerticesDirty();
        }
        nameTag.ForceMeshUpdate(true);
        body.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();

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

        float panelHeight = Pad + NameHeight + Pad + bodyH + Pad;
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

        nr.anchoredPosition = new Vector2(contentLeft + Pad, -Pad);
        br.anchoredPosition = new Vector2(contentLeft + Pad, -(Pad + NameHeight + Pad));

        // First frame may report ~0 mesh sizes; re-measure next frame so the box never collapses to nothing.
        if (bodyH < 1f && open)
        {
            StopCoroutine("ReflowNextFrame");
            StartCoroutine(ReflowNextFrame());
        }
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
