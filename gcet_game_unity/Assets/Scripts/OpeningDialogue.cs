using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opens a short, Inspector-authored player monologue the first time gameplay begins.
/// The shared <see cref="Dialogue"/> renderer keeps its usual frame layout and portrait fitting.
/// </summary>
[DisallowMultipleComponent]
public sealed class OpeningDialogue : MonoBehaviour
{
    [Header("Opening Line")]
    [Tooltip("Speaker key shown by the dialogue system and used to select the frame and portrait below.")]
    [SerializeField] private string speakerName = "player";

    [Tooltip("Expression key associated with the opening portrait.")]
    [SerializeField] private string expression = "confused";

    [TextArea(2, 4)]
    [SerializeField] private string openingLine = "Where am I?";

    [Header("Presentation")]
    [Tooltip("Character dialogue frame used behind the opening line.")]
    [SerializeField] private Sprite dialogueBackground;

    [Tooltip("Portrait placed and aspect-fitted inside the dialogue frame's portrait window.")]
    [SerializeField] private Sprite portrait;

    [Header("Sequence")]
    [Tooltip("Optional title-card overlay shown and completed before the opening line appears.")]
    [SerializeField] private OpeningOverlay openingOverlay;

    private IEnumerator Start()
    {
        if (GameProgress.Instance != null && !GameProgress.Instance.TryBeginOpeningDialogue())
        {
            yield break;
        }

        if (openingOverlay != null)
        {
            openingOverlay.ShowImmediately();
            yield return openingOverlay.Play();
        }

        EnsureDialogueExists();

        if (Dialogue.Instance == null)
        {
            Debug.LogError("[OpeningDialogue] Could not create the shared Dialogue renderer.", this);
            yield break;
        }

        Dialogue dialogue = Dialogue.Instance;
        dialogue.SetSteps(new List<DialogueStep>
        {
            new DialogueStep
            {
                speakerName = speakerName,
                expression = expression,
                text = openingLine
            }
        });
        dialogue.SetSpeakerBackgrounds(new List<SpeakerDialogueBackground>
        {
            new SpeakerDialogueBackground
            {
                speakerName = speakerName,
                background = dialogueBackground
            }
        });
        dialogue.SetSpeakerPortraits(new List<SpeakerDialoguePortrait>
        {
            new SpeakerDialoguePortrait
            {
                speakerName = speakerName,
                expression = expression,
                portrait = portrait
            }
        });
        dialogue.SetAuxiliaryPanelBackgrounds(null, null);

        GameArea area = GameArea.GetAreaContaining(transform.position);
        int col = area != null ? area.AreaCol : 0;
        int row = area != null ? area.AreaRow : 0;
        dialogue.Open(col, row);
    }

    private static void EnsureDialogueExists()
    {
        if (Dialogue.Instance != null)
        {
            return;
        }

        GameObject dialogueObject = new GameObject("Dialogue");
        dialogueObject.AddComponent<Dialogue>();
    }
}
