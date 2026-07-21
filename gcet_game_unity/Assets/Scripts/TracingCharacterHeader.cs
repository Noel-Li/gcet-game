using TMPro;
using UnityEngine;

/// <summary>Keeps the tracing header synchronized with the active CharacterData.</summary>
[DisallowMultipleComponent]
public sealed class TracingCharacterHeader : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Tracer whose active character is shown. If empty, the scene tracer is found automatically.")]
    [SerializeField] private Script1 traceManager;

    [Header("Labels")]
    [Tooltip("Large Hanzi label in the top header.")]
    [SerializeField] private TMP_Text hanziLabel;

    [Tooltip("Tone-marked pinyin label beside the Hanzi.")]
    [SerializeField] private TMP_Text pinyinLabel;

    private void Awake()
    {
        EnableDynamicGlyphExpansion(hanziLabel);
        EnableDynamicGlyphExpansion(pinyinLabel);
    }

    private void OnEnable()
    {
        ResolveTraceManager();
        if (traceManager != null)
        {
            traceManager.CurrentCharacterChanged += Refresh;
        }
    }

    private void Start()
    {
        ResolveTraceManager();
        Refresh(traceManager != null ? traceManager.CurrentCharacter : null);
    }

    private void OnDisable()
    {
        if (traceManager != null)
        {
            traceManager.CurrentCharacterChanged -= Refresh;
        }
    }

    private void ResolveTraceManager()
    {
        if (traceManager == null)
        {
            traceManager = FindFirstObjectByType<Script1>();
        }
    }

    private void Refresh(CharacterData data)
    {
        if (hanziLabel != null)
        {
            hanziLabel.text = data != null ? data.characterName : string.Empty;
        }

        if (pinyinLabel != null)
        {
            pinyinLabel.text = data != null ? data.pinyin : string.Empty;
        }
    }

    /// <summary>Allows tone marks and future Hanzi to expand a dynamic TMP font instead of showing squares.</summary>
    private static void EnableDynamicGlyphExpansion(TMP_Text label)
    {
        if (label != null && label.font != null)
        {
            label.font.isMultiAtlasTexturesEnabled = true;
        }
    }
}
