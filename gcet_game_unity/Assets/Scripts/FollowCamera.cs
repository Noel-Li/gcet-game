using UnityEngine;

/// <summary>
/// Clamped-follow camera built around four rules:
///
///   1. It follows the player — the desired center each frame is the player's position.
///   2. That center is always clamped to a region's bounds, so the viewport never shows void or anything off-region.
///      For a 1×1 region the viewport exactly fills it, so the clamp pins to center (player always centered, camera
///      does not move); for a larger region (e.g. 2×2) the camera tracks the player inside it.
///   3. The region is resolved from the player's position every frame, so the instant the player crosses into a new
///      region the camera clamps to that new region. The transition is gated by the gates between the rooms: the
///      camera only snaps to a new region once the player has actually crossed the shared boundary (an unlocked
///      wall, or no wall there). While a locked wall still blocks the crossing the camera stays on the side the
///      player is on, so the player is never caught half-off-screen on the far side of a gate.
///   4. Because the clamp only engages at a region's edges, the player sits at screen center at all times except
///      when a camera edge is pressed against a region edge — exactly then the player is pushed off-center.
///
/// If the player is outside every region (in a gap between rooms), the camera clamps to the nearest region so void
/// is never shown.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    private float defaultZ;
    private Camera cam;
    private Vector2 viewportHalf;
    private GameArea framedArea;

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
        FrameTarget(player != null ? player.position : transform.position);
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
        FrameTarget(player.position);
    }

    private void FrameTarget(Vector3 playerPos)
    {
        GameArea area = GameArea.GetAreaContaining(playerPos);
        if (area == null)
        {
            // Player is in a gap between regions: clamp to the nearest region so void is never shown.
            area = NearestRegion(playerPos);
        }

        if (area != null)
        {
            // Gate the transition: don't snap to a new region while a locked wall still blocks the player from
            // reaching it. Without this, a player standing at a locked gate whose body overlaps the far region
            // would make the camera leap to that far region and show the player cut in half at the screen edge.
            if (area != framedArea && framedArea != null && !HasCrossedInto(framedArea, area, playerPos))
            {
                area = framedArea;
            }

            if (area != framedArea)
            {
                framedArea = area;
            }
            Vector3 clamped;
            area.ClampCamera(viewportHalf, playerPos, out clamped);
            transform.position = new Vector3(clamped.x, clamped.y, defaultZ);
        }
    }

    /// <summary>True once the player has actually crossed from 'from' into 'to' — i.e. the shared edge between them has
    /// no locked wall blocking it (open gate, or the player is squarely on the 'to' side even if a wall is locked).</summary>
    private static bool HasCrossedInto(GameArea from, GameArea to, Vector3 playerPos)
    {
        Bounds a = from.Bounds;
        Bounds b = to.Bounds;

        // Shared horizontal edge (rooms stacked vertically): the gate is a horizontal wall at the shared Y. The player
        // has crossed into 'to' only if there is no LOCKED horizontal wall on that edge within the player's X.
        bool stacked = (Mathf.Abs(a.max.y - b.min.y) < 1f || Mathf.Abs(b.max.y - a.min.y) < 1f) &&
                       (Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x) > 0f);
        if (stacked)
        {
            float sharedY = (a.max.y + b.min.y) / 2f;       // approximate shared edge Y
            if (WallsBlockHorizontal(sharedY, playerPos.x)) return false; // still gated
            return true;
        }

        // Shared vertical edge (rooms side by side): the gate is a vertical wall at the shared X.
        bool sidebyside = (Mathf.Abs(a.max.x - b.min.x) < 1f || Mathf.Abs(b.max.x - a.min.x) < 1f) &&
                          (Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y) > 0f);
        if (sidebyside)
        {
            float sharedX = (a.max.x + b.min.x) / 2f;
            if (WallsBlockVertical(sharedX, playerPos.y)) return false;
            return true;
        }

        // No shared edge (diagonal / non-adjacent): allow the snap.
        return true;
    }

    private static bool WallsBlockHorizontal(float y, float x)
    {
        foreach (var wall in InvisibleWall.GetRegistered())
        {
            if (wall == null || !wall.Locked) continue;
            if (!wall.Horizontal) continue;
            if (Mathf.Abs(wall.WallY - y) > wall.Span + 0.5f) continue;
            if (x >= wall.WallX - wall.Span && x <= wall.WallX + wall.Span) return true;
        }
        return false;
    }

    private static bool WallsBlockVertical(float x, float y)
    {
        foreach (var wall in InvisibleWall.GetRegistered())
        {
            if (wall == null || !wall.Locked) continue;
            if (wall.Horizontal) continue;
            if (Mathf.Abs(wall.WallX - x) > wall.Span + 0.5f) continue;
            if (y >= wall.WallY - wall.Span && y <= wall.WallY + wall.Span) return true;
        }
        return false;
    }

    private static GameArea NearestRegion(Vector3 point)
    {
        GameArea best = null;
        float bestDist = float.MaxValue;
        foreach (var area in GameArea.GetRegistered())
        {
            if (area == null) continue;
            float d = Vector2.Distance(point, area.Bounds.center);
            if (d < bestDist)
            {
                bestDist = d;
                best = area;
            }
        }
        return best;
    }

    private void LocatePlayer()
    {
        var po = GameObject.Find("Player");
        if (po != null)
            player = po.transform;
    }
}
