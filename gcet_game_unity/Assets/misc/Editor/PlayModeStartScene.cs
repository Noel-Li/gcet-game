using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Makes the Unity Play button follow the same entry point as a built game.
/// Unity normally starts from the scene currently open in the Editor, which can
/// accidentally bypass the intro when a designer is editing the gameplay scene.
/// </summary>
[InitializeOnLoad]
internal static class PlayModeStartScene
{
    private const string IntroScenePath = "Assets/Scenes/StartScene.unity";

    static PlayModeStartScene()
    {
        SetIntroAsPlayModeStartScene();
    }

    [MenuItem("Tools/GCET/Set Intro as Play Mode Start Scene")]
    private static void SetIntroAsPlayModeStartScene()
    {
        SceneAsset introScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroScenePath);
        if (introScene == null)
        {
            Debug.LogError($"[PlayModeStartScene] Could not find the intro scene at {IntroScenePath}.");
            return;
        }

        if (EditorSceneManager.playModeStartScene != introScene)
        {
            EditorSceneManager.playModeStartScene = introScene;
        }
    }
}
