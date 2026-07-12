using UnityEngine;

/// <summary>
/// One Area = exactly one camera view. Its size is derived from the camera at runtime so it
/// always fills the screen precisely (no void visible around it). The player is clamped to
/// the Area's bounds (PlayerMovement.ClampToArea) so they can never go off-camera.
///
/// Areas tile a grid by <see cref="areaCol"/>/<see cref="areaRow"/>. Movement in each direction
/// is gated independently:
///   - Right / Top require both an unlock flag AND a neighbour area in that direction.
///   - Left / Bottom are existence-gated only (no neighbour = the void, always blocked).
/// The camera snaps to whichever room the player occupies, so the view always shows exactly one
/// full Area and never a locked-off region.
/// </summary>
public class GameArea : MonoBehaviour
{
    [Header("Grid coordinates")]
    [SerializeField] private int areaCol = 0;
    [SerializeField] private int areaRow = 0;

    [Header("Identity")]
    [SerializeField] private string areaName;

    [Header("Gates")]
    [Tooltip("When true AND a room exists to the right, the player may walk right into it.")]
    [SerializeField] private bool rightExitUnlocked = false;

    [Tooltip("When true AND a room exists above, the player may walk up into it. An NPC flips this.")]
    [SerializeField] private bool topExitUnlocked = false;

    [Header("Visual")]
    [SerializeField] private Color areaColor = new Color(0.12f, 0.12f, 0.16f, 1f);

    /// <summary>All live Areas.</summary>
    private static readonly System.Collections.Generic.List<GameArea> registered = new System.Collections.Generic.List<GameArea>();

