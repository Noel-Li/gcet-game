using UnityEngine;
using UnityEngine.SceneManagement;

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

<<<<<<< HEAD
    [SerializeField] private int requiredTraceCount = 1;
    [SerializeField] private int completedTraceCount;
=======
    /// <summary>
    /// The player position captured just before leaving for the tracing scene, so the reload can put the
    /// player back where they were instead of at the scene's authored default. Null until BeginTrace runs.
    /// </summary>
    private Vector3? savedPlayerPosition;
>>>>>>> 2c2d1b97148ec011665f29748ec2acf48d562863

    /// <summary>The NPC scene to load back into.</summary>
    [SerializeField] private string mainSceneName = "game1";

    /// <summary>The hanzi tracing scene to open on the writing step.</summary>
    [SerializeField] private string traceSceneName = "hanzi tracing base";

    /// <summary>Grid coordinate of the room whose forward exit should open on a correct trace.</summary>
    [SerializeField] private int targetCol;
    [SerializeField] private int targetRow;

    /// <summary>Set the moment the hanzi tracer finishes a character correctly.</summary>
    public bool tracePassed { get; private set; }

    public int ResumeStep => resumeStep;
    public int RequiredTraceCount => requiredTraceCount;
    public int CompletedTraceCount => completedTraceCount;

    /// <summary>
    /// Called from the dialogue's writing step. Records which room this NPC gates and which conversation step to resume
    /// at on return, then opens the tracing scene. The player may move forward only once the trace completes.
    /// </summary>
    public void BeginTrace(int areaCol, int areaRow, int stepToResume, int charactersToComplete = 1)
    {
        targetCol = areaCol;
        targetRow = areaRow;
        resumeStep = stepToResume;
        requiredTraceCount = Mathf.Max(1, charactersToComplete);
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

<<<<<<< HEAD
    /// <summary>
    /// Fired by <see cref="Script1.OnCharacterDone"/> whenever one character is traced correctly.
    /// The tracing scene remains open until every character requested by the dialogue has been completed.
    /// </summary>
=======
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
        }

        savedPlayerPosition = null;
    }

    /// <summary>Fired by <see cref="Script1.OnCharacterDone"/> when the character is traced correctly.</summary>
>>>>>>> 2c2d1b97148ec011665f29748ec2acf48d562863
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

    /// <summary>Flips the NPC's forward exit once the main scene has reloaded and its Areas are registered.</summary>
    private void ApplyForwardUnlock()
    {
        if (!tracePassed)
        {
            return;
        }
        GameArea area = GameArea.GetAreaAt(targetCol, targetRow);
        if (area != null)
        {
            area.UnlockTopExit();
        }
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
            // pre-trace position onto it.
            ApplyForwardUnlock();
            ApplySavedPlayerPosition();
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