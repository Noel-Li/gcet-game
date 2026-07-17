using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads the next scene in the opening flow from the start screen. Attach to the Begin
/// button and configure the destination in the Inspector (normally ComicScene).
/// </summary>
public class StartGame : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("First scene loaded after Begin. The opening flow uses ComicScene.")]
    [SerializeField] private string gameSceneName = "ComicScene";

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
