using System.Collections.Generic;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    public enum State {Patrol, Pursue};
    private State currentState = State.Patrol;
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] float turnSpeed = 360f;
    [SerializeField] Transform player;
    [SerializeField] float attackRange = 2f;
    [SerializeField] float damagePerHit = 20f;
    [SerializeField] float attackCooldown = 1f;
    private Waypoint previousWaypoint;
    public Waypoint startWaypoint;
    private Waypoint currentWaypoint;
    private CharacterController controller;
    private float verticalVelocity;
    private PlayerDetection detection;
    private PlayerHealth playerHealth;
    private float nextAttackTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentWaypoint = startWaypoint;
        detection = GetComponent<PlayerDetection>();
        controller = GetComponent<CharacterController>();

        playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = player.gameObject.AddComponent<PlayerHealth>();
    }

    // Update is called once per frame
    void Update()
    {
        if (PauseMenuController.IsPaused)
            return;

        if (detection.CanSeePlayer)
        {
            currentState = State.Pursue;
        }
        else
        {
            currentState = State.Patrol;
        }
        switch(currentState)
        {
            case State.Patrol:
                PatrolBehavior();
                break;
            case State.Pursue:
                PursueBehavior();
                break;
        }
    }

    void MoveTowardsPosition(Vector3 target)
    {
        FacePosition(target);

        if (controller.isGrounded)
            verticalVelocity = -1f;
        else
            verticalVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 direction = (target - transform.position);
        direction.y = 0f;
        direction.Normalize();

        Vector3 move = direction * moveSpeed;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }

    void FacePosition(Vector3 target)
    {
        Vector3 direction = (target - transform.position);
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }
    }
    void PatrolBehavior()
    {
        if (currentWaypoint == null)
        {
            Debug.LogWarning($"{name} has no waypoint", this);
            return;
        }

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

    void PursueBehavior()
    {
        Vector3 flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatPlayer = new Vector3(player.position.x, 0f, player.position.z);

        if (Vector3.Distance(flatSelf, flatPlayer) > attackRange)
        {
            MoveTowardsPosition(player.position);
        }
        else
        {
            FacePosition(player.position);
            TryAttack();
        }
    }

    void TryAttack()
    {
        if (playerHealth == null || playerHealth.IsDead)
            return;

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;
        playerHealth.TakeDamage(damagePerHit);
    }
}
