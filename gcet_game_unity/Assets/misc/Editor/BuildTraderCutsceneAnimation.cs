using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Rebuilds the reusable Scene 3 Part 1 animation asset from the GIF-derived frame folder.</summary>
public static class BuildTraderCutsceneAnimation
{
    private const string FramesFolder = "Assets/cutscenes/sceneThreePT1_frames";
    private const string TimingPath = FramesFolder + "/frame_durations.txt";
    private const string AssetPath = "Assets/cutscenes/sceneThreePT1 Animation.asset";

    [MenuItem("Tools/GCET/Rebuild Trader Cutscene Animation")]
    public static void Rebuild()
    {
        string absoluteFolder = Path.GetFullPath(FramesFolder);
        string[] absoluteFramePaths = Directory.GetFiles(absoluteFolder, "frame_*.png");
        Array.Sort(absoluteFramePaths, StringComparer.Ordinal);

        string[] durationLines = File.ReadAllLines(Path.GetFullPath(TimingPath));
        if (absoluteFramePaths.Length == 0 || durationLines.Length != absoluteFramePaths.Length)
        {
            throw new InvalidOperationException(
                $"Trader animation requires one duration per frame; found {absoluteFramePaths.Length} frames and {durationLines.Length} durations.");
        }

        var sprites = new List<Sprite>(absoluteFramePaths.Length);
        var durations = new List<float>(absoluteFramePaths.Length);
        for (int i = 0; i < absoluteFramePaths.Length; i++)
        {
            string assetPath = FramesFolder + "/" + Path.GetFileName(absoluteFramePaths[i]);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("No TextureImporter found for " + assetPath);
            }

            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Could not load animation frame sprite " + assetPath);
            }

            if (!float.TryParse(durationLines[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float duration))
            {
                throw new InvalidOperationException("Invalid frame duration at line " + (i + 1));
            }

            sprites.Add(sprite);
            durations.Add(Mathf.Max(0.01f, duration));
        }

        CutsceneAnimation animation = AssetDatabase.LoadAssetAtPath<CutsceneAnimation>(AssetPath);
        if (animation == null)
        {
            animation = ScriptableObject.CreateInstance<CutsceneAnimation>();
            AssetDatabase.CreateAsset(animation, AssetPath);
        }

        var serialized = new SerializedObject(animation);
        WriteObjectArray(serialized.FindProperty("frames"), sprites);
        WriteFloatArray(serialized.FindProperty("frameDurations"), durations);
        serialized.FindProperty("defaultFrameDuration").floatValue = 0.1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(animation);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GCET] Rebuilt Trader cutscene animation with {sprites.Count} timed frames at {AssetPath}.");
    }

    private static void WriteObjectArray(SerializedProperty property, IList<Sprite> values)
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void WriteFloatArray(SerializedProperty property, IList<float> values)
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).floatValue = values[i];
        }
    }
}
