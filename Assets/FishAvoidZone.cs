using System.Collections.Generic;
using UnityEngine;

/// Avoidance zone attached to an obstacle such as a leg.
/// The avoidance shape is taken directly from the attached Collider.
/// CapsuleCollider is recommended.
/// SphereCollider is also supported.
[RequireComponent(typeof(Collider))]
public class FishAvoidZone : MonoBehaviour
{
    public static readonly List<FishAvoidZone> Active = new List<FishAvoidZone>();

    private Collider cachedCollider;

    public Collider Collider => cachedCollider;

    void Awake()
    {
        cachedCollider = GetComponent<Collider>();
    }

    void OnEnable()
    {
        if (cachedCollider == null)
            cachedCollider = GetComponent<Collider>();

        if (!Active.Contains(this))
            Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    /// Returns the closest point on the avoidance volume.
    public Vector3 ClosestPoint(Vector3 point)
    {
        return cachedCollider.ClosestPoint(point);
    }

    /// Returns an approximate radius used only for target filtering.
    public float ApproximateRadius
    {
        get
        {
            if (cachedCollider is CapsuleCollider capsule)
            {
                float scale = Mathf.Max(
                    transform.lossyScale.x,
                    transform.lossyScale.z);

                return capsule.radius * scale;
            }

            if (cachedCollider is SphereCollider sphere)
            {
                return sphere.radius * transform.lossyScale.x;
            }

            return 0.05f;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (cachedCollider == null)
            cachedCollider = GetComponent<Collider>();

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);

        if (cachedCollider is CapsuleCollider capsule)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.DrawWireCube(
                capsule.center,
                new Vector3(
                    capsule.radius * 2f,
                    capsule.height,
                    capsule.radius * 2f));

            Gizmos.matrix = oldMatrix;
        }
        else if (cachedCollider is SphereCollider sphere)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(
                sphere.center,
                sphere.radius);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}