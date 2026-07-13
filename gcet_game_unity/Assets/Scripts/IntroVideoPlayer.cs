using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Plays the clip from <c>Assets/Intro Video.mov</c> once at startup (stretched to the back of the main camera via <see cref="VideoRenderMode.CameraNearPlane"/> — no manually-sized Canvas layout that can collapse to zero), then renders the menu back on end/skip.</summary>
[RequireComponent(typeof(AudioSource))]
public class IntroVideoPlayer : MonoBehaviour
{
    [Header("Clip (assign Intro Video.mov in the Inspector)")]
    [SerializeField] private VideoClip clip;

    [Header("Menu objects hidden during video")]
    [SerializeField] private GameObject[] menuObjectsToHide = new GameObject[0];

    [Header("Options")]
    [Tooltip("If true, pressing Escape/Space/Enter or clicking skips to the menu.")]
    [SerializeField] private bool allowSkip = true;

    private VideoPlayer videoPlayer;
    private Camera targetCam;
    private bool ended;

    private void Awake()
    {
        foreach (var obj in menuObjectsToHide)
        {
            if (obj != null) obj.SetActive(false);
        }

        targetCam = Camera.main;
        if (targetCam == null)
        {
            targetCam = FindObjectOfType<Camera>();
        }
        if (targetCam == null)
        {
            Debug.LogError("[IntroVideoPlayer] No camera present in StartScene; skipping video.");
            RevealMenu();
            return;
        }

        // The clip reference in the scene YAML can drift to null during scripted meta regeneration. Resolve at runtime from the asset database as a robust fallback.
        if (clip == null)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:VideoClip");
            foreach (string g in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                VideoClip found = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                if (found != null)
                {
                    clip = found;
                    Debug.Log($"[IntroVideoPlayer] Resolved clip via asset DB: {path}");
                    break;
                }
            }
#else
            VideoClip[] all = Resources.FindObjectsOfTypeAll<VideoClip>();
            if (all.Length > 0)
            {
                clip = all[0];
                Debug.Log($"[IntroVideoPlayer] Resolved clip via Resources search: {clip.name}");
            }
#endif
        }
        if (clip == null)
        {
            Debug.LogWarning("[IntroVideoPlayer] No clip; revealing menu.");
            RevealMenu();
            return;
        }

        // Host the VideoPlayer on the main camera. CameraNearPlane mode projects a full-screen quad at the near clip plane — no layout sizing games.
        videoPlayer = targetCam.gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
        videoPlayer.targetCamera = targetCam;
        targetCam.nearClipPlane = 0.05f; // pull the near plane in for CameraNearPlane (default 0.3 would shrink the projected quad in ortho space)

        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, GetComponent<AudioSource>());
        if (targetCam.GetComponent<AudioListener>() == null)
        {
            targetCam.gameObject.AddComponent<AudioListener>();
        }

        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.loopPointReached += OnVideoEnd;
        videoPlayer.errorReceived += OnVideoError;

        videoPlayer.clip = clip;
        Debug.Log($"[IntroVideoPlayer] Awake: shot to camera '{targetCam.name}' clip='{clip.name}' {clip.width}x{clip.height} {clip.frameCount} frames; ortho={targetCam.orthographic} size={targetCam.orthographicSize}");
        videoPlayer.Prepare();
        Invoke(nameof(GuaranteeReveal), 8f);

        // Start disabled, enable once prepare completes — this hides the quad for the first frame while prepare runs (some frames of black can appear otherwise).
        videoPlayer.enabled = false;
    }

    private int tickCount = 0;

    private void Update()
    {
        tickCount++;
        if (tickCount <= 5 && !ended)
        {
            bool playing = videoPlayer != null && videoPlayer.isPlaying;
            long frame = videoPlayer != null ? videoPlayer.frame : -1;
            Debug.Log($"[IntroVideoPlayer] tick #{tickCount}: isPlaying={playing} frame={frame} near={targetCam?.nearClipPlane} far={targetCam?.farClipPlane}");
        }
        if (ended || videoPlayer == null || !allowSkip) return;
        bool mouse = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
        bool key = false;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            key = kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
        }
        if (mouse || key) EndVideo();
    }

    private void OnPrepared(VideoPlayer vp)
    {
        Debug.Log($"[IntroVideoPlayer] OnPrepared: isPrepared={vp.isPrepared} isPlaying={vp.isPlaying} frame={vp.frame} length={vp.length}");
        if (ended) return;
        CancelInvoke(nameof(GuaranteeReveal));
        // Safe to show now — preparing buffers the first frame, so enable to start rendering immediately.
        vp.enabled = true;
        vp.Play();
        Invoke(nameof(GuaranteeReveal), Mathf.Max(10f, (float)vp.length + 3f));
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        CancelInvoke(nameof(GuaranteeReveal));
        if (!ended)
        {
            ended = true;
            RevealMenu();
        }
    }

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogWarning($"[IntroVideoPlayer] error '{message}' — revealing menu.");
        RevealMenu();
    }

    private void GuaranteeReveal()
    {
        if (!ended)
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
            ended = true;
            RevealMenu();
        }
    }

    private void EndVideo()
    {
        if (ended) return;
        ended = true;
        CancelInvoke(nameof(GuaranteeReveal));
        if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        RevealMenu();
    }

    private void RevealMenu()
    {
        foreach (var obj in menuObjectsToHide)
        {
            if (obj != null) obj.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepared;
            videoPlayer.loopPointReached -= OnVideoEnd;
            videoPlayer.errorReceived -= OnVideoError;
        }
        CancelInvoke();
    }
}
