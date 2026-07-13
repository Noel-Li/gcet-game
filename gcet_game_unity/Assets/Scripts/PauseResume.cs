using UnityEngine;

/// <summary>
/// Resumes the game from the pause screen. Attach to the pause scene's root manager, and wire the pause scene's Resume button's onClick to this component's <see cref="Resume"/> method. We locate the <see cref="PauseController"/> at runtime (via the additive-loaded main scene) and call its Resume() so timeScale is restored and the pause scene is unloaded.</summary>
public class PauseResume : MonoBehaviour
{
    public void Resume()
    {
        var controllers = FindObjectsOfType<PauseController>();
        if (controllers.Length > 0)
        {
            controllers[0].Resume();
            return;
        }
        // Fallback: if no controller could be found, resume anyway.
        Time.timeScale = 1f;
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("PauseScene");
        if (scene.isLoaded)
        {
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("PauseScene");
        }
    }
}
