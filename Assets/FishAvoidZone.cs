using System.Collections.Generic;
using UnityEngine;

/// Capsule-shaped avoidance zone (attach to Capsule1 / Capsule2 around the legs).
/// The shape is read from the CapsuleCollider on the same GameObject,
/// including its center, height, direction and the Transform's scale.
///
/// Provides a signed distance to the capsule surface: negative values mean
/// the point is inside the capsule, and outwardDir always points away from
/// the capsule core, so fish can escape even when already overlapping.
[RequireComponent(typeof(CapsuleCollider))]
public class FishAvoidZone : MonoBehaviour
{
    public static readonly List<FishAvoidZone> Active = new List<FishAvoidZone>();

    private CapsuleCollider capsule;

    void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();
    }

    void OnEnable()
    {
        if (capsule == null)
            capsule = GetComponent<CapsuleCollider>();

        if (!Active.Contains(this))
            Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    /// World-space capsule: core segment (p0 -> p1) plus radius.
    /// Follows Unity's CapsuleCollider scaling rules.
    public void GetWorldCapsule(out Vector3 p0, out Vector3 p1, out float radius)
    {
        if (capsule == null)
            capsule = GetComponent<CapsuleCollider>();

        Vector3 lossy = transform.lossyScale;
        Vector3 axisLocal;
        float axisScale;
        float radiusScale;

        switch (capsule.direction)
        {
            case 0: // X axis
                axisLocal = Vector3.right;
                axisScale = Mathf.Abs(lossy.x);
                radiusScale = Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
                break;
            case 2: // Z axis
                axisLocal = Vector3.forward;
                axisScale = Mathf.Abs(lossy.z);
                radiusScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
                break;
            default: // Y axis (Unity default)
                axisLocal = Vector3.up;
                axisScale = Mathf.Abs(lossy.y);
                radiusScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
                break;
        }

        radius = capsule.radius * radiusScale;

        // Length of the straight core segment (total height minus both caps).
        float half = Mathf.Max(0f, capsule.height * axisScale * 0.5f - radius);

        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 axis = transform.TransformDirection(axisLocal).normalized;

        p0 = center - axis * half;
        p1 = center + axis * half;
    }

    /// Signed distance from a point to the capsule surface.
    /// Negative = inside the capsule.
    /// outwardDir points from the capsule core towards the point (escape direction).
    public float SignedDistance(Vector3 point, out Vector3 outwardDir)
    {
        GetWorldCapsule(out Vector3 p0, out Vector3 p1, out float radius);

        Vector3 onAxis = ClosestPointOnSegment(point, p0, p1);
        Vector3 delta = point - onAxis;
        float d = delta.magnitude;

        outwardDir = d > 0.0001f ? delta / d : Vector3.up;

        return d - radius;
    }

    static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float sqrLen = ab.sqrMagnitude;

        if (sqrLen < 1e-8f)
            return a;

        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / sqrLen);
        return a + ab * t;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);

        GetWorldCapsule(out Vector3 p0, out Vector3 p1, out float r);

        Gizmos.DrawWireSphere(p0, r);
        Gizmos.DrawWireSphere(p1, r);

        Vector3 axis = p1 - p0;
        if (axis.sqrMagnitude > 1e-8f)
        {
            axis.Normalize();

            Vector3 n1 = Vector3.Cross(axis, Vector3.up);
            if (n1.sqrMagnitude < 1e-6f)
                n1 = Vector3.Cross(axis, Vector3.right);
            n1.Normalize();

            Vector3 n2 = Vector3.Cross(axis, n1);

            Gizmos.DrawLine(p0 + n1 * r, p1 + n1 * r);
            Gizmos.DrawLine(p0 - n1 * r, p1 - n1 * r);
            Gizmos.DrawLine(p0 + n2 * r, p1 + n2 * r);
            Gizmos.DrawLine(p0 - n2 * r, p1 - n2 * r);
        }
    }
}
