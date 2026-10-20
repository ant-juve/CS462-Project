using System.Collections.Generic;
using UnityEngine;

// Something radioactive: a spill, a leaking pipe, the reactor. Add it to any object.
// It's invisible in game; the zone is drawn in the Scene view so you can see it while building levels.
//   Point  strongest at the center, falling off with distance out to the radius (pipes, barrels, the reactor)
//   Area   full strength inside a box, falling off past its edges (floor spills)
// No colliders needed: every enabled source registers itself and the Dosimeter reads the list.
public class RadiationSource : MonoBehaviour
{
    public enum Shape { Point, Area }

    private static readonly List<RadiationSource> active = new List<RadiationSource>();
    public static IReadOnlyList<RadiationSource> Active => active;

    public Shape shape = Shape.Point;
    [Tooltip("Dose rate at the center (Point) or anywhere inside the box (Area), in mrem/hr")]
    public float doseRate = 5000f;
    [Tooltip("How far from the source (or the box edge) the rate drops to half, in meters. " +
             "Bigger keeps it strong further out")]
    public float halfStrengthDistance = 1f;

    [Header("Point")]
    [Tooltip("How far it reaches at all, in meters")]
    public float radius = 6f;

    [Header("Area")]
    [Tooltip("Size of the full-strength box, in the object's local space")]
    public Vector3 areaSize = new Vector3(3f, 2f, 3f);
    [Tooltip("How far past the box edges it reaches at all, in meters")]
    public float edgeFade = 6f;

    private static readonly Color GizmoColor = new Color(1f, 0.65f, 0.1f);

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    // Dose rate this source gives at a world position, in mrem/hr
    public float RateAt(Vector3 position)
    {
        float distance;
        float reach;
        if (shape == Shape.Point)
        {
            distance = Vector3.Distance(position, transform.position);
            reach = radius;
        }
        else
        {
            // Distance from the box surface; zero anywhere inside it
            Vector3 local = transform.InverseTransformPoint(position);
            Vector3 half = areaSize * 0.5f;
            Vector3 closest = new Vector3(
                Mathf.Clamp(local.x, -half.x, half.x),
                Mathf.Clamp(local.y, -half.y, half.y),
                Mathf.Clamp(local.z, -half.z, half.z));
            distance = Vector3.Distance(position, transform.TransformPoint(closest));
            reach = edgeFade;
        }

        if (distance <= 0f)
            return doseRate;
        if (distance >= reach)
            return 0f;

        // Inverse-square, like real radiation: strong up close with a long faint tail.
        // Softened so it doesn't shoot to infinity at the source, then eased to zero at the reach
        float d = distance / Mathf.Max(halfStrengthDistance, 0.01f);
        float inverseSquare = 1f / (1f + d * d);
        float t = distance / reach;
        float window = (1f - t * t) * (1f - t * t);
        return doseRate * inverseSquare * window;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = GizmoColor;
        if (shape == Shape.Point)
        {
            Gizmos.DrawWireSphere(transform.position, radius);
        }
        else
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, areaSize);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, 0.15f);
        if (shape == Shape.Point)
        {
            Gizmos.DrawSphere(transform.position, radius);
        }
        else
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, areaSize);
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireCube(Vector3.zero, areaSize + Vector3.one * edgeFade * 2f); // where the fade ends
        }
    }
}
