using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

/// <summary>
/// Survives scene loads (<see cref="DontDestroyOnLoad"/>) as the contract between the main scene and the hanzi tracing
/// scene. The NPC's placeholder dialogue runs as a multi-step conversation; one of its steps links to the hanzi scene.
/// The tracer fires <see cref="Script1.OnCharacterDone"/> the moment a character is completed correctly. Once that fires
/// we do the work the NPC used to do immediately — unlock the forward (Top) exit of the room the NPC stands in — and
/// reload the main scene at the post-writing step so the conversation can continue and tell the player where to go next.
/// (Uses the same Top-exit gate the player movement already respects, so forward progress is data-driven.)
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

    [Header("Opening Dialogue")]
    [Tooltip("Persists across scene reloads so the player's opening line is shown only on the first arrival in game1.")]
    [SerializeField] private bool openingDialogueShown;

    [Header("NPC Conversations")]
    [Tooltip("Stable NPC keys whose first conversation has already closed during this play session.")]
    [SerializeField] private List<string> completedConversationKeys = new List<string>();


    [SerializeField] private int requiredTraceCount = 1;
    [SerializeField] private int completedTraceCount;
    [SerializeField] private List<string> requiredTraceCharacters = new List<string>();
    [SerializeField] private string traceOwnerKey;

    /// <summary>
    /// The player position captured just before leaving for the tracing scene, so the reload can put the
    /// player back where they were instead of at the scene's authored default. Null until BeginTrace runs.
    /// </summary>
    private Vector3? savedPlayerPosition;


    /// <summary>The NPC scene to load back into.</summary>
    [SerializeField] private string mainSceneName = "game1";

    /// <summary>The hanzi tracing scene to open on the writing step.</summary>
    [SerializeField] private string traceSceneName = "hanzi tracing base";

    /// <summary>Grid coordinate of the room whose forward exit should open on a correct trace.</summary>
    [SerializeField] private int targetCol;
    [SerializeField] private int targetRow;

    /// <summary>Grid coordinate of the invisible wall that gates forward progress until the conversation completes.
    /// Mirrors <see cref="targetCol"/>/<see cref="targetRow"/> but for the wall, which is now the only forward gate.</summary>
    [SerializeField] private int wallCol = 1;
    [SerializeField] private int wallRow = 1;

    /// <summary>Set the moment the hanzi tracer finishes a character correctly.</summary>
    public bool tracePassed { get; private set; }

    public int ResumeStep => resumeStep;
    public int RequiredTraceCount => requiredTraceCount;
    public int CompletedTraceCount => completedTraceCount;
    public IReadOnlyList<string> RequiredTraceCharacters => requiredTraceCharacters;
    public string TraceOwnerKey => traceOwnerKey;

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
    /// Called from the dialogue's writing step. Records which room this NPC gates and which conversation step to resume
    /// at on return, then opens the tracing scene. The player may move forward only once the trace completes.
    /// </summary>
    public void BeginTrace(
        int areaCol,
        int areaRow,
        int stepToResume,
        int charactersToComplete = 1,
        string ownerKey = null,
        IList<string> charactersToTrace = null)
    {
        targetCol = areaCol;
        targetRow = areaRow;
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
        traceOwnerKey = ownerKey ?? string.Empty;
        completedTraceCount = 0;
        tracePassed = false;
        dialogueResumed = false;

        // Capture the player's position now, while the main scene (and the Player it spawns) still exists.
        // The main scene is about to be unloaded for the tracing scene, which destroys the Player; we'll
        // restore this position in OnSceneLoaded when the player returns so they don't snap back to the
        // scene's authored default.
        PlayerMovement pm = FindObjectOfType<PlayerMovement>();
        savedPlayerPosition = pm != null ? pm.transform.position : (Vector3?)null;

        if (SceneManager.GetSceneByName(traceSceneName).isLoaded)
        {
            return;
        }
        SceneManager.LoadScene(traceSceneName);
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


    /// <summary>
    /// Fired by <see cref="Script1.OnCharacterDone"/> whenever one character is traced correctly.
    /// The tracing scene remains open until every character requested by the dialogue has been completed.
    /// </summary>

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

    /// <summary>Fired by <see cref="Script1.OnCharacterDone"/> when the character is traced correctly.</summary>

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

        // The Areas live in the main scene, which is currently unloaded. Defer the gate unlock to OnSceneLoaded.
        SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>Flips the forward gate once the main scene has reloaded and its Areas are registered.
    /// Area exits are open now, so the only thing sealing the way ahead is the invisible wall — unlock it.</summary>
    private void ApplyForwardUnlock()
    {
        if (!tracePassed)
        {
            return;
        }
        InvisibleWall.UnlockWallAt(wallCol, wallRow);
    }

    /// <summary>Ensures the invisible wall governing forward progress exists in the main scene. It is an invisible,
    /// non-persistent GameObject, so it is rebuilt whenever (re)entering the main scene — exactly like the dialogue.</summary>
    private void EnsureWallBootstrap()
    {
        if (InvisibleWall.GetWallAt(wallCol, wallRow) != null)
        {
            return;
        }
        var wallObj = new GameObject($"InvisibleWall_{wallCol}_{wallRow}");
        var wall = wallObj.AddComponent<InvisibleWall>();
        wall.Col = wallCol;
        wall.Row = wallRow;
    }

    /// <summary>
    /// The invisible wall is bootstrapped from <see cref="OnSceneLoaded"/>, so <see cref="GameProgress"/> must exist before the
    /// main scene finishes loading — but it is otherwise only created on the conversation's writing step. Create it here at
    /// startup (inert until a trace begins) so the wall — and the door into its cell — exist from the first frame.
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
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainSceneName)
        {
            // Run first so the fresh Player exists at its (default) position before we restore the
            // pre-trace position onto it — and bootstrap the (non-persistent) invisible wall before either.
            EnsureWallBootstrap();
            ApplyForwardUnlock();
            ApplySavedPlayerPosition();
            StartCoroutine(LogPlayerPositionAfterTrace());
        }

        bool traceLoaded = SceneManager.GetSceneByName(traceSceneName).isLoaded;
        if ((scene.name == traceSceneName || traceLoaded) && !tracingSubscribed)
        {
            Script1.OnCharacterDone += HandleCharacterDone;
            tracingSubscribed = true;
        }
        else if (tracingSubscribed)
        {
            Script1.OnCharacterDone -= HandleCharacterDone;
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
}
