
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class MeltdownWarning : MonoBehaviour
{
    [Header("Warning UI")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private TMP_Text continuePrompt;

    [Header("Player Controls")]
    [SerializeField] private MonoBehaviour firstPersonController;
    [SerializeField] private PlayerInput playerInput;

    [Header("Warning Settings")]
    [SerializeField] private float blinkSpeed = 2f;

    private bool warningActive;
    private bool controllerWasEnabled;
    private bool inputWasEnabled;

    void Start()
    {
        ShowWarning();
    }

    void Update()
    {
        if (!warningActive)
            return;

        // Make the acknowledgment prompt blink.
        if (continuePrompt != null)
        {
            Color color = continuePrompt.color;

            color.a = Mathf.PingPong(
                Time.unscaledTime * blinkSpeed, 1f
            );

            continuePrompt.color = color;
        }

        // Check for the E key using the new Input System.
        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            DismissWarning();
        }
    }

    public void ShowWarning()
    {
        warningActive = true;

        if (warningPanel != null)
            warningPanel.SetActive(true);

        // Disable movement and camera controls.
        if (firstPersonController != null)
        {
            controllerWasEnabled =
                firstPersonController.enabled;

            firstPersonController.enabled = false;
        }

        // Disable gameplay input.
        if (playerInput != null)
        {
            inputWasEnabled = playerInput.enabled;
            playerInput.enabled = false;
        }

        // Release the mouse cursor.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void DismissWarning()
    {
        warningActive = false;

        if (warningPanel != null)
            warningPanel.SetActive(false);

        // Restore the original player controller state.
        if (firstPersonController != null)
            firstPersonController.enabled =
                controllerWasEnabled;

        // Restore player input.
        if (playerInput != null)
            playerInput.enabled = inputWasEnabled;

        // Lock the mouse for first-person gameplay.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
