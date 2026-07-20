using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class GameArea : MonoBehaviour
{
    private static readonly List<GameArea> registered = new List<GameArea>();
    private BoxCollider2D col;

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        // The collider is a trigger: it is purely an editor handle the user drags to size the region. All gating is done
        // mathematically by ClampPlayer — a solid collider would physically block the player and fight the math.
        col.isTrigger = true;
    }
    private void OnEnable() { if (!registered.Contains(this)) registered.Add(this); }
    private void OnDisable() { registered.Remove(this); }

    public string AreaName => name;
    public Vector3 Center => transform.position;
    public float Width => col != null ? col.size.x : 0f;
    public float Height => col != null ? col.size.y : 0f;
    public Bounds Bounds => new Bounds(Center, new Vector3(Width, Height, 0f));

    /// <summary>
    /// Grid identity of this region — a stable integer label derived from its world position, used by NpcController and
    /// the dialogue system to identify which cell this region occupies. It is a label only; the region's actual position
    /// is always its live transform. Nominal cell size is one camera viewport (cellW = camera.aspect*ortho*2, cellH = ortho*2).
    /// </summary>
    public int AreaCol
    {
        get
        {
            float cellW = Width > 0f ? Width : 17.78f;
            return Mathf.RoundToInt((Center.x - Width * 0.5f) / cellW);
        }
    }
    public int AreaRow
    {
        get
        {
            float cellH = Height > 0f ? Height : 10f;
            return Mathf.RoundToInt((Center.y - Height * 0.5f) / cellH);
        }
    }

    public bool ContainsPoint(Vector3 point)
    {
        Bounds b = Bounds;
        return point.x >= b.min.x && point.x <= b.max.x && point.y >= b.min.y && point.y <= b.max.y;
    }

    public static GameArea GetAreaContaining(Vector3 point)
    {
        foreach (var area in registered) if (area != null && area.ContainsPoint(point)) return area;
        return null;
    }

    public Vector3 ClampPlayer(Vector3 pos, float playerHalf)
    {
        Bounds b = Bounds;
        float minX = b.min.x + playerHalf, maxX = b.max.x - playerHalf;
        float minY = b.min.y + playerHalf, maxY = b.max.y - playerHalf;

        bool leftOpen = InvisibleWall.CoversVerticalEdge(b.min.x, pos.y);
        bool rightOpen = InvisibleWall.CoversVerticalEdge(b.max.x, pos.y);
        bool bottomOpen = InvisibleWall.CoversHorizontalEdge(b.min.y, pos.x);
        bool topOpen = InvisibleWall.CoversHorizontalEdge(b.max.y, pos.x);

        if (!leftOpen && pos.x < minX) { Debug.Log($"[GameArea {name}] clamp LEFT edge (void)"); pos.x = minX; }
        if (!rightOpen && pos.x > maxX) { Debug.Log($"[GameArea {name}] clamp RIGHT edge (void) pos.x={pos.x:F2}>{maxX:F2} rightOpen={rightOpen}"); pos.x = maxX; }
        if (!bottomOpen && pos.y < minY) { Debug.Log($"[GameArea {name}] clamp BOTTOM edge (void)"); pos.y = minY; }
        if (!topOpen && pos.y > maxY) { Debug.Log($"[GameArea {name}] clamp TOP edge (void) pos.y={pos.y:F2}>{maxY:F2} topOpen={topOpen}"); pos.y = maxY; }
        return pos;
    }

    public bool ClampCamera(Vector2 viewportHalf, Vector3 center, out Vector3 clamped)
    {
        bool changed = false;
        float cx = center.x, cy = center.y, cz = center.z;
        Bounds b = Bounds;
        float bandX = (Width * 0.5f) - viewportHalf.x;
        float bandY = (Height * 0.5f) - viewportHalf.y;
        float acx = b.min.x + Width * 0.5f, acy = b.min.y + Height * 0.5f;
        if (bandX <= 0f) { if (Mathf.Abs(cx - acx) > 1e-4f) changed = true; cx = acx; }
        else { float lo = acx - bandX, hi = acx + bandX; float n = Mathf.Clamp(cx, lo, hi); if (Mathf.Abs(n - cx) > 1e-4f) changed = true; cx = n; }
        if (bandY <= 0f) { if (Mathf.Abs(cy - acy) > 1e-4f) changed = true; cy = acy; }
        else { float lo = acy - bandY, hi = acy + bandY; float n = Mathf.Clamp(cy, lo, hi); if (Mathf.Abs(n - cy) > 1e-4f) changed = true; cy = n; }
        clamped = new Vector3(cx, cy, cz);
        return changed;
    }

    public static IReadOnlyList<GameArea> GetRegistered()
    {
        registered.RemoveAll(a => a == null);
        return registered;
    }

    private void OnDrawGizmos()
    {
        Bounds b = Bounds;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawCube(b.center, new Vector3(b.size.x, b.size.y, 0f));
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, b.size.y, 0f));
    }
}
