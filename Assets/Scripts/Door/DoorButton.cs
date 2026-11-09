
using UnityEngine;

public class DoorButton : MonoBehaviour
{
    public DoorController door;
    public float interactionDistance = 2.5f;
    public GameObject interactionPrompt;

    void Update()
    {
        if (Camera.main == null)
            return;

        Ray ray = new Ray(
            Camera.main.transform.position,
            Camera.main.transform.forward
        );

        bool lookingAtPanel = false;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            Debug.Log("Looking at" + hit.transform.name);
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                lookingAtPanel = true;

                if (Input.GetKeyDown(KeyCode.E))
                {
                    Debug.Log("E pressed — opening door", door);
                    door.OpenDoor();
                }
            }
        }

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(lookingAtPanel);
        }
    }
}
