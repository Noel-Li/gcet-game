using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens and closes the controls reference scene with Tab. The scene is additive so the
/// player returns to the exact same gameplay state when the overlay closes.
/// </summary>
[DisallowMultipleComponent]
public sealed class ControlsOverlayController : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Additive scene containing the controls reference UI.")]
    [SerializeField] private string controlsSceneName = "ControlsScene";

    public static bool IsOpen { get; private set; }

    private static ControlsOverlayController instance;
    private bool transitionInProgress;
    private bool closeWhenLoaded;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        IsOpen = SceneManager.GetSceneByName(controlsSceneName).isLoaded;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || transitionInProgress || PauseController.IsPaused || OpeningOverlay.IsShowing)
        {
            return;
        }

        if (!keyboard.tabKey.wasPressedThisFrame)
        {
            return;
        }

        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void Open()
    {
        if (IsOpen || transitionInProgress)
        {
            return;
        }

        transitionInProgress = true;
        closeWhenLoaded = false;
        IsOpen = true;
        Time.timeScale = 0f;

        AsyncOperation load = SceneManager.LoadSceneAsync(controlsSceneName, LoadSceneMode.Additive);
        if (load == null)
        {
            transitionInProgress = false;
            IsOpen = false;
            Time.timeScale = 1f;
            Debug.LogError($"[ControlsOverlayController] Could not load scene '{controlsSceneName}'. Add it to Build Settings.");
            return;
        }

        load.completed += _ =>
        {
            transitionInProgress = false;
            if (closeWhenLoaded)
            {
                closeWhenLoaded = false;
                UnloadControlsScene();
            }
        };
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        Time.timeScale = PauseController.IsPaused ? 0f : 1f;

        Scene scene = SceneManager.GetSceneByName(controlsSceneName);
        if (!scene.isLoaded)
        {
            // Escape can request Pause during the short additive-load window. Finish the
            // load, then immediately remove this scene so it cannot be left orphaned.
            closeWhenLoaded = transitionInProgress;
            return;
        }

        UnloadControlsScene();
    }

    private void UnloadControlsScene()
    {
        transitionInProgress = true;
        AsyncOperation unload = SceneManager.UnloadSceneAsync(controlsSceneName);
        if (unload == null)
        {
            transitionInProgress = false;
            return;
        }

        unload.completed += _ => transitionInProgress = false;
    }

    /// <summary>Closes the controls overlay before another full-screen flow, such as Pause, opens.</summary>
    public static bool CloseIfOpen()
    {
        if (!IsOpen)
        {
            return false;
        }

        if (instance != null)
        {
            instance.Close();
        }
        else
        {
            IsOpen = false;
        }

        return true;
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        instance = null;
        IsOpen = false;
    }
}
