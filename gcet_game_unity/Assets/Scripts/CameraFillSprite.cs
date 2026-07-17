using UnityEngine;

/// <summary>
/// Stretches a sprite to the current orthographic camera viewport so no clear-color bars remain.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CameraFillSprite : MonoBehaviour
{
    [Header("Viewport")]
    [Tooltip("Orthographic camera whose visible area this sprite should fill.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Keep the sprite centered on the camera while fitting it.")]
    [SerializeField] private bool centerOnCamera = true;

    private SpriteRenderer spriteRenderer;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        FillViewport();
    }

    private void LateUpdate()
    {
        FillViewport();
    }

    private void OnValidate()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        FillViewport();
    }

    private void FillViewport()
    {
        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null || !cameraToUse.orthographic || spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        float cameraHeight = cameraToUse.orthographicSize * 2f;
        float cameraWidth = cameraHeight * cameraToUse.aspect;
        transform.localScale = new Vector3(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y, 1f);

        if (centerOnCamera)
        {
            Vector3 position = transform.position;
            position.x = cameraToUse.transform.position.x;
            position.y = cameraToUse.transform.position.y;
            transform.position = position;
        }
    }
}
