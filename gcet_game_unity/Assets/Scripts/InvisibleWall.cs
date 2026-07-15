using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// An invisible, conversation-gated barrier on a single cell edge of the world grid. It has
/// NO sprite, renderer, or collider — it is pure logic. While locked, a player whose horizontal
/// extent overlaps the wall is blocked from crossing it in its blocking direction, and a small
/// popup is shown to let the player know the way is shut. Once <see cref="Unlock"/> is called
/// (by the NPC/trace success path, mirroring how the top region opens) the wall stops blocking
/// and the popup is suppressed so the player walks straight through.
///
/// The wall derives its world position from the main camera at runtime, so it always sits exactly
/// on the target cell edge regardless of aspect ratio — same source of truth as
/// <see cref="GameArea.FitToCamera"/>. Attach it to any GameObject (its own Transform position is
/// irrelevant; only <see cref="col"/>/<see cref="row"/>/<see cref="wallOnTop"/> matter).
/// </summary>
public class InvisibleWall : MonoBehaviour
{
    [Header("Grid cell this wall belongs to")]
    [Tooltip("Column of the cell this wall sits on (matches GameArea.areaCol).")]
    [SerializeField] private int col = 1;
    [Tooltip("Row of the cell this cell sits on (matches GameArea.areaRow).")]
    [SerializeField] private int row = 1;

    [Header("Edge")]
    [Tooltip("Which edge of the cell the wall blocks. True = top (blocks moving up); False = right (blocks moving right).")]
    [SerializeField] private bool wallOnTop = true;

    [Header("Locked message")]
    [Tooltip("Shown when the player tries to pass before the conversation unlocks the wall.")]
    [SerializeField] private string blockedMessage = "you cannot go through there";

    [Header("Popup look")]
    [SerializeField] private Color popupColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float fontSize = 22f;
    [Range(0.2f, 2f)]
    [SerializeField] private float popupScreenOffset = 0.6f;
    [Range(0.1f, 0.9f)]
    [SerializeField] private float maxWidthFraction = 0.5f;

    /// <summary>All live walls.</summary>
    private static readonly List<InvisibleWall> registered = new List<InvisibleWall>();

    // World-space geometry (matching GameArea.FitToCamera).
    private float cellW;
    private float cellH;
    private float wallX;
    private float wallY;
    private float wallHalfW;   // for a top wall: half the width it spans; for a right wall: ~0
    private float wallHalfH;   // for a right wall: half the height it spans; for a top wall: ~0

    private bool locked = true;
    private bool forceReposition;
    private bool popupBuilt;
    private Canvas canvas;
    private RectTransform panel;
    private TextMeshProUGUI text;
    private Transform player;

    private const string PlayerName = "Player";

    public int Col { get => col; set { col = value; CacheCellMetrics(); forceReposition = true; } }
    public int Row { get => row; set { row = value; CacheCellMetrics(); forceReposition = true; } }
    public bool Locked => locked;
    public float CellW => cellW;
    public float CellH => cellH;

    /// <summary>Center of the wall's cell in world space — the room the camera frames when the player stands here.</summary>
    public Vector3 CellCenter => new Vector3(col * cellW, row * cellH, 0f);

    /// <summary>Full cell bounds (the room the camera frames when the player occupies this wall-governed cell).</summary>
    public Bounds CellBounds
    {
        get
        {
            Vector3 size = new Vector3(cellW, cellH, 0f);
            return new Bounds(CellCenter, size);
        }
    }

    /// <summary>
    /// Clamps a camera center so the viewport stays inside this wall's cell, exactly as
    /// GameArea.ClampCamera does for a room. Because the cell is derived from the camera viewport,
    /// there is never any slack: the camera pins to the cell center and frames exactly this cell.
    /// Lets FollowCamera treat a wall-governed cell as a real room so the gate is always on-screen.
    /// </summary>
    public bool ClampCamera(Vector2 viewportHalf, Vector3 center, out Vector3 clamped)
    {
        clamped = new Vector3(CellCenter.x, CellCenter.y, center.z);
        return true;
    }

    private void Awake()
    {
        CacheCellMetrics();
        SnapToWall();
        BuildPopup();
    }

    private void SnapToWall()
    {
        transform.position = new Vector3(wallX, wallY, 0f);
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

    private void Update()
    {
        // Col/Row may be assigned after Awake (GameProgress bootstraps the wall with a forced position), so keep the Transform on the edge.
        if (forceReposition)
        {
            SnapToWall();
            forceReposition = false;
        }

        // While the conversation has not completed, the wall is a gate: keep the player out
        // and surface a message the moment they press against it.
        if (!locked)
        {
            SetPopupVisible(false);
            return;
        }

        if (player == null)
        {
            LocatePlayer();
        }
        bool touching = player != null && IsPlayerTouching();
        SetPopupVisible(touching);
    }

    // -------------------------------------------------------------------------
    // Geometry — kept in sync with GameArea.FitToCamera so cells line up exactly.
    // -------------------------------------------------------------------------
    private void CacheCellMetrics()
    {
        Camera mainCam = Camera.main;
        float ortho = mainCam != null ? mainCam.orthographicSize : 5f;
        float aspect = mainCam != null ? mainCam.aspect : 16f / 9f;
        cellH = ortho * 2f;
        cellW = cellH * aspect;

        float cellCenterX = col * cellW;
        float cellCenterY = row * cellH;

        // The cell spans [center - size/2, center + size/2] on each axis. The top edge is at
        // center + H/2 and runs the full cell width; the right edge is at center + W/2 and runs the full height.
        if (wallOnTop)
        {
            wallX = cellCenterX;
            wallY = cellCenterY + cellH * 0.5f;
            wallHalfW = cellW * 0.5f;
            wallHalfH = 0.05f;
        }
        else
        {
            wallX = cellCenterX + cellW * 0.5f;
            wallY = cellCenterY;
            wallHalfW = 0.05f;
            wallHalfH = cellH * 0.5f;
        }
    }

    /// <summary>
    /// Clamps the player out of every locked wall. A horizontal (top) wall blocks +Y movement of
    /// any player horizontally overlapping it; a vertical (right) wall blocks +X movement of any
    /// player vertically overlapping it. Mirrors <see cref="GameArea.ClampPlayer"/>'s push-back semantics.
    /// </summary>
    public static void ClampPlayer(Vector3 pos, float playerHalf, ref Vector3 clamped)
    {
        clamped = pos;
        foreach (var wall in registered)
        {
            if (wall == null || !wall.locked)
            {
                continue;
            }

            if (wall.wallOnTop)
            {
                // Horizontal wall: only matters if the player spans its x-extent.
                bool overlapsX = clamped.x + playerHalf > wall.wallX - wall.wallHalfW
                                 && clamped.x - playerHalf < wall.wallX + wall.wallHalfW;
                if (overlapsX)
                {
                    float limitY = wall.wallY - playerHalf;
                    if (clamped.y > limitY)
                    {
                        clamped.y = limitY;
                    }
                }
            }
            else
            {
                // Vertical wall: only matters if the player spans its y-extent.
                bool overlapsY = clamped.y + playerHalf > wall.wallY - wall.wallHalfH
                                 && clamped.y - playerHalf < wall.wallY + wall.wallHalfH;
                if (overlapsY)
                {
                    float limitX = wall.wallX - playerHalf;
                    if (clamped.x > limitX)
                    {
                        clamped.x = limitX;
                    }
                }
            }
        }
    }

    /// <summary>The conversation finished — stop blocking and hide the message so the player passes freely.</summary>
    public void Unlock()
    {
        locked = false;
        SetPopupVisible(false);
    }

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------
    public static InvisibleWall GetWallAt(int col, int row)
    {
        foreach (var wall in registered)
        {
            if (wall != null && wall.col == col && wall.row == row)
            {
                return wall;
            }
        }
        return null;
    }

    public static void UnlockWallAt(int col, int row)
    {
        var wall = GetWallAt(col, row);
        if (wall != null)
        {
            wall.Unlock();
        }
    }

    /// <summary>The wall whose cell contains the world point, or null. Lets the camera frame a wall-governed cell.</summary>
    public static InvisibleWall GetCellContaining(Vector3 point)
    {
        foreach (var wall in registered)
        {
            if (wall == null)
            {
                continue;
            }
            Bounds b = wall.CellBounds;
            if (point.x >= b.min.x && point.x <= b.max.x &&
                point.y >= b.min.y && point.y <= b.max.y)
            {
                return wall;
            }
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // Popup
    // -------------------------------------------------------------------------
    private bool IsPlayerTouching()
    {
        Vector3 p = player.position;
        float half = player.localScale.x * 0.5f;
        float margin = 0.6f; // distance within which we call it "pressing into" the wall

        if (wallOnTop)
        {
            bool overlapX = p.x + half > wallX - wallHalfW
                            && p.x - half < wallX + wallHalfW;
            bool nearFromBelow = p.y < wallY && (wallY - p.y) <= margin + half;
            return overlapX && nearFromBelow;
        }
        else
        {
            bool overlapY = p.y + half > wallY - wallHalfH
                            && p.y - half < wallY + wallHalfH;
            bool nearFromLeft = p.x < wallX && (wallX - p.x) <= margin + half;
            return overlapY && nearFromLeft;
        }
    }

    private void SetPopupVisible(bool visible)
    {
        if (canvas != null && canvas.gameObject.activeSelf != visible)
        {
            canvas.gameObject.SetActive(visible);
        }
        if (visible && canvas != null && canvas.gameObject.activeSelf)
        {
            PositionPopup();
        }
    }

    private void PositionPopup()
    {
        if (panel == null || Camera.main == null)
        {
            return;
        }
        // Anchor the popup over the wall's world position and let it self-size to the text.
        Vector3 screen = Camera.main.WorldToViewportPoint(new Vector3(wallX, wallY, 0f));
        float px = screen.x * Screen.width;
        float py = screen.y * Screen.height;
        panel.position = new Vector3(px, py - popupScreenOffset * Screen.height, 0f);
    }

    private void BuildPopup()
    {
        if (popupBuilt)
        {
            return;
        }

        var canvasObj = new GameObject("InvisibleWallPopupCanvas");
        canvasObj.transform.SetParent(null, false);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Sit above the dialogue canvas (sortingOrder 100) so the wall message is readable over the world.
        canvas.sortingOrder = 150;
        canvasObj.AddComponent<CanvasScaler>();

        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        var panelObj = new GameObject("Popup");
        panelObj.transform.SetParent(canvasObj.transform, false);
        panel = panelObj.AddComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);

        var image = panelObj.AddComponent<Image>();
        image.sprite = SolidSprite();
        image.color = popupColor;
        image.raycastTarget = false;

        panelObj.AddComponent<RectMask2D>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(panelObj.transform, false);
        text = MakeText(textObj, textColor, fontSize);
        text.text = blockedMessage;

        // Size to content.
        text.enableWordWrapping = true;
        float maxWidth = Screen.width * maxWidthFraction;
        var tr = text.GetComponent<RectTransform>();
        tr.sizeDelta = new Vector2(maxWidth, 0f);
        text.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        float textW = Mathf.Min(text.preferredWidth, maxWidth);
        float textH = Mathf.Max(text.preferredHeight, 1f);
        tr.sizeDelta = new Vector2(textW, textH);

        float pad = 18f;
        panel.sizeDelta = new Vector2(textW + pad * 2f, textH + pad * 2f);
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
            if (tmp.font.material != null)
            {
                tmp.material = tmp.font.material;
            }
            else
            {
                var mat = new Material(Shader.Find("TextMeshPro/Mobile/Distance Field"));
                if (mat != null)
                {
                    tmp.material = mat;
                }
            }
        }
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableAutoSizing = false;
        tmp.outlineWidth = 0.14f;
        tmp.outlineColor = Color.black;
        tmp.richText = true;
        tmp.raycastTarget = false;
        var rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return tmp;
    }

    private static Sprite SolidSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        s.name = "InvisibleWallSolid";
        return s;
    }

    private void LocatePlayer()
    {
        var playerObj = GameObject.Find(PlayerName);
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }
}
