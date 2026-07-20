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
        // A short, reusable default exchange for gate NPCs: one line that tells the player the way
        // is open. Author explicit steps on the Conversation component to replace it with richer content.
        return new List<DialogueStep>
        {
            new DialogueStep
            {
                speakerName = "soldier",
                expression = "normal",
                text = "The way ahead is open now. Go on.",
                action = DialogueAction.None,
                nextStep = -1
            }
        };
    }
}
