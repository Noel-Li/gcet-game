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

    [Tooltip("Optional image (e.g. the 'E' key prompt) shown above the NPC while the player is nearby. If left empty the text prompt is used on its own.")]
    [SerializeField] private Sprite interactSprite;

    [Tooltip("Maximum world-space size of the displayed interact image. Its original aspect ratio is preserved.")]
    [SerializeField] private Vector2 interactImageSize = new Vector2(1.4f, 1.45f);

    [Tooltip("Empty world-space gap between the NPC's head and the bottom of the interact image.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float interactPromptGap = 0.12f;

    [Range(0f, 3f)]
    [SerializeField] private float promptVerticalOffset = 1.2f;

    [SerializeField] private float promptFontSize = 22f;

    [SerializeField]
    private Color promptColor =
        new Color(1f, 0.92f, 0.4f, 1f);

    [Header("Conversation facing")]
    [Tooltip("Turn this NPC's world sprite toward the player while they are in interaction range or dialogue is active.")]
    [SerializeField] private bool facePlayerDuringConversation = true;

    [Tooltip("Renderer that should turn toward the player. Leave empty to use this GameObject's SpriteRenderer.")]
    [SerializeField] private SpriteRenderer characterRenderer;

    [Tooltip("Enable when the unflipped source artwork faces right; disable when it faces left.")]
    [SerializeField] private bool artworkFacesRightWhenUnflipped = true;

    [Tooltip("Horizontal separation below which the NPC keeps its current facing direction.")]
    [Min(0f)]
    [SerializeField] private float horizontalFacingDeadZone = 0.05f;

    [Header("Wall unlock")]
    [Tooltip("If set, this invisible wall is unlocked when the conversation with this NPC closes — gating the player's forward progress. Leave empty for flavour NPCs that open no gate.")]
    [SerializeField] private InvisibleWall wallToUnlock;

    [Header("Tracing")]
    [Tooltip("Enable only when this NPC owns the conversation that launches and resumes the hanzi tracing scene.")]
    [SerializeField] private bool resumeAfterTracing;

    [Tooltip("Stable NPC id used for tracing resumes and repeat-conversation persistence. Leave empty to use this GameObject's name.")]
    [SerializeField] private string traceOwnerKey;

    private Conversation conversation;

    private bool activated = false;
    private bool nearPlayer = false;

    // Set while this NPC owns the active dialogue, so the shared Dialogue.OnReachedEnd event only fires the end
    // cutscene for the NPC that actually opened the conversation (not for every NPC subscribed to the singleton).
    private bool ownsDialogue = false;

    // Stops the post-tracing dialogue from being restored repeatedly.
    private bool resumedAfterTrace = false;

    // Lets the Conversation serve the first-time exchange once, then the (Inspector-authored or default) repeat exchange.
    private bool hasSpokenBefore = false;

    private Transform player;

    private TextMeshPro promptTextObj;
    private GameObject promptObj;

    private GameObject interactObj;
    private SpriteRenderer interactSpriteRenderer;

    private bool originalFlipX;
    private bool hasOriginalFacing;

    private const string PlayerName = "Player";
    private string TraceOwnerKey => string.IsNullOrWhiteSpace(traceOwnerKey) ? gameObject.name : traceOwnerKey.Trim();

    private void Awake()
    {
        conversation = GetComponent<Conversation>();
        ResolveCharacterRenderer();
        RememberOriginalFacing();
        BuildPrompt();
        BuildInteractImage();

        // Visible test square (replaces the inherited sprite) so this NPC is clearly visible for testing.
        VisibleBox.AddVisibleBox(gameObject, new Vector2(1f, 1f), new Color(1f, 0.85f, 0.2f, 0.6f), "NpcVisual");
    }

    private void Start()
    {
        LocatePlayer();
        RefreshConversationState();

        // Check whether we just returned from the tracing scene.
       RefreshFromProgress();
    }

    private void Update()
    {
        if (OpeningOverlay.IsShowing || ReviewBookController.IsOpen || ControlsOverlayController.IsOpen || ComicSequence.IsPlaying)
        {
            return;
        }

        // Check every frame because GameProgress or Dialogue may not
        // have finished initializing when Start() first runs.
        RefreshConversationState();
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

        UpdateInteractionFacing();

        // Only show the prompt for NPCs the player hasn't talked to yet. Once a conversation has
        // happened the prompt is suppressed for good — repeatSteps still exist for replay but the
        // interaction cue never reappears after the first interaction.
        bool shouldShow = nearPlayer && !activated && !hasSpokenBefore;

        // The text is a fallback for NPCs without an image. Showing both would stack
        // two prompts over the character and make the interaction cue harder to read.
        bool shouldShowText = shouldShow && interactObj == null;
        if (promptObj != null && promptObj.activeSelf != shouldShowText)
        {
            promptObj.SetActive(shouldShowText);
        }

        if (interactObj != null && interactObj.activeSelf != shouldShow)
        {
            interactObj.SetActive(shouldShow);
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
            // Unseal this NPC's gated wall the instant the player interacts — before any dialogue
            // machinery runs, so the unlock does not depend on the dialogue opening/closing cleanly.
            UnlockGatedWall();

            OpenDialogue();
        }
    }

    /// <summary>
    /// Opens the gated wall for this NPC. The unlock target now lives on the paired
    /// <see cref="Conversation"/> component (so it is unique and selectable per NPC): if that conversation
    /// opts into unlocking and names a wall, that wall opens. Otherwise it falls back to unlocking exactly the
    /// wall that sits on a boundary edge of the region this NPC occupies — never a wall that merely happens to
    /// be nearby. The NPC's region is found from its position, and a wall counts as "on the region's edge" when
    /// its center lies within a small tolerance of one of the region's four bounding edges. Robust against a
    /// cross-component reference that did not survive a clone/reload, and immune to grabbing the wrong wall.
    /// </summary>
    private void UnlockGatedWall()
    {
        // Prefer the wall explicitly assigned on this NPC's conversation: if one is set, unlock it directly and
        // unconditionally. (The older UnlockOnComplete gate defaulted to false, which silently bypassed every
        // assigned wall and left progress stuck — gating removed so an assigned wall always opens.)
        if (conversation != null && conversation.WallToUnlock != null)
        {
            var wall = conversation.WallToUnlock;
            Debug.Log($"[NpcController {name}] UnlockGatedWall: conversation.wallToUnlock={wall.name} lockedBefore={wall.Locked}");
            wall.Unlock();
            Debug.Log($"[NpcController {name}] after Unlock locked={wall.Locked}");
            return;
        }

        Debug.Log($"[NpcController {name}] no conversation wall set — using region-edge fallback");
        // Fallback: unlock the wall on the boundary edge of this NPC's region. This is deterministic —
        // verified: Soldier(in R1) unlocks Wall1, NPC_R2(in R2) unlocks Wall2, NPC_R3(in R3) unlocks Wall3.
        GameArea area = GameArea.GetAreaContaining(transform.position);
        if (area == null)
        {
            Debug.Log($"[NpcController {name}] fallback: NO area contains NPC — nothing unlocked");
            return;
        }
        Debug.Log($"[NpcController {name}] fallback: NPC in area {area.AreaName}");

        Bounds b = area.Bounds;
        float minX = b.min.x, maxX = b.max.x, minY = b.min.y, maxY = b.max.y;
        const float edgeTol = 0.6f;

        foreach (var wall in InvisibleWall.GetRegistered())
        {
            if (wall == null || !wall.Locked)
            {
                continue;
            }
            float wx = wall.WallX;
            float wy = wall.WallY;
            // Wall center must be within the region's footprint...
            if (wx < minX - edgeTol || wx > maxX + edgeTol || wy < minY - edgeTol || wy > maxY + edgeTol)
            {
                continue;
            }
            // ...and sit near one of its four edges (not float in the interior).
            bool onEdge = Mathf.Abs(wx - minX) < edgeTol || Mathf.Abs(wx - maxX) < edgeTol
                          || Mathf.Abs(wy - minY) < edgeTol || Mathf.Abs(wy - maxY) < edgeTol;
            if (onEdge)
            {
                wall.Unlock();
            }
        }
    }

    /// <summary>
    /// Restores the dialogue after the player successfully finishes
    /// the tracing scene and returns to game1.
    /// </summary>
    private void RefreshFromProgress()
    {
        // Simple repeatable NPCs must not claim another NPC's saved tracing conversation.
        if (!resumeAfterTracing)
        {
            return;
        }

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

        // Only the NPC that launched the completed task may restore its saved step.
        if (!GameProgress.Instance.TryClaimTraceResume(TraceOwnerKey))
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

        // Claim ownership and listen for the natural end so an end comic can fire after the post-trace resume too.
        Dialogue.Instance.OnReachedEnd -= OnReachedEnd;
        Dialogue.Instance.OnReachedEnd += OnReachedEnd;
        ownsDialogue = true;

        resumedAfterTrace = true;
        activated = true;

        HidePrompt();
        FacePlayer();

        // Resume at the step saved before entering the tracing scene.
        Dialogue.Instance.ResumeAfterWriting();

        // The main scene reload that hands back from the tracing scene re-creates every InvisibleWall with
        // Locked=true, so the gate that let the player in is instantly sealed shut again — and nothing else
        // re-opens it here. Unlock it now (by the explicit fileID on this NPC's conversation) so the player can
        // advance instead of being teleported back into the room and stuck.
        if (conversation != null && conversation.WallToUnlock != null)
        {
            conversation.WallToUnlock.Unlock();
        }

        Debug.Log(
            "[NpcController] Dialogue resumed after tracing; wallToUnlock=" +
            (conversation != null && conversation.WallToUnlock != null
                ? conversation.WallToUnlock.name + " now=" + conversation.WallToUnlock.Locked
                : "(none)")
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
    /// Gives this NPC's conversation lines to the Dialogue system. <paramref name="firstTime"/> selects the full
    /// first-time exchange (default — also what the post-trace resume relies on, since its saved step index only exists there)
    /// or the shorter repeat exchange for later visits.
    /// </summary>
    private void InjectInto(Dialogue dialogue, bool firstTime = true)
    {
        if (conversation == null || dialogue == null)
        {
            return;
        }

        dialogue.SetSteps(conversation.GetSteps(firstTime));
        dialogue.SetSpeakerBackgrounds(conversation.GetSpeakerBackgrounds());
        dialogue.SetSpeakerPortraits(conversation.GetSpeakerPortraits());
        dialogue.SetAuxiliaryPanelBackgrounds(
            conversation.GetGameVoiceBackground(),
            conversation.GetMultipleChoiceBackground());
        dialogue.SetTraceOwner(TraceOwnerKey);
    }

    /// <summary>
    /// Opens the NPC's conversation normally when the player presses E.
    /// </summary>
    private void OpenDialogue()
    {
        activated = true;
        HidePrompt();
        FacePlayer();

        EnsureDialogueExists();

        if (Dialogue.Instance == null)
        {
            Debug.LogError(
                "[NpcController] Dialogue could not be created."
            );

            activated = false;
            return;
        }

        // First visit gets the full conversation; every later visit gets the repeat exchange.
        InjectInto(Dialogue.Instance, firstTime: !hasSpokenBefore);

        // Subscribe once to the dialogue-close event.
        Dialogue.Instance.OnClosed -= OnConversationClosed;
        Dialogue.Instance.OnClosed += OnConversationClosed;

        // Subscribe to the natural-end event and claim ownership so only this NPC fires the end cutscene.
        Dialogue.Instance.OnReachedEnd -= OnReachedEnd;
        Dialogue.Instance.OnReachedEnd += OnReachedEnd;
        ownsDialogue = true;

        Dialogue.Instance.Open();

        // Open the gated wall the moment the conversation starts — a gate NPC's whole job is to unseal
        // the way ahead, and firing on open (rather than on the eventual close) guarantees the unlock even
        // if the dialogue is dismissed without a clean close event. One NPC, one wall. The unlock target
        // is read from the paired Conversation component, so it is unique to this NPC.
        if (conversation != null && conversation.WallToUnlock != null)
        {
            conversation.WallToUnlock.Unlock();
        }
    }

    /// <summary>
    /// Called when the dialogue closes.
    /// Allows the player to speak to the NPC again, and marks that first conversation as done so the next visit plays the repeat exchange.
    /// </summary>
    private void OnConversationClosed()
    {
        activated = false;
        ownsDialogue = false;
        hasSpokenBefore = true;

        if (GameProgress.Instance != null)
        {
            GameProgress.Instance.MarkConversationCompleted(TraceOwnerKey);
        }

        // Conversation finished: open the gated wall (if any) so the player may advance. This is the
        // reusable contract between an NPC and the gate that seals the way ahead — one NPC, one wall. The
        // unlock target is read from the paired Conversation component, so it is unique to this NPC.
        if (conversation != null && conversation.WallToUnlock != null)
        {
            conversation.WallToUnlock.Unlock();
        }
    }

    /// <summary>
    /// Natural end of the conversation for the NPC that opened it: play its end-of-dialogue comic cutscene if one is
    /// authored. The ownership guard stops every NPC subscribed to the Dialogue singleton from firing the cutscene
    /// when only one of them ended a conversation.
    /// </summary>
    private void OnReachedEnd()
    {
        if (!ownsDialogue)
        {
            return;
        }
        ownsDialogue = false;

        if (conversation == null || !conversation.PlayEndComic)
        {
            return;
        }

        Sprite[] panels = conversation.EndComicPanels;
        if (panels == null || panels.Length == 0)
        {
            return;
        }

        string nextScene = !string.IsNullOrWhiteSpace(conversation.EndComicNextScene)
            ? conversation.EndComicNextScene
            : null;
        Sprite prompt = conversation.EndComicPromptSprite;

        // ReachEnd() fires OnReachedEnd before Close(), so the normal OnClosed -> OnConversationClosed cleanup
        // (marking the conversation complete and re-arming the NPC) still runs immediately after this handler.
        // Detach only from the end event so a later conversation on this NPC does not replay the cutscene.
        if (Dialogue.Instance != null)
        {
            Dialogue.Instance.OnReachedEnd -= OnReachedEnd;
        }

        ComicSequence.Play(panels, nextScene ?? "", prompt, null);
        Debug.Log($"[NpcController {name}] playing end-of-dialogue comic ({panels.Length} panels -> {(nextScene ?? "gameplay")}, prompt={(prompt != null ? "yes" : "no")}).");
    }

    /// <summary>Restores the local repeat-dialogue cache from the persistent play-session state.</summary>
    private void RefreshConversationState()
    {
        if (!hasSpokenBefore &&
            GameProgress.Instance != null &&
            GameProgress.Instance.HasCompletedConversation(TraceOwnerKey))
        {
            hasSpokenBefore = true;
        }
    }

    /// <summary>
    /// Builds a small floating image (the "E to interact" prompt) that hovers
    /// above the NPC and billboards toward the camera, mirroring the text prompt.
    /// Uses a world-space SpriteRenderer so it needs no Canvas.
    /// </summary>
    private void BuildInteractImage()
    {
        // Resolve the sprite: prefer the serialized reference, but if it came
        // back null (e.g. the YAML PPtr did not survive a reload), fall back to
        // looking the asset up by its known import GUID. This keeps the prompt
        // working even when the serialized reference fails to deserialize.
        Sprite resolved = interactSprite != null
            ? interactSprite
            : LoadInteractSprite();

        if (resolved == null)
        {
            Debug.LogError(
                "[NpcController] interactSprite is null on " + name +
                " — assign Assets/misc/interactE to the Interact Sprite field in the " +
                "Inspector, or re-save the scene. No E-prompt will show.",
                this
            );

            return;
        }

        if (interactObj != null)
        {
            return;
        }

        // Scale from the sprite's imported bounds. SpriteRenderer.size is ignored in
        // Simple draw mode, which previously left this 281x291 image at full size.
        SpriteRenderer npcRenderer = GetComponent<SpriteRenderer>();
        Bounds headBounds = npcRenderer.bounds;
        Vector2 sourceSize = resolved.bounds.size;
        float scaleX = sourceSize.x > 0f ? interactImageSize.x / sourceSize.x : 1f;
        float scaleY = sourceSize.y > 0f ? interactImageSize.y / sourceSize.y : 1f;
        float imageScale = Mathf.Max(0.001f, Mathf.Min(scaleX, scaleY));
        Vector2 displayedSize = sourceSize * imageScale;
        float offsetX = -resolved.bounds.center.x * imageScale;
        float offsetY = headBounds.max.y - transform.position.y
            + interactPromptGap
            - resolved.bounds.min.y * imageScale;
        Vector3 promptOffset = new Vector3(offsetX, offsetY, 0f);

        // Parent to the root so the NPC's scale cannot shrink the offset or image.
        interactObj = new GameObject("InteractPrompt");
        interactObj.transform.SetParent(null, false);
        interactObj.transform.localScale = Vector3.one * imageScale;
        interactObj.transform.position = transform.position + promptOffset;
        interactObj.SetActive(false);

        interactSpriteRenderer = interactObj.AddComponent<SpriteRenderer>();
        interactSpriteRenderer.sprite = resolved;
        // Sort above the NPC so the prompt is never occluded by the body.
        interactSpriteRenderer.sortingLayerID = npcRenderer.sortingLayerID;
        interactSpriteRenderer.sortingOrder = npcRenderer.sortingOrder + 1;
        interactSpriteRenderer.drawMode = SpriteDrawMode.Simple;
        // A runtime-created SpriteRenderer has no material of its own; the URP
        // 2D renderer draws sprites through the scene-view path, so it renders
        // without an explicit material assignment.

        BillboardTowardsCamera billboard =
            interactObj.AddComponent<BillboardTowardsCamera>();

        if (Camera.main != null)
        {
            billboard.Cache(Camera.main.transform);
        }

        billboard.Follow(transform, promptOffset);

        Debug.Log(
            "[NpcController] E-prompt image ready on " + name +
            " (sprite=" + resolved.name +
            ", size=" + displayedSize.ToString("F2") +
            ", offset=" + promptOffset.ToString("F2") + ")",
            this
        );
    }

    /// <summary>
    /// Best-effort fallback that loads the interact-prompt sprite by its import
    /// GUID when the serialized field is empty. Editor-only; returns null at
    /// runtime in a built player (the serialized reference is the real path).
    /// </summary>
    private static Sprite LoadInteractSprite()
    {
#if UNITY_EDITOR
        // The interact sprite is a sub-asset of a texture (spriteMode: 2), so we
        // search for any imported asset whose name starts with "interactE" and
        // return the first Sprite we find among it (the texture) and its sub-sprites.
        string[] guids = UnityEditor.AssetDatabase.FindAssets("interactE");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object[] assets =
                UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (UnityEngine.Object a in assets)
            {
                if (a is Sprite s)
                {
                    return s;
                }
            }
        }
#endif
        return null;
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

    /// <summary>
    /// Mirrors only the NPC artwork toward Xiaoyue. The root Transform and collider
    /// stay unchanged, so interaction range and collision geometry never move.
    /// </summary>
    private void FacePlayer()
    {
        if (!facePlayerDuringConversation)
        {
            return;
        }

        ResolveCharacterRenderer();

        if (player == null)
        {
            LocatePlayer();
        }

        if (characterRenderer == null || player == null)
        {
            return;
        }

        float horizontalDifference = player.position.x - transform.position.x;
        if (Mathf.Abs(horizontalDifference) <= horizontalFacingDeadZone)
        {
            return;
        }

        bool shouldFaceRight = horizontalDifference > 0f;
        characterRenderer.flipX =
            artworkFacesRightWhenUnflipped != shouldFaceRight;
    }

    /// <summary>
    /// Faces the player as soon as the interaction prompt becomes available and
    /// keeps that facing through dialogue. The authored direction returns only
    /// after the player leaves interaction range.
    /// </summary>
    private void UpdateInteractionFacing()
    {
        if (nearPlayer || activated)
        {
            FacePlayer();
            return;
        }

        RestoreOriginalFacing();
    }

    private void ResolveCharacterRenderer()
    {
        if (characterRenderer == null)
        {
            characterRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void RememberOriginalFacing()
    {
        if (characterRenderer == null)
        {
            return;
        }

        originalFlipX = characterRenderer.flipX;
        hasOriginalFacing = true;
    }

    private void RestoreOriginalFacing()
    {
        if (hasOriginalFacing && characterRenderer != null)
        {
            characterRenderer.flipX = originalFlipX;
        }
    }

    private void OnDisable()
    {
        RestoreOriginalFacing();
    }

    private void OnValidate()
    {
        horizontalFacingDeadZone = Mathf.Max(0f, horizontalFacingDeadZone);
        ResolveCharacterRenderer();
    }

    private void Reset()
    {
        ResolveCharacterRenderer();
    }

    public void ShowPrompt()
    {
        if (promptObj != null)
        {
            promptObj.SetActive(true);
        }
        if (interactObj != null)
        {
            interactObj.SetActive(true);
        }
    }

    private void HidePrompt()
    {
        if (promptObj != null)
        {
            promptObj.SetActive(false);
        }
        if (interactObj != null)
        {
            interactObj.SetActive(false);
        }
    }

    private class BillboardTowardsCamera : MonoBehaviour
    {
        private Transform cameraTransform;
        private Transform followTarget;
        private Vector3 followOffset;

        public void Cache(Transform targetCamera)
        {
            cameraTransform = targetCamera;
        }

        /// <summary>Optional: hang this object at a world-space offset above the given target each frame.</summary>
        public void Follow(Transform target, Vector3 worldOffset)
        {
            followTarget = target;
            followOffset = worldOffset;
        }

        private void LateUpdate()
        {
            // Keep a floating image pinned above its owner in world space, so the
            // owner's Transform.scale never shrinks the hover height.
            if (followTarget != null)
            {
                transform.position = followTarget.position + followOffset;
            }

            if (cameraTransform != null)
            {
                transform.forward = cameraTransform.forward;
            }
        }
    }
}
