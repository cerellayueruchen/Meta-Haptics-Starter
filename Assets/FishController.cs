using System.Collections;
using UnityEngine;

/// Controls the procedural swimming behaviour of a fish.
/// Default state: Wander randomly inside the bucket.
/// After receiving StartBiting(), the fish swims to the bite point,
/// stays there briefly, returns home, repeats for the specified
/// number of bites, and finally resumes wandering.
///
/// Swimming is generated procedurally. The fish always moves forward
/// along its heading while smoothly steering toward its target.
/// A side-to-side body oscillation creates a natural swimming motion.
public class FishController : MonoBehaviour
{
    public enum FishState
    {
        Wander,
        GoingToBite,
        Biting,
        Returning
    }

    [Header("Target Points")]
    public Transform homePoint;
    public Transform bitePoint;

    [Header("Wander")]
    [Tooltip("Center position of wandering. If empty, homePoint is used.")]
    public Transform wanderCenter;

    public float wanderRadius = 0.07f;
    public float wanderHeight = 0.03f;
    public float wanderSpeed = 0.05f;

    [Header("Movement")]
    public float swimSpeed = 0.2f;
    public float rotateSpeed = 3f;

    [Header("Swimming Motion")]
    public float wiggleAmplitude = 10f;
    public float wiggleFrequency = 2.5f;

    [Range(0f, 1f)]
    public float pitchFlatten = 0.35f;

    [Header("Jellyfish Mode")]
    [Tooltip("Keep upright, rotate only around Y, and pulse instead of wiggling.")]
    public bool jellyfishMode = false;

    [Header("Obstacle Avoidance")]
    public bool avoidObstacles = true;
    public float avoidDistance = 0.03f;
    public float avoidStrength = 2f;

    [Header("Behaviour")]
    public float waitAtHome = 2f;
    public float waitAtBite = 1f;

    /// Fired when the fish reaches the bite point.
    public event System.Action<FishController> OnBite;

    public FishState State { get; private set; } = FishState.Wander;

    private Vector3 wanderTarget;
    private Quaternion bodyRotation;
    private float wigglePhase;
    private Vector3 baseScale;
    private Coroutine biteRoutine;

    void Start()
    {
        if (homePoint != null)
            transform.position = homePoint.position;

        bodyRotation = transform.rotation;
        baseScale = transform.localScale;

        // Offset each fish's animation phase so they do not move identically.
        wigglePhase = Random.value * 100f;

        PickNewWanderTarget();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (State == FishState.Wander)
        {
            SwimTowards(wanderTarget, wanderSpeed, dt);

            if (Vector3.Distance(transform.position, wanderTarget) < 0.02f)
                PickNewWanderTarget();
        }
        else if (State == FishState.Biting)
        {
            ApplySwimVisual(dt, 0f, 0.4f);
        }

        // GoingToBite and Returning are driven by coroutines.
    }

    // --------------------------------------------------
    // Public API
    // --------------------------------------------------

    /// Start the biting behaviour.
    public void StartBiting(int biteCount)
    {
        StopBiting();
        biteRoutine = StartCoroutine(BiteRoutine(biteCount));
    }

    /// Stop biting immediately and return to wandering.
    public void StopBiting()
    {
        if (biteRoutine != null)
        {
            StopCoroutine(biteRoutine);
            biteRoutine = null;
        }

        State = FishState.Wander;
        PickNewWanderTarget();
    }

    // --------------------------------------------------
    // Internal Behaviour
    // --------------------------------------------------

    IEnumerator BiteRoutine(int biteCount)
    {
        for (int i = 0; i < biteCount; i++)
        {
            State = FishState.GoingToBite;
            yield return SwimUntilReached(bitePoint, swimSpeed);

            State = FishState.Biting;

            OnBite?.Invoke(this);

            yield return new WaitForSeconds(waitAtBite);

            State = FishState.Returning;

            yield return SwimUntilReached(homePoint, swimSpeed);

            yield return new WaitForSeconds(waitAtHome);
        }

        biteRoutine = null;

        State = FishState.Wander;

        PickNewWanderTarget();
    }

