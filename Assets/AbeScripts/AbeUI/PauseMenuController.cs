using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class PauseMenuController : MonoBehaviour
{
    [Header("Pause Menu UI")]
    public GameObject pauseMenuRoot;
    public Slider lookSensitivitySlider;
    public TMP_Text lookSensitivityValueLabel;

    [Header("Input")]
    public InputActionReference pauseAction; // Drag the Pause action here from the Input Actions asset.

    [Header("Player Reference")]
    public FirstPersonLook playerLook;

    [Header("Look Sensitivity Settings")]
    public float minLookSensitivity = 0.1f;
    public float maxLookSensitivity = 10f;

    private bool isPaused = false;

    private void OnEnable()
    {
        if (pauseAction != null)
            pauseAction.action.Enable();
    }

    private void OnDisable()
    {
        if (pauseAction != null)
            pauseAction.action.Disable();
    }

    private void Start()
    {
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);

        if (playerLook == null)
            playerLook = FindFirstObjectByType<FirstPersonLook>();

        if (lookSensitivitySlider != null && playerLook != null)
        {
            lookSensitivitySlider.minValue = minLookSensitivity;
            lookSensitivitySlider.maxValue = maxLookSensitivity;

            // Use the sensitivity already authored in FirstPersonLook as the starting slider value.
            lookSensitivitySlider.value = playerLook.sensitivityX;

            lookSensitivitySlider.onValueChanged.AddListener(SetLookSensitivity);
        }

        if (playerLook != null)
            SetLookSensitivity(playerLook.sensitivityX);

        ResumeGame();
    }

    private void Update()
    {
        bool pausePressed = pauseAction != null && pauseAction.action.WasPressedThisFrame();

        if (pausePressed)
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }

        // Reconnect the reference if the player object was recreated after a scene load.
        if (playerLook == null)
            playerLook = FindFirstObjectByType<FirstPersonLook>();
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // Freeze gameplay while the pause menu is open.

        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(true);

        if (playerLook != null)
            playerLook.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);

        if (playerLook != null)
            playerLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // Always restore time before changing scenes.

        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadMainMenu();
        }
        else
        {
            Debug.LogWarning("No SceneController found in the scene.");
        }
    }

    public void SetLookSensitivity(float value)
    {
        if (playerLook != null)
        {
            // The pause menu directly changes the public sensitivity floats in FirstPersonLook.
            playerLook.sensitivityX = value;
            playerLook.sensitivityY = value;
        }

        if (lookSensitivityValueLabel != null)
            lookSensitivityValueLabel.text = value.ToString("F2");
    }
}
