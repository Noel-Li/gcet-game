using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Toggles the pause flow with the Escape key. On Escape it either (a) loads <see cref="pauseSceneName"/> additively and freezes time, or (b) if the pause scene is already loaded, asks it to resume. The exact hand-off to the loading scene is done through a <see cref="PauseResume"/> component found in the pause scene (on its root), with timeScale managed by this controller.</summary>
public class PauseController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string pauseSceneName = "PauseScene";

    /// <summary>True while the pause scene is up and the game is frozen. Read by Dialogue / NpcController so they suspend input.</summary>
    public static bool IsPaused { get; private set; }

    private bool pausing;

    private void Awake()
    {
        // Ensure a single controller cross the two scenes even when the pause scene is loaded additively.
        var existing = FindObjectsOfType<PauseController>();
        if (existing != null && existing.Length > 1)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        if (OpeningOverlay.IsShowing)
        {
            return;
        }

        var kb = Keyboard.current;
        if (kb == null)
        {
            return;
        }
        if (!kb.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (IsPauseSceneLoaded())
        {
            Resume();
        }
        else
        {
            ControlsOverlayController.CloseIfOpen();
            Pause();
        }
    }

    private bool IsPauseSceneLoaded()
    {
        return SceneManager.GetSceneByName(pauseSceneName).isLoaded;
    }

    private void Pause()
    {
        if (pausing)
        {
            return;
        }
        pausing = true;
        IsPaused = true;
        Time.timeScale = 0f;
        SceneManager.LoadScene(pauseSceneName, LoadSceneMode.Additive);
    }

    /// <summary>Called by the pause screen's Resume button (via <see cref="PauseResume"/>) and by a second ESC press. Unloads the additive pause scene and restores time.</summary>
    public void Resume()
    {
        Time.timeScale = 1f;
        pausing = false;
        IsPaused = false;
        Scene scene = SceneManager.GetSceneByName(pauseSceneName);
        if (scene.isLoaded)
        {
            SceneManager.UnloadSceneAsync(pauseSceneName);
        }
    }
}
