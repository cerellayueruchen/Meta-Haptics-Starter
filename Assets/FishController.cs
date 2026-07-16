using System.Collections;
using UnityEngine;

/// 鱼的行为控制：
/// 默认状态 = 在桶内随机游动（Wander）
/// 收到 StartBiting() 命令后 = 从当前位置游到咬钩点咬住 → 回家 → 重复若干次 → 回到随机游动
public class FishController : MonoBehaviour
{
    public enum FishState { Wander, GoingToBite, Biting, Returning }

    [Header("Target Points")]
    public Transform homePoint;
    public Transform bitePoint;

    [Header("Wander (随机游动)")]
    [Tooltip("随机游动的中心点，不填则以 homePoint 为中心")]
    public Transform wanderCenter;
    public float wanderRadius = 0.06f;   // 水平随机范围（米）
    public float wanderHeight = 0.03f;   // 垂直随机范围（米）

    [Header("Movement")]
    public float swimSpeed = 0.2f;
    public float biteSwimSpeed = 0.35f;  // 冲向咬钩点时稍快一点
    public float rotateSpeed = 3f;

    [Header("Behaviour")]
    public float waitAtHome = 2f;        // 两次咬之间在家停留的时间
    public float waitAtBite = 1f;        // 咬住时停留的时间

    /// 咬到用户的瞬间触发（之后接震动就订阅这个事件）
    public event System.Action<FishController> OnBite;

    public FishState State { get; private set; } = FishState.Wander;

    private Vector3 wanderTarget;
    private float wanderPause;
    private Coroutine biteRoutine;

    void Start()
    {
        if (homePoint != null)
        {
            transform.position = homePoint.position;
            transform.rotation = homePoint.rotation;
        }
        PickNewWanderTarget();
    }

    void Update()
    {
        // 咬钩流程由协程驱动，这里只负责默认的随机游动
        if (State != FishState.Wander)
            return;

        if (wanderPause > 0f)
        {
            wanderPause -= Time.deltaTime;
            return;
        }

        MoveTowards(wanderTarget, swimSpeed);

        if (Vector3.Distance(transform.position, wanderTarget) < 0.01f)
        {
            wanderPause = Random.Range(0.5f, 1.5f);
            PickNewWanderTarget();
        }
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
            yield return MoveUntilReached(bitePoint, biteSwimSpeed);

            State = FishState.Biting;
            OnBite?.Invoke(this);   // TODO: 之后在这里触发咬钩震动
            yield return new WaitForSeconds(waitAtBite);

            State = FishState.Returning;
            yield return MoveUntilReached(homePoint, swimSpeed);
            yield return new WaitForSeconds(waitAtHome);
        }

        biteRoutine = null;
        State = FishState.Wander;
        PickNewWanderTarget();
    }

    IEnumerator MoveUntilReached(Transform target, float speed)
    {
        if (target == null)
            yield break;

        while (Vector3.Distance(transform.position, target.position) > 0.01f)
        {
            MoveTowards(target.position, speed);
            yield return null;
        }
    }

    void MoveTowards(Vector3 target, float speed)
    {
        Vector3 direction = target - transform.position;

        // 转向
        if (direction.magnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime);
        }

        // 移动
        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            speed * Time.deltaTime);
    }

    void PickNewWanderTarget()
    {
        Vector3 center = wanderCenter != null ? wanderCenter.position
                       : homePoint != null ? homePoint.position
                       : transform.position;

        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        wanderTarget = center + new Vector3(offset.x, Random.Range(-wanderHeight, wanderHeight), offset.y);
    }
}
