using UnityEngine;

public class PlayerDetection : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] float detectionRange = 12f;
    [SerializeField, Range(0, 360)] float fieldOfView = 110f;
    [SerializeField] float eyeHeight = 1.6f;
    [SerializeField] LayerMask obstructionMask;   // walls, props (exclude Player and the mob itself)
    [SerializeField] float checkInterval = 0.2f;  // don't check every frame

    public bool CanSeePlayer { get; private set; }
    float nextCheck;

    // Update is called once per frame
    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;
        CanSeePlayer = CheckVisibility();
    }

    bool CheckVisibility()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * 1.0f; // aim at chest, not feet
        Vector3 toPlayer = target - eye;
        Debug.DrawRay(eye, toPlayer, Color.red, checkInterval);
        float dist = toPlayer.magnitude;

        // 1. Range (sqrMagnitude version is cheaper if you have many mobs)
        if (dist > detectionRange) return false;

        // 2. Field of view
        if (Vector3.Angle(transform.forward, toPlayer) > fieldOfView * 0.5f) return false;

        // 3. Line of sight
        if (Physics.Raycast(eye, toPlayer.normalized, dist, obstructionMask)) return false;

        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Vector3 l = Quaternion.Euler(0, -fieldOfView / 2, 0) * transform.forward;
        Vector3 r = Quaternion.Euler(0,  fieldOfView / 2, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, l * detectionRange);
        Gizmos.DrawRay(transform.position, r * detectionRange);
    }
}
