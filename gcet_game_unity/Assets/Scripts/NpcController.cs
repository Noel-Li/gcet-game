using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A clickable NPC. Clicking it opens the in-game placeholder dialogue (black square, top of screen) instead of
/// instantly unlocking its room. The conversation <em>content</em> lives on a sibling <see cref="Conversation"/> component
/// (editable in the Inspector, per-NPC); this class only knows how to open/advance it. Forward progress is gated the same way
/// it was — the Top-exit gate the player movement already respects flips only on a correct trace, handled by
/// <see cref="GameProgress"/>. Clicking again does nothing; after the gate has opened the NPC stays activated.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Conversation))]
public class NpcController : MonoBehaviour
{
    private Conversation conversation;
    private bool activated = false;
    private Collider2D npcCollider;
    private Camera activeCamera;

    private void Awake()
    {
        npcCollider = GetComponent<Collider2D>();
        activeCamera = Camera.main;
        if (activeCamera == null)
        {
            activeCamera = FindObjectOfType<Camera>();
        }
        conversation = GetComponent<Conversation>();
    }

    private void Start()
    {
        RefreshFromProgress();
    }

    private void RefreshFromProgress()
    {
        if (activated)
        {
            return;
        }

        if (Dialogue.Instance != null)
        {
            InjectInto(Dialogue.Instance);
        }

        if (GameProgress.Instance != null && GameProgress.Instance.tracePassed && Dialogue.Instance != null)
        {
            // Passed the trace mid-conversation: resume the post-writing lines automatically.
            activated = true;
            Dialogue.Instance.ResumeAfterWriting();
        }
    }

    private void InjectInto(Dialogue d)
    {
        if (conversation == null)
        {
            return;
        }
        d.SetSteps(conversation.GetSteps());
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

        OpenDialogue();
    }

    private void OpenDialogue()
    {
        activated = true;

        GameArea area = GameArea.GetAreaContaining(transform.position);
        int col = area != null ? area.AreaCol : 0;
        int row = area != null ? area.AreaRow : 0;

        if (Dialogue.Instance == null)
        {
            var obj = new GameObject("Dialogue");
            obj.AddComponent<Dialogue>();
        }

        // Conversation.GetSteps() always returns a populated list (falling back to defaults when the Inspector list is
        // empty), so the NPC always has something to say.
        InjectInto(Dialogue.Instance);
        Dialogue.Instance.Open(col, row);
    }
}
