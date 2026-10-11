using UnityEngine;

// Put this on any object in the world the player should be able to pick up.
// The object needs a Collider so the player's look raycast can hit it.
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    public string itemName = "Item";
    public Sprite icon;

    private Rigidbody body;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    // Called by the hotbar when the player picks this up
    public void OnPickedUp()
    {
        gameObject.SetActive(false);
    }

    // Called by the hotbar when the player drops this back into the world
    public void OnDropped(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        gameObject.SetActive(true);

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
