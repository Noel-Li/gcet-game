using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the dialogue for this NPC.
/// Attach this component to the same GameObject as NpcController.
/// </summary>
[RequireComponent(typeof(NpcController))]
public class Conversation : MonoBehaviour
{
    [Header("Dialogue")]

    [Tooltip("Dialogue lines shown the FIRST time the player talks to this NPC.")]
    [SerializeField]
    private List<DialogueStep> steps = new List<DialogueStep>();

    [Tooltip("Dialogue lines shown on the SECOND and later conversations. Leave empty to use a short default 'move along' exchange.")]
    [SerializeField]
    private List<DialogueStep> repeatSteps = new List<DialogueStep>();

    /// <summary>
    /// Returns which conversation this is for this NPC: the full first-time exchange when <paramref name="firstTime"/> is true,
    /// otherwise the (Inspector-authored or default) repeat exchange. Override the per-NPC dialogue by filling the lists above.
    /// </summary>
    public List<DialogueStep> GetSteps(bool firstTime = true)
    {
        if (firstTime)
        {
            if (steps != null && steps.Count > 0)
            {
                return steps;
            }
            return DefaultSteps();
        }

        if (repeatSteps != null && repeatSteps.Count > 0)
        {
            return new List<DialogueStep>(repeatSteps);
        }
        return DefaultRepeatSteps();
    }

    /// <summary>Short, reusable exchange shown on repeat visits when the NPC has nothing specific to add.</summary>
    private static List<DialogueStep> DefaultRepeatSteps()
    {
        return new List<DialogueStep>
        {
            new DialogueStep
            {
                speakerName = "soldier",
                text = "Remember our talk. 继续走！(Jìxù zǒu!)\nGo on — don't keep 王喜悦 waiting.",
                action = DialogueAction.None
            },

            new DialogueStep
            {
                speakerName = "player",
                text = "好的。(Hǎo de.)",
                action = DialogueAction.None
            }
        };
    }

    private static List<DialogueStep> DefaultSteps()
    {
        return new List<DialogueStep>
        {
            // =========================================================
            // INTRODUCTION
            // =========================================================

            // Step 0
            new DialogueStep
            {
                speakerName = "soldier",
                text = "Hello, are you 张三?",
                action = DialogueAction.None
            },

            // Step 1
            // Player traces 不 to answer "no."
            new DialogueStep
            {
                speakerName = "player",
                text = "Use the tracer to write your answer: 不",
                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Trace 不",
                        targetStep = 2,
                        action = DialogueAction.Writing
                    }
                }
            },

            // Step 2
            new DialogueStep
            {
                speakerName = "player",
                text = "不。(Bù.)",
                action = DialogueAction.None
            },

            // Step 3
            new DialogueStep
            {
                speakerName = "soldier",
                text = "What is the purpose of you being here?",
                action = DialogueAction.None
            },

            // =========================================================
            // FIRST TRANSLATION QUESTION
            // =========================================================

            // Step 4
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "你叫什么名字？(Nǐ jiào shénme míngzì?)\n\n" +
                    "Select the correct translation:",

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

            // Step 5
            // Wrong answer: How are you?
            new DialogueStep
            {
                speakerName = "player",
                text = "我很好。(Wǒ hěn hǎo.)",
                action = DialogueAction.None
            },

            // Step 6
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "I didn't ask how you are. I asked “你叫什么名字？” " +
                    "My name is 李沅诺 (Lǐ yuán nuò). " +
                    "你叫什么名字？(Nǐ jiào shénme míngzì?)",

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

            // Step 7
            // Wrong answer: Where are you from?
            new DialogueStep
            {
                speakerName = "player",
                text = "美国。(Měiguó.)",
                action = DialogueAction.None
            },

            // Step 8
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "I didn't ask where you are from. I asked “你叫什么名字？” " +
                    "My name is 李沅诺 (Lǐ yuán nuò). " +
                    "你叫什么名字？(Nǐ jiào shénme míngzì?)",

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

            // =========================================================
            // CORRECT ANSWER BRANCH
            // =========================================================

            // Step 9
            new DialogueStep
            {
                speakerName = "player",
                text = "过小月。(Guò xiǎo yuè.)",
                action = DialogueAction.None
            },

            // Step 10
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "Hello, 小月 (xiǎo yuè). My name is 李沅诺 " +
                    "(Lǐ yuán nuò). Wait... did you say 过 (Guò)?",

