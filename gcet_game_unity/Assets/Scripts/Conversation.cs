using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the dialogue for this NPC.
/// Attach this component to the same GameObject as NpcController.
/// </summary>
[RequireComponent(typeof(NpcController))]
public class Conversation : MonoBehaviour
{
    [Tooltip("Dialogue lines in speaking order.")]
    [SerializeField]
    private List<DialogueStep> steps =
        new List<DialogueStep>();

    /// <summary>
    /// Uses the Inspector dialogue when the list contains entries.
    /// Otherwise, it uses the default dialogue written below.
    /// </summary>
    public List<DialogueStep> GetSteps()
    {
        if (steps != null && steps.Count > 0)
        {
            return steps;
        }

        return DefaultSteps();
    }

    private static List<DialogueStep> DefaultSteps()
    {
        return new List<DialogueStep>
        {
            // ---------------------------------------------------------
            // Step 0
            // The soldier asks whether the player is 张三.
            // ---------------------------------------------------------
            new DialogueStep
            {
                speakerName = "soldier",
                text = "Hello, are you 张三?",
                action = DialogueAction.None
            },

            // ---------------------------------------------------------
            // Step 1
            // The player must trace 不 to answer the question.
            // After tracing, the conversation resumes at Step 2.
            // ---------------------------------------------------------
            new DialogueStep
            {
                speakerName = "player",
                text = "Use the tracer to write your answer: 不",
                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "开始书写“不” (Trace 不)",
                        targetStep = 2,
                        action = DialogueAction.Writing
                    }
                }
            },

            // ---------------------------------------------------------
            // Step 2
            // 不 appears normally in the dialogue after tracing.
            // ---------------------------------------------------------
            new DialogueStep
            {
                speakerName = "player",
                text = "不。(Bù.)",
                action = DialogueAction.None
            },

            // ---------------------------------------------------------
            // Step 3
            // The soldier asks why the player is here.
            // ---------------------------------------------------------
            new DialogueStep
            {
                speakerName = "soldier",
                text = "What is the purpose of you being here?",
                action = DialogueAction.None
            },

            // ---------------------------------------------------------
            // Step 4
            // Translation question.
            //
            // Correct answer goes to Step 9.
            // “How are you?” goes to Step 5.
            // “Where are you from?” goes to Step 7.
            // ---------------------------------------------------------
            new DialogueStep
            {
                speakerName = "soldier",
                text = "你叫什么名字？(Nǐ jiào shénme míngzì?)\n\nSelect the correct translation:",
                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "What is your name?",
                        targetStep = 9,
                        action = DialogueAction.None
                    },

                    new DialogueChoice
                    {
                        label = "How are you?",
                        targetStep = 5,
                        action = DialogueAction.None
                    },

                    new DialogueChoice
                    {
                        label = "Where are you from?",
                        targetStep = 7,
                        action = DialogueAction.None
                    }
                }
            },

            // ---------------------------------------------------------
            // Step 5
            // Wrong answer branch: “How are you?”
            // ---------------------------------------------------------
            new DialogueStep
            {
                speakerName = "player",
                text = "我很好。(Wǒ hěn hǎo.)",
                action = DialogueAction.None
            },

            // ---------------------------------------------------------
            // Step 6
            // Explain the mistake and return to Step 4.
            // ---------------------------------------------------------
            new DialogueStep
            {
                speakerName = "soldier",
                text = "“我很好” means “I am fine.” That answers “How are you?”, but I asked for your name. Try again.",
                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Go back to the question",
                        targetStep = 4,
                        action = DialogueAction.None
                    }
                }
            },

            // ---------------------------------------------------------
            // Step 7
            // Wrong answer branch: “Where are you from?”
            // ---------------------------------------------------------
            // Step 7: Player selected “Where are you from?”
new DialogueStep
{
    speakerName = "player",
    text = "美国。(Měiguó.)",
    action = DialogueAction.None
},

// Step 8: Soldier corrects the player
new DialogueStep
{
    speakerName = "soldier",
    text = "I didn’t ask where you are from. I asked “你叫什么名字？” My name is 李沅诺 (Lǐ yuán nuò). 你叫什么名字？(Nǐ jiào shénme míngzì?)",
    action = DialogueAction.None,

    choices = new List<DialogueChoice>
    {
        new DialogueChoice
        {
            label = "Answer the question again",
            targetStep = 4,
            action = DialogueAction.None
        }
    }
},

            // ---------------------------------------------------------
            // Step 9
            // Correct answer branch.
            // ---------------------------------------------------------
            new DialogueStep
            {
                speakerName = "player",
                text = "过小月。(Guò xiǎo yuè.)",
                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Continue",
                        targetStep = -1,
                        action = DialogueAction.None
                    }
                }
            }
        };
    }
}