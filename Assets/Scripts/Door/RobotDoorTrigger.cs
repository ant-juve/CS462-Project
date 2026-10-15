
using UnityEngine;

public class RobotDoorTrigger : MonoBehaviour
{
    public DoorController door;

    private int robotCollidersInside = 0;

    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Robot"))
        {
            robotCollidersInside++;
            door.SetRobotInside(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Robot"))
        {
            robotCollidersInside = Mathf.Max(
                0, robotCollidersInside - 1
            );

            if (robotCollidersInside == 0)
            {
                door.SetRobotInside(false);
            }
        }
    }

}
