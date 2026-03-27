using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    [Header("Loading Screen UI")]
    public GameObject loadingScreenRoot;
    public Slider progressBar;
    public TMP_Text progressLabel;

    [Header("Main Menu")]
    public string mainMenuSceneName = "MainMenu";

    private bool isLoading = false;

    private void Awake()
    {
        // Singleton: only one SceneController should exist across all scenes.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingScreenRoot != null)
            loadingScreenRoot.SetActive(false);
    }

    // -- API --

    public void LoadScene(string sceneName)
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    public void LoadMainMenu()
    {
        LoadScene(mainMenuSceneName);
    }

    public void ReloadCurrentScene()
    {
        if (isLoading) return;

        string currentScene = SceneManager.GetActiveScene().name;
        StartCoroutine(LoadSceneAsync(currentScene));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        isLoading = true;

        if (loadingScreenRoot != null)
            loadingScreenRoot.SetActive(true);

        if (progressBar != null)
            progressBar.value = 0f;

        if (progressLabel != null)
            progressLabel.text = "Loading...";

        yield return null;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false; // Hold scene activation until loading UI reaches 100%.

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            if (progressBar != null)
                progressBar.value = progress;

            if (progressLabel != null)
                progressLabel.text = "Loading... " + Mathf.RoundToInt(progress * 100f) + "%";

            if (operation.progress >= 0.9f)
            {
                if (progressBar != null)
                    progressBar.value = 1f;

                if (progressLabel != null)
                    progressLabel.text = "Loading... 100%";

                yield return new WaitForSecondsRealtime(0.2f);
                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        if (loadingScreenRoot != null)
            loadingScreenRoot.SetActive(false);

        isLoading = false;
    }
}