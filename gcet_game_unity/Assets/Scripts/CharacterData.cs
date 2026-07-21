using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Tracing/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Display")]
    [Tooltip("The Hanzi shown in the tracing header and used to match dialogue trace requests.")]
    public string characterName;

    [Tooltip("Tone-marked pinyin shown beside the Hanzi in the tracing header, for example wǒ.")]
    public string pinyin;

    [Tooltip("Concise English meaning shown after a single-character tracing task.")]
    public string meaning;

    [Header("Review example")]
    [Tooltip("Chinese example sentence shown after this character is retraced from the review book.")]
    [TextArea(1, 3)]
    public string reviewExampleChinese;

    [Tooltip("English translation paired with the Chinese review example.")]
    [TextArea(1, 3)]
    public string reviewExampleEnglish;

    [Header("Stroke data")]
    public List<StrokeData> strokes = new List<StrokeData>();
}

[System.Serializable]
public class StrokeData
{
    public string strokeName;
    public List<Vector3> points = new List<Vector3>();
}
