using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTestSwitcher : MonoBehaviour
{
    public void SwitchToNextScene()
    {
        int count = SceneManager.sceneCountInBuildSettings;
        if (count <= 0) return;
        int nextIndex = (SceneManager.GetActiveScene().buildIndex + 1) % count;
        SceneManager.LoadScene(nextIndex);
    }

    public void LoadSceneByName(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
