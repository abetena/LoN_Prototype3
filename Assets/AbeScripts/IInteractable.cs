// This interface defines the basic interaction contract for all interactable objects.
// Any object that implements IInteractable must define what happens when the player:
// - Looks at it
// - Interacts with it
// - Stops looking at it

public interface IInteractable
{
    // Called when the player starts looking at the object
    void OnLookAt();

    // Called when the player presses the interact button while looking at it
    void OnInteract();

    // Called when the player looks away or moves out of range
    void OnDisengage();
}