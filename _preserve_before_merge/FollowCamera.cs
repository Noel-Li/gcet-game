using UnityEngine;

/// <summary>
/// Clamped-follow camera with two distinct behaviours:
///
///  * WITHIN a region — smoothly follows the player (SmoothDamp) and clamps the result to the
///    current region's bounds, so the view never shows void or an adjacent room. For a 1×1 region
///    the viewport exactly fills it, so the clamp pins to center (no follow needed); for a larger
///    region (e.g. 2×2) the camera actually tracks the player inside it.
///
///  * BETWEEN regions — the frame the player is in is resolved from their position each frame. The
///    instant it changes, the camera SNAPS to the new region's clamped target (instead of easing
///    across and briefly showing the void/gap between rooms). Smoothing resumes once settled.
///
/// If the player is outside every region/wall the camera holds on the last valid room (clamped) as
/// long as the player is still within its bounds; once the player leaves even that, it free-follows
/// so the player is never dragged off-screen.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Motion")]
    [SerializeField] private float smoothTime = 0.15f;

    private float velocityX;
    private float velocityY;
    private float defaultZ;
    private Camera cam;
    private Vector2 viewportHalf;

    // The region/cell we are currently framing. Compared each frame to detect a transition (→ snap).
    private GameArea framedArea;
    private InvisibleWall framedWall;

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
        FrameTarget start = ResolveTarget(player != null ? player.position : transform.position);
        if (start.area != null)
        {
            framedArea = start.area;
            Vector3 clamped;
            start.area.ClampCamera(viewportHalf, player != null ? player.position : transform.position, out clamped);
            transform.position = new Vector3(clamped.x, clamped.y, defaultZ);
        }
        else if (start.wall != null)
        {
            framedWall = start.wall;
            Vector3 clamped;
            start.wall.ClampCamera(viewportHalf, player != null ? player.position : transform.position, out clamped);
            transform.position = new Vector3(clamped.x, clamped.y, defaultZ);
        }
        velocityX = 0f;
        velocityY = 0f;
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
            LocatePlayer();
            if (player == null)
            {
                return;
            }
        }

        CacheViewportHalf();
        FrameTarget target = ResolveTarget(player.position);

        // Decide purely on WHERE the player stands. A wall-governed cell frames like a room so the
        // gate is always on-screen; an empty cell is the void.
        if (target.area != null)
        {
            if (target.area != framedArea)
            {
                // Entered a new region → snap, do not ease (avoids showing the gap/void between rooms).
                framedArea = target.area;
                framedWall = null;
                Vector3 clamped;
                framedArea.ClampCamera(viewportHalf, player.position, out clamped);
                transform.position = new Vector3(clamped.x, clamped.y, defaultZ);
                velocityX = 0f;
                velocityY = 0f;
            }
            else
            {
                SmoothClampThrough(framedArea.ClampCamera, player.position);
            }
        }
        else if (target.wall != null)
        {
            if (target.wall != framedWall)
            {
                framedWall = target.wall;
                framedArea = null;
                Vector3 clamped;
                framedWall.ClampCamera(viewportHalf, player.position, out clamped);
                transform.position = new Vector3(clamped.x, clamped.y, defaultZ);
                velocityX = 0f;
                velocityY = 0f;
            }
            else
            {
                SmoothClampThrough(framedWall.ClampCamera, player.position);
            }
        }
        else
        {
            // Void: hold on the last room while the player is still inside it, else free-follow.
            if (framedArea != null && framedArea.ContainsPoint(player.position))
            {
                SmoothClampThrough(framedArea.ClampCamera, player.position);
            }
            else if (framedWall != null && framedWall.CellBounds.Contains(player.position))
            {
                SmoothClampThrough(framedWall.ClampCamera, player.position);
            }
            else
            {
                // Player has left every room — free-follow so they stay on-screen.
                framedArea = null;
                framedWall = null;
                Vector3 desired = DesiredUnclamped(player.position);
                transform.position = desired;
                velocityX = 0f;
                velocityY = 0f;
            }
        }
    }

    /// <summary>Smooth-eases toward the player then runs the supplied room/wall clamp.</summary>
    private void SmoothClampThrough(CameraClamp clamp, Vector3 desired)
    {
        float targetX = Mathf.SmoothDamp(transform.position.x, desired.x, ref velocityX, smoothTime);
        float targetY = Mathf.SmoothDamp(transform.position.y, desired.y, ref velocityY, smoothTime);
        Vector3 clamped;
        clamp(viewportHalf, new Vector3(targetX, targetY, defaultZ), out clamped);
        transform.position = clamped;
    }

    /// <summary>Smooth-eases the camera toward the player with no room clamp — used once the player has left every room.</summary>
    private Vector3 DesiredUnclamped(Vector3 desired)
    {
        float targetX = Mathf.SmoothDamp(transform.position.x, desired.x, ref velocityX, smoothTime);
        float targetY = Mathf.SmoothDamp(transform.position.y, desired.y, ref velocityY, smoothTime);
        return new Vector3(targetX, targetY, defaultZ);
    }

    /// <summary>Resolves what frames the player: an Area, a wall-governed cell, or nothing (void).</summary>
    private FrameTarget ResolveTarget(Vector3 point)
    {
        return new FrameTarget
        {
            area = GameArea.GetAreaContaining(point),
            wall = InvisibleWall.GetCellContaining(point),
        };
    }

    private void LocatePlayer()
    {
        var playerObj = GameObject.Find("Player");
        if (playerObj != null)
        {
            var pm = playerObj.GetComponent<PlayerMovement>();
            player = pm != null ? pm.transform : playerObj.transform;
        }
    }

    private struct FrameTarget
    {
        public GameArea area;
        public InvisibleWall wall;
    }

    /// <summary>A camera-clamp: pins a desired center so the viewport stays inside one room. Shared by rooms and wall cells.</summary>
    private delegate bool CameraClamp(Vector2 viewportHalf, Vector3 center, out Vector3 clamped);
}
