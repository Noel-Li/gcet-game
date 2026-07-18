using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves a square player with WASD. The player walks freely but is always kept on-camera by two
/// layered clamps: the current <see cref="GameArea"/> clamps the player to its bounds (leaving a
/// gap only on edges where an <see cref="InvisibleWall"/> sits), and each wall then blocks or
/// permits passage based on its lock state. A locked wall seals the way from both sides; an
/// unlocked one lets the player walk straight through into the next region. The camera follows.
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
        float playerHalf = transform.localScale.x * 0.5f + 0.001f;
        InvisibleWall.ClampPlayer(pos, playerHalf, ref pos);
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
