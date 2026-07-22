using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Presents a sequence of comic panels. Space advances one panel; pressing Space after the final panel
/// either loads <see cref="nextSceneName"/> or, when that is empty, finishes and returns to gameplay.
///
/// Two usages share this component:
///  * Scene-authored (ComicScene): a full-screen <see cref="comicImage"/> is assigned in the Inspector and
///    the panels play over that pre-built canvas.
///  * Runtime-instantiated (end-of-dialogue cutscene): call <see cref="Play"/>. With no image assigned the
///    component builds its own overlay canvas, plays the panels, then tears itself back down. This keeps the
///    end cutscene a data-driven, Inspector-authored asset on the NPC's Conversation instead of a whole scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class ComicSequence : MonoBehaviour
{
    [Header("Comic")]
    [Tooltip("Full-screen UI Image used to display the current panel. Leave empty to build a runtime overlay canvas (end-of-dialogue cutscene).")]
    [SerializeField] private Image comicImage;

    [Tooltip("Panels shown in order. The current story uses game-related scenes/1.png through 6.png.")]
    [SerializeField] private Sprite[] comicPanels;

    [Tooltip("Optional prompt shown bottom-right (e.g. a 'Press Space to Continue' image). Shown only on the runtime overlay canvas.")]
    [SerializeField] private Sprite pressSpacePrompt;

    [Header("Flow")]
    [Tooltip("Scene loaded after the player presses Space once more on the final panel. Leave empty to return to gameplay instead of loading a scene.")]
    [SerializeField] private string nextSceneName = "ControlsScene";

    [Tooltip("Prevents transition input from immediately skipping the first panel.")]
    [Min(0f)]
    [SerializeField] private float initialInputDelay = 0.2f;

    /// <summary>True while any comic sequence is on screen. Player movement reads this to freeze the player during a cutscene.</summary>
    public static bool IsPlaying { get; private set; }

    /// <summary>Raised when a runtime-played comic finishes without loading a new scene (the empty-nextScene path).</summary>
    public event System.Action OnFinished;

    private int panelIndex;
    private float inputReadyAt;
    private bool loadingNextScene;
    private bool runtimeCanvas;

    private void Awake()
    {
        panelIndex = 0;
        inputReadyAt = Time.unscaledTime + initialInputDelay;
        // The scene-authored case (ComicScene) has both the image and panels serialized, so it can begin at once.
        // The runtime factory assigns fields AFTER Awake, so it calls Begin() itself and must not start here.
        if (comicImage != null && comicPanels != null && comicPanels.Length > 0)
        {
            Begin();
        }
    }

    /// <summary>Build the throwaway overlay canvas (called from Begin() so runtime-assigned fields like the prompt sprite are already set).</summary>
    private void EnsureRuntimeCanvas()
    {
        if (comicImage != null)
        {
            return;
        }
        var canvasObj = new GameObject("ComicCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Render above the Dialogue canvas (sortingOrder 100) so the cutscene covers an active conversation.
        canvas.sortingOrder = 200;
        canvasObj.AddComponent<CanvasScaler>();

        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        // Black letterbox behind the art so no game frame leaks around a non-matching aspect.
        var backgroundObj = new GameObject("LetterboxBackground");
        backgroundObj.transform.SetParent(canvasObj.transform, false);
        var backgroundImage = backgroundObj.AddComponent<Image>();
        backgroundImage.color = Color.black;
        backgroundImage.raycastTarget = false;
        var backgroundRect = backgroundObj.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var panelObj = new GameObject("ComicPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        comicImage = panelObj.AddComponent<Image>();
        comicImage.raycastTarget = false;
        var panelRect = comicImage.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        BuildPressSpacePrompt(canvasObj);

        runtimeCanvas = true;
    }

    /// <summary>
    /// Play a comic cutscene as a runtime overlay. Builds its own canvas, shows the panels, and on the final
    /// panel either loads <paramref name="nextSceneName"/> (when non-empty) or invokes <paramref name="onComplete"/>
    /// and tears the overlay down. Returns the component so callers can subscribe to <see cref="OnFinished"/>.
    /// </summary>
    public static ComicSequence Play(Sprite[] panels, string nextSceneName, Sprite promptSprite, System.Action onComplete = null)
    {
        var go = new GameObject("ComicSequence");
        var sequence = go.AddComponent<ComicSequence>();
        sequence.comicPanels = panels != null ? panels : new Sprite[0];
        sequence.nextSceneName = nextSceneName;
        sequence.pressSpacePrompt = promptSprite;
        sequence.OnFinished = null;
        if (onComplete != null)
        {
            sequence.OnFinished += onComplete;
        }
        sequence.Begin();
        return sequence;
    }

    /// <summary>Reset to the first panel and start accepting advance input. Safe to call once after assigning panels at runtime.</summary>
    public void Begin()
    {
        panelIndex = 0;
        EnsureRuntimeCanvas();
        inputReadyAt = Time.unscaledTime + initialInputDelay;
        ShowCurrentPanel();
        IsPlaying = true;
    }

    /// <summary>Build the bottom-right "press space" prompt onto the runtime canvas. No-op when no prompt sprite is assigned.</summary>
    private void BuildPressSpacePrompt(GameObject canvasObj)
    {
        if (pressSpacePrompt == null || canvasObj == null)
        {
            return;
        }
        var promptObj = new GameObject("PressSpacePrompt");
        promptObj.transform.SetParent(canvasObj.transform, false);
        var promptImage = promptObj.AddComponent<Image>();
        promptImage.sprite = pressSpacePrompt;
        promptImage.raycastTarget = false;
        promptImage.preserveAspect = true;
        var promptRect = promptImage.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(1f, 0f);
        promptRect.anchorMax = new Vector2(1f, 0f);
        promptRect.pivot = new Vector2(1f, 0f);
        promptRect.anchoredPosition = new Vector2(-24f, 24f);
        promptRect.sizeDelta = new Vector2(250f, 150f);
    }

    private void Update()
    {
        if (!IsPlaying || loadingNextScene || Time.unscaledTime < inputReadyAt)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame)
        {
            return;
        }

        if (comicPanels != null && panelIndex < comicPanels.Length - 1)
        {
            panelIndex++;
            ShowCurrentPanel();
            return;
        }

        // Final panel. Either load the next scene or finish and return to gameplay.
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            FinishWithoutScene();
        }
        else
        {
            LoadNextScene();
        }
    }

    private void ShowCurrentPanel()
    {
        if (comicImage == null)
        {
            Debug.LogError("[ComicSequence] Comic Image is not assigned.", this);
            return;
        }

        if (comicPanels == null || comicPanels.Length == 0)
        {
            Debug.LogError("[ComicSequence] No comic panels are assigned.", this);
            return;
        }

        panelIndex = Mathf.Clamp(panelIndex, 0, comicPanels.Length - 1);
        comicImage.sprite = comicPanels[panelIndex];
        // The comic art is authored at the 1920x1080 reference resolution. Stretching
        // with the Canvas avoids side bars in wider Game views while keeping every
        // edge of the panel visible.
        comicImage.preserveAspect = false;
    }

    private void LoadNextScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("[ComicSequence] Next Scene Name is empty.", this);
            return;
        }

        loadingNextScene = true;
        IsPlaying = false;
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }

    private void FinishWithoutScene()
    {
        loadingNextScene = true;
        IsPlaying = false;
        var callback = OnFinished;
        OnFinished = null;
        // This overlay canvas exists only for the length of the cutscene; remove it so gameplay is untouched.
        if (runtimeCanvas)
        {
            Destroy(gameObject);
        }
        callback?.Invoke();
    }

    private void OnValidate()
    {
        initialInputDelay = Mathf.Max(0f, initialInputDelay);
    }
}
