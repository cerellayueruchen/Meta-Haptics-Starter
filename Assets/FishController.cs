using System.Collections;
using UnityEngine;

/// 鱼的行为控制（程序化游动，适用于没有骨骼动画的静态鱼模型）：
/// 默认状态 = 在桶内随机游动（Wander）
/// 收到 StartBiting() 命令后 = 游到咬钩点咬住 → 回家 → 重复若干次 → 回到随机游动
///
/// 游动原理：鱼永远只沿自己头部朝向前进，通过平滑转向画出弧线，
/// 同时叠加左右摆尾，让静态模型看起来也像在游。
public class FishController : MonoBehaviour
{
    public enum FishState { Wander, GoingToBite, Biting, Returning }

    [Header("Target Points")]
    public Transform homePoint;
    public Transform bitePoint;

    [Header("Wander (随机游动)")]
    [Tooltip("随机游动的中心点，不填则以 homePoint 为中心")]
    public Transform wanderCenter;
    public float wanderRadius = 0.07f;   // 水平随机范围（米）
    public float wanderHeight = 0.03f;   // 垂直随机范围（米）
    public float wanderSpeed = 0.05f;    // 闲逛速度（慢慢游）

    [Header("Movement")]
    public float swimSpeed = 0.2f;       // 去咬钩/回家时的速度
    public float rotateSpeed = 3f;       // 转向的平滑速度

    [Header("游动姿态")]
    public float wiggleAmplitude = 10f;  // 摆尾角度（度）
    public float wiggleFrequency = 2.5f; // 摆尾频率（次/秒）
    [Range(0f, 1f)]
    public float pitchFlatten = 0.35f;   // 俯仰压平：越小鱼越不会大幅抬头/低头

    [Header("避让（腿等障碍，见 FishAvoidZone）")]
    public bool avoidObstacles = true;
    public float avoidDistance = 0.03f;  // 距避让区表面多远开始绕
    public float avoidStrength = 2f;     // 绕开的力度

    [Header("Behaviour")]
    public float waitAtHome = 2f;        // 两次咬之间在家停留
    public float waitAtBite = 1f;        // 咬住时停留

    /// 咬到用户的瞬间触发（之后接震动就订阅这个事件）
    public event System.Action<FishController> OnBite;

    public FishState State { get; private set; } = FishState.Wander;

    private Vector3 wanderTarget;
    private Quaternion bodyRotation;     // 平滑转向的基础朝向（摆尾叠加在它上面）
    private float wigglePhase;
    private Coroutine biteRoutine;

    void Start()
    {
        if (homePoint != null)
            transform.position = homePoint.position;

        bodyRotation = transform.rotation;
        wigglePhase = Random.value * 100f;   // 每条鱼相位错开，不会同步摆尾
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
            // 咬住时原地轻轻摆动
            wigglePhase += dt * wiggleFrequency * Mathf.PI * 2f * 0.5f;
            transform.rotation = bodyRotation
                * Quaternion.Euler(0f, Mathf.Sin(wigglePhase) * wiggleAmplitude * 0.4f, 0f);
        }
        // GoingToBite / Returning 由协程每帧调用 SwimTowards 驱动
    }

    // ---------- 外部命令（由 ExperienceTimeline 调用） ----------

    /// 命令：去咬用户 biteCount 次（每次：游到咬钩点 → 咬住 → 回家）
    public void StartBiting(int biteCount)
    {
        StopBiting();
        biteRoutine = StartCoroutine(BiteRoutine(biteCount));
    }

    /// 命令：停止咬钩，回到随机游动
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

    // ---------- 内部逻辑 ----------

    IEnumerator BiteRoutine(int biteCount)
    {
        for (int i = 0; i < biteCount; i++)
        {
            State = FishState.GoingToBite;
            yield return SwimUntilReached(bitePoint, swimSpeed);

            State = FishState.Biting;
            OnBite?.Invoke(this);   // TODO: 之后在这里触发咬钩震动
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
        while (Vector3.Distance(transform.position, target.position) > 0.012f
               && Time.time - startTime < timeout)
        {
            float dt = Time.deltaTime;
            SwimTowards(target.position, speed, dt);

            // 距离很近时额外向目标吸附一点，避免绕着目标转圈到不了
            float d = Vector3.Distance(transform.position, target.position);
            if (d < 0.05f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, target.position, speed * 0.8f * dt);
            }
            yield return null;
        }
    }

    /// 核心游动：平滑转向 + 沿头部朝向前进 + 摆尾
    void SwimTowards(Vector3 target, float speed, float dt)
    {
        if (dt <= 0f)
            return;

        // 期望朝向（俯仰压平，鱼不会大幅抬头低头）
        Vector3 dir = target - transform.position;
        dir.y *= pitchFlatten;
        if (avoidObstacles)
            dir = ApplyAvoidance(dir, target);
        if (dir.sqrMagnitude > 0.0000001f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized);
            bodyRotation = Quaternion.Slerp(bodyRotation, look, rotateSpeed * dt);
        }

        // 只沿自己的朝向往前游（转弯自然形成弧线，而不是横向平移）
        transform.position += bodyRotation * Vector3.forward * (speed * dt);

        // 垂直方向单独缓慢修正，保证能游到目标高度
        float newY = Mathf.MoveTowards(transform.position.y, target.y, speed * 0.5f * dt);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // 摆尾：游得越快摆得越快
        wigglePhase += dt * wiggleFrequency * Mathf.PI * 2f * (1f + speed * 4f);
        float wiggle = Mathf.Sin(wigglePhase) * wiggleAmplitude;
        transform.rotation = bodyRotation * Quaternion.Euler(0f, wiggle, 0f);

        // 保险：万一已经和避让区重叠，缓缓推出去
        if (avoidObstacles)
            PushOutOfZones(target, dt);
    }

    /// 转向避让：把"远离所有避让区"的分量叠加到期望方向上。
    /// 目标点本身在某个避让区里/附近时（= 要咬的那条腿），忽略那个区，鱼才咬得到。
    Vector3 ApplyAvoidance(Vector3 desiredDir, Vector3 finalTarget)
    {
        if (desiredDir.sqrMagnitude < 0.0000001f)
            return desiredDir;

        Vector3 steer = desiredDir.normalized;
        foreach (var zone in FishAvoidZone.Active)
        {
            if (zone == null)
                continue;
            if (Vector3.Distance(finalTarget, zone.transform.position) < zone.radius + 0.02f)
                continue;   // 目标就在这个区里，不避让它

            Vector3 away = transform.position - zone.transform.position;
            float d = away.magnitude;
            float influence = zone.radius + avoidDistance;
            if (d < influence && d > 0.0001f)
            {
                steer += (away / d) * ((1f - d / influence) * avoidStrength);
            }
        }
        return steer;
    }

    /// 位置修正：已经进入避让区内部时，往外轻推（不瞬移）
    void PushOutOfZones(Vector3 finalTarget, float dt)
    {
        foreach (var zone in FishAvoidZone.Active)
        {
            if (zone == null)
                continue;
            if (Vector3.Distance(finalTarget, zone.transform.position) < zone.radius + 0.02f)
                continue;

            Vector3 away = transform.position - zone.transform.position;
            float d = away.magnitude;
            if (d < zone.radius)
            {
                Vector3 outDir = d > 0.0001f ? away / d : Vector3.up;
                float newDist = Mathf.MoveTowards(d, zone.radius, 0.15f * dt);
                transform.position = zone.transform.position + outDir * newDist;
            }
        }
    }

    bool InsideAnyZone(Vector3 point)
    {
        foreach (var zone in FishAvoidZone.Active)
        {
            if (zone == null)
                continue;
            if (Vector3.Distance(point, zone.transform.position) < zone.radius + 0.01f)
                return true;
        }
        return false;
    }

    void PickNewWanderTarget()
    {
        Vector3 center = wanderCenter != null ? wanderCenter.position
                       : homePoint != null ? homePoint.position
                       : transform.position;

        // 尽量挑离当前位置远一点的点，让鱼画出完整的弧线
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = center + new Vector3(
                offset.x, Random.Range(-wanderHeight, wanderHeight), offset.y);

            if (Vector3.Distance(candidate, transform.position) > wanderRadius * 0.8f
                && !InsideAnyZone(candidate))
            {
                wanderTarget = candidate;
                return;
            }
        }
        wanderTarget = center;
    }
}