    IEnumerator SwimUntilReached(Transform target, float speed, float timeout = 12f)
    {
        if (target == null)
            yield break;

        float startTime = Time.time;

        while (Vector3.Distance(transform.position, target.position) > 0.012f &&
               Time.time - startTime < timeout)
        {
            float dt = Time.deltaTime;

            SwimTowards(target.position, speed, dt);

            float d = Vector3.Distance(transform.position, target.position);

            if (d < 0.05f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target.position,
                    speed * 0.8f * dt);
            }

            yield return null;
        }
    }

    /// Main swimming routine.
    void SwimTowards(Vector3 target, float speed, float dt)
    {
        if (dt <= 0f)
            return;

        Vector3 dir = target - transform.position;

        dir.y *= pitchFlatten;

        // Only wander behaviour uses obstacle avoidance.
        if (avoidObstacles && State == FishState.Wander)
        {
            dir = ApplyAvoidance(dir, target);
        }

        if (jellyfishMode)
            dir.y = 0f;

        if (dir.sqrMagnitude > 0.0000001f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized);

            bodyRotation = Quaternion.Slerp(
                bodyRotation,
                look,
                rotateSpeed * dt);
        }

        transform.position +=
            bodyRotation *
            Vector3.forward *
            (speed * dt);

        float newY = Mathf.MoveTowards(
            transform.position.y,
            target.y,
            speed * 0.5f * dt);

        transform.position = new Vector3(
            transform.position.x,
            newY,
            transform.position.z);

        ApplySwimVisual(dt, speed, 1f);

        // Push the fish out only while wandering.
        if (avoidObstacles && State == FishState.Wander)
        {
            PushOutOfZones(target, dt);
        }
    }


    /// Visual swimming animation.
    /// Fish sway left and right, while jellyfish pulse vertically.
    void ApplySwimVisual(float dt, float speed, float amplitudeScale)
    {
        if (jellyfishMode)
        {
            wigglePhase += dt * wiggleFrequency * Mathf.PI * 2f;

            float s = Mathf.Sin(wigglePhase);

            transform.localScale = new Vector3(
                baseScale.x * (1f - s * 0.06f),
                baseScale.y * (1f + s * 0.12f),
                baseScale.z * (1f - s * 0.06f));

            transform.rotation = bodyRotation;
        }
        else
        {
            wigglePhase +=
                dt *
                wiggleFrequency *
                Mathf.PI *
                2f *
                (1f + speed * 4f);

            float wiggle =
                Mathf.Sin(wigglePhase) *
                wiggleAmplitude *
                amplitudeScale;

            transform.rotation =
                bodyRotation *
                Quaternion.Euler(0f, wiggle, 0f);
        }
    }

    /// Apply steering forces away from all active avoidance zones.
    /// The zone containing the current bite target is ignored so the fish
    /// can still reach the user during a bite.
    Vector3 ApplyAvoidance(Vector3 desiredDir, Vector3 finalTarget)
    {
        if (desiredDir.sqrMagnitude < 0.0000001f)
            return desiredDir;

        Vector3 steer = desiredDir.normalized;

        foreach (var zone in FishAvoidZone.Active)
        {
            if (zone == null)
                continue;

            // Ignore the zone containing the target.
            Vector3 closestToTarget = zone.ClosestPoint(finalTarget);

            if (Vector3.Distance(finalTarget, closestToTarget)
                < avoidDistance)
            {
                continue;
            }

            Vector3 away =
                transform.position -
                zone.transform.position;

            float distance = away.magnitude;

            float influence =
            zone.ApproximateRadius +
            avoidDistance;

            if (distance < influence &&
                distance > 0.0001f)
            {
                float weight =
                    1f -
                    distance / influence;

                steer +=
                    (away / distance) *
                    (weight * avoidStrength);
            }
        }

        return steer.normalized;
    }

    /// Push the fish gently outside any avoidance zone
    /// if it accidentally enters one.
    void PushOutOfZones(Vector3 finalTarget, float dt)
    {
        foreach (var zone in FishAvoidZone.Active)
        {
            if (zone == null)
                continue;

            // Ignore the zone containing the target.
            Vector3 closestToTarget = zone.ClosestPoint(finalTarget);

            if (Vector3.Distance(finalTarget, closestToTarget)
                < avoidDistance)
            {
                continue;
            }

            Vector3 closest = zone.ClosestPoint(transform.position);

            Vector3 away =
                transform.position -
                closest;

            float distance = away.magnitude;

            if (distance < 0.001f)
            {
                Vector3 outDir =
                    distance > 0.0001f ?
                    away / distance :
                    Vector3.up;

                float newDistance =
                    Mathf.MoveTowards(
                     distance,
                     avoidDistance,
                     0.15f * dt);

                transform.position =
                    zone.transform.position +
                    outDir * newDistance;
            }
        }
    }


    /// Returns true if the specified point is inside any avoidance zone.
    bool InsideAnyZone(Vector3 point)
    {
        foreach (var zone in FishAvoidZone.Active)
        {
            if (zone == null)
                continue;

            if (Vector3.Distance(
                    point,
                    zone.transform.position)
                < zone.radius + avoidDistance)
            {
                return true;
            }
        }

        return false;
    }

    /// Select a new random wander destination.
    /// The target should be reasonably far away and outside
    /// all avoidance zones to encourage smooth curved swimming.
    void PickNewWanderTarget()
    {
        Vector3 center =
            wanderCenter != null ?
            wanderCenter.position :
            homePoint != null ?
            homePoint.position :
            transform.position;

        // Try several random candidates before falling back
        // to the center position.
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset =
                Random.insideUnitCircle *
                wanderRadius;

            Vector3 candidate =
                center +
                new Vector3(
                    offset.x,
                    Random.Range(
                        -wanderHeight,
                        wanderHeight),
                    offset.y);

            // Encourage longer swimming arcs instead of tiny movements.
            if (Vector3.Distance(
                    candidate,
                    transform.position)
                > wanderRadius * 0.8f
                &&
                !InsideAnyZone(candidate))
            {
                wanderTarget = candidate;
                return;
            }
        }

        // Fallback if no valid target is found.
        wanderTarget = center;
    }
}