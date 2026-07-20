using UnityEngine;

/// <summary>
/// Utility that turns a GameObject into a visible, editable white square — a test/visual placeholder with a
/// BoxCollider2D (trigger) sized to a given size, tinted and semi-transparent so the object is clearly visible
/// in both Scene and Game views and can be dragged/resized to tweak its position. Runtime-only: the white
/// sprite is generated once and shared, so no asset files are needed.
/// </summary>
public static class VisibleBox
{
    private static Sprite sharedSprite;

    private static Sprite WhiteSprite()
    {
        if (sharedSprite != null)
        {
            return sharedSprite;
        }
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        sharedSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sharedSprite.name = "VisibleBoxSolid";
        return sharedSprite;
    }

    /// <summary>Adds (or reuses) a visible, editable square child: a SpriteRenderer using a shared white sprite plus a
    /// trigger BoxCollider2D, sized to <paramref name="size"/>. Returns the child's Transform so the caller can
/// tint or layer it.</summary>
    public static Transform AddVisibleBox(GameObject host, Vector2 size, Color? tint = null, string name = "Visual")
    {
        var hostTransform = host.transform;
        var existing = hostTransform.Find(name);
        Transform child;
        if (existing != null)
        {
            child = existing;
        }
        else
        {
            var newChildObj = new GameObject(name);
            newChildObj.transform.SetParent(hostTransform, false);
            child = newChildObj.transform;
        }
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;

        var go = child.gameObject;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = go.AddComponent<SpriteRenderer>();
        }
        sr.sprite = WhiteSprite();
        sr.color = tint ?? new Color(1f, 1f, 1f, 0.25f);
        sr.sortingOrder = 5;

        var bc = go.GetComponent<BoxCollider2D>();
        if (bc == null)
        {
            bc = go.AddComponent<BoxCollider2D>();
        }
        bc.isTrigger = true;
        bc.size = size;

        // The SpriteRenderer draws the 1x1 sprite; scale it to the requested size so the visible box matches the
        // collider (and the region/wall it represents) exactly.
        child.localScale = new Vector3(size.x, size.y, 1f);

        return child;
    }
}
