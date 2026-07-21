using UnityEngine;

/// <summary>Keeps the gameplay review-book hint hidden for the complete lifetime of a dialogue.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class ReviewBookHintVisibility : MonoBehaviour
{
    private Canvas hintCanvas;

    private void Awake()
    {
        hintCanvas = GetComponent<Canvas>();
        Refresh();
    }

    private void OnEnable()
    {
        Dialogue.OpenStateChanged += HandleDialogueStateChanged;
        Refresh();
    }

    private void OnDisable()
    {
        Dialogue.OpenStateChanged -= HandleDialogueStateChanged;
    }

    private void LateUpdate()
    {
        // Keep a polling fallback for scene reloads and disabled-domain-reload Editor configurations.
        Refresh();
    }

    private void HandleDialogueStateChanged(bool isOpen)
    {
        SetVisible(!isOpen);
    }

    private void Refresh()
    {
        SetVisible(!Dialogue.IsAnyOpen);
    }

    private void SetVisible(bool visible)
    {
        if (hintCanvas != null && hintCanvas.enabled != visible)
        {
            hintCanvas.enabled = visible;
        }
    }
}
