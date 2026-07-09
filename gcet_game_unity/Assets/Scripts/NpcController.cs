using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A clickable NPC. Clicking it changes its colour and unlocks the TOP exit of whichever Area
/// currently contains it, letting the player leave that room upward. Clicking again does nothing.
///
/// Uses a 2D raycast (Physics2D.GetRayIntersection) because OnMouseDown only fires on 3D
/// colliders and this NPC uses a BoxCollider2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class NpcController : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color activatedColor = Color.green;

    private SpriteRenderer spriteRenderer;
    private Color defaultColor;
    private bool activated = false;
    private Collider2D npcCollider;
    private Camera activeCamera;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        npcCollider = GetComponent<Collider2D>();
        if (spriteRenderer != null)
        {
            defaultColor = spriteRenderer.color;
        }

        activeCamera = Camera.main;
        if (activeCamera == null)
        {
            activeCamera = FindObjectOfType<Camera>();
        }
    }

    private void Update()
    {
        if (activated)
        {
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // Raycast from the camera through the mouse position into the 2D physics world.
        if (activeCamera == null)
        {
            return;
        }
        Vector2 mousePos = mouse.position.ReadValue();
        Ray ray = activeCamera.ScreenPointToRay(mousePos);
        RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, ~0);

        if (hit.collider == null || hit.collider != npcCollider)
        {
            return;
        }

        Activated();
    }

    private void Activated()
    {
        activated = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = activatedColor;
        }

        // Unlock the top exit of whatever room the NPC stands in so the player can move up.
        GameArea area = GameArea.GetAreaContaining(transform.position);
        if (area != null)
        {
            area.UnlockTopExit();
        }
    }
}
