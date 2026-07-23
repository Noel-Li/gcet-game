using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Controls which scene the Unity Play button starts from (Play Mode Start Scene).
///
/// By default the editor would start from whichever scene is open, which can bypass the
/// intro while a designer is editing a gameplay scene. We pin a single entry point instead,
/// and expose menu items to switch it between the real intro and a throwaway test scene so
/// either can be launched directly with the Play button. Switching back to the intro
/// restores the normal built-game entry point — nothing here touches scene content.
/// </summary>
[InitializeOnLoad]
internal static class PlayModeStartScene
{
    private const string IntroScenePath = "Assets/Scenes/StartScene.unity";
    private const string TestScenePath = "Assets/Scenes/game1_test.unity";
    private const string EndScenePath = "Assets/Scenes/EndScene.unity";

    private static SceneAsset introScene;
    private static SceneAsset testScene;
    private static SceneAsset endScene;

    static PlayModeStartScene()
    {
        introScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroScenePath);
        testScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath);
        endScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EndScenePath);

        // Default to the intro so a fresh editor session still boots the real opening.
        SetIntroAsPlayModeStartScene();
    }

    [MenuItem("Tools/GCET/Play Mode Start Scene/Start Scene (Intro)")]
    private static void SetIntroAsPlayModeStartScene()
    {
        if (introScene == null)
        {
            introScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroScenePath);
        }
        if (introScene == null)
        {
            Debug.LogError($"[PlayModeStartScene] Could not find the intro scene at {IntroScenePath}.");
            return;
        }

        EditorSceneManager.playModeStartScene = introScene;
        Debug.Log("[PlayModeStartScene] Play Mode Start Scene set to: Intro (StartScene).");
    }

    [MenuItem("Tools/GCET/Play Mode Start Scene/game1_test (Standalone Test)")]
    private static void SetTestAsPlayModeStartScene()
    {
        if (testScene == null)
        {
            testScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath);
        }
        if (testScene == null)
        {
            Debug.LogError($"[PlayModeStartScene] Could not find the test scene at {TestScenePath}.");
            return;
        }

        EditorSceneManager.playModeStartScene = testScene;
        Debug.Log("[PlayModeStartScene] Play Mode Start Scene set to: game1_test (standalone test).");
    }

    [MenuItem("Tools/GCET/Play Mode Start Scene/EndScene (Standalone)")]
    private static void SetEndAsPlayModeStartScene()
    {
        if (endScene == null)
        {
            endScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EndScenePath);
        }
        if (endScene == null)
        {
            Debug.LogError($"[PlayModeStartScene] Could not find the end scene at {EndScenePath}.");
            return;
        }

        EditorSceneManager.playModeStartScene = endScene;
        Debug.Log("[PlayModeStartScene] Play Mode Start Scene set to: EndScene (standalone).");
    }

    [MenuItem("Tools/GCET/Play Mode Start Scene/Clear (Use Open Scene)")]
    private static void ClearPlayModeStartScene()
    {
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("[PlayModeStartScene] Play Mode Start Scene cleared — Play will use whichever scene is open.");
    }
}
