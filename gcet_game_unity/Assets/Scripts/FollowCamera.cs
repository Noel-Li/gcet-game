using UnityEngine;

/// <summary>
/// Clamped-follow camera. Each frame it smoothly follows the player (via SmoothDamp) and then clamps the result to the
/// current Area's bounds through <see cref="GameArea.ClampCamera"/>, so the view never shows void or the
/// adjacent/locked room — only the playable area the player stands in. The current Area is resolved from the player's
/// position each frame; if the player is outside every Area the camera holds on the last valid room so void is never
/// shown on the way out.
///
/// Smoothing only runs while inside a known room; on the frame re-entering a room the camera snaps to its clamped target
/// first so it never lags past the visible boundary and shows emptiness.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Motion")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Follow slack")]
    [Tooltip("Insets the player from the room edges (world units) so the camera eases before hitting a wall.")]
    [SerializeField] private float lookAheadInset = 0.0f;

    private float velocityX;
    private float velocityY;
    private float defaultZ;
    private Camera cam;
    private GameArea lastArea;
    private Vector2 viewportHalf;

    private void Reset()
    {
        var found = FindObjectOfType<PlayerMovement>();
        if (found != null)
        {
            player = found.transform;
        }
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        CacheViewportHalf();
    }

    private void Start()
    {
        defaultZ = transform.position.z;
        CacheViewportHalf();

        // Start framed on the player's room so we never snap-arrive.
        GameArea startArea = player != null ? GameArea.GetAreaContaining(player.position) : null;
        if (startArea != null)
        {
            lastArea = startArea;
            Vector3 clamped;
            startArea.ClampCamera(viewportHalf, player.position, out clamped);
            transform.position = new Vector3(clamped.x, clamped.y, defaultZ);
            velocityX = 0f;
            velocityY = 0f;
        }
    }

    private void CacheViewportHalf()
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }
        if (cam != null && cam.orthographic)
        {
            viewportHalf = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
        }
        else if (cam != null)
        {
            float dist = Mathf.Abs(transform.position.z);
            viewportHalf = new Vector2(cam.fieldOfView * Mathf.Deg2Rad * 0.5f * dist, 0f);
            viewportHalf.y = viewportHalf.x / cam.aspect;
        }
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        GameArea area = GameArea.GetAreaContaining(player.position);
        if (area != null)
        {
            lastArea = area;
        }
        GameArea target = area ?? lastArea;
        if (target == null)
        {
            return;
        }

        CacheViewportHalf();

        // Smoothly ease toward the player, then clamp so the viewport stays inside the room. Clamping guarantees the
        // camera can never reveal void or the next room even right up against a boundary.
        float targetX = Mathf.SmoothDamp(transform.position.x, player.position.x, ref velocityX, smoothTime);
        float targetY = Mathf.SmoothDamp(transform.position.y, player.position.y, ref velocityY, smoothTime);

        Vector3 desired = new Vector3(targetX, targetY, defaultZ);
        Vector3 clamped;
        target.ClampCamera(viewportHalf, desired, out clamped);

        transform.position = clamped;
    }
}
