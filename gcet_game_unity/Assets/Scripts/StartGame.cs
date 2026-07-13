using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads the game scene from the start screen. Attach to the Start button and wire its <c>onClick</c> event in the
/// Inspector to <see cref="LoadGame"/>. The scene file name is provided via <see cref="gameSceneName"/> so the target can
/// be changed in the Editor without touching code, and kept in sync with <see cref="GameProgress"/>'s main scene list.
/// </summary>
public class StartGame : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string gameSceneName = "game1";

    /// <summary>Called by the Start button's onClick event.</summary>
    public void LoadGame()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[StartGame] gameSceneName is empty — assign it in the Inspector.");
            return;
        }
        SceneManager.LoadScene(gameSceneName);
    }
}
