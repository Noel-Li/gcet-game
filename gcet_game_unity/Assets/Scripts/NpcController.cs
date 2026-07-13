using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// A proximity-activated NPC. When the Player walks within <see cref="interactRange"/> units the NPC shows a "Press E to talk" prompt above its head; pressing E opens the dialogue. The <em>content</em> lives on a sibling <see cref="Conversation"/> component (editable in the Inspector, per-NPC); this class only knows how to open it. Forward progress is gated the same way it was — the Top-exit gate the player movement already respects flips only on a correct trace, handled by <see cref="GameProgress"/>. The NPC returns to an interactable state when the conversation ends.</summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Conversation))]
public class NpcController : MonoBehaviour
{
    [Header("Proximity")]
    [Tooltip("World-unit distance within which the player can interact with the NPC.")]
    [SerializeField] private float interactRange = 2.5f;

    [Header("Prompt")]
    [SerializeField] private string promptText = "Press E to talk";
    [Range(0f, 3f)]
    [SerializeField] private float promptVerticalOffset = 1.2f;
    [SerializeField] private float promptFontSize = 22f;
    [SerializeField] private Color promptColor = new Color(1f, 0.92f, 0.4f, 1f);

    private Conversation conversation;
    private bool activated = false;
    private bool nearPlayer = false;
    private Transform player;
    private TextMeshPro promptTextObj;
    private GameObject promptObj;
    private const string PlayerName = "Player";

    private void Awake()
    {
        conversation = GetComponent<Conversation>();
        BuildPrompt();
    }

    private void Start()
    {
        // Locate Player (may be destroyed/re-instantiated — refreshed in Update).
        LocatePlayer();
        RefreshFromProgress();
    }

    private void RefreshFromProgress()
    {
        if (activated)
        {
            return;
        }
        if (Dialogue.Instance != null)
        {
            InjectInto(Dialogue.Instance);
        }
        if (GameProgress.Instance != null && GameProgress.Instance.tracePassed && Dialogue.Instance != null)
        {
            // Passed the trace mid-conversation: resume the post-writing lines automatically.
            activated = true;
            Dialogue.Instance.ResumeAfterWriting();
        }
    }

    private void InjectInto(Dialogue d)
    {
        if (conversation == null)
        {
            return;
        }
        d.SetSteps(conversation.GetSteps());
    }

    private void Update()
    {
        if (PauseController.IsPaused)
        {
            return;
        }

        // Keep a valid Player reference even after scene reloads.
        if (player == null)
        {
            LocatePlayer();
        }
        nearPlayer = player != null && Vector2.Distance(transform.position, player.position) <= interactRange;

        // Toggle the proximity prompt.
        if (promptObj != null && promptObj.activeSelf != nearPlayer && !activated)
        {
            promptObj.SetActive(nearPlayer && !activated);
        }

        if (activated)
        {
            return;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame && nearPlayer)
        {
            OpenDialogue();
        }
    }

    private void OpenDialogue()
    {
        activated = true;
        HidePrompt();

        GameArea area = GameArea.GetAreaContaining(transform.position);
        int col = area != null ? area.AreaCol : 0;
        int row = area != null ? area.AreaRow : 0;

        if (Dialogue.Instance == null)
        {
            var obj = new GameObject("Dialogue");
            obj.AddComponent<Dialogue>();
        }

        // Conversation.GetSteps() always returns a populated list (falling back to defaults when the Inspector list is
        // empty), so the NPC always has something to say.
        InjectInto(Dialogue.Instance);

        // Subscribe ONCE to the close event so we can re-arm the NPC for subsequent conversations. Unsubscribe first to be idempotent across multiple Dialogue lifetimes.
        Dialogue.Instance.OnClosed -= OnConversationClosed;
        Dialogue.Instance.OnClosed += OnConversationClosed;

        Dialogue.Instance.Open(col, row);
    }

    /// <summary>Callback when the conversation closes — clears <see cref="activated"/> so the player can E-interact again.</summary>
    private void OnConversationClosed()
    {
        activated = false;
    }

    private void BuildPrompt()
    {
        // A tiny billboarded TextMeshPro we can show/hide. Built inside the NPC's own transform and positioned above its head in world space.
        if (promptObj != null)
        {
            return;
        }
        promptObj = new GameObject("NpcPrompt");
        promptObj.transform.SetParent(transform, false);
        promptObj.transform.localPosition = new Vector3(0f, promptVerticalOffset, 0f);
        promptObj.SetActive(false);

        promptTextObj = promptObj.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            promptTextObj.font = TMP_Settings.defaultFontAsset;
        }
        promptTextObj.text = promptText;
        promptTextObj.fontSize = promptFontSize;
        promptTextObj.color = promptColor;
        promptTextObj.alignment = TextAlignmentOptions.Center;
        promptTextObj.outlineWidth = 0.18f;
        promptTextObj.outlineColor = Color.black;
        promptTextObj.overflowMode = TextOverflowModes.Overflow;
        promptTextObj.enableWordWrapping = false;
        promptTextObj.raycastTarget = false;
        // Scale the TMP so it stays roughly screen-fixed regardless of distance.
        promptTextObj.rectTransform.localScale = new Vector3(0.07f, 0.07f, 0.07f);

        // Face the camera.
        var locked = promptObj.AddComponent<BillboardTowardsCamera>();
        if (Camera.main != null) locked.Cache(Camera.main.transform);
    }

    private void LocatePlayer()
    {
        GameObject go = GameObject.Find(PlayerName);
        if (go != null) player = go.transform;
    }

    public void ShowPrompt()
    {
        if (promptObj != null) promptObj.SetActive(true);
    }

    private void HidePrompt()
    {
        if (promptObj != null) promptObj.SetActive(false);
    }

    private class BillboardTowardsCamera : MonoBehaviour
    {
        private Transform cam;
        public void Cache(Transform c) => cam = c;
        private void LateUpdate()
        {
            if (cam != null) transform.forward = cam.forward;
        }
    }
}
