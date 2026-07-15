using System.Collections.Generic;
using UnityEngine;

/// <summary>Stores reusable, Inspector-editable dialogue and speaker presentation for this NPC.</summary>
[RequireComponent(typeof(NpcController))]
public class Conversation : MonoBehaviour
{
    [Header("Dialogue")]
    [Tooltip("Dialogue shown the first time the player talks to this NPC. Leave empty to use the authored default below.")]
    [SerializeField] private List<DialogueStep> steps = new List<DialogueStep>();

    [Tooltip("Optional dialogue shown on later conversations. Leave empty to reuse the main dialogue.")]
    [SerializeField] private List<DialogueStep> repeatSteps = new List<DialogueStep>();

    [Header("Speaker backgrounds")]
    [Tooltip("Dialogue frame selected by speakerName.")]
    [SerializeField] private List<SpeakerDialogueBackground> speakerBackgrounds = new List<SpeakerDialogueBackground>();

    [Header("Speaker portraits")]
    [Tooltip("Portrait selected by speakerName and each dialogue step's expression key.")]
    [SerializeField] private List<SpeakerDialoguePortrait> speakerPortraits = new List<SpeakerDialoguePortrait>();

    [Header("Auxiliary panels")]
    [Tooltip("Yellow frame used for narrator instructions and translation prompts.")]
    [SerializeField] private Sprite gameVoiceBackground;

    [Tooltip("Blue frame shown at the bottom-right when a step has choices.")]
    [SerializeField] private Sprite multipleChoiceBackground;

    public List<DialogueStep> GetSteps(bool firstTime = true)
    {
        if (!firstTime && repeatSteps != null && repeatSteps.Count > 0)
        {
            return new List<DialogueStep>(repeatSteps);
        }

        return steps != null && steps.Count > 0
            ? new List<DialogueStep>(steps)
            : DefaultSteps();
    }

    public List<SpeakerDialogueBackground> GetSpeakerBackgrounds()
    {
        return speakerBackgrounds != null
            ? new List<SpeakerDialogueBackground>(speakerBackgrounds)
            : new List<SpeakerDialogueBackground>();
    }

    public List<SpeakerDialoguePortrait> GetSpeakerPortraits()
    {
        return speakerPortraits != null
            ? new List<SpeakerDialoguePortrait>(speakerPortraits)
            : new List<SpeakerDialoguePortrait>();
    }

    public Sprite GetGameVoiceBackground() => gameVoiceBackground;
    public Sprite GetMultipleChoiceBackground() => multipleChoiceBackground;

