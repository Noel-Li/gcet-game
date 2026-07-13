using UnityEngine;
using UnityEngine.SceneManagement;

public class DialogueSceneChanger : MonoBehaviour
{
    public void GoToTracingScene()
    {
        SceneManager.LoadScene("hanzi tracing base");
    }
}
