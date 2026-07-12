using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Tracing/Character Data")]
public class CharacterData : ScriptableObject
{
    public string characterName;
    public List<StrokeData> strokes = new List<StrokeData>();
}

[System.Serializable]
public class StrokeData
{
    public string strokeName;
    public List<Vector3> points = new List<Vector3>();
}