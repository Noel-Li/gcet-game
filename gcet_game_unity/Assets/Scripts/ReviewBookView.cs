using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds the twelve review entries over the white slots painted into the review artwork.</summary>
[DisallowMultipleComponent]
public sealed class ReviewBookView : MonoBehaviour
{
    private const string LatinFontAssetName = "LiberationSans SDF - Fallback";

    [Header("Character library")]
    [Tooltip("CharacterData assets used to resolve each unlocked Hanzi and its pinyin.")]
    [SerializeField] private List<CharacterData> characterLibrary = new List<CharacterData>();

    [Header("Responsive slot layout")]
    [Tooltip("Canvas-space parent that receives the runtime review buttons.")]
    [SerializeField] private RectTransform slotParent;

    [Range(1, 12)]
    [SerializeField] private int capacity = 12;

    [Tooltip("Normalized horizontal centers of the three painted columns.")]
    [SerializeField] private List<float> columnCenters = new List<float> { 0.164f, 0.5f, 0.835f };

    [Tooltip("Normalized vertical centers of the four painted rows, ordered top to bottom.")]
    [SerializeField] private List<float> rowCenters = new List<float> { 0.726f, 0.537f, 0.339f, 0.136f };

    [Tooltip("Normalized width and height of one painted white slot.")]
    [SerializeField] private Vector2 normalizedSlotSize = new Vector2(0.217f, 0.159f);

    [Header("Typography")]
    [SerializeField] private Color textColor = new Color(0.16f, 0.06f, 0.04f, 1f);

    [Min(1f)]
    [SerializeField] private float hanziFontSize = 72f;

    [Min(1f)]
    [SerializeField] private float pinyinFontSize = 32f;

    private void Start()
    {
        BuildSlots();
    }

    private void BuildSlots()
    {
        RectTransform parent = slotParent != null ? slotParent : transform as RectTransform;
        if (parent == null || columnCenters == null || rowCenters == null)
        {
            Debug.LogError("[ReviewBookView] Slot layout is not configured.");
            return;
        }

        IReadOnlyList<string> unlocked = GameProgress.Instance != null
            ? GameProgress.Instance.ReviewCharacters
            : null;
        int layoutCapacity = Mathf.Min(capacity, columnCenters.Count * rowCenters.Count);
        int slotIndex = 0;

        for (int row = 0; row < rowCenters.Count && slotIndex < layoutCapacity; row++)
        {
            for (int column = 0; column < columnCenters.Count && slotIndex < layoutCapacity; column++)
            {
                CharacterData data = null;
                if (unlocked != null && slotIndex < unlocked.Count)
                {
                    data = FindCharacter(unlocked[slotIndex]);
                }

                CreateSlot(parent, slotIndex, columnCenters[column], rowCenters[row], data);
                slotIndex++;
            }
        }
    }

    private CharacterData FindCharacter(string characterName)
    {
        if (characterLibrary == null)
        {
            return null;
        }

        for (int i = 0; i < characterLibrary.Count; i++)
        {
            CharacterData candidate = characterLibrary[i];
            if (candidate != null && candidate.characterName == characterName)
            {
                return candidate;
            }
        }

        Debug.LogWarning("[ReviewBookView] No CharacterData is assigned for unlocked Hanzi '" + characterName + "'.");
        return null;
    }

    private void CreateSlot(RectTransform parent, int index, float centerX, float centerY, CharacterData data)
    {
        GameObject slotObject = new GameObject("Review Slot " + (index + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.SetParent(parent, false);

        Vector2 halfSize = normalizedSlotSize * 0.5f;
        Vector2 center = new Vector2(centerX, centerY);
        slotRect.anchorMin = center - halfSize;
        slotRect.anchorMax = center + halfSize;
        slotRect.offsetMin = Vector2.zero;
        slotRect.offsetMax = Vector2.zero;

        Image hitArea = slotObject.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = data != null;

        Button button = slotObject.GetComponent<Button>();
        button.targetGraphic = hitArea;
        button.interactable = data != null;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = new Color(1f, 0.82f, 0.3f, 0.12f);
        colors.pressedColor = new Color(1f, 0.72f, 0.2f, 0.22f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = Color.clear;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        if (data == null)
        {
            slotObject.SetActive(false);
            return;
        }

        CharacterData selectedCharacter = data;
        button.onClick.AddListener(() => BeginReviewTrace(selectedCharacter));

        GameObject labelObject = new GameObject("Hanzi and Pinyin", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(slotRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont != null)
        {
            defaultFont.isMultiAtlasTexturesEnabled = true;
            label.font = defaultFont;
        }
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = true;
        label.raycastTarget = false;
        label.text =
            "<size=" + hanziFontSize + ">" + selectedCharacter.characterName + "</size>\n" +
            "<font=\"" + LatinFontAssetName + "\"><size=" + pinyinFontSize + ">" +
            selectedCharacter.pinyin + "</size></font>";
    }

    private static void BeginReviewTrace(CharacterData character)
    {
        if (character == null || GameProgress.Instance == null)
        {
            return;
        }

        GameProgress.Instance.BeginReviewTrace(character.characterName);
    }

    private void OnValidate()
    {
        capacity = Mathf.Clamp(capacity, 1, 12);
        normalizedSlotSize.x = Mathf.Clamp01(normalizedSlotSize.x);
        normalizedSlotSize.y = Mathf.Clamp01(normalizedSlotSize.y);
    }
}