                action = DialogueAction.None
            },

            // Step 11
            new DialogueStep
            {
                speakerName = "player",
                text = "Yes...?",
                action = DialogueAction.None
            },

            // Step 12
            new DialogueStep
            {
                speakerName = "soldier",
                text = "Are you related to 过品玉 (Guò pǐn yù)?",
                action = DialogueAction.None
            },

            // Step 13
            new DialogueStep
            {
                speakerName = "player",
                text =
                    "*Hmm... that name sounds familiar. Wait—that's the " +
                    "first person in my family line to have powers!*",

                action = DialogueAction.None
            },

            // Step 14
            new DialogueStep
            {
                speakerName = "player",
                text = "Yes! I am. How did you know?",
                action = DialogueAction.None
            },

            // Step 15
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "She is our local magistrate. 她做的 (Tā zuò de) " +
                    "equipment 是最好的 (shì zuì hǎo de). " +
                    "We all don't know how, but we don't ask questions.",

                action = DialogueAction.None
            },

            // =========================================================
            // SECOND TRANSLATION QUESTION
            // =========================================================

            // Step 16
            new DialogueStep
            {
                speakerName = "soldier",
                text = "What does “她做最好的...” mean?",

                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "He makes the best...",
                        targetStep = 17,
                        action = DialogueAction.None
                    },

                    new DialogueChoice
                    {
                        label = "She makes the best...",
                        targetStep = 19,
                        action = DialogueAction.None
                    },

                    new DialogueChoice
                    {
                        label = "She drinks the best...",
                        targetStep = 18,
                        action = DialogueAction.None
                    }
                }
            },

            // Step 17
            // Wrong answer: He
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "Not quite. 她 (tā) means “she,” not “he.” " +
                    "Try the question again.",

                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Try again",
                        targetStep = 16,
                        action = DialogueAction.None
                    }
                }
            },

            // Step 18
            // Wrong answer: Drinks
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "Not quite. 做 (zuò) means “to make” or “to do.” " +
                    "It does not mean “to drink.”",

                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Try again",
                        targetStep = 16,
                        action = DialogueAction.None
                    }
                }
            },

            // Step 19
            new DialogueStep
            {
                speakerName = "player",
                text = "Do you know where I can find her?",
                action = DialogueAction.None
            },

            // Step 20
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "她从不 (Tā cóng bù) stay 同一地方 (tóngyī dìfāng), " +
                    "but I'm sure 王喜悦 (Wáng Xǐyuè) knows where she is. " +
                    "She sells her silk in town. You can 问她 (Wèn tā). " +
                    "She is just up ahead. 不要 (Bùyào) cause any trouble.",

                action = DialogueAction.None
            },

            // =========================================================
            // THIRD TRANSLATION QUESTION
            // =========================================================

            // Step 21
            new DialogueStep
            {
                speakerName = "soldier",
                text = "What does “问...” translate to?",

                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Ask",
                        targetStep = 24,
                        action = DialogueAction.None
                    },

                    new DialogueChoice
                    {
                        label = "Chase",
                        targetStep = 22,
                        action = DialogueAction.None
                    },

                    new DialogueChoice
                    {
                        label = "Hide",
                        targetStep = 23,
                        action = DialogueAction.None
                    }
                }
            },

            // Step 22
            // Wrong answer: Chase
            new DialogueStep
            {
                speakerName = "soldier",
                text = "No. 问 (wèn) means “to ask,” not “to chase.”",

                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Try again",
                        targetStep = 21,
                        action = DialogueAction.None
                    }
                }
            },

            // Step 23
            // Wrong answer: Hide
            new DialogueStep
            {
                speakerName = "soldier",
                text = "No. 问 (wèn) means “to ask,” not “to hide.”",

                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Try again",
                        targetStep = 21,
                        action = DialogueAction.None
                    }
                }
            },

            // Step 24
            new DialogueStep
            {
                speakerName = "player",
                text = "谢谢！(Xièxiè!)",
                action = DialogueAction.None
            },

            // =========================================================
            // FINAL TRACING TASK
            // =========================================================

            // Step 25
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "Trace characters to remember important information " +
                    "from the conversation. Trace 不 and 要. " +
                    "Make sure to remember how to write them.",

                action = DialogueAction.None,

                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Start tracing 不 and 要",
                        targetStep = 26,
                        action = DialogueAction.Writing
                    }
                }
            },

            // Step 26
            // Dialogue resumes here after tracing is completed.
            new DialogueStep
            {
                speakerName = "soldier",
                text =
                    "很好！(Hěn hǎo!) You remembered 不 and 要. " +
                    "王喜悦 is just up ahead. Continue to the next area!",

                action = DialogueAction.GoTop
            },

            // Step 27
            new DialogueStep
            {
                speakerName = "player",
                text = "I will find her. 谢谢！(Xièxiè!)",
                action = DialogueAction.None
            }
        };
    }
}