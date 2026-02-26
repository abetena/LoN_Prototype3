using UnityEngine;
using UnityEngine.UI; // Needed if you are using standard UI Text

public class ReadableObject : MonoBehaviour, IInteractable
{
    public string objectName = "A heavy stone door";

    [Header("References")]
    public GameObject wall;          // The actual door/wall to deactivate
    public GameObject uiPrompt;      // The UI element that says "Press E"
    public AudioSource interactSound; // The AudioSource component to play

    [Header("State")]
    private bool isOpened = false;   // Tracks if the door has already been used

    // Triggered when the player looks at the object
    public void OnLookAt()
    {
        // If the door is already opened, do nothing and exit early
        if (isOpened) return;

        Debug.Log("Looking at: " + objectName);

        // Show the "Press E" legend on the screen
        if (uiPrompt != null)
        {
            uiPrompt.SetActive(true);
        }
    }

    // Triggered when the player presses the interact button
    public void OnInteract()
    {
        // If already opened, we don't want to play sound or run logic again
        if (isOpened) return;

        Debug.Log("Interacting with: " + objectName);

        // 1. Play the sound effect
        if (interactSound != null)
        {
            interactSound.Play();
        }

        // 2. Deactivate the wall
        wall.SetActive(false);

        // 3. Hide the UI prompt immediately
        if (uiPrompt != null)
        {
            uiPrompt.SetActive(false);
        }

        // 4. Set the state to true so this script "shuts down"
        isOpened = true;
    }

    // Triggered when the player stops looking at the object
    public void OnDisengage()
    {
        // Hide the prompt when looking away (only if the door isn't already gone)
        if (uiPrompt != null)
        {
            uiPrompt.SetActive(false);
        }

        Debug.Log("Disengage: " + objectName);
    }
}
