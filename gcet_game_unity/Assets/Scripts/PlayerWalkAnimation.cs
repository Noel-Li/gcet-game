using UnityEngine;

/// <summary>
/// Gives a single player illustration a lightweight walk cycle without changing the
/// player's collider: the visual child flips horizontally and bounces while moving.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerWalkAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Movement component whose current input drives the animation.")]
    [SerializeField] private PlayerMovement movement;

    [Tooltip("Child transform containing only the player artwork. The bounce is applied here so collisions stay still.")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("Renderer to reflect when the player changes horizontal direction.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Facing")]
    [Tooltip("Enable this when the source artwork faces right. Moving left will then reflect it.")]
    [SerializeField] private bool facesRightByDefault = true;

    [Tooltip("Horizontal input smaller than this keeps the last facing direction.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float horizontalFlipThreshold = 0.05f;

    [Header("Bounce")]
    [Tooltip("Maximum upward offset of the artwork in local units.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float bounceHeight = 0.18f;

    [Tooltip("Number of complete up-and-down steps per second.")]
    [Min(0.1f)]
    [SerializeField] private float bounceFrequency = 3f;

    [Tooltip("How quickly the artwork settles back to its resting position after movement stops.")]
    [Min(0.1f)]
    [SerializeField] private float idleReturnSpeed = 1.5f;

    private Vector3 restingLocalPosition;
    private float bouncePhase;
    private bool hasRestingPosition;

    private void Awake()
    {
        ResolveReferences();
        RememberRestingPosition();
    }

    private void LateUpdate()
    {
        if (movement == null || visualRoot == null || spriteRenderer == null)
        {
            return;
        }

        Vector2 input = movement.MoveInput;
        UpdateFacing(input.x);
        UpdateBounce(input.sqrMagnitude > 0.0001f);
    }

    private void UpdateFacing(float horizontalInput)
    {
        if (Mathf.Abs(horizontalInput) <= horizontalFlipThreshold)
        {
            return;
        }

        bool movingLeft = horizontalInput < 0f;
        spriteRenderer.flipX = facesRightByDefault ? movingLeft : !movingLeft;
    }

    private void UpdateBounce(bool isWalking)
    {
        if (isWalking)
        {
            bouncePhase = Mathf.Repeat(
                bouncePhase + Time.deltaTime * bounceFrequency * Mathf.PI * 2f,
                Mathf.PI * 2f);

            float normalizedHeight = 0.5f - 0.5f * Mathf.Cos(bouncePhase);
            visualRoot.localPosition = restingLocalPosition + Vector3.up * (normalizedHeight * bounceHeight);
            return;
        }

        bouncePhase = 0f;
        visualRoot.localPosition = Vector3.MoveTowards(
            visualRoot.localPosition,
            restingLocalPosition,
            idleReturnSpeed * Time.deltaTime);
    }

    private void OnDisable()
    {
        if (hasRestingPosition && visualRoot != null)
        {
            visualRoot.localPosition = restingLocalPosition;
        }

        bouncePhase = 0f;
    }

    private void OnValidate()
    {
        horizontalFlipThreshold = Mathf.Max(0f, horizontalFlipThreshold);
        bounceHeight = Mathf.Max(0f, bounceHeight);
        bounceFrequency = Mathf.Max(0.1f, bounceFrequency);
        idleReturnSpeed = Mathf.Max(0.1f, idleReturnSpeed);
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
        }

        if (visualRoot == null && transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0);
        }

        if (spriteRenderer == null && visualRoot != null)
        {
            spriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
        }
    }

    private void RememberRestingPosition()
    {
        if (visualRoot == null)
        {
            return;
        }

        restingLocalPosition = visualRoot.localPosition;
        hasRestingPosition = true;
    }
}
