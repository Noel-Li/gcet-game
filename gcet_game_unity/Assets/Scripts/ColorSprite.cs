using UnityEngine;

// Recreated stub so the existing background references to the previous meta GUID stay valid. Keeps a plain-coloured square in case it is referenced anywhere; harmless if disabled.
public class ColorSprite : MonoBehaviour
{
    [SerializeField] private Color color = Color.white;
    private static Sprite sharedSquare;
    private void Awake()
    {
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite != null) return;
        if (sharedSquare == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sharedSquare = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            sharedSquare.name = "DialogueSolid";
        }
        renderer.sprite = sharedSquare;
        renderer.color = color;
    }
}
