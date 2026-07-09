using UnityEngine;

/// <summary>
/// Creates a solid-colored square at runtime and applies it to the
/// attached SpriteRenderer. This avoids depending on any external sprite or
/// material asset, so a scene made only of ColorSprite objects opens
/// without missing-reference errors.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ColorSprite : MonoBehaviour
{
    [SerializeField] private Color color = Color.white;

    private static Sprite sharedSquare;

    private void Awake()
    {
        // Only assign the sprite here. Color is taken from the serialized m_Color field
        // (set in the scene) and/or applied later via SetColor, so tinting done by
        // GameArea/NpcController is never overwritten by this Awake.
        var renderer = GetComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
    }

    /// <summary>Applies a new color (used by GameArea to tint the background).</summary>
    public void SetColor(Color newColor)
    {
        color = newColor;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = newColor;
        }
    }

    private static Sprite GetSquareSprite()
    {
        if (sharedSquare != null)
        {
            return sharedSquare;
        }

        // Build a tiny 1x1 white texture and turn it into a 1-unit sprite.
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        sharedSquare = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f,
            0,
            SpriteMeshType.FullRect
        );
        sharedSquare.name = "GeneratedSquare";
        return sharedSquare;
    }
}
