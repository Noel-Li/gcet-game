using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts gameplay from the standalone controls screen. When ControlsScene is loaded
/// additively over game1, the gameplay scene's ControlsOverlayController owns Tab instead.
/// </summary>
[DisallowMultipleComponent]
public sealed class ControlsSceneEntry : MonoBehaviour
{
    [Header("Flow")]
    [Tooltip("Gameplay scene loaded when Tab is pressed after the opening comics.")]
    [SerializeField] private string gameSceneName = "game1";

    [Header("Standalone Rendering")]
    [Tooltip("Camera used when ControlsScene is opened alone. It is disabled automatically in additive overlay mode.")]
    [SerializeField] private Camera sceneCamera;

    [Tooltip("Listener used when ControlsScene is opened alone. It is disabled in additive overlay mode to avoid competing with the gameplay listener.")]
    [SerializeField] private AudioListener sceneAudioListener;

    private bool loadingGame;

    private void Awake()
    {
        bool isStandalone = !SceneManager.GetSceneByName(gameSceneName).isLoaded;

        if (sceneCamera != null)
            sceneCamera.enabled = isStandalone;

        if (sceneAudioListener != null)
            sceneAudioListener.enabled = isStandalone;
    }

    private void Update()
    {
        if (loadingGame || SceneManager.GetSceneByName(gameSceneName).isLoaded)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame)
        {
            return;
        }

        loadingGame = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
