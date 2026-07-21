using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Displays a Game Voice meaning card before the tracing scene returns to gameplay.</summary>
[DisallowMultipleComponent]
public sealed class TracingMeaningOverlay : MonoBehaviour
{
    [Header("Presentation")]
    [Tooltip("Game Voice panel enabled only after the tracing task is complete.")]
    [SerializeField] private GameObject panel;

    [Tooltip("Text formatted as Hanzi or phrase, an arrow, and its English meaning.")]
    [SerializeField] private TMP_Text meaningLabel;

    [Header("Input")]
    [Tooltip("Prevents the stroke-ending mouse release from immediately dismissing the meaning card.")]
    [Min(0f)]
    [SerializeField] private float inputDelay = 0.25f;

    private Action onDismissed;
    private float shownAt;
    private bool open;

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (meaningLabel != null && meaningLabel.font != null)
        {
            meaningLabel.font.isMultiAtlasTexturesEnabled = true;
        }
    }

    private void Update()
    {
        if (!open || Time.unscaledTime - shownAt < inputDelay)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool pressed = (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame));

        if (pressed)
        {
            Dismiss();
        }
    }

    public void Show(string meaningText, Action dismissed)
    {
        onDismissed = dismissed;
        shownAt = Time.unscaledTime;
        open = true;

        if (meaningLabel != null)
        {
            meaningLabel.text = meaningText +
                "\n<size=24>Click or press Space to continue</size>";
        }

        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    private void Dismiss()
    {
        if (!open)
        {
            return;
        }

        open = false;
        if (panel != null)
        {
            panel.SetActive(false);
        }

        Action callback = onDismissed;
        onDismissed = null;
        callback?.Invoke();
    }
}
