using UnityEngine;
using UnityEngine.InputSystem;

// This script uses Unity's new Input System to rotate the player left/right
// and tilt the camera up/down.
public class FirstPersonLook : MonoBehaviour
{
    [Header("Mouse Sensitivity Settings")]
    public float sensitivityX = 1.0f;
    public float sensitivityY = 1.0f;

    [Header("Camera Setup")]
    public Transform cameraRoot;

    private float yaw;
    private float pitch;

    [HideInInspector]
    public bool canLook = true;

    private InputAction lookAction;

    private void Start()
    {
        lookAction = InputSystem.actions.FindAction("Look");

        if (lookAction != null)
            lookAction.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Update()
    {
        if (!canLook || lookAction == null)
            return;

        Vector2 delta = lookAction.ReadValue<Vector2>();

        yaw = delta.x * sensitivityX;
        transform.Rotate(0f, yaw, 0f);

        pitch -= delta.y * sensitivityY;
        pitch = Mathf.Clamp(pitch, -60f, 60f);

        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public void DisableLook()
    {
        canLook = false;

        // Disabling the action is stronger than only checking a bool.
        if (lookAction != null)
            lookAction.Disable();
    }

    public void EnableLook()
    {
        canLook = true;

        if (lookAction != null)
            lookAction.Enable();
    }
}