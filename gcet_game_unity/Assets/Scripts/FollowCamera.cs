using UnityEngine;

/// <summary>
/// Clamped-follow camera. Each frame it smoothly follows the player (via SmoothDamp) and
/// then clamps the result to the current Area's bounds, so the view never shows outside
/// the Area the player is standing in.
///
/// Near an Area edge the camera hits its clamp and stops while the player keeps walking to
/// the boundary — exactly the "player moves, camera does not" feel we want.
///
/// The current Area is resolved from the player's position each frame through
/// GameArea.GetAreaContaining, so the camera needs no hard-coded coordinates.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Motion")]
    [SerializeField] private float smoothTime = 0.15f;

    private float velocityX;
    private float velocityY;
    private float defaultZ;
    private Camera cam;
    private GameArea lastArea;

    private void Reset()
    {
        var found = FindObjectOfType<PlayerMovement>();
        if (found != null)
        {
            player = found.transform;
        }
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        defaultZ = transform.position.z;
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        // Resolve the room the player stands in and smoothly ease the camera toward that room's
        // center. Because each room is exactly one camera view, this snaps the camera per-room
        // and the view always shows exactly one full Area — never the void between rooms.
        GameArea area = GameArea.GetAreaContaining(player.position);
        if (area != null)
        {
            lastArea = area;
        }
        GameArea target = area ?? lastArea;
        if (target == null)
        {
            return;
        }

        float x = Mathf.SmoothDamp(transform.position.x, target.Center.x, ref velocityX, smoothTime);
        float y = Mathf.SmoothDamp(transform.position.y, target.Center.y, ref velocityY, smoothTime);
        transform.position = new Vector3(x, y, defaultZ);
    }
}
