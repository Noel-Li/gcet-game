using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Creates a solid-colored square at runtime and applies it to the
/// attached SpriteRenderer. This avoids depending on any external sprite or
/// material asset, so a scene made only of ColorSprite objects opens
/// without missing-reference errors.
///
/// If this object already has a sprite assigned in the editor it is never clobbered. The resolved sprite is recorded once
/// and re-applied on every subsequent Awake, so an editor-assigned sprite can never be lost because of a scene reload
/// (the character scene does two Single-mode LoadScene calls on the dialogue's writing step).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ColorSprite : MonoBehaviour
{
    [SerializeField] private Color color = Color.white;

    private static Sprite sharedSquare;
    // Safeguard for scene reload: remember what sprite each object ended up showing the first time we saw it (editing
    // the scene file can never change this — only RemoveAllComponents or a code bug can). Re-applying keeps the character
    // scene's repeated Loads from ever flipping the player/NPC back to the white fallback when they both started with
    // their proper avatar sprites.
    private static readonly Dictionary<string, Sprite> resolvedByObject = new Dictionary<string, Sprite>();

    private void Awake()
    {
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return;
        }
        string key = StabilityKey();
        if (resolvedByObject.TryGetValue(key, out Sprite resolved) && resolved != null)
        {
            // We already know what this object should show (whether textured or the fallback square). Restore it — this is
            // what keeps the player/NPC sprites intact across the writing-step scene reload.
            renderer.sprite = resolved;
            return;
        }

        if (renderer.sprite == null)
        {
            renderer.sprite = GetSquareSprite();
        }
        resolvedByObject[key] = renderer.sprite;
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

    /// <summary>
    /// Stable identity for this object that survives a scene reload. Based on the GameObject's full path: the scene file
    /// controls that, so the same key resolves after every LoadScene the dialogue's writing step performs.
    /// </summary>
    private string StabilityKey()
    {
        return GetType().Name + "@" + transform.GetSiblingIndex() + ":" + name + " @" + gameObject.scene.name;
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
