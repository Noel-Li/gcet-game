using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// An invisible, conversation-gated barrier on a shared edge between two regions. Pure logic — no sprite, renderer, or
/// physical collider of its own. Its position and span are read directly from this GameObject: WallX/WallY = its
/// Transform.position, span = its BoxCollider2D long dimension. There is no child object and no cached geometry to go stale —
/// drag the wall or resize its collider in the Scene view and it follows exactly, for any span or placement.
/// While locked, a player whose extent overlaps the wall is blocked from crossing in either direction and a fixed on-screen
/// popup shows the way is shut; Unlock() (called by the NPC) flips Locked to false so the player passes. The neighbour
/// GameArea leaves a gap on any edge this wall covers.
///
/// If the two adjacent regions are flush (no void gap), <see cref="SnapToSharedEdge"/> snaps this wall onto their shared
/// boundary at runtime — the only geometrically correct gate position — and spans it to seal that whole edge.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class InvisibleWall : MonoBehaviour
{
    private static readonly List<InvisibleWall> registered = new List<InvisibleWall>();
    private BoxCollider2D col;

    [SerializeField] private string blockedMessage = "you cannot go through there";
    [SerializeField] private Color popupColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float fontSize = 22f;
    [SerializeField] private Vector2 fixedScreenAnchor = new Vector2(0.5f, 0.8f);
    [Range(0.2f, 2f)] [SerializeField] private float popupScreenOffset = 0.6f;
    [Range(0.1f, 0.9f)] [SerializeField] private float maxWidthFraction = 0.5f;

    private bool locked = true;
    private bool popupBuilt;
    private bool snapped;
    private Canvas canvas;
    private RectTransform panel;
    private TextMeshProUGUI text;
    private Transform player;

    private const string PlayerName = "Player";
    private const float EdgeEpsilon = 0.05f;
    private const float SnapThreshold = 8f;

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        // Trigger, not a solid collider: the wall is pure logic. A solid collider would physically block the player
        // and fight the mathematical gating done by ClampPlayer. The collider is an editor handle the user drags to
        // size the wall; crossing is detected geometrically, not by physics.
        col.isTrigger = true;
        BuildPopup();
    }

    private void OnEnable()
    {
        if (!registered.Contains(this))
        {
            registered.Add(this);
        }
        snapped = false;
    }

    private void OnDisable() { registered.Remove(this); }

    public bool Locked => locked;
    public float WallX => transform.position.x;
    public float WallY => transform.position.y;
    public bool Horizontal => col == null || col.size.x >= col.size.y;
    public float Span => col != null ? (Horizontal ? col.size.x : col.size.y) : 0f;
    public Vector3 CellCenter => new Vector3(WallX, WallY, 0f);
    public Bounds CellBounds => new Bounds(CellCenter, new Vector3(17.78f, 10f, 0f));

    /// <summary>Grid coordinate this wall gates (identity). Used by GameProgress to find and bootstrap the forward gate.</summary>
    public int Col { get; set; }
    public int Row { get; set; }

    public bool ClampCamera(Vector2 viewportHalf, Vector3 center, out Vector3 clamped)
    {
        clamped = new Vector3(CellCenter.x, CellCenter.y, center.z);
        return true;
    }

    private void Update()
    {
        if (!snapped)
        {
            SnapToSharedEdge();
            snapped = true;
        }

        if (!locked)
        {
            SetPopupVisible(false);
            return;
        }
        if (player == null) LocatePlayer();
        SetPopupVisible(player != null && IsPlayerTouching());
    }

    private bool IsPlayerTouching()
    {
        Vector3 p = player.position;
        float half = player.localScale.x * 0.5f;
        float margin = 0.6f;
        float span = Span;
        if (Horizontal)
        {
            bool overlapX = p.x + half > WallX - span && p.x - half < WallX + span;
            return overlapX && Mathf.Abs(p.y - WallY) <= margin + half;
        }
        else
        {
            bool overlapY = p.y + half > WallY - span && p.y - half < WallY + span;
            return overlapY && Mathf.Abs(p.x - WallX) <= margin + half;
        }
    }

    public static bool CoversVerticalEdge(float x, float y)
    {
        foreach (var wall in registered)
        {
            if (wall == null || wall.Horizontal) continue;
            if (Mathf.Abs(wall.WallX - x) > EdgeEpsilon) continue;
            if (y >= wall.WallY - wall.Span && y <= wall.WallY + wall.Span) return true;
        }
        return false;
    }

    public static bool CoversHorizontalEdge(float y, float x)
    {
        foreach (var wall in registered)
        {
            if (wall == null || !wall.Horizontal) continue;
            if (Mathf.Abs(wall.WallY - y) > EdgeEpsilon) continue;
            if (x >= wall.WallX - wall.Span && x <= wall.WallX + wall.Span) return true;
        }
        return false;
    }

    public static void ClampPlayer(Vector3 pos, float playerHalf, ref Vector3 clamped)
    {
        clamped = pos;
        foreach (var wall in registered)
        {
            if (wall == null || !wall.Locked) continue;
            float span = wall.Span;
            if (wall.Horizontal)
            {
                bool overlapX = clamped.x + playerHalf > wall.WallX - span && clamped.x - playerHalf < wall.WallX + span;
                if (!overlapX) continue;
                float before = clamped.y;
                clamped.y = (clamped.y < wall.WallY) ? Mathf.Min(clamped.y, wall.WallY - playerHalf) : Mathf.Max(clamped.y, wall.WallY + playerHalf);
                if (Mathf.Abs(before - clamped.y) > 1e-4f) Debug.Log($"[InvisibleWall {wall.name}] BLOCKING horizontal: player y {before:F2} -> {clamped.y:F2} (wallY={wall.WallY})");
            }
            else
            {
                bool overlapY = clamped.y + playerHalf > wall.WallY - span && clamped.y - playerHalf < wall.WallY + span;
                if (!overlapY) continue;
                float before = clamped.x;
                clamped.x = (clamped.x < wall.WallX) ? Mathf.Min(clamped.x, wall.WallX - playerHalf) : Mathf.Max(clamped.x, wall.WallX + playerHalf);
                if (Mathf.Abs(before - clamped.x) > 1e-4f) Debug.Log($"[InvisibleWall {wall.name}] BLOCKING vertical: player x {before:F2} -> {clamped.x:F2} (wallX={wall.WallX})");
            }
        }
    }

    public void Unlock()
    {
        Debug.Log($"[InvisibleWall {name}] UNLOCKED");
        locked = false;
        SetPopupVisible(false);
    }

    public bool ContainsPoint(Vector3 point)
    {
        Bounds b = new Bounds(CellCenter, new Vector3(17.78f, 10f, 0f));
        return point.x >= b.min.x && point.x <= b.max.x && point.y >= b.min.y && point.y <= b.max.y;
    }

    public static InvisibleWall GetCellContaining(Vector3 point)
    {
        foreach (var wall in registered) if (wall != null && wall.ContainsPoint(point)) return wall;
        return null;
    }

    public static InvisibleWall GetWallAt(int col, int row)
    {
        foreach (var wall in registered) if (wall != null && wall.Col == col && wall.Row == row) return wall;
        return null;
    }

    public static void UnlockWallAt(int col, int row)
    {
        var wall = GetWallAt(col, row);
        if (wall != null) wall.Unlock();
    }

    public static IReadOnlyList<InvisibleWall> GetRegistered()
    {
        registered.RemoveAll(a => a == null);
        return registered;
    }

    /// <summary>
    /// Snaps this wall onto the shared boundary between its two nearest regions. A wall gates the edge where two regions
    /// touch: a horizontally-stacked pair (R1 below, R2 above) gets a horizontal wall at their shared Y spanning their
    /// X-overlap; a side-by-side pair gets a vertical wall at their shared X spanning their Y-overlap. Insensitive to rough
    // manual placement: it just needs the wall to start somewhere near the two regions that share an edge.
    /// </summary>
    private void SnapToSharedEdge()
    {
        GameArea r1 = null, r2 = null;
        float d1 = float.MaxValue, d2 = float.MaxValue;
        Vector2 wp = new Vector2(transform.position.x, transform.position.y);
        foreach (var area in GameArea.GetRegistered())
        {
            if (area == null) continue;
            float d = Vector2.Distance(wp, area.Bounds.center);
            if (d < d1) { d2 = d1; r2 = r1; d1 = d; r1 = area; }
            else if (d < d2) { d2 = d; r2 = area; }
        }
        if (r1 == null) return;
        if (r2 != null)
        {
            Bounds b1 = r1.Bounds, b2 = r2.Bounds;
            // X-overlap (stacked vertically) vs Y-overlap (side by side).
            float xol = Mathf.Min(b1.max.x, b2.max.x) - Mathf.Max(b1.min.x, b2.min.x);
            float yol = Mathf.Min(b1.max.y, b2.max.y) - Mathf.Max(b1.min.y, b2.min.y);
            bool stacked = xol > 0f && (Mathf.Abs(b1.max.y - b2.min.y) < SnapThreshold || Mathf.Abs(b2.max.y - b1.min.y) < SnapThreshold);
            bool sidebyside = yol > 0f && (Mathf.Abs(b1.max.x - b2.min.x) < SnapThreshold || Mathf.Abs(b2.max.x - b1.min.x) < SnapThreshold);

            if (stacked)
            {
                float sharedY = (b1.max.y + b2.min.y) / 2f;
                float cx = Mathf.Clamp(b1.center.x, Mathf.Max(b1.min.x, b2.min.x), Mathf.Min(b1.max.x, b2.max.x));
                transform.position = new Vector3(cx, sharedY, 0f);
                col.size = new Vector2(Mathf.Abs(xol), 0.1f); // wide => horizontal
                Debug.Log($"[InvisibleWall {name}] snap stacked {r1.AreaName}/{r2.AreaName} y={sharedY:F2} spanX={Mathf.Abs(xol):F2}");
                return;
            }
            if (sidebyside)
            {
                float sharedX = (b1.max.x + b2.min.x) / 2f;
                float cy = Mathf.Clamp(b1.center.y, Mathf.Max(b1.min.y, b2.min.y), Mathf.Min(b1.max.y, b2.max.y));
                transform.position = new Vector3(sharedX, cy, 0f);
                col.size = new Vector2(0.1f, Mathf.Abs(yol)); // tall => vertical
                Debug.Log($"[InvisibleWall {name}] snap side {r1.AreaName}/{r2.AreaName} x={sharedX:F2} spanY={Mathf.Abs(yol):F2}");
                return;
            }
        }
        // Fallback: single region, snap to its nearest edge.
        Bounds b = r1.Bounds;
        float dt = Mathf.Abs(wp.y - b.max.y), db = Mathf.Abs(wp.y - b.min.y), dl = Mathf.Abs(wp.x - b.min.x), dr = Mathf.Abs(wp.x - b.max.x);
        float min = Mathf.Min(dt, db, dl, dr);
        if (min > SnapThreshold) return;
        float x = wp.x, y = wp.y; bool horiz;
        if (min == dt) { horiz = true; y = b.max.y; x = Mathf.Clamp(x, b.min.x, b.max.x); }
        else if (min == db) { horiz = true; y = b.min.y; x = Mathf.Clamp(x, b.min.x, b.max.x); }
        else if (min == dl) { horiz = false; x = b.min.x; y = Mathf.Clamp(y, b.min.y, b.max.y); }
        else { horiz = false; x = b.max.x; y = Mathf.Clamp(y, b.min.y, b.max.y); }
        transform.position = new Vector3(x, y, 0f);
        col.size = horiz ? new Vector2(b.size.x, 0.1f) : new Vector2(0.1f, b.size.y);
        Debug.Log($"[InvisibleWall {name}] snap single {r1.AreaName} ({x:F2},{y:F2}) span={b.size.x:F2}");    }

    private void SetPopupVisible(bool visible)
    {
        if (canvas != null && canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
        if (visible && canvas != null && canvas.gameObject.activeSelf) PositionPopup();
    }

    private void PositionPopup()
    {
        if (panel == null || Camera.main == null) return;
        float px = fixedScreenAnchor.x * Screen.width;
        float py = fixedScreenAnchor.y * Screen.height;
        panel.position = new Vector3(px, py - popupScreenOffset * Screen.height, 0f);
    }

    private void BuildPopup()
    {
        if (popupBuilt) return;
        var co = new GameObject("InvisibleWallPopupCanvas");
        co.transform.SetParent(null, false);
        canvas = co.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        co.AddComponent<CanvasScaler>();
        var cr = canvas.GetComponent<RectTransform>();
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
        var po = new GameObject("Popup");
        po.transform.SetParent(co.transform, false);
        panel = po.AddComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 1f); panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        var im = po.AddComponent<Image>();
        im.sprite = SolidSprite(); im.color = popupColor; im.raycastTarget = false;
        po.AddComponent<RectMask2D>();
        var tob = new GameObject("Text");
        tob.transform.SetParent(po.transform, false);
        text = MakeText(tob, textColor, fontSize);
        text.text = blockedMessage;
        text.enableWordWrapping = true;
        float maxW = Screen.width * maxWidthFraction;
        var tr = text.GetComponent<RectTransform>();
        tr.sizeDelta = new Vector2(maxW, 0f);
        text.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        float tW = Mathf.Min(text.preferredWidth, maxW);
        float tH = Mathf.Max(text.preferredHeight, 1f);
        tr.sizeDelta = new Vector2(tW, tH);
        float pad = 18f;
        panel.sizeDelta = new Vector2(tW + pad * 2f, tH + pad * 2f);
        tr.anchoredPosition = Vector2.zero;
        var pr = panel.GetComponent<RectTransform>();
        pr.anchoredPosition = Vector2.zero;
        canvas.gameObject.SetActive(false);
        popupBuilt = true;
    }

    private TextMeshProUGUI MakeText(GameObject host, Color color, float size)
    {
        var tmp = host.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font.material != null) tmp.material = tmp.font.material;
            else { var m = new Material(Shader.Find("TextMeshPro/Mobile/Distance Field")); if (m != null) tmp.material = m; }
        }
        tmp.fontSize = size; tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true; tmp.overflowMode = TextOverflowModes.Overflow; tmp.enableAutoSizing = false;
        tmp.outlineWidth = 0.14f; tmp.outlineColor = Color.black; tmp.richText = true; tmp.raycastTarget = false;
        var rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;
        return tmp;
    }

    private static Sprite SolidSprite()
    {
        var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
        var s = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        s.name = "InvisibleWallSolid";
        return s;
    }

    private void LocatePlayer()
    {
        var po = GameObject.Find(PlayerName);
        if (po != null) player = po.transform;
    }
}
