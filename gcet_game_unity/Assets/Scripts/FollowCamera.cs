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
    private InvisibleWall lastWall;
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

        // The wall sits on top of an empty cell (no Area), so when the player walks into that cell the camera must
        // still frame it — otherwise the player walks off-screen to reach a wall that the popup can only show on-screen.
        // A wall-governed cell acts as its own room; track it as the last valid target.
        InvisibleWall wall = InvisibleWall.GetCellContaining(player.position);
        if (wall != null)
        {
            lastWall = wall;
        }

        // Decide purely on WHERE the player stands, not on the clamp result. Pin to the current room/cell if one
        // contains them; otherwise pin to the last room/cell — but ONLY while the player is still within its bounds.
        // The wall is a GATE, not a room to live in: once the player walks through it into a region that has no room,
        // no area and no wall-cell contains them, so any fallback clamp would pin the camera to an old center and slide
        // the player off-screen. In that case free-follow the player instead of pinning.
        CacheViewportHalf();
        Vector3 clampedPos = transform.position;
        bool pin;

        if (area != null)
        {
            pin = true;
            ClampThrough(area.ClampCamera, out clampedPos);
        }
        else if (wall != null)
        {
            pin = true;
            ClampThrough(wall.ClampCamera, out clampedPos);
        }
        else if (lastArea != null && lastArea.ContainsPoint(player.position))
        {
            pin = true;
            ClampThrough(lastArea.ClampCamera, out clampedPos);
        }
        else if (lastWall != null && lastWall.CellBounds.Contains(player.position))
        {
            pin = true;
            ClampThrough(lastWall.ClampCamera, out clampedPos);
        }
        else
        {
            pin = false;
        }

        transform.position = pin ? clampedPos : DesiredUnclamped();
    }

    /// <summary>Smooth-eases toward the player then runs the supplied room/wall clamp. Used when a valid target contains the player.</summary>
    private void ClampThrough(CameraClamp clamp, out Vector3 clamped)
    {
        float targetX = Mathf.SmoothDamp(transform.position.x, player.position.x, ref velocityX, smoothTime);
        float targetY = Mathf.SmoothDamp(transform.position.y, player.position.y, ref velocityY, smoothTime);
        clamp(viewportHalf, new Vector3(targetX, targetY, defaultZ), out clamped);
    }

    /// <summary>Smooth-eases the camera toward the player with no room clamp — used once the player has left every room.</summary>
    private Vector3 DesiredUnclamped()
    {
        float targetX = Mathf.SmoothDamp(transform.position.x, player.position.x, ref velocityX, smoothTime);
        float targetY = Mathf.SmoothDamp(transform.position.y, player.position.y, ref velocityY, smoothTime);
        return new Vector3(targetX, targetY, defaultZ);
    }

    /// <summary>A camera-clamp: pins a desired center so the viewport stays inside one room. Shared by rooms and wall cells.</summary>
    private delegate bool CameraClamp(Vector2 viewportHalf, Vector3 center, out Vector3 clamped);
}
