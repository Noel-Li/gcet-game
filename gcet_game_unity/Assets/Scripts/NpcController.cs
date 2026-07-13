using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controls an NPC that the player can speak to when nearby.
///
/// This version also restores the dialogue after the player returns
/// from the hanzi tracing scene.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Conversation))]
public class NpcController : MonoBehaviour
{
    [Header("Proximity")]
    [Tooltip("Distance within which the player can interact with this NPC.")]
    [SerializeField] private float interactRange = 2.5f;

    [Header("Prompt")]
    [SerializeField] private string promptText = "Press E to talk";

    [Range(0f, 3f)]
    [SerializeField] private float promptVerticalOffset = 1.2f;

    [SerializeField] private float promptFontSize = 22f;

    [SerializeField]
    private Color promptColor =
        new Color(1f, 0.92f, 0.4f, 1f);

    private Conversation conversation;

    private bool activated = false;
    private bool nearPlayer = false;

    // Stops the post-tracing dialogue from being restored repeatedly.
    private bool resumedAfterTrace = false;

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
        LocatePlayer();

        // Check whether we just returned from the tracing scene.
        RefreshFromProgress();
    }

    private void Update()
    {
        if (PauseController.IsPaused)
        {
            return;
        }

        // Check every frame because GameProgress or Dialogue may not
        // have finished initializing when Start() first runs.
        RefreshFromProgress();

        // Find the player again if the scene was reloaded.
        if (player == null)
        {
            LocatePlayer();
        }

        nearPlayer =
            player != null &&
            Vector2.Distance(transform.position, player.position)
            <= interactRange;

        // Show or hide the "Press E to talk" message.
        if (promptObj != null)
        {
            bool shouldShow = nearPlayer && !activated;

            if (promptObj.activeSelf != shouldShow)
            {
                promptObj.SetActive(shouldShow);
            }
        }

        // Do not allow another interaction while dialogue is active.
        if (activated)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (
            keyboard != null &&
            keyboard.eKey.wasPressedThisFrame &&
            nearPlayer
        )
        {
            OpenDialogue();
        }
    }

    /// <summary>
    /// Restores the dialogue after the player successfully finishes
    /// the tracing scene and returns to game1.
    /// </summary>
    private void RefreshFromProgress()
    {
        // We already restored the dialogue once.
        if (resumedAfterTrace)
        {
            return;
        }

        // GameProgress does not exist yet.
        if (GameProgress.Instance == null)
        {
            return;
        }

        // The tracing task has not been passed yet.
        if (!GameProgress.Instance.tracePassed)
        {
            return;
        }

        // game1 was reloaded, so the previous Dialogue object
        // may have been destroyed. Create a new one when necessary.
        EnsureDialogueExists();

        if (Dialogue.Instance == null)
        {
            Debug.LogError(
                "[NpcController] Could not create Dialogue after tracing."
            );

            return;
        }

        // Give the newly created Dialogue object this NPC's lines.
        InjectInto(Dialogue.Instance);

        // Reconnect the dialogue closing event.
        Dialogue.Instance.OnClosed -= OnConversationClosed;
        Dialogue.Instance.OnClosed += OnConversationClosed;

        resumedAfterTrace = true;
        activated = true;

        HidePrompt();

        // Resume at the step saved before entering the tracing scene.
        Dialogue.Instance.ResumeAfterWriting();

        Debug.Log(
            "[NpcController] Dialogue resumed after tracing."
        );
    }

    /// <summary>
    /// Creates the Dialogue object if the previous one was destroyed
    /// during a scene change.
    /// </summary>
    private void EnsureDialogueExists()
    {
        if (Dialogue.Instance != null)
        {
            return;
        }

        GameObject dialogueObject = new GameObject("Dialogue");
        dialogueObject.AddComponent<Dialogue>();
    }

    /// <summary>
    /// Gives this NPC's conversation lines to the Dialogue system.
    /// </summary>
    private void InjectInto(Dialogue dialogue)
    {
        if (conversation == null || dialogue == null)
        {
            return;
        }

        dialogue.SetSteps(conversation.GetSteps());
    }

    /// <summary>
    /// Opens the NPC's conversation normally when the player presses E.
    /// </summary>
    private void OpenDialogue()
    {
        activated = true;
        HidePrompt();

        GameArea area =
            GameArea.GetAreaContaining(transform.position);

        int col = area != null ? area.AreaCol : 0;
        int row = area != null ? area.AreaRow : 0;

        EnsureDialogueExists();

        if (Dialogue.Instance == null)
        {
            Debug.LogError(
                "[NpcController] Dialogue could not be created."
            );

            activated = false;
            return;
        }

        InjectInto(Dialogue.Instance);

        // Subscribe once to the dialogue-close event.
        Dialogue.Instance.OnClosed -= OnConversationClosed;
        Dialogue.Instance.OnClosed += OnConversationClosed;

        Dialogue.Instance.Open(col, row);
    }

    /// <summary>
    /// Called when the dialogue closes.
    /// Allows the player to speak to the NPC again.
    /// </summary>
    private void OnConversationClosed()
    {
        activated = false;
    }

    private void BuildPrompt()
    {
        if (promptObj != null)
        {
            return;
        }

        promptObj = new GameObject("NpcPrompt");
        promptObj.transform.SetParent(transform, false);

        promptObj.transform.localPosition =
            new Vector3(0f, promptVerticalOffset, 0f);

        promptObj.SetActive(false);

        promptTextObj = promptObj.AddComponent<TextMeshPro>();

        if (TMP_Settings.defaultFontAsset != null)
        {
            promptTextObj.font =
                TMP_Settings.defaultFontAsset;
        }

        promptTextObj.text = promptText;
        promptTextObj.fontSize = promptFontSize;
        promptTextObj.color = promptColor;
        promptTextObj.alignment =
            TextAlignmentOptions.Center;

        promptTextObj.outlineWidth = 0.18f;
        promptTextObj.outlineColor = Color.black;
        promptTextObj.overflowMode =
            TextOverflowModes.Overflow;

        promptTextObj.enableWordWrapping = false;
        promptTextObj.raycastTarget = false;

        promptTextObj.rectTransform.localScale =
            new Vector3(0.07f, 0.07f, 0.07f);

        BillboardTowardsCamera billboard =
            promptObj.AddComponent<BillboardTowardsCamera>();

        if (Camera.main != null)
        {
            billboard.Cache(Camera.main.transform);
        }
    }

    private void LocatePlayer()
    {
        GameObject playerObject =
            GameObject.Find(PlayerName);

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    public void ShowPrompt()
    {
        if (promptObj != null)
        {
            promptObj.SetActive(true);
        }
    }

    private void HidePrompt()
    {
        if (promptObj != null)
        {
            promptObj.SetActive(false);
        }
    }

    private class BillboardTowardsCamera : MonoBehaviour
    {
        private Transform cameraTransform;

        public void Cache(Transform targetCamera)
        {
            cameraTransform = targetCamera;
        }

        private void LateUpdate()
        {
            if (cameraTransform != null)
            {
                transform.forward = cameraTransform.forward;
            }
        }
    }
}