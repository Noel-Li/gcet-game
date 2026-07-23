using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

/// <summary>
/// Survives scene loads (<see cref="DontDestroyOnLoad"/>) as the contract between the main scene and the hanzi tracing
/// scene. The NPC's placeholder dialogue runs as a multi-step conversation; one of its steps links to the hanzi scene.
/// Each <see cref="Script1.OnCharacterCompleted"/> event records a distinct Hanzi for the review book.
/// Once the complete request is reviewed, <see cref="Script1.OnCharacterDone"/> returns to the launcher scene so
/// story traces can resume their owning conversation and review traces can return directly to gameplay.
///
/// The gated room and the conversation step to resume at are stored by plain data, so they survive the main scene being
/// torn down and rebuilt.
/// </summary>
public class GameProgress : MonoBehaviour
{
    public static GameProgress Instance { get; private set; }

    private bool tracingSubscribed;

    [SerializeField] private int resumeStep = -1;
    private bool dialogueResumed;
    [SerializeField] private bool postTraceCutscenePending;

    [Header("Opening Dialogue")]
    [Tooltip("Persists across scene reloads so the player's opening line is shown only on the first arrival in game1.")]
    [SerializeField] private bool openingDialogueShown;

    [Header("NPC Conversations")]
    [Tooltip("Stable NPC keys whose first conversation has already closed during this play session.")]
    [SerializeField] private List<string> completedConversationKeys = new List<string>();

    [Header("Review Book")]
    [Tooltip("Distinct traced Hanzi in first-completed order. The review artwork has twelve slots.")]
    [SerializeField] private List<string> reviewCharacters = new List<string>();

    [SerializeField] private bool reviewTraceActive;

    private const int ReviewCapacity = 12;
    private const string ReviewTraceOwnerKey = "__review_book__";


    [SerializeField] private int requiredTraceCount = 1;
    [SerializeField] private int completedTraceCount;
    [SerializeField] private List<string> requiredTraceCharacters = new List<string>();
    [SerializeField] private string traceOwnerKey;

    /// <summary>Stable keys (<see cref="InvisibleWall.StableKey"/>) of walls the player has opened. A scene reload
    /// (the trace reloads the main scene) re-creates every InvisibleWall with Locked=true, so without this the way the
    /// player already opened would be re-sealed and they'd be teleported back into the previous region and stuck.
    /// GameProgress is DontDestroyOnLoad, so this set survives the reload and is reapplied in
    /// <see cref="OnSceneLoaded"/>.</summary>
    [SerializeField] private List<string> unlockedWallKeys = new List<string>();

    /// <summary>
    /// The player position captured just before leaving for the tracing scene, so the reload can put the
    /// player back where they were instead of at the scene's authored default. Null until BeginTrace runs.
    /// </summary>
    private Vector3? savedPlayerPosition;


    /// <summary>The name of the scene the player gets returned to after a trace. Captured at the moment a trace
    /// begins (see <see cref="BeginTrace"/>) so the tracer always reloads whatever scene it was launched from —
    /// never a hardcoded scene. This keeps the region-demo scene (game1_test) from being swapped for the bare
    /// game1 scene, which previously teleported the player to the wrong place.</summary>
    [SerializeField] private string mainSceneName;

    /// <summary>The hanzi tracing scene to open on the writing step.</summary>
    [SerializeField] private string traceSceneName = "hanzi tracing base";

    /// <summary>Set the moment the hanzi tracer finishes a character correctly.</summary>
    public bool tracePassed { get; private set; }

    public int ResumeStep => resumeStep;
    public int RequiredTraceCount => requiredTraceCount;
    public int CompletedTraceCount => completedTraceCount;
    public IReadOnlyList<string> RequiredTraceCharacters => requiredTraceCharacters;
    public string TraceOwnerKey => traceOwnerKey;
    public IReadOnlyList<string> ReviewCharacters =>
        reviewCharacters ?? (reviewCharacters = new List<string>());
    public bool IsReviewTrace => reviewTraceActive;

    /// <summary>Returns whether this NPC should use its repeat conversation after a scene reload.</summary>
    public bool HasCompletedConversation(string npcKey)
    {
        string normalizedKey = NormalizeNpcKey(npcKey);
        if (string.IsNullOrEmpty(normalizedKey))
        {
            return false;
        }

        if (completedConversationKeys == null)
        {
            completedConversationKeys = new List<string>();
            return false;
        }

        for (int i = 0; i < completedConversationKeys.Count; i++)
        {
            if (string.Equals(completedConversationKeys[i], normalizedKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Persists that this NPC's first conversation has closed for the rest of the play session.</summary>
    public void MarkConversationCompleted(string npcKey)
    {
        string normalizedKey = NormalizeNpcKey(npcKey);
        if (string.IsNullOrEmpty(normalizedKey) || HasCompletedConversation(normalizedKey))
        {
            return;
        }

        if (completedConversationKeys == null)
        {
            completedConversationKeys = new List<string>();
        }

        completedConversationKeys.Add(normalizedKey);
    }

    private static string NormalizeNpcKey(string npcKey)
    {
        return string.IsNullOrWhiteSpace(npcKey) ? string.Empty : npcKey.Trim();
    }

    /// <summary>Claims the one-time opening line for this game session.</summary>
    public bool TryBeginOpeningDialogue()
    {
        if (openingDialogueShown)
        {
            return false;
        }

        openingDialogueShown = true;
        return true;
    }

    /// <summary>
    /// Called from the dialogue's writing step. Records the conversation step to resume on return and which NPC owns
    /// the trace, then opens the tracing scene. The player may move forward only once the trace completes — at which
    /// point the NPC re-opens its conversation (see <see cref="NpcController.RefreshFromProgress"/>), which reads the
    /// gate wall straight from the <see cref="Conversation.WallToUnlock"/> reference rather than from any grid
    /// coordinate, so the wall is unlocked by stable fileID and no invisible wall is ever spawned at runtime.
    /// </summary>
    public void BeginTrace(
        int stepToResume,
        int charactersToComplete = 1,
        string ownerKey = null,
        IList<string> charactersToTrace = null,
        bool launchedFromReview = false,
        bool playPostTraceCutscene = false)
    {
        reviewTraceActive = launchedFromReview;
        postTraceCutscenePending = !launchedFromReview && playPostTraceCutscene;
        resumeStep = stepToResume;
        requiredTraceCharacters.Clear();
        if (charactersToTrace != null)
        {
            for (int i = 0; i < charactersToTrace.Count; i++)
            {
                string characterName = charactersToTrace[i]?.Trim();
                if (!string.IsNullOrEmpty(characterName))
                {
                    requiredTraceCharacters.Add(characterName);
                }
            }
        }
        requiredTraceCount = requiredTraceCharacters.Count > 0
            ? requiredTraceCharacters.Count
            : Mathf.Max(1, charactersToComplete);
        traceOwnerKey = launchedFromReview ? ReviewTraceOwnerKey : ownerKey ?? string.Empty;
        completedTraceCount = 0;
        tracePassed = false;
        dialogueResumed = false;

        // Record which scene we're leaving so OnTraceCorrect reloads THIS scene (not a hardcoded one). Without
        // this, launching the tracer from a non-default scene (e.g. game1_test) snaps the player back into the
        // bare game1 scene and they reappear at the wrong place.
        // Capture the player's position now, while the main scene (and the Player it spawns) still exists.
        // The main scene is about to be unloaded for the tracing scene, which destroys the Player; we'll
        // restore this position in OnSceneLoaded when the player returns so they don't snap back to the
        // scene's authored default — they resume exactly where they were when they entered the interaction.
        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        // Prefer the Player's owning scene so an additive review overlay can never become the return target.
        mainSceneName = pm != null ? pm.gameObject.scene.name : SceneManager.GetActiveScene().name;
        savedPlayerPosition = pm != null ? pm.transform.position : (Vector3?)null;

        if (SceneManager.GetSceneByName(traceSceneName).isLoaded)
        {
            return;
        }
        SceneManager.LoadScene(traceSceneName);
    }

    /// <summary>Launches a one-character practice trace from an unlocked review-book slot.</summary>
    public void BeginReviewTrace(string characterName)
    {
        string normalizedName = string.IsNullOrWhiteSpace(characterName)
            ? string.Empty
            : characterName.Trim();
        if (string.IsNullOrEmpty(normalizedName) || !HasReviewCharacter(normalizedName))
        {
            Debug.LogWarning("[GameProgress] Review trace requested for a character that is not unlocked: '" + normalizedName + "'.");
            return;
        }

        Time.timeScale = 1f;
        BeginTrace(-1, 1, ReviewTraceOwnerKey, new[] { normalizedName }, true);
    }

    public bool HasReviewCharacter(string characterName)
    {
        if (reviewCharacters == null || string.IsNullOrWhiteSpace(characterName))
        {
            return false;
        }

        string normalizedName = characterName.Trim();
        for (int i = 0; i < reviewCharacters.Count; i++)
        {
            if (string.Equals(reviewCharacters[i], normalizedName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Atomically lets the NPC that launched the trace restore its dialogue exactly once.</summary>
    public bool TryClaimTraceResume(string ownerKey)
    {
        if (!tracePassed || dialogueResumed ||
            !string.Equals(traceOwnerKey, ownerKey ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        dialogueResumed = true;
        return true;
    }

    /// <summary>Consumes the optional post-trace cutscene for the NPC that owns the completed tracing task.</summary>
    public bool TryConsumePostTraceCutscene(string ownerKey)
    {
        if (!postTraceCutscenePending ||
            !string.Equals(traceOwnerKey, ownerKey ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        postTraceCutscenePending = false;
        return true;
    }


    /// <summary>One-shot diagnostic: logs the player position for the first several frames after a trace restore so the
    /// post-trace "teleported down" can be pinned to a specific frame/clamp.</summary>
    private System.Collections.IEnumerator LogPlayerPositionAfterTrace()
    {
        for (int i = 0; i < 10; i++)
        {
            var pm = FindObjectOfType<PlayerMovement>();
            if (pm != null)
            {
                var area = GameArea.GetAreaContaining(pm.transform.position);
                string areaName = area != null ? area.AreaName : "NONE";
                Debug.Log($"[GameProgress] post-trace frame {i}: player at {pm.transform.position} (area={areaName})");
            }
            yield return null;
        }
    }

    /// <summary>Repositions the freshly-spawned Player back to where they were when the trace began.</summary>
    private void ApplySavedPlayerPosition()
    {
        if (!savedPlayerPosition.HasValue)
        {
            return;
        }

        PlayerMovement pm = FindObjectOfType<PlayerMovement>();
        if (pm != null)
        {
            Debug.Log($"[GameProgress] ApplySavedPlayerPosition: restoring player to {savedPlayerPosition.Value} (was at {pm.transform.position})");
            // Rigidbody-driven movement: move through the body so collision stays consistent and physics
            // doesn't fight the teleport.
            Rigidbody2D body = pm.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = savedPlayerPosition.Value;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            else
            {
                pm.transform.position = savedPlayerPosition.Value;
            }
            Debug.Log($"[GameProgress] after restore player at {pm.transform.position}");
        }

        savedPlayerPosition = null;
    }

    /// <summary>Testing aid: skip the trace and return to the dialogue as if every character were traced correctly.
    /// Mirrors the end state of <see cref="OnTraceCorrect"/> (tracePassed + reload the scene the trace was launched from),
    /// so the post-trace conversation resumes identically. Bound to Delete+Backspace in the tracer.</summary>
    public void ForcePassTrace()
    {
        if (tracePassed)
        {
            return;
        }

        tracePassed = true;
        reviewTraceActive = false;

        if (string.IsNullOrEmpty(mainSceneName))
        {
            Debug.LogWarning("[GameProgress] ForcePassTrace: no launcher scene recorded — nothing to return to.");
            return;
        }
        SceneManager.LoadScene(mainSceneName);
    }

    public void OnTraceCorrect()
    {
        if (tracePassed)
        {
            return;
        }

        completedTraceCount++;
        if (completedTraceCount < requiredTraceCount)
        {
            return;
        }

        tracePassed = true;
        reviewTraceActive = false;

        // The main scene is reloaded so its Objects (Player, dialogue, walls) are re-created fresh at their authored
        // transforms. No invisible wall is spawned here: the gate wall assigned to each NPC by fileID in the Scene is
        // unlocked through <see cref="Conversation.WallToUnlock"/> / <see cref="NpcController.RefreshFromProgress"/>
        // once the conversation re-opens after the trace, so there is nothing for GameProgress to create. NOTE: the
        // reload re-creates every InvisibleWall with Locked=true, so any wall the player already opened is re-sealed —
        // <see cref="ReapplyUnlockedWalls"/> in <see cref="OnSceneLoaded"/> re-opens them from this persistent set.
        SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>Records that this wall stays open for the rest of the play session. Called from
    /// <see cref="InvisibleWall.Unlock"/>. Keys are stable across reloads because they are derived from each wall's
    /// authored (never-moved) world position.</summary>
    public void PersistUnlockedWall(string wallKey)
    {
        if (string.IsNullOrEmpty(wallKey))
        {
            return;
        }
        if (unlockedWallKeys == null)
        {
            unlockedWallKeys = new List<string>();
        }
        if (!unlockedWallKeys.Contains(wallKey))
        {
            unlockedWallKeys.Add(wallKey);
        }
    }

    /// <summary>Re-opens every wall the player previously unlocked. The trace reloads the main scene and re-creates
    /// each InvisibleWall with Locked=true, so without this the way already opened is re-sealed and the player is
    /// teleported back into the prior region and stuck there. Safe to call every scene load — only matching walls are
    /// affected, and unlocking an already-unlocked wall is a no-op.</summary>
    public void ReapplyUnlockedWalls()
    {
        if (unlockedWallKeys == null || unlockedWallKeys.Count == 0)
        {
            return;
        }
        var keys = new HashSet<string>(unlockedWallKeys);
        foreach (var wall in InvisibleWall.GetRegistered())
        {
            if (wall != null && keys.Contains(wall.StableKey))
            {
                wall.Unlock();
            }
        }
    }

    /// <summary>
    /// <see cref="GameProgress"/> must exist before the main scene finishes loading so it can subscribe to
    /// <see cref="SceneManager.sceneLoaded"/>; the static constructor guarantees that.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null)
        {
            return;
        }
        var carrier = new GameObject("GameProgress");
        carrier.AddComponent<GameProgress>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (tracingSubscribed)
        {
            Script1.OnCharacterDone -= HandleCharacterDone;
            Script1.OnCharacterCompleted -= HandleCharacterCompleted;
            tracingSubscribed = false;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainSceneName)
        {
            // Restore the player to the position captured before the tracer launched (see
            // <see cref="ApplySavedPlayerPosition"/>) so they resume the interaction exactly where they entered it.
            // No invisible wall is (re)created here — gates are wall objects authored in the Scene and unlocked
            // through the NPC's conversation once it re-opens.
            ApplySavedPlayerPosition();
            // The reload re-created every InvisibleWall with Locked=true, re-sealing any way the player already
            // opened. Re-open them from the persistent set so a wall, once unlocked, stays unlocked for the session.
            ReapplyUnlockedWalls();
            StartCoroutine(LogPlayerPositionAfterTrace());
        }

        bool traceLoaded = SceneManager.GetSceneByName(traceSceneName).isLoaded;
        if ((scene.name == traceSceneName || traceLoaded) && !tracingSubscribed)
        {
            Script1.OnCharacterDone += HandleCharacterDone;
            Script1.OnCharacterCompleted += HandleCharacterCompleted;
            tracingSubscribed = true;
        }
        else if (tracingSubscribed)
        {
            Script1.OnCharacterDone -= HandleCharacterDone;
            Script1.OnCharacterCompleted -= HandleCharacterCompleted;
            tracingSubscribed = false;
        }
    }

    private void HandleCharacterDone(bool correct)
    {
        if (correct)
        {
            OnTraceCorrect();
        }
    }

    private void HandleCharacterCompleted(CharacterData character)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.characterName))
        {
            return;
        }

        string normalizedName = character.characterName.Trim();
        if (HasReviewCharacter(normalizedName))
        {
            return;
        }

        if (reviewCharacters == null)
        {
            reviewCharacters = new List<string>();
        }

        if (reviewCharacters.Count >= ReviewCapacity)
        {
            Debug.LogWarning("[GameProgress] Review book is full; cannot add '" + normalizedName + "'.");
            return;
        }

        reviewCharacters.Add(normalizedName);
    }
}