    private static List<DialogueStep> DefaultSteps()
    {
        return new List<DialogueStep>
        {
            // 0 — Character dialogue, normal soldier portrait.
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "normal",
                text = "Hello, are you 张三?"
            },

            // 1 — Game Voice instruction. The opening task traces only 不.
            new DialogueStep
            {
                speakerName = "game voice",
                panelStyle = DialoguePanelStyle.GameVoice,
                text = "Use the tracer to write the player's answer: 不",
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Trace 不",
                        targetStep = 2,
                        action = DialogueAction.Writing,
                        requiredTraceCount = 1
                    }
                }
            },

            // 2
            new DialogueStep
            {
                speakerName = "player",
                expression = "normal",
                text = "不。(Bù.)"
            },

            // 3 — Soldier asks the question in the character panel.
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "normal",
                text = "你叫什么名字？(Nǐ jiào shénme míngzì?)"
            },

            // 4 — Game Voice prompt on the left; answers appear in the Multiple Choice panel on the right.
            new DialogueStep
            {
                speakerName = "game voice",
                panelStyle = DialoguePanelStyle.GameVoice,
                text = "Select the translation for this:",
                useMultipleChoicePanel = true,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { label = "What is your name?", targetStep = 9 },
                    new DialogueChoice { label = "How are you?", targetStep = 5 },
                    new DialogueChoice { label = "Where are you from?", targetStep = 7 }
                }
            },

            // 5-6 — Wrong: How are you? Clicking the correction returns directly to the choices.
            new DialogueStep
            {
                speakerName = "player",
                expression = "normal",
                text = "我很好。(Wǒ hěn hǎo.)"
            },
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "normal",
                text =
                    "I didn't ask how you are. I asked “你叫什么名字？” My name is 李沅诺 " +
                    "(Lǐ yuán nuò). 你叫什么名字？(Nǐ jiào shénme míngzì?)",
                nextStep = 4
            },

            // 7-8 — Wrong: Where are you from? Clicking the correction returns directly to the choices.
            new DialogueStep
            {
                speakerName = "player",
                expression = "normal",
                text = "美国。(Měiguó.)"
            },
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "normal",
                text =
                    "I didn't ask where you are from. I asked “你叫什么名字？” My name is 李沅诺 " +
                    "(Lǐ yuán nuò). 你叫什么名字？(Nǐ jiào shénme míngzì?)",
                nextStep = 4
            },

            // 9 — Correct response.
            new DialogueStep
            {
                speakerName = "player",
                expression = "normal",
                text = "过小月。(Guò xiǎo yuè.)"
            },

            // 10-11 — Split so the soldier changes from normal to confused.
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "normal",
                text = "Hello, 小月 (xiǎo yuè). My name is 李沅诺 (Lǐ yuán nuò)."
            },
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "confused",
                text = "Wait... did you say 过 (Guò)?"
            },

            // 12-15 — Family connection.
            new DialogueStep
            {
                speakerName = "player",
                expression = "confused",
                text = "Yes...?"
            },
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "confused",
                text = "Are you related to 过品玉 (Guò pǐn yù)?"
            },
            new DialogueStep
            {
                speakerName = "player",
                expression = "worried",
                text = "*Hmm... that name sounds familiar. Wait—that's the first person in my family line to have powers!*"
            },
            new DialogueStep
            {
                speakerName = "player",
                expression = "excited",
                text = "Yes! I am. How did you know?"
            },

            // 16
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "surprised",
                text =
                    "She is our local Artisan. 她做的 (Tā zuò de) equipment 是最好的 (shì zuì hǎo de). " +
                    "We all don't know how, but we don't ask questions."
            },

            // 17 — Game Voice prompt + bottom-right Multiple Choice panel.
            new DialogueStep
            {
                speakerName = "game voice",
                panelStyle = DialoguePanelStyle.GameVoice,
                text = "What does “她做最好的...” mean?",
                useMultipleChoicePanel = true,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { label = "He makes the best...", targetStep = 18 },
                    new DialogueChoice { label = "She makes the best...", targetStep = 19 },
                    new DialogueChoice { label = "She drinks the best...", targetStep = 18 }
                }
            },

            // 18 — Game Voice only; the next click retries the question without an extra choice panel.
            new DialogueStep
            {
                speakerName = "game voice",
                panelStyle = DialoguePanelStyle.GameVoice,
                text = "Wrong answer. Try again.",
                nextStep = 17
            },

            // 19-20
            new DialogueStep
            {
                speakerName = "player",
                expression = "confused",
                text = "Do you know where I can find her?"
            },
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "stern",
                text =
                    "她从不 (Tā cóng bù) stay 同一地方 (tóngyī dìfāng), but I'm sure 王喜悦 " +
                    "(Wáng Xǐyuè) knows where she is. She sells her silk in town. You can 问她 (Wèn tā). " +
                    "She is just up ahead. 不要 (Bùyào) cause any trouble."
            },

            // 21 — Game Voice prompt + bottom-right Multiple Choice panel.
            new DialogueStep
            {
                speakerName = "game voice",
                panelStyle = DialoguePanelStyle.GameVoice,
                text = "What does “问...” translate to?",
                useMultipleChoicePanel = true,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { label = "Ask", targetStep = 23 },
                    new DialogueChoice { label = "Chase", targetStep = 22 },
                    new DialogueChoice { label = "Hide", targetStep = 22 }
                }
            },

            // 22 — Game Voice only; click to retry.
            new DialogueStep
            {
                speakerName = "game voice",
                panelStyle = DialoguePanelStyle.GameVoice,
                text = "Wrong answer. Try again.",
                nextStep = 21
            },

            // 23
            new DialogueStep
            {
                speakerName = "player",
                expression = "excited",
                text = "谢谢！(Xièxiè!)"
            },

            // 24 — Game Voice instruction. The later task traces 不 again, followed by 要.
            new DialogueStep
            {
                speakerName = "game voice",
                panelStyle = DialoguePanelStyle.GameVoice,
                text =
                    "Trace characters to remember important information from the conversation. " +
                    "Trace 不 and 要. Make sure to remember how to write them.",
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        label = "Start tracing 不 and 要",
                        targetStep = 25,
                        action = DialogueAction.Writing,
                        requiredTraceCount = 2
                    }
                }
            },

            // 25-26 — Resume after both characters are complete.
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "normal",
                text = "很好！(Hěn hǎo!) You remembered 不 and 要. 王喜悦 is just up ahead.",
                action = DialogueAction.GoTop
            },
            new DialogueStep
            {
                speakerName = "player",
                expression = "normal",
                text = "I will find her. 谢谢！(Xièxiè!)"
            }
        };
    }
}
