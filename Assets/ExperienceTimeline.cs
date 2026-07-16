using System.Collections;
using UnityEngine;

/// 整个体验的时间轴（总长 120 秒）：
///   0s   开场：水流轻抚（震动之后再接），所有鱼在桶内随机游动
///  30s   4 条小鱼多次轮流咬用户下肢
///  60s   3 条大鱼咬用户小腿
///  90s   水母咬用户小腿（模型还没放进去，数组先留空即可）
/// 110s   所有归于平静，鱼回到随机游动，水流轻抚
/// 120s   体验结束
public class ExperienceTimeline : MonoBehaviour
{
    [Header("鱼的分组")]
    public FishController[] smallFish;   // 4条小鱼
    public FishController[] bigFish;     // 3条大鱼
    public FishController[] jellyfish;   // 水母（模型放进场景后再拖进来）

    [Header("时间点（秒）")]
    public float totalDuration = 120f;
    public float smallFishBiteTime = 30f;
    public float bigFishBiteTime = 60f;
    public float jellyfishBiteTime = 90f;
    public float calmDownTime = 110f;    // 收尾：所有鱼归于平静

    [Header("咬钩参数")]
    [Tooltip("同一组的鱼依次出发的间隔（秒），做出“轮流咬”的效果")]
    public float turnInterval = 2f;
    public int smallFishBiteCount = 3;   // 每条小鱼咬几次
    public int bigFishBiteCount = 2;
    public int jellyfishBiteCount = 1;

    [Header("其他")]
    public bool playOnStart = true;

    private Coroutine timelineRoutine;

    void Start()
    {
        if (playOnStart)
            Play();
    }

    /// 从头开始播放整个体验
    public void Play()
    {
        if (timelineRoutine != null)
            StopCoroutine(timelineRoutine);
        timelineRoutine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        float startTime = Time.time;
        Debug.Log("[Timeline] 0s 体验开始：水流轻抚，所有鱼随机游动");
        // TODO: 这里之后触发“水流轻抚”的震动

        // ---- 30s：小鱼多次轮流咬下肢 ----
        yield return WaitUntil(startTime, smallFishBiteTime);
        Debug.Log("[Timeline] 30s：小鱼开始轮流咬下肢");
        yield return StartGroupInTurns(smallFish, smallFishBiteCount);

        // ---- 60s：大鱼咬小腿 ----
        yield return WaitUntil(startTime, bigFishBiteTime);
        Debug.Log("[Timeline] 60s：大鱼开始咬小腿");
        StopGroup(smallFish);                       // 小鱼回到随机游动
        yield return StartGroupInTurns(bigFish, bigFishBiteCount);

        // ---- 90s：水母咬小腿 ----
        yield return WaitUntil(startTime, jellyfishBiteTime);
        Debug.Log("[Timeline] 90s：水母咬小腿");
        StopGroup(bigFish);
        yield return StartGroupInTurns(jellyfish, jellyfishBiteCount);

        // ---- 110s：归于平静 ----
        yield return WaitUntil(startTime, calmDownTime);
        Debug.Log("[Timeline] 110s 归于平静：所有鱼回到随机游动，水流轻抚");
        StopGroup(smallFish);
        StopGroup(bigFish);
        StopGroup(jellyfish);
        // TODO: 这里之后触发“水流轻抚”的震动

        // ---- 120s：结束 ----
        yield return WaitUntil(startTime, totalDuration);
        Debug.Log("[Timeline] 120s 体验结束");
        timelineRoutine = null;
    }

    // 同一组的鱼每隔 turnInterval 秒依次出发，形成轮流咬的效果
    IEnumerator StartGroupInTurns(FishController[] group, int biteCount)
    {
        if (group == null)
            yield break;

        foreach (var fish in group)
        {
            if (fish == null)
                continue;
            fish.StartBiting(biteCount);
            yield return new WaitForSeconds(turnInterval);
        }
    }

    void StopGroup(FishController[] group)
    {
        if (group == null)
            return;

        foreach (var fish in group)
        {
            if (fish != null)
                fish.StopBiting();
        }
    }

    // 等到时间轴上的绝对时间点 t（秒）
    IEnumerator WaitUntil(float startTime, float t)
    {
        while (Time.time - startTime < t)
            yield return null;
    }
}
