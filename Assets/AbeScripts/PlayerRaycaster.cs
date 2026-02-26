using UnityEngine;
using UnityEngine.InputSystem;

// This script detects objects the player looks at, determines if they’re interactable,
// and triggers relevant interactions.
// It polls an Interact input every frame
public class PlayerRaycaster : MonoBehaviour
{
    [Header("Raycast")]
    public Transform rayOrigin;              // Usually CameraRoot
    public float maxDistance = 3f;           // How far the ray reaches
    public LayerMask interactionMask;        // What layers can be interacted with

    [Header("Input")]
    public InputActionReference interactAction; // Drag your "Interact" action here

    [HideInInspector]
    public bool canInteract = true;          // Use this outside to stop interactions

    private IInteractable currentTarget;     // Currently looked-at interactable

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.Enable();
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.Disable();
    }

    void Update()
    {
        if (!canInteract) return;

        // 1) Raycast / target tracking
        InteractionCheck();

        // 2) Input polling (simple boolean logic)
        bool interactPressed = (interactAction != null) && interactAction.action.WasPressedThisFrame();

        if (interactPressed && currentTarget != null)
        {
            currentTarget.OnInteract();
        }
    }

    // Checks what we're looking at and calls OnLookAt / OnDisengage only when the target changes.
    private void InteractionCheck()
    {
        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        RaycastHit hit;

        IInteractable previousTarget = currentTarget;

        if (Physics.Raycast(ray, out hit, maxDistance, interactionMask))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                if (interactable != currentTarget)
                {
                    if (previousTarget != null)
                        previousTarget.OnDisengage();

                    currentTarget = interactable;
                    currentTarget.OnLookAt();
                }
                return;
            }
        }

        // If we got here, we are not looking at a valid interactable
        if (currentTarget != null)
        {
            currentTarget.OnDisengage();
            currentTarget = null;
        }
    }

    // Draws a debug ray in the Scene view to visualize the interaction line.
    void OnDrawGizmos()
    {
        if (!rayOrigin) return;

        Gizmos.color = Color.green;

        Vector3 start = rayOrigin.position;
        Vector3 end = start + rayOrigin.forward * maxDistance;
        Gizmos.DrawLine(start, end);
    }
}