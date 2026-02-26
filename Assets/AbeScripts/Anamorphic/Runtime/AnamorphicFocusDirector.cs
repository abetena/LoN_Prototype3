using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine 3 (Unity.Cinemachine) director that switches between:
/// - Player CM camera (normal gameplay)
/// - Focus CM camera (anamorphic reveal) aligned to an AnamorphicDrawingInstance viewpoint + look-at
///
/// Switching method:
/// - Priority switching (CinemachineBrain chooses the highest-priority enabled CM camera)
///
/// Snapping method:
/// - ForceCameraPosition() for reliable instant placement (avoids pipeline overwriting manual transform edits)
/// </summary>
public class AnamorphicFocusDirector : MonoBehaviour
{
    [Header("Cinemachine Cameras (CM3)")]
    [Tooltip("The player's normal gameplay CinemachineCamera (often child of CameraRoot).")]
    public CinemachineCamera playerCam;

    [Tooltip("A separate CinemachineCamera used for anamorphic reveals.")]
    public CinemachineCamera focusCam;

    [Header("Player Rig")]
    [Tooltip("Optional. Scripts to disable during focus (Move/Jump/Crouch/Look, etc.).")]
    public MonoBehaviour[] disableDuringFocus;

    [Header("Priorities")]
    public int activePriority = 20;
    public int inactivePriority = 0;

    [Header("Focus Options")]
    [Tooltip("If true, use the drawing's stored viewpoint rotation. If false, compute rotation from viewpoint -> lookAt.")]
    public bool useStoredViewRotation = false;

    [Tooltip("If true, set focus camera FOV from drawing asset recommendedFOV.")]
    public bool useRecommendedFOV = true;

    [Tooltip("If true, unlock/show cursor during focus, then restore on return.")]
    public bool manageCursor = true;

    [Header("Cursor State During Focus")]
    public CursorLockMode focusCursorLockMode = CursorLockMode.None;
    public bool focusCursorVisible = true;

    // Helper transforms that Cinemachine can Follow / LookAt.
    private Transform _focusFollow;
    private Transform _focusLookAt;

    // Cursor restore state
    private CursorLockMode _prevLockMode;
    private bool _prevCursorVisible;

    private void Awake()
    {
        if (playerCam == null || focusCam == null)
        {
            Debug.LogError($"{nameof(AnamorphicFocusDirector)}: Assign both playerCam and focusCam.");
            enabled = false;
            return;
        }

        _focusFollow = CreateOrFindHelper("_ANA_FocusFollow");
        _focusLookAt = CreateOrFindHelper("_ANA_FocusLookAt");

        // Default state: player camera live
        SetActiveCamera(playerCam);
    }

    private Transform CreateOrFindHelper(string name)
    {
        var existing = transform.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    /// <summary>
    /// Optional helper if you want to rewire the player target at runtime.
    /// In CM3, playerCam.Follow/LookAt can point directly to your player/camera root.
    /// </summary>
    public void ApplyPlayerTargets(Transform follow, Transform lookAt)
    {
        if (playerCam == null) return;
        playerCam.Follow = follow;
        playerCam.LookAt = lookAt;
    }

    public void FocusOnDrawing(AnamorphicDrawingInstance drawing)
    {
        if (drawing == null || drawing.asset == null)
        {
            Debug.LogWarning("FocusOnDrawing: drawing or drawing.asset is null.");
            return;
        }

        // Disable player control scripts
        SetPlayerControlEnabled(false);

        // World-space data from the placed instance (not directly from the asset)
        Vector3 viewPos = drawing.GetViewpointWorldPosition();
        Quaternion storedViewRot = drawing.GetViewpointWorldRotation();
        Vector3 lookPos = drawing.GetLookAtWorldPosition();

        // Place helper transforms
        _focusFollow.position = viewPos;
        _focusLookAt.position = lookPos;

        // Wire focus camera tracking
        focusCam.Follow = _focusFollow;
        focusCam.LookAt = _focusLookAt;

        // Compute snap rotation (if we are not using stored rotation)
        Quaternion snapRot = storedViewRot;
        if (!useStoredViewRotation)
        {
            Vector3 dir = lookPos - viewPos;
            if (dir.sqrMagnitude < 0.000001f)
                dir = drawing.transform.forward; // fallback
            snapRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // Snap the CM camera reliably
        focusCam.ForceCameraPosition(viewPos, snapRot);

        // FOV
        if (useRecommendedFOV)
        {
            float fov = Mathf.Clamp(drawing.asset.recommendedFOV, 1f, 179f);
            var lens = focusCam.Lens;   // Lens is a struct, so copy-modify-assign is safest
            lens.FieldOfView = fov;
            focusCam.Lens = lens;
        }

        // Switch active camera
        SetActiveCamera(focusCam);

        // Cursor management
        if (manageCursor)
            SetCursorForFocus(true);
    }

    public void ReturnToPlayer()
    {
        SetActiveCamera(playerCam);
        SetPlayerControlEnabled(true);

        if (manageCursor)
            SetCursorForFocus(false);
    }

    public void ToggleFocus(AnamorphicDrawingInstance drawing)
    {
        if (IsActive(focusCam)) ReturnToPlayer();
        else FocusOnDrawing(drawing);
    }

    private void SetActiveCamera(CinemachineCamera cam)
    {
        if (playerCam != null) playerCam.Priority = (cam == playerCam) ? activePriority : inactivePriority;
        if (focusCam != null) focusCam.Priority = (cam == focusCam) ? activePriority : inactivePriority;

        // Optional: If both have same priority and are enabled, CM3 can use "most recently activated" behavior.
        // Priority switching alone is usually enough.
    }

    private bool IsActive(CinemachineCamera cam)
    {
        return cam != null && cam.Priority >= activePriority;
    }

    private void SetPlayerControlEnabled(bool enabledControl)
    {
        if (disableDuringFocus == null) return;

        for (int i = 0; i < disableDuringFocus.Length; i++)
        {
            if (disableDuringFocus[i] == null) continue;
            disableDuringFocus[i].enabled = enabledControl;
        }
    }

    private void SetCursorForFocus(bool focusing)
    {
        if (focusing)
        {
            _prevLockMode = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;

            Cursor.lockState = focusCursorLockMode;
            Cursor.visible = focusCursorVisible;
        }
        else
        {
            Cursor.lockState = _prevLockMode;
            Cursor.visible = _prevCursorVisible;
        }
    }
}
