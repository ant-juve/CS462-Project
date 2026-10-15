
using UnityEngine;

public class DoorController : MonoBehaviour
{
    public Transform leftDoor;
    public Transform rightDoor;

    public float slideDistance = 1.5f;
    public float openSpeed = 2f;
    public float closeDelay = 3f;

    private Vector3 leftClosed;
    private Vector3 rightClosed;
    private Vector3 leftOpen;
    private Vector3 rightOpen;

    private bool isOpen = false;
    private bool robotInside = false;
    private float closeTimer;

    void Start()
    {
        leftClosed = leftDoor.localPosition;
        rightClosed = rightDoor.localPosition;

        leftOpen = leftClosed + Vector3.left * slideDistance;
        rightOpen = rightClosed + Vector3.right * slideDistance;
    }

    void Update()
    {
        Vector3 leftTarget = isOpen ? leftOpen : leftClosed;
        Vector3 rightTarget = isOpen ? rightOpen : rightClosed;

        leftDoor.localPosition = Vector3.MoveTowards(
            leftDoor.localPosition,
            leftTarget,
            openSpeed * Time.deltaTime
        );

        rightDoor.localPosition = Vector3.MoveTowards(
            rightDoor.localPosition,
            rightTarget,
            openSpeed * Time.deltaTime
        );

        bool fullyOpen =
            Vector3.Distance(leftDoor.localPosition, leftOpen) < 0.01f &&
            Vector3.Distance(rightDoor.localPosition, rightOpen) < 0.01f;

        if (isOpen && fullyOpen && !robotInside)
        {
            closeTimer -= Time.deltaTime;

            if (closeTimer <= 0f)
            {
                CloseDoor();
            }
        }
    }

    public void OpenDoor()
    {
        isOpen = true;
        closeTimer = closeDelay;
    }

    public void CloseDoor()
    {
        if (!robotInside)
        {
            isOpen = false;
        }
    }

    public void SetRobotInside(bool inside)
    {
        robotInside = inside;

        if (inside)
        {
            OpenDoor();
        }
        else
        {
            closeTimer = closeDelay;
        }
    }
}
