using UnityEngine;

public class SceneLoadZone : MonoBehaviour
{
    [Header("Scene To Load")]
    public string sceneToLoad;

    [Header("Detection Options")]
    public bool loadOnTriggerEnter = true;
    public bool loadOnCollisionEnter = false;

    [Header("Filter")]
    public string requiredTag = "Player";

    private bool hasLoaded = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!loadOnTriggerEnter || hasLoaded) return;

        if (other.CompareTag(requiredTag))
        {
            LoadSceneNow();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!loadOnCollisionEnter || hasLoaded) return;

        if (collision.gameObject.CompareTag(requiredTag))
        {
            LoadSceneNow();
        }
    }

    private void LoadSceneNow()
    {
        hasLoaded = true; // Prevent repeated scene-load calls.

        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogWarning("No SceneController found in the scene.");
        }
    }
}