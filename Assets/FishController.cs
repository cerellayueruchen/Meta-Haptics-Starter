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

    public enum SwimPattern
    {
        Wander = 0,      // Random wandering (used by the jellyfish)
        Circle = 1,      // Endless circling (big fish)
        FigureEight = 2, // Figure-eight around the two legs
        Waypoints = 3    // Random hops between fixed swim points (small fish)
    }

    [Header("Target Points")]
    public Transform homePoint;
    public Transform bitePoint;

    [Tooltip("Optional bite targets (Fish_bite_*). When set, every bite picks a different random point; otherwise bitePoint is used.")]
    public Transform[] bitePoints;

    [Header("Wander")]
    [Tooltip("Center position of wandering. If empty, homePoint is used.")]
    public Transform wanderCenter;

    public float wanderRadius = 0.07f;
    public float wanderHeight = 0.03f;
    public float wanderSpeed = 0.05f;

    [Header("Cruise Pattern")]
    [Tooltip("Default swimming style while not biting.")]
    public SwimPattern swimPattern = SwimPattern.Wander;

    [Tooltip("Circle: center of the circle (e.g. the WaterCylinder). Falls back to wanderCenter/homePoint.")]
    public Transform circleCenter;
    public float circleRadius = 0.11f;

    [Tooltip("FigureEight: the two leg capsules the eight wraps around.")]
    public Transform eightPointA;
    public Transform eightPointB;

    [Tooltip("FigureEight: length of the eight = half leg distance x this factor.")]
    public float eightWidth = 1.5f;

    [Tooltip("Waypoints: the fish cruises randomly between these points (Fish_swim_*).")]
    public Transform[] swimPoints;

    [Header("Movement")]
    public float swimSpeed = 0.2f;
    public float rotateSpeed = 3f;

    [Tooltip("Distance (m) from the fish pivot to its mouth, along the swimming direction. Bites aim so the MOUTH touches the bite point instead of the body center. 0 = aim with the pivot.")]
    public float mouthOffset = 0f;

    [Header("Swimming Motion")]
    [Tooltip("Whole-body sway in degrees. Keep 0 for rigid models - it reads as shaking.")]
    public float wiggleAmplitude = 0f;
    public float wiggleFrequency = 2.5f;

    [Tooltip("Extra yaw applied to the model. Set 180 when the mesh's head points at -Z, so the fish swims head-first.")]
    public float headingOffsetY = 0f;

    [Tooltip("Direction of the circle / figure-eight cruise.")]
    public bool patternClockwise = true;

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
    private float patternPhase;
    private Vector3 baseScale;
    private Coroutine biteRoutine;
    private int lastSwimIndex = -1;
    private int lastBiteIndex = -1;

    // How far ahead of the fish the moving pattern target stays.
    private const float PatternLead = 0.06f;

    void Start()
    {
        if (homePoint != null)
            transform.position = homePoint.position;

        bodyRotation = transform.rotation;
        baseScale = transform.localScale;

        // Offset each fish's animation phase so they do not move identically.
        wigglePhase = Random.value * 100f;

        ResumeCruise();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (State == FishState.Wander)
        {
            if (UsingPattern)
            {
                // Follow a target point that travels along the pattern curve.
                // The phase only advances while the fish keeps up, so the
                // fish smoothly rejoins the curve after each bite.
                Vector3 carrot = PatternPoint(patternPhase);

                if (Vector3.Distance(transform.position, carrot) < PatternLead)
                {
                    float direction = patternClockwise ? -1f : 1f;

                    patternPhase +=
                        direction * (wanderSpeed / PatternRadius()) * 1.2f * dt;
                }

                SwimTowards(carrot, wanderSpeed, dt);
            }
            else
            {
                SwimTowards(wanderTarget, wanderSpeed, dt);

                if (Vector3.Distance(transform.position, wanderTarget) < 0.02f)
                    PickNewWanderTarget();
            }
        }
        else if (State == FishState.Biting)
        {
            ApplySwimVisual(dt, 0f, 0.4f);
        }

        // GoingToBite and Returning are driven by coroutines.
    }

    /// True when a fixed cruise pattern (circle / figure-eight) is
    /// selected and fully configured.
    bool UsingPattern
    {
        get
        {
            if (swimPattern == SwimPattern.Circle)
                return true; // Falls back to wanderCenter/homePoint as center.

            if (swimPattern == SwimPattern.FigureEight)
                return eightPointA != null && eightPointB != null;

            return false;
        }
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
        ResumeCruise();
    }

    // --------------------------------------------------
    // Internal Behaviour
    // --------------------------------------------------

    IEnumerator BiteRoutine(int biteCount)
    {
        for (int i = 0; i < biteCount; i++)
        {
            State = FishState.GoingToBite;

            // Each bite goes to a different random bite point (if assigned).
            Transform target = PickBiteTarget();

            // Approach casually: first drift to a random spot beside the
            // bite point, then close the last bit slowly, like a curious
            // nibble instead of an attack run.
            if (target != null)
            {
                Vector3 side = Random.onUnitSphere;
                side.y *= 0.3f;

                if (side.sqrMagnitude < 0.001f)
                    side = Vector3.right;

                Vector3 approach =
                    target.position + side.normalized * 0.05f;

                yield return SwimToPoint(approach, swimSpeed * 0.8f, 0.02f);
            }

            yield return SwimUntilReached(target, swimSpeed * 0.5f, 12f, true);

            State = FishState.Biting;

            OnBite?.Invoke(this);

            yield return new WaitForSeconds(waitAtBite);

            State = FishState.Returning;

            yield return SwimUntilReached(homePoint, swimSpeed * 0.7f);

            yield return new WaitForSeconds(waitAtHome);
        }

        biteRoutine = null;

        State = FishState.Wander;

        ResumeCruise();
    }

    /// Prepare the default cruise behaviour: sync the pattern phase to the
    /// closest point on the curve, or pick a random wander target.
    void ResumeCruise()
    {
        if (UsingPattern)
        {
            SyncPhaseToPattern();
        }
        else
        {
            // Re-enter the waypoint loop at the nearest point.
            lastSwimIndex = -1;
            PickNewWanderTarget();
        }
    }

    /// Index of the swim point closest to the fish's current position.
    int NearestSwimPointIndex()
    {
        int best = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < swimPoints.Length; i++)
        {
            if (swimPoints[i] == null)
                continue;

            float d = Vector3.Distance(
                transform.position,
                swimPoints[i].position);

            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        return best;
    }

    /// Pick a random bite point, avoiding an immediate repeat.
    /// Falls back to the single bitePoint when no array is assigned.
    Transform PickBiteTarget()
    {
        if (bitePoints != null && bitePoints.Length > 0)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int idx = Random.Range(0, bitePoints.Length);

                if (bitePoints[idx] == null)
                    continue;

                if (bitePoints.Length > 1 && idx == lastBiteIndex)
                    continue;

                lastBiteIndex = idx;
                return bitePoints[idx];
            }

            foreach (var p in bitePoints)
                if (p != null)
                    return p;
        }

        return bitePoint;
    }

    /// Swim to a fixed point with a loose arrival threshold
    /// (used for the casual approach before a bite).
    IEnumerator SwimToPoint(Vector3 point, float speed, float arriveDistance, float timeout = 8f)
    {
        float startTime = Time.time;

        while (Vector3.Distance(transform.position, point) > arriveDistance &&
               Time.time - startTime < timeout)
        {
            SwimTowards(point, speed, Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator SwimUntilReached(Transform target, float speed, float timeout = 12f,
                                 bool aimWithMouth = false)
    {
        if (target == null)
            yield break;

        float startTime = Time.time;

        while (Time.time - startTime < timeout)
        {
            float dt = Time.deltaTime;

            // When aiming with the mouth, steer the pivot towards a goal
            // that sits mouthOffset behind the target along the heading,
            // so the mouth (not the body center) lands on the point.
            Vector3 goal = target.position;

            if (aimWithMouth && mouthOffset > 0f)
                goal -= (bodyRotation * Vector3.forward) * mouthOffset;

            if (Vector3.Distance(transform.position, goal) <= 0.012f)
                break;

            SwimTowards(goal, speed, dt);

            float d = Vector3.Distance(transform.position, goal);

            if (d < 0.05f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    goal,
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

        // Avoidance runs only while wandering or returning home.
        // It is fully disabled while the fish approaches or holds a bite,
        // so the fish can always reach the leg.
        if (avoidObstacles && AvoidanceActive)
        {
            dir = ApplyAvoidance(dir);
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

        // Safety net: gently push the fish out if it overlaps a zone.
        if (avoidObstacles && AvoidanceActive)
        {
            PushOutOfZones(dt);
        }
    }

    /// Avoidance is active only while wandering or returning home.
    bool AvoidanceActive =>
        State == FishState.Wander || State == FishState.Returning;


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
                Quaternion.Euler(0f, wiggle + headingOffsetY, 0f);
        }
    }

    /// Apply steering forces away from all active avoidance zones.
    /// Uses the signed distance to each capsule, so it also works
    /// when the fish is already inside a zone.
    Vector3 ApplyAvoidance(Vector3 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.0000001f)
            return desiredDir;

        Vector3 steer = desiredDir.normalized;

        foreach (var zone in FishAvoidZone.Active)
        {
            if (zone == null)
                continue;

            float signedDist = zone.SignedDistance(
                transform.position,
                out Vector3 outward);

            // Start steering away within avoidDistance of the surface.
            // Inside the capsule (signedDist < 0) the weight grows above 1.
            if (signedDist < avoidDistance)
            {
                float weight = Mathf.Clamp(
                    1f - signedDist / avoidDistance,
                    0f,
                    3f);

                steer += outward * (weight * avoidStrength);
            }
        }

        return steer.normalized;
    }

    /// Safety net: if the fish somehow ends up inside a zone,
    /// gently move it back out along the shortest escape direction.
    void PushOutOfZones(float dt)
    {
        foreach (var zone in FishAvoidZone.Active)
        {
            if (zone == null)
                continue;

            float signedDist = zone.SignedDistance(
                transform.position,
                out Vector3 outward);

            if (signedDist < 0f)
            {
                float step = Mathf.Min(-signedDist, 0.25f * dt);
                transform.position += outward * step;
            }
        }
    }

    /// Returns true if the point is inside (or nearly inside) any avoidance zone.
    bool InsideAnyZone(Vector3 point)
    {
        foreach (var zone in FishAvoidZone.Active)
        {
            if (zone == null)
                continue;

            if (zone.SignedDistance(point, out _) < avoidDistance * 0.5f)
                return true;
        }

        return false;
    }

    // --------------------------------------------------
    // Cruise patterns (circle / figure-eight)
    // --------------------------------------------------

    /// Point on the cruise curve for the given phase (radians).
    /// The height follows the fish's home point with a gentle bob.
    Vector3 PatternPoint(float phase)
    {
        float baseY = homePoint != null
            ? homePoint.position.y
            : transform.position.y;

        float bobY = Mathf.Sin(phase * 0.5f) * (wanderHeight * 0.5f);

        if (swimPattern == SwimPattern.Circle)
        {
            Vector3 c =
                circleCenter != null ? circleCenter.position :
                wanderCenter != null ? wanderCenter.position :
                homePoint != null ? homePoint.position :
                transform.position;

            return new Vector3(
                c.x + Mathf.Cos(phase) * circleRadius,
                baseY + bobY,
                c.z + Mathf.Sin(phase) * circleRadius);
        }

        // Figure-eight (Gerono lemniscate) wrapping both legs:
        // one lobe around each capsule, crossing between them.
        Vector3 pa = eightPointA.position;
        Vector3 pb = eightPointB.position;

        Vector3 center = (pa + pb) * 0.5f;

        Vector3 axis = pb - pa;
        axis.y = 0f;

        float halfDist = axis.magnitude * 0.5f;

        if (halfDist < 0.001f)
        {
            axis = Vector3.right;
            halfDist = 0.05f;
        }
        else
        {
            axis /= halfDist * 2f;
        }

        Vector3 perp = Vector3.Cross(Vector3.up, axis);

        float a = halfDist * eightWidth;
        float s = Mathf.Sin(phase);
        float c2 = Mathf.Cos(phase);

        Vector3 p = center
            + axis * (a * s)
            + perp * (a * s * c2);

        return new Vector3(p.x, baseY + bobY, p.z);
    }

    /// Approximate curve radius, used to convert linear speed
    /// into phase speed.
    float PatternRadius()
    {
        if (swimPattern == SwimPattern.Circle)
            return Mathf.Max(circleRadius, 0.01f);

        if (eightPointA != null && eightPointB != null)
        {
            Vector3 axis = eightPointB.position - eightPointA.position;
            axis.y = 0f;

            return Mathf.Max(axis.magnitude * 0.5f * eightWidth, 0.01f);
        }

        return 0.05f;
    }

    /// Set the phase to the closest point on the curve so the fish
    /// rejoins the pattern smoothly instead of cutting across the bucket.
    void SyncPhaseToPattern()
    {
        const int Samples = 64;

        float bestPhase = 0f;
        float bestDist = float.MaxValue;

        for (int i = 0; i < Samples; i++)
        {
            float t = (i / (float)Samples) * Mathf.PI * 2f;
            float d = Vector3.Distance(transform.position, PatternPoint(t));

            if (d < bestDist)
            {
                bestDist = d;
                bestPhase = t;
            }
        }

        patternPhase = bestPhase;
    }

    /// Select a new random wander destination.
    /// The target should be reasonably far away and outside
    /// all avoidance zones to encourage smooth curved swimming.
    void PickNewWanderTarget()
    {
        // Waypoints mode: visit the swim points strictly in array order
        // (V1 -> V2 -> ... -> V5 -> V1 ...). After a bite the fish
        // re-enters the loop at the nearest point.
        if (swimPattern == SwimPattern.Waypoints &&
            swimPoints != null && swimPoints.Length > 0)
        {
            for (int attempt = 0; attempt < swimPoints.Length; attempt++)
            {
                int idx = lastSwimIndex < 0
                    ? NearestSwimPointIndex()
                    : (lastSwimIndex + 1) % swimPoints.Length;

                lastSwimIndex = idx;

                if (swimPoints[idx] == null)
                    continue; // skip empty slots, keep advancing

                // Small random offset so repeated visits look organic.
                Vector2 jitter = Random.insideUnitCircle * 0.012f;

                wanderTarget = swimPoints[idx].position
                    + new Vector3(jitter.x, 0f, jitter.y);

                return;
            }
        }

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