using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The full back-and-forth dialogue for one NPC, edited as a component on the NPC GameObject and read by <see cref="NpcController"/>.
/// Each <see cref="DialogueStep"/> carries its own <see cref="DialogueStep.speakerName"/> ("player", "npc", ...), so a single list
/// naturally holds both speakers' lines in the spoken order. Attach one Conversation per NPC; the lines are whatever you type in
/// the Inspector, so whole conversations are authored in the Unity Editor, not in code.
///
/// If the Inspector list is empty (common the first time you add the component before typing anything), <see cref="GetSteps"/>
/// rebuilds the placeholder conversation at runtime so the NPC still talks — but prefer filling the list in the Inspector.
/// </summary>
[RequireComponent(typeof(NpcController))]
public class Conversation : MonoBehaviour
{
    [Tooltip("The NPC and player's lines, in speaking order. Each line's speakerName says who speaks it.")]
    [TextArea(2, 4)]
    [SerializeField] private List<DialogueStep> steps = new List<DialogueStep>();

    /// <summary>The steps to play. Falls back to a placeholder conversation if the Inspector list is empty.</summary>
    public List<DialogueStep> GetSteps()
    {
        if (steps != null && steps.Count > 0)
        {
            return steps;
        }
        return DefaultSteps();
    }

    // Two of these steps drive the wider game: Writing hands off to the hanzi tracing scene, GoTop points the player upstairs.
    // Names here are whatever the NPC/Player are called; swap them in the Inspector per NPC.
    private static List<DialogueStep> DefaultSteps()
    {
        return new List<DialogueStep>
        {
            new DialogueStep { speakerName = "npc",    text = "Hey there. There's a door up ahead you can't pass yet.", action = DialogueAction.None },
            new DialogueStep { speakerName = "player", text = "Why not? What's blocking it?", action = DialogueAction.None },
            new DialogueStep { speakerName = "npc",    text = "An old trial. Trace this character correctly and the gate opens for you.", action = DialogueAction.Writing },
            // --- the lines below play again after the hanzi trace is succeeded, via ResumeAfterWriting ---
            new DialogueStep { speakerName = "player", text = "I did it! The trace was tough but I passed.", action = DialogueAction.None },
            new DialogueStep { speakerName = "npc",    text = "Nicely done! The gate to the top region is open now.", action = DialogueAction.None },
            new DialogueStep { speakerName = "player", text = "Sweet. So which way do I go now?", action = DialogueAction.None },
            new DialogueStep { speakerName = "npc",    text = "Head back the way you came and go up to the top region.", action = DialogueAction.GoTop },
            new DialogueStep { speakerName = "player", text = "Got it. I'll head to the top. Thanks!", action = DialogueAction.None },
        };
    }
}
