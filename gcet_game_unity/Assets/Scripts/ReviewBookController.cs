using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens and closes the review book as an additive overlay while preserving the live game state.
/// </summary>
[DisallowMultipleComponent]
public sealed class ReviewBookController : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Additive scene containing the full-screen review book UI.")]
    [SerializeField] private string reviewBookSceneName = "ReviewBookScene";

    public static bool IsOpen { get; private set; }

    private static ReviewBookController instance;
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
        IsOpen = SceneManager.GetSceneByName(reviewBookSceneName).isLoaded;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || transitionInProgress || OpeningOverlay.IsShowing || IsConversationOpen())
        {
            return;
        }

        if (!keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (IsOpen)
        {
            Close();
        }
        else
        {
            ControlsOverlayController.CloseIfOpen();
            Open();
        }
    }

    private void Open()
    {
        if (IsOpen || transitionInProgress || IsConversationOpen())
        {
            return;
        }

        transitionInProgress = true;
        closeWhenLoaded = false;
        IsOpen = true;
        Time.timeScale = 0f;

        AsyncOperation load = SceneManager.LoadSceneAsync(reviewBookSceneName, LoadSceneMode.Additive);
        if (load == null)
        {
            transitionInProgress = false;
            IsOpen = false;
            Time.timeScale = 1f;
            Debug.LogError($"[ReviewBookController] Could not load scene '{reviewBookSceneName}'. Add it to Build Settings.");
            return;
        }

        load.completed += _ =>
        {
            transitionInProgress = false;
            if (closeWhenLoaded)
            {
                closeWhenLoaded = false;
                UnloadReviewBook();
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
        Time.timeScale = 1f;

        Scene scene = SceneManager.GetSceneByName(reviewBookSceneName);
        if (!scene.isLoaded)
        {
            closeWhenLoaded = transitionInProgress;
            return;
        }

        UnloadReviewBook();
    }

    private void UnloadReviewBook()
    {
        transitionInProgress = true;
        AsyncOperation unload = SceneManager.UnloadSceneAsync(reviewBookSceneName);
        if (unload == null)
        {
            transitionInProgress = false;
            return;
        }

        unload.completed += _ => transitionInProgress = false;
    }

    private static bool IsConversationOpen()
    {
        return Dialogue.IsAnyOpen;
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        instance = null;
        IsOpen = false;
        Time.timeScale = 1f;
    }
}
