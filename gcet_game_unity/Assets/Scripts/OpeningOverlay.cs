using System.Collections;
using UnityEngine;

/// <summary>
/// Presents a centered, screen-space title card before gameplay begins.
/// The visuals remain scene-authored while this component owns only timing and fading.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class OpeningOverlay : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds the title card remains fully visible. Uses unscaled time.")]
    [Min(0f)]
    [SerializeField] private float holdDuration = 3f;

    [Tooltip("Seconds used to fade the title card before the next opening step begins.")]
    [Min(0f)]
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Wiring")]
    [Tooltip("CanvasGroup controlling the complete overlay, including its dimmed backdrop.")]
    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>True while the title card should block gameplay input.</summary>
    public static bool IsShowing { get; private set; }

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        HideImmediately();
    }

    /// <summary>Shows, holds, and fades the authored overlay.</summary>
    public IEnumerator Play()
    {
        if (canvasGroup == null)
        {
            Debug.LogError("[OpeningOverlay] A CanvasGroup is required.", this);
            yield break;
        }

        ShowImmediately();

        if (holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdDuration);
        }

        float elapsed = 0f;
        while (fadeDuration > 0f && elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        HideImmediately();
    }

    /// <summary>Makes the card visible now, before the first frame can render.</summary>
    public void ShowImmediately()
    {
        gameObject.SetActive(true);
        IsShowing = true;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void HideImmediately()
    {
        IsShowing = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        IsShowing = false;
    }

    private void OnValidate()
    {
        holdDuration = Mathf.Max(0f, holdDuration);
        fadeDuration = Mathf.Max(0f, fadeDuration);

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
