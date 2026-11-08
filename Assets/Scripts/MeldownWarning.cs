
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class MeltdownWarning : MonoBehaviour
{
    [Header("Photosensitivity Warning")]
    [SerializeField] private GameObject epilepsyWarningPanel;
    [SerializeField] private TMP_Text epilepsyContinuePrompt;

    [Header("Meltdown Warning")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private TMP_Text continuePrompt;

    [Header("Player Controls")]
    [SerializeField] private MonoBehaviour firstPersonController;
    [SerializeField] private PlayerInput playerInput;

    [Header("Facility Lighting")]
    [SerializeField] private FacilityBlackout facilityBlackout;

    [Header("Warning Settings")]
    [SerializeField] private float blinkSpeed = 2f;

    private enum WarningState
    {
        Epilepsy,
        Meltdown,
        Gameplay
    }

    private WarningState currentState;

    private bool controllerWasEnabled;
    private bool inputWasEnabled;
    private bool lightingWasEnabled;

    void Start()
    {
        // Remember the original component states.
        if (firstPersonController != null)
            controllerWasEnabled = firstPersonController.enabled;

        if (playerInput != null)
            inputWasEnabled = playerInput.enabled;

        if (facilityBlackout != null)
            lightingWasEnabled = facilityBlackout.enabled;

        // Disable player controls while warnings are displayed.
        if (firstPersonController != null)
            firstPersonController.enabled = false;

        if (playerInput != null)
            playerInput.enabled = false;

        // Prevent lighting effects during the warnings.
        if (facilityBlackout != null)
            facilityBlackout.enabled = false;

        // Display the photosensitivity warning first.
        currentState = WarningState.Epilepsy;

        if (epilepsyWarningPanel != null)
            epilepsyWarningPanel.SetActive(true);

        if (warningPanel != null)
            warningPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (currentState == WarningState.Gameplay)
            return;

        if (currentState == WarningState.Epilepsy)
        {
            BlinkPrompt(epilepsyContinuePrompt);

            if (Keyboard.current != null &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ShowMeltdownWarning();
            }
        }
        else if (currentState == WarningState.Meltdown)
        {
            BlinkPrompt(continuePrompt);

            if (Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                DismissWarning();
            }
        }
    }

    void BlinkPrompt(TMP_Text prompt)
    {
        if (prompt == null)
            return;

        Color color = prompt.color;

        color.a = Mathf.PingPong(
            Time.unscaledTime * blinkSpeed, 1f
        );

        prompt.color = color;
    }

    void ShowMeltdownWarning()
    {
        currentState = WarningState.Meltdown;

        // Hide the photosensitivity warning.
        if (epilepsyWarningPanel != null)
            epilepsyWarningPanel.SetActive(false);

        // Display the reactor warning.
        if (warningPanel != null)
            warningPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Kept public for compatibility with existing references.
    public void ShowWarning()
    {
        if (currentState == WarningState.Gameplay)
            return;

        ShowMeltdownWarning();
    }

    public void DismissWarning()
    {
        if (currentState != WarningState.Meltdown)
            return;

        currentState = WarningState.Gameplay;

        if (epilepsyWarningPanel != null)
            epilepsyWarningPanel.SetActive(false);

        if (warningPanel != null)
            warningPanel.SetActive(false);

        // Restore player movement and camera controls.
        if (firstPersonController != null)
            firstPersonController.enabled = controllerWasEnabled;

        // Restore gameplay input.
        if (playerInput != null)
            playerInput.enabled = inputWasEnabled;

        // Begin the facility lighting cycle.
        if (facilityBlackout != null)
            facilityBlackout.enabled = lightingWasEnabled;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
