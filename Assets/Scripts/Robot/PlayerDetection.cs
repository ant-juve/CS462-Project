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
    readonly RaycastHit[] obstructionHits = new RaycastHit[8];

    // Update is called once per frame
    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;
        CanSeePlayer = CheckVisibility();
    }

    bool CheckVisibility()
    {
        if (player == null) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * 1.0f; // aim at chest, not feet
        Vector3 toPlayer = target - eye;
        float dist = toPlayer.magnitude;

        Color debugColor = Color.red;
        bool canSee = false;

        // 1. Range (sqrMagnitude version is cheaper if you have many mobs)
        if (dist > detectionRange)
        {
            debugColor = Color.yellow;
        }
        // 2. Field of view
        else if (Vector3.Angle(transform.forward, toPlayer) > fieldOfView * 0.5f)
        {
            debugColor = new Color(1f, 0.5f, 0f);
        }
        // 3. Line of sight
        else
        {
            canSee = !IsObstructed(eye, toPlayer.normalized, dist);
            debugColor = canSee ? Color.green : Color.red;
        }

        Debug.DrawRay(eye, toPlayer, debugColor, checkInterval);
        return canSee;
    }

    bool IsObstructed(Vector3 origin, Vector3 direction, float distance)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, obstructionHits, distance, obstructionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = obstructionHits[i].collider;
            if (hit == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
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
