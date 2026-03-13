using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [Header("First Scene To Load")]
    public string firstGameplayScene = "Level01";

    public void StartGame()
    {
        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadScene(firstGameplayScene);
        }
        else
        {
            Debug.LogWarning("No SceneController found in the scene.");
        }
    }
}