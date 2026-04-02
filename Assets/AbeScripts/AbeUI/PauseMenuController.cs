using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class PauseMenuController : MonoBehaviour
{
    [Header("Pause Menu UI")]
    public GameObject pauseMenuPanel;
    public Slider lookSensitivitySlider;
    public TMP_Text lookSensitivityValueLabel;

    [Header("Input")]
    public InputActionReference pauseAction;

    [Header("Player Reference")]
    public FirstPersonLook playerLook; // Drag the exact object that has FirstPersonLook.cs

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
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (playerLook == null)
        {
            Debug.LogWarning("PauseMenuController: playerLook is not assigned in the Inspector.");
            return;
        }

        if (lookSensitivitySlider != null)
        {
            lookSensitivitySlider.minValue = minLookSensitivity;
            lookSensitivitySlider.maxValue = maxLookSensitivity;
            lookSensitivitySlider.value = playerLook.sensitivityX;
            lookSensitivitySlider.onValueChanged.AddListener(SetLookSensitivity);
        }

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
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);

        if (playerLook != null)
            playerLook.DisableLook();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (playerLook != null)
            playerLook.EnableLook();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (SceneController.Instance != null)
            SceneController.Instance.LoadMainMenu();
        else
            Debug.LogWarning("No SceneController found in the scene.");
    }

    public void SetLookSensitivity(float value)
    {
        if (playerLook != null)
        {
            playerLook.sensitivityX = value;
            playerLook.sensitivityY = value;
        }

        if (lookSensitivityValueLabel != null)
            lookSensitivityValueLabel.text = value.ToString("F2");
    }
}