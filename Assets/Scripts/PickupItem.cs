using UnityEngine;

// Put this on any object in the world the player should be able to pick up.
// The object needs a Collider so the player's look raycast can hit it.
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    public string itemName = "Item";
    public Sprite icon;

    private Rigidbody body;
    private Collider[] colliders;
    private bool wasKinematic;
    private bool captured; // original parent/scale have been recorded for this pickup

    // Captured on pickup so the item can be restored on drop
    private Transform originalParent;
    private Vector3 originalScale;
    private Vector3 worldScale;
    private Vector3 localBoundsCenter;

    // Where the item sits relative to the hand, recorded at the moment the hand closes on it
    private bool hasHeldPose;
    private Vector3 heldLocalPosition;
    private Quaternion heldLocalRotation;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>(true);
    }

    // Center of the item's visible bounds, in world space
    public Vector3 WorldCenter => transform.TransformPoint(localBoundsCenter);

    // Called when the hand starts reaching for this item: stop physics and detach it from the level
    public void BeginGrab()
    {
        if (captured)
            return;
        captured = true;

        originalParent = transform.parent;
        originalScale = transform.localScale;
        worldScale = transform.lossyScale;
        MeasureBounds();

        // While carried it shouldn't collide with anything or block the pickup raycast
        SetPhysicsEnabled(false);
        transform.SetParent(null, true);
    }

    // Remember how the item sits in the hand right now, so it's held exactly like this
    public void CaptureHeldPose(Transform hand)
    {
        heldLocalPosition = hand.InverseTransformPoint(transform.position);
        heldLocalRotation = Quaternion.Inverse(hand.rotation) * transform.rotation;
        hasHeldPose = true;
    }

    // Called by the hotbar when the player picks this up
    public void OnPickedUp()
    {
        BeginGrab();
        gameObject.SetActive(false);
    }

    // Show the item in the player's hand at its real size
    public void Hold(Transform hand)
    {
        transform.SetParent(hand, false);
        Vector3 handScale = hand.lossyScale;
        transform.localScale = new Vector3(worldScale.x / handScale.x, worldScale.y / handScale.y, worldScale.z / handScale.z);

        if (hasHeldPose)
        {
            transform.localPosition = heldLocalPosition;
            transform.localRotation = heldLocalRotation;
        }
        else
        {
            // No hand closed on it (no first-person arms): just center it on the hand point
            transform.localRotation = Quaternion.identity;
            transform.localPosition = -Vector3.Scale(localBoundsCenter, transform.localScale);
        }
        gameObject.SetActive(true);
    }

    // Hide the item while it's in the hotbar but not selected
    public void Stow()
    {
        gameObject.SetActive(false);
    }

    // Called by the hotbar when the player drops this back into the world
    public void OnDropped(Vector3 position, Quaternion rotation)
    {
        captured = false;
        hasHeldPose = false;
        transform.SetParent(originalParent, true);
        transform.localScale = originalScale;
        transform.SetPositionAndRotation(position, rotation);
        SetPhysicsEnabled(true);
        gameObject.SetActive(true);

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    // ---------- Surface queries (work while the colliders are disabled) ----------

    // Distance from p to the item's surface; negative when p is inside
    public float SurfaceDistance(Vector3 p)
    {
        float best = float.MaxValue;
        foreach (Collider col in colliders)
            best = Mathf.Min(best, ColliderDistance(col, p, out _));
        return best;
    }

    // Where a line from the item's center out toward `from` leaves the surface: the middle of the
    // side facing `from`, which is where a hand reaching from there would naturally land
    public Vector3 SurfacePointToward(Vector3 from)
    {
        Vector3 center = WorldCenter;
        if (SurfaceDistance(from) <= 0f)
            return from;

        float inside = 0f, outside = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (inside + outside) * 0.5f;
            if (SurfaceDistance(Vector3.Lerp(center, from, mid)) > 0f) outside = mid;
            else inside = mid;
        }
        return Vector3.Lerp(center, from, outside);
    }

    // Closest point on the item's surface to p
    public Vector3 ClosestSurfacePoint(Vector3 p)
    {
        float best = float.MaxValue;
        Vector3 bestPoint = WorldCenter;
        foreach (Collider col in colliders)
        {
            float d = ColliderDistance(col, p, out Vector3 point);
            if (d < best)
            {
                best = d;
                bestPoint = point;
            }
        }
        return bestPoint;
    }

    static float ColliderDistance(Collider col, Vector3 p, out Vector3 closest)
    {
        Transform t = col.transform;
        switch (col)
        {
            case SphereCollider sphere:
            {
                Vector3 center = t.TransformPoint(sphere.center);
                float radius = sphere.radius * MaxScaleComponent(t.lossyScale);
                Vector3 dir = p - center;
                closest = center + (dir.sqrMagnitude > 0f ? dir.normalized : Vector3.up) * radius;
                return dir.magnitude - radius;
            }
            case CapsuleCollider capsule:
            {
                Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                Vector3 scale = t.lossyScale;
                float axisScale = Mathf.Abs(Vector3.Dot(scale, axis));
                Vector3 across = Vector3.Scale(scale, Vector3.one - axis);
                float radius = capsule.radius * MaxScaleComponent(across);
                float halfSegment = Mathf.Max(0f, capsule.height * 0.5f * axisScale - radius) / Mathf.Max(axisScale, 0.0001f);
                Vector3 a = t.TransformPoint(capsule.center - axis * halfSegment);
                Vector3 b = t.TransformPoint(capsule.center + axis * halfSegment);
                Vector3 ab = b - a;
                float k = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                Vector3 onSegment = a + ab * k;
                Vector3 dir = p - onSegment;
                closest = onSegment + (dir.sqrMagnitude > 0f ? dir.normalized : Vector3.up) * radius;
                return dir.magnitude - radius;
            }
            case BoxCollider box:
                return BoxDistance(t, box.center, box.size, p, out closest);
            case MeshCollider mesh when mesh.sharedMesh != null:
                return BoxDistance(t, mesh.sharedMesh.bounds.center, mesh.sharedMesh.bounds.size, p, out closest);
            default:
                closest = t.position;
                return Vector3.Distance(p, t.position);
        }
    }

    static float BoxDistance(Transform t, Vector3 center, Vector3 size, Vector3 p, out Vector3 closest)
    {
        Vector3 local = t.InverseTransformPoint(p) - center;
        Vector3 half = size * 0.5f;
        Vector3 clamped = new Vector3(
            Mathf.Clamp(local.x, -half.x, half.x),
            Mathf.Clamp(local.y, -half.y, half.y),
            Mathf.Clamp(local.z, -half.z, half.z));
        closest = t.TransformPoint(clamped + center);

        if (clamped != local)
            return Vector3.Distance(p, closest); // outside

        // Inside: distance to the nearest face, in world units
        Vector3 scale = t.lossyScale;
        float dx = (half.x - Mathf.Abs(local.x)) * Mathf.Abs(scale.x);
        float dy = (half.y - Mathf.Abs(local.y)) * Mathf.Abs(scale.y);
        float dz = (half.z - Mathf.Abs(local.z)) * Mathf.Abs(scale.z);
        return -Mathf.Min(dx, Mathf.Min(dy, dz));
    }

    void SetPhysicsEnabled(bool enabled)
    {
        foreach (Collider col in colliders)
            col.enabled = enabled;

        if (body == null)
            return;

        if (!enabled)
            wasKinematic = body.isKinematic;
        body.isKinematic = enabled ? wasKinematic : true;
    }

    // Records the visible center (in the item's own local space) while it's still active
    void MeasureBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);
        localBoundsCenter = transform.InverseTransformPoint(bounds.center);
    }

    static float MaxScaleComponent(Vector3 v)
    {
        return Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z), 0.001f);
    }
}
