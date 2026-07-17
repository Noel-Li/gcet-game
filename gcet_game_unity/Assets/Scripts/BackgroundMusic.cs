using UnityEngine;

/// <summary>
/// Owns the single looping music source that survives the game's scene changes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class BackgroundMusic : MonoBehaviour
{
    [Header("Music")]
    [Tooltip("Music played after the player presses Begin.")]
    [SerializeField] private AudioClip music;

    [Tooltip("Background music volume.")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.727f;

    private static BackgroundMusic current;
    private AudioSource audioSource;

    private void Awake()
    {
        if (current != null && current != this)
        {
            Destroy(gameObject);
            return;
        }

        current = this;
        audioSource = GetComponent<AudioSource>();
        ApplySettings();
    }

    /// <summary>Starts the loop and keeps its GameObject alive across scene loads.</summary>
    public void Play()
    {
        if (current != null && current != this)
        {
            current.Play();
            return;
        }

        audioSource ??= GetComponent<AudioSource>();
        ApplySettings();

        if (music == null)
        {
            Debug.LogError("[BackgroundMusic] No music clip is assigned in the Inspector.");
            return;
        }

        DontDestroyOnLoad(gameObject);

        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    private void OnValidate()
    {
        audioSource = GetComponent<AudioSource>();
        ApplySettings();
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
    }

    private void ApplySettings()
    {
        if (audioSource == null)
            return;

        audioSource.clip = music;
        audioSource.volume = volume;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }
}
