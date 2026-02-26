using UnityEngine;
using UnityEngine.InputSystem;

// Script for crouching with smooth transition of camera and capsule height. Also prevents standing if there is a ground object on top
public class FirstPersonCrouch : MonoBehaviour
{
    private CharacterController controller;

    [HideInInspector]
    public bool isCrouching = false;// Shared flag that other scripts (e.g., movement) can access

    InputAction crouchAction;//Check for Crouch action in Input System

    // Ceiling detection
    private bool canStand;// Determines if there's space above to stand
    private float ceilingDistance = 0.4f;// Radius of the ceiling check sphere
    public Transform ceilingCheck;// Empty GameObject at the top of the head
    public LayerMask ceilingMask;// Layer(s) considered solid for standing clearance

    // Height and camera references
    public float crouchingHeight = 1.0f;
    public Transform cameraRoot;
    public float transitionDuration = 0.2f;

    // Cached values for original standing configuration
    private float standingHeight;
    private Vector3 standingCenter;
    private float bottomOffset;// Helps maintain feet grounded when resizing the capsule

    private Vector3 standingCamera;
    private Vector3 crouchingCamera;

    // Transition variables
    private float startHeight;
    private float targetHeight;

    private Vector3 startCenter;
    private Vector3 targetCenter;

    private Vector3 startCameraPos;
    private Vector3 targetCameraPos;

    private float elapsedTime = 0f;

    // Called once at the beginning to declare components and cache values
    void Start()
    {
        controller = GetComponent<CharacterController>();

        crouchAction = InputSystem.actions.FindAction("Crouch");

        //Store original dimensions of player capsule
        standingHeight = controller.height;
        standingCenter = controller.center;
        bottomOffset = standingCenter.y - (standingHeight * 0.5f);//Calculate middle from feet 

        standingCamera = cameraRoot.localPosition; // Store original camera position

        // Calculate lowered camera position when crouched
        crouchingCamera = new Vector3(cameraRoot.localPosition.x,
            cameraRoot.localPosition.y - (standingHeight - crouchingHeight),
            cameraRoot.localPosition.z
            );

        // Initialize transition data
        startHeight = standingHeight;
        targetHeight = standingHeight;

        startCenter = standingCenter;
        targetCenter = standingCenter;

        startCameraPos = standingCamera;
        targetCameraPos = standingCamera;

    }

    void Update()
    {
        // --- Input (cause) ---
        bool crouchPressed = crouchAction.WasPressedThisFrame();

        // --- Ceiling check (can we stand?) ---
        bool ceilingBlocked = Physics.CheckSphere(ceilingCheck.position, ceilingDistance, ceilingMask);
        bool canStand = !ceilingBlocked;

        // --- Toggle crouch when pressed (effect) ---
        if (crouchPressed)
        {
            // If we are standing, we can always crouch.
            // If we are crouching, only stand if there is space above.
            if (!isCrouching || canStand)
            {
                isCrouching = !isCrouching;

                // Restart transition and capture current state as the start
                elapsedTime = 0f;
                startHeight = controller.height;
                startCenter = controller.center;
                startCameraPos = cameraRoot.localPosition;

                // Decide our targets based on new crouch state
                targetHeight = isCrouching ? crouchingHeight : standingHeight;

                targetCenter = new Vector3(
                    standingCenter.x,
                    bottomOffset + (targetHeight * 0.5f),
                    standingCenter.z
                );

                targetCameraPos = isCrouching ? crouchingCamera : standingCamera;
            }
        }

        // --- Smooth transition each frame ---
        elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(elapsedTime / transitionDuration);

        controller.height = Mathf.Lerp(startHeight, targetHeight, t);
        controller.center = Vector3.Lerp(startCenter, targetCenter, t);
        cameraRoot.localPosition = Vector3.Lerp(startCameraPos, targetCameraPos, t);
    }
}
