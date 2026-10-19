
using UnityEngine;
using TMPro;

public class MalfunctionPanel : MonoBehaviour
{
    public float interactionDistance = 5f;
    public GameObject interactionPrompt;
    public TextMeshProUGUI messageText;
    public float messageDuration = 3f;

    private float messageTimer;

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
            // Check whether the hit object belongs to this panel
            bool recognized =
                hit.transform == transform ||
                hit.transform.IsChildOf(transform);

            Debug.Log(
                "Hit: " + hit.transform.name +
                " | Recognized as panel: " + recognized
            );

            if (recognized)
            {
                lookingAtPanel = true;

                if (Input.GetKeyDown(KeyCode.E))
                {
                    Debug.Log("E pressed on malfunction panel");

                    if (messageText != null)
                    {
                        messageText.text = "ERROR";
                        messageText.gameObject.SetActive(true);
                        messageTimer = messageDuration;
                    }
                }
            }
        }

        // Display Press E when looking at the panel
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(lookingAtPanel);
        }

        // Hide malfunction message after the timer expires
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;

            if (messageTimer <= 0f)
            {
                if (messageText != null)
                {
                    messageText.gameObject.SetActive(false);
                }
            }
        }
    }
}
