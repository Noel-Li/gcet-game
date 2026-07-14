using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves a square player with WASD. The player walks freely across Areas but is always
/// clamped inside the current Area (PlayerMovement.ClampToArea) so they can never go
/// off-camera. The top edge of a room acts as a gate: the player cannot leave upward until
/// that Area.ExitUnlocked is true (an NPC flips that). Once unlocked the player walks up
/// into the next room and the camera follows.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Optional: drag the InputActions asset here (Player / Move). If empty, a WASD action is created automatically.")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField] private float speed = 5f;

    private InputAction moveAction;

    private void Awake()
    {
        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Player", false);
            if (map != null)
            {
                moveAction = map.FindAction("Move", false);
            }
        }

        if (moveAction == null)
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        moveAction.Enable();
    }

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.Disable();
        }
    }

    private void Update()
    {
        // Freeze the player while a dialogue is open so they can't walk away mid-conversation (or drift
        // the NPC into a locked region). Update still runs so the input device stays live.
        if (Dialogue.Instance != null && Dialogue.Instance.IsOpen)
        {
            return;
        }

        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector3 pos = transform.position;

        pos.x += input.x * speed * Time.deltaTime;
        pos.y += input.y * speed * Time.deltaTime;

        ClampToArea(pos, out pos);
        transform.position = pos;
    }

    private void OnDestroy()
    {
        if (moveAction != null)
        {
            moveAction.Dispose();
        }
    }

    /// <summary>Keeps the player fully inside whichever Area they currently occupy.</summary>
    private void ClampToArea(Vector3 pos, out Vector3 clamped)
    {
        clamped = pos;

        GameArea area = GameArea.GetAreaContaining(clamped);
        if (area == null)
        {
            return;
        }

        float playerHalf = transform.localScale.x * 0.5f + 0.001f;
        clamped = area.ClampPlayer(clamped, playerHalf);
    }
}
