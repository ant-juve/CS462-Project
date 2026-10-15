
using UnityEngine;
using TMPro;

public class DoorButton : MonoBehaviour
{
    public DoorController door;
    public float interactionDistance = 2.5f;
    public GameObject interactionPrompt;

    private bool activated = false;

    void Update()
    {
        if (activated) return;

        Camera playerCamera = Camera.main;

        if (playerCamera == null) return;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        bool lookingAtPanel = false;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            lookingAtPanel =
                hit.transform == transform ||
                hit.transform.IsChildOf(transform);
        }

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(lookingAtPanel);
        }

        if (lookingAtPanel && Input.GetKeyDown(KeyCode.E))
        {
            door.OpenDoor();
            activated = true;

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }
}
