
using UnityEngine;

public class DoorController : MonoBehaviour
{
    public Transform leftDoor;
    public Transform rightDoor;

    public float slideDistance = 1.5f;
    public float openSpeed = 2f;

    private Vector3 leftClosed;
    private Vector3 rightClosed;

    private Vector3 leftOpen;
    private Vector3 rightOpen;

    private bool isOpen = false;

    void Start()
    {
        leftClosed = leftDoor.localPosition;
        rightClosed = rightDoor.localPosition;

        leftOpen = leftClosed + Vector3.left * slideDistance;
        rightOpen = rightClosed + Vector3.right * slideDistance;
    }

    void Update()
    {
        if (isOpen)
        {
            leftDoor.localPosition = Vector3.MoveTowards(
                leftDoor.localPosition,
                leftOpen,
                openSpeed * Time.deltaTime
            );

            rightDoor.localPosition = Vector3.MoveTowards(
                rightDoor.localPosition,
                rightOpen,
                openSpeed * Time.deltaTime
            );
        }
    }

    public void OpenDoor()
    {
        isOpen = true;
    }
}
