using StarterAssets;
using UnityEngine;

// Connects the Starter Assets first-person controller to the rest of the game:
// the pause menu, the sensitivity setting, the hotbar, the EPD and the health bar.
// Goes on the PlayerCapsule alongside FirstPersonController.
[RequireComponent(typeof(FirstPersonController))]
public class FirstPersonPlayer : MonoBehaviour
{
    // Starter Assets scales mouse movement by 0.05, the old Input Manager axes by 0.1.
    private const float SensitivityToRotationSpeed = 2f;

    [Header("Size")]
    [Tooltip("Total height of the player's body, in meters (Starter Assets default is 2)")]
    public float bodyHeight = 2.1f;
    [Tooltip("Camera height above the feet, in meters (Starter Assets default is 1.375)")]
    public float eyeHeight = 1.8f;

    private FirstPersonController controller;
    private StarterAssetsInputs input;
    private bool wasPaused;

    public float MouseSensitivity
    {
        get => controller.RotationSpeed / SensitivityToRotationSpeed;
        set => controller.RotationSpeed = value * SensitivityToRotationSpeed;
    }

    void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        input = GetComponent<StarterAssetsInputs>();
        ApplySize();

        if (GetComponent<Hotbar>() == null)
            gameObject.AddComponent<Hotbar>();
        if (GetComponent<Dosimeter>() == null)
            gameObject.AddComponent<Dosimeter>();
        if (GetComponent<EPDDisplay>() == null)
            gameObject.AddComponent<EPDDisplay>();
        if (GetComponent<PlayerHealth>() == null)
            gameObject.AddComponent<PlayerHealth>();
        if (GetComponent<HealthBar>() == null)
            gameObject.AddComponent<HealthBar>();
        if (GetComponentInChildren<GeigerCounter>() == null)
        {
            // Own child object so its AudioSource isn't shared with the footsteps
            GameObject geiger = new GameObject("GeigerCounter");
            geiger.transform.SetParent(transform, false);
            geiger.AddComponent<GeigerCounter>();
        }
        if (GetComponentInChildren<PlayerCough>() == null)
        {
            GameObject cough = new GameObject("Cough");
            cough.transform.SetParent(transform, false);
            cough.AddComponent<PlayerCough>();
        }
    }

    void ApplySize()
    {
        // Grow the collider upward, keeping its bottom where it was so the feet stay on the floor
        CharacterController body = GetComponent<CharacterController>();
        float oldHeight = body.height;
        float bottom = body.center.y - oldHeight * 0.5f;
        body.height = bodyHeight;
        body.center = new Vector3(body.center.x, bottom + bodyHeight * 0.5f, body.center.z);

        // Stretch the visible capsule mesh to match
        Transform mesh = transform.Find("Capsule");
        if (mesh != null)
        {
            float stretch = bodyHeight / oldHeight;
            mesh.localScale = Vector3.Scale(mesh.localScale, new Vector3(1f, stretch, 1f));
            mesh.localPosition = new Vector3(mesh.localPosition.x, mesh.localPosition.y * stretch, mesh.localPosition.z);
        }

        Transform cameraRoot = controller.CinemachineCameraTarget.transform;
        cameraRoot.localPosition = new Vector3(cameraRoot.localPosition.x, eyeHeight, cameraRoot.localPosition.z);
    }

    void Update()
    {
        bool paused = PauseMenuController.IsPaused;
        if (paused == wasPaused)
            return;

        wasPaused = paused;

        // Mouse look doesn't use deltaTime, so timeScale = 0 alone wouldn't stop the camera turning
        controller.enabled = !paused;

        if (input != null)
        {
            input.cursorLocked = !paused; // stop it re-locking the cursor when the window regains focus
            input.LookInput(Vector2.zero);
            input.MoveInput(Vector2.zero);
            input.JumpInput(false);
        }
    }
}
