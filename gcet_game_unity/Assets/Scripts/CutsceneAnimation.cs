using UnityEngine;

/// <summary>
/// Reusable, Inspector-authored frame animation for comic cutscenes. Per-frame timing preserves pauses from
/// source GIF artwork without requiring a runtime GIF decoder.
/// </summary>
[CreateAssetMenu(fileName = "Cutscene Animation", menuName = "GCET/Cutscene Animation")]
public sealed class CutsceneAnimation : ScriptableObject
{
    [SerializeField] private Sprite[] frames = new Sprite[0];

    [Tooltip("Seconds each corresponding frame remains visible. Missing entries use Default Frame Duration.")]
    [SerializeField] private float[] frameDurations = new float[0];

    [Min(0.01f)]
    [SerializeField] private float defaultFrameDuration = 0.1f;

    public int FrameCount => frames != null ? frames.Length : 0;

    public Sprite GetFrame(int index)
    {
        return frames != null && index >= 0 && index < frames.Length ? frames[index] : null;
    }

    public float GetFrameDuration(int index)
    {
        if (frameDurations != null && index >= 0 && index < frameDurations.Length)
        {
            return Mathf.Max(0.01f, frameDurations[index]);
        }

        return Mathf.Max(0.01f, defaultFrameDuration);
    }

    private void OnValidate()
    {
        defaultFrameDuration = Mathf.Max(0.01f, defaultFrameDuration);
        if (frameDurations == null)
        {
            return;
        }

        for (int i = 0; i < frameDurations.Length; i++)
        {
            frameDurations[i] = Mathf.Max(0.01f, frameDurations[i]);
        }
    }
}
