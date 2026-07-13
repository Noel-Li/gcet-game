using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RainBackground : MonoBehaviour
{
    public int dropCount = 80;

    public float minSpeed = 180f;
    public float maxSpeed = 320f;

    public float minLength = 18f;
    public float maxLength = 45f;

    public float minWidth = 2f;
    public float maxWidth = 4f;

    public Color rainColor = new Color(0.75f, 0.9f, 1f, 0.35f);

    private RectTransform container;
    private List<RainDrop> drops = new List<RainDrop>();
    private Sprite whiteSprite;

    private class RainDrop
    {
        public RectTransform rectTransform;
        public float speed;
    }

    void Awake()
    {
        container = GetComponent<RectTransform>();
        whiteSprite = CreateWhiteSprite();
        CreateRain();
    }

    void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = 0; i < drops.Count; i++)
        {
            RainDrop drop = drops[i];

            Vector2 position = drop.rectTransform.anchoredPosition;
            position.y -= drop.speed * deltaTime;

            drop.rectTransform.anchoredPosition = position;

            if (position.y < -container.rect.height / 2f - 80f)
            {
                ResetDrop(drop, true);
            }
        }
    }

    void CreateRain()
    {
        for (int i = 0; i < dropCount; i++)
        {
            GameObject dropObject = new GameObject("RainDrop");

            dropObject.transform.SetParent(transform, false);

            Image image = dropObject.AddComponent<Image>();
            image.sprite = whiteSprite;
            image.color = rainColor;
            image.raycastTarget = false;

            RectTransform rectTransform = dropObject.GetComponent<RectTransform>();

            RainDrop drop = new RainDrop();
            drop.rectTransform = rectTransform;
            drop.speed = Random.Range(minSpeed, maxSpeed);

            float width = Random.Range(minWidth, maxWidth);
            float height = Random.Range(minLength, maxLength);

            rectTransform.sizeDelta = new Vector2(width, height);
            rectTransform.rotation = Quaternion.Euler(0f, 0f, -15f);

            drops.Add(drop);

            ResetDrop(drop, false);
        }
    }

    void ResetDrop(RainDrop drop, bool startAtTop)
    {
        float halfWidth = container.rect.width / 2f;
        float halfHeight = container.rect.height / 2f;

        float x = Random.Range(-halfWidth, halfWidth);
        float y;

        if (startAtTop)
        {
            y = halfHeight + Random.Range(20f, 200f);
        }
        else
        {
            y = Random.Range(-halfHeight, halfHeight);
        }

        drop.rectTransform.anchoredPosition = new Vector2(x, y);
        drop.speed = Random.Range(minSpeed, maxSpeed);
    }

    Sprite CreateWhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f)
        );
    }
}