    private void Awake()
    {
        if (string.IsNullOrEmpty(areaName))
        {
            areaName = $"Area_{areaCol}_{areaRow}";
        }
        FitToCamera();

        var renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = -10;
        }
        var colorSprite = GetComponentInChildren<ColorSprite>();
        if (colorSprite != null)
        {
            colorSprite.SetColor(areaColor);
        }
    }

    private void OnEnable()
    {
        if (!registered.Contains(this))
        {
            registered.Add(this);
        }
    }

    private void OnDisable()
    {
        registered.Remove(this);
    }

    public string AreaName => areaName;
    public int AreaCol => areaCol;
    public int AreaRow => areaRow;
    public bool RightExitUnlocked => rightExitUnlocked;
    public bool TopExitUnlocked => topExitUnlocked;

    /// <summary>Call from the NPC / a switch to open this room's right-hand gate.</summary>
    public void UnlockRightExit()
    {
        rightExitUnlocked = true;
    }

    /// <summary>Call from the NPC to open this room's top gate.</summary>
    public void UnlockTopExit()
    {
        topExitUnlocked = true;
    }

    private void FitToCamera()
    {
        Camera mainCam = Camera.main;
        float ortho = mainCam != null ? mainCam.orthographicSize : 5f;
        float aspect = mainCam != null ? mainCam.aspect : 16f / 9f;
        float height = ortho * 2f;
        float width = height * aspect;

        // Tile the grid: each room is one camera view, placed edge to edge with no gaps.
        transform.position = new Vector3(areaCol * width, areaRow * height, 0f);

        Transform background = transform.Find("Background");
        if (background != null)
        {
            background.localScale = new Vector3(width, height, 1f);
        }

        cachedSize = new Vector2(width, height);
    }

    private Vector2 cachedSize;

    public float Width => cachedSize.x;
    public float Height => cachedSize.y;

    public Vector3 Center => transform.position;

    public Bounds Bounds
    {
        get
        {
            Vector3 half = new Vector3(Width, Height, 0f) * 0.5f;
            return new Bounds(Center, half * 2f);
        }
    }

    public bool ContainsPoint(Vector3 point)
    {
        Bounds b = Bounds;
        return point.x >= b.min.x && point.x <= b.max.x &&
               point.y >= b.min.y && point.y <= b.max.y;
    }

    public static GameArea GetAreaContaining(Vector3 point)
    {
        foreach (var area in registered)
        {
            if (area != null && area.ContainsPoint(point))
            {
                return area;
            }
        }
        return null;
    }

    /// <summary>Finds the live Area at a specific grid coordinate, or null if none exists.</summary>
    public static GameArea GetAreaAt(int col, int row)
    {
        foreach (var area in registered)
        {
            if (area != null && area.areaCol == col && area.areaRow == row)
            {
                return area;
            }
        }
        return null;
    }

    /// <summary>All live Areas with stale entries removed.</summary>
    public static System.Collections.Generic.IReadOnlyList<GameArea> GetRegistered()
    {
        registered.RemoveAll(a => a == null);
        return registered;
    }

    /// <summary>
    /// Clamps a player position so the player stays on-camera. Each edge is blocked unless the
    /// player may actually leave through it: Right/Top need their unlock flag AND a neighbour;
    /// Left/Bottom need only a neighbour (no neighbour is the void). This guarantees the player
    /// can never walk into an empty direction or a locked room.
    /// </summary>
    public Vector3 ClampPlayer(Vector3 pos, float playerHalf)
    {
        Bounds b = Bounds;

        // An open edge lifts its wall entirely so the player may step through into the neighbour.
        // A closed edge keeps both walls, trapping the player inside this room.
        bool leftOpen = GetAreaAt(areaCol - 1, areaRow) != null;
        bool rightOpen = rightExitUnlocked && GetAreaAt(areaCol + 1, areaRow) != null;
        bool bottomOpen = GetAreaAt(areaCol, areaRow - 1) != null;
        bool topOpen = topExitUnlocked && GetAreaAt(areaCol, areaRow + 1) != null;

        float minX = b.min.x + playerHalf;
        float maxX = b.max.x - playerHalf;
        float minY = b.min.y + playerHalf;
        float maxY = b.max.y - playerHalf;

        if (!leftOpen) { pos.x = Mathf.Max(pos.x, minX); }
        if (!rightOpen) { pos.x = Mathf.Min(pos.x, maxX); }
        if (!bottomOpen) { pos.y = Mathf.Max(pos.y, minY); }
        if (!topOpen) { pos.y = Mathf.Min(pos.y, maxY); }

        return pos;
    }

    /// <summary>
    /// Clamps a camera center so the whole orthographic viewport stays inside this Area's bounds, returning whether the
    /// clamp positions changed. Each axis is pinned to the area center when the viewport is exactly the area width/height
    /// (the current fit), and clamped to the centered slack band when it is smaller — which is what stops the view from
    /// ever revealing the void or the adjacent/locked room while still letting a smaller camera follow the player.
    /// </summary>
    public bool ClampCamera(Vector2 viewportHalf, Vector3 center, out Vector3 clamped)
    {
        bool changed = false;
        float cx = center.x;
        float cy = center.y;
        float cz = center.z;

        Bounds b = Bounds;
        float bandX = (Width * 0.5f) - viewportHalf.x;
        float bandY = (Height * 0.5f) - viewportHalf.y;

        float areaCenterX = b.min.x + Width * 0.5f;
        float areaCenterY = b.min.y + Height * 0.5f;

        // If the viewport is >= the area size along this axis there is no room to move — lock to the area center so
        // the whole Area fills the screen and nothing outside (void/other rooms) is ever visible.
        if (bandX <= 0f)
        {
            if (Mathf.Abs(cx - areaCenterX) > 0.0001f)
            {
                changed = true;
            }
            cx = areaCenterX;
        }
        else
        {
            float minCx = areaCenterX - bandX;
            float maxCx = areaCenterX + bandX;
            float ncx = Mathf.Clamp(cx, minCx, maxCx);
            if (Mathf.Abs(ncx - cx) > 0.0001f)
            {
                changed = true;
            }
            cx = ncx;
        }

        if (bandY <= 0f)
        {
            if (Mathf.Abs(cy - areaCenterY) > 0.0001f)
            {
                changed = true;
            }
            cy = areaCenterY;
        }
        else
        {
            float minCy = areaCenterY - bandY;
            float maxCy = areaCenterY + bandY;
            float ncy = Mathf.Clamp(cy, minCy, maxCy);
            if (Mathf.Abs(ncy - cy) > 0.0001f)
            {
                changed = true;
            }
            cy = ncy;
        }

        clamped = new Vector3(cx, cy, cz);
        return changed;
    }
}
