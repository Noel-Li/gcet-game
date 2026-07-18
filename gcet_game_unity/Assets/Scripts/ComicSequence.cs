using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Presents an Inspector-authored sequence of comic panels. Space advances one panel;
/// pressing Space after the final panel loads the configured next scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class ComicSequence : MonoBehaviour
{
    [Header("Comic")]
    [Tooltip("Full-screen UI Image used to display the current panel.")]
    [SerializeField] private Image comicImage;

    [Tooltip("Panels shown in order. The current story uses game-related scenes/1.png through 6.png.")]
    [SerializeField] private Sprite[] comicPanels;

    [Header("Flow")]
    [Tooltip("Scene loaded after the player presses Space once more on the final panel.")]
    [SerializeField] private string nextSceneName = "ControlsScene";

    [Tooltip("Prevents transition input from immediately skipping the first panel.")]
    [Min(0f)]
    [SerializeField] private float initialInputDelay = 0.2f;

    private int panelIndex;
    private float inputReadyAt;
    private bool loadingNextScene;

    private void Awake()
    {
        panelIndex = 0;
        inputReadyAt = Time.unscaledTime + initialInputDelay;
        ShowCurrentPanel();
    }

    private void Update()
    {
        if (loadingNextScene || Time.unscaledTime < inputReadyAt)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame)
        {
            return;
        }

        if (comicPanels != null && panelIndex < comicPanels.Length - 1)
        {
            panelIndex++;
            ShowCurrentPanel();
            return;
        }

        LoadNextScene();
    }

    private void ShowCurrentPanel()
    {
        if (comicImage == null)
        {
            Debug.LogError("[ComicSequence] Comic Image is not assigned.", this);
            return;
        }

        if (comicPanels == null || comicPanels.Length == 0)
        {
            Debug.LogError("[ComicSequence] No comic panels are assigned.", this);
            return;
        }

        panelIndex = Mathf.Clamp(panelIndex, 0, comicPanels.Length - 1);
        comicImage.sprite = comicPanels[panelIndex];
        // The comic art is authored at the 1920x1080 reference resolution. Stretching
        // with the Canvas avoids side bars in wider Game views while keeping every
        // edge of the panel visible.
        comicImage.preserveAspect = false;
    }

    private void LoadNextScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("[ComicSequence] Next Scene Name is empty.", this);
            return;
        }

        loadingNextScene = true;
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }

    private void OnValidate()
    {
        initialInputDelay = Mathf.Max(0f, initialInputDelay);
    }
}
