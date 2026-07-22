using UnityEngine;

/// <summary>
/// Quits the game from an end screen. Wired to the Exit button's onClick in EndScene: in a built player this
/// closes the application; in the Editor it stops play mode. Keep it component-free (no MonoBehaviour requirements)
/// so it can sit on any button's GameObject.
/// </summary>
public class ExitGame : MonoBehaviour
{
    /// <summary>Called by the Exit button's onClick event.</summary>
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
