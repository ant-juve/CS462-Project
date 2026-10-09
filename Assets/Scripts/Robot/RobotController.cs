using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    public enum State {Patrol};
    private State currentState = State.Patrol;
    public float moveSpeed = 3f;
    private Waypoint previousWaypoint;
    public Waypoint startWaypoint;
    private Waypoint currentWaypoint;
    private CharacterController controller;
    private float verticalVelocity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentWaypoint = startWaypoint;
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        switch(currentState)
        {
            case State.Patrol:
                PatrolBehavior();
                break;
        }
    }

    void MoveTowardsPosition(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        direction.Normalize();

        if (controller.isGrounded)
            verticalVelocity = -1f;
        else
            verticalVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 move = direction * moveSpeed;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }
    void PatrolBehavior()
    {
        Vector3 targetPosition = currentWaypoint.transform.position;
        MoveTowardsPosition(targetPosition);
        Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(targetPosition.x, 0f, targetPosition.z);
        if (Vector3.Distance(flatPos, flatTarget) < 0.5f)
        {
            Waypoint next = PickNextWaypoint();
            previousWaypoint = currentWaypoint;
            currentWaypoint = next;
        }
    }

    Waypoint PickNextWaypoint()
    {
        List<Waypoint> options = new List<Waypoint>(currentWaypoint.neighbors);

        // Remove the last visited waypoint, but only if there's another choice
        if (options.Count > 1)
            options.Remove(previousWaypoint);

        if (options.Count == 0)
            return currentWaypoint; // no neighbors, stay put

        return options[Random.Range(0, options.Count)];
    }
}
