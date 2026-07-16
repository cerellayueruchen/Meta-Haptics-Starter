using System.Collections;
using UnityEngine;

/// 整个体验的时间轴（总长 60 秒）：
///   0s      开场：水流轻抚，所有鱼在桶内随机游动
///  15-30s   小鱼疯狂咬：每 1 秒派一条小鱼冲去咬（4条轮换）
///  30-45s   大鱼咬：每 2 秒派一条大鱼咬一次（2条轮换）
///  48s/55s  水母各攻击一次
///  60s      体验结束，归于平静
public class ExperienceTimeline : MonoBehaviour
{
    [Header("鱼的分组")]
    public FishController[] smallFish;   // 4条小鱼 FishV1-V4
    public FishController[] bigFish;     // 大鱼 fish01/fish02
    public FishController[] jellyfish;   // 水母

    [Header("总时长（秒）")]
    public float totalDuration = 60f;

    [Header("小鱼疯狂咬")]
    public float smallFishStartTime = 15f;
    public float smallFishEndTime = 30f;
    public float smallFishInterval = 1f;   // 每 1 秒派一条

    [Header("大鱼咬")]
    public float bigFishStartTime = 30f;
    public float bigFishEndTime = 45f;
    public float bigFishInterval = 2f;     // 每 2 秒派一条

    [Header("水母攻击时间点")]
    public float[] jellyfishAttackTimes = { 48f, 55f };

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

        // ---- 15-30s：小鱼疯狂咬，每秒一条 ----
        yield return DispatchLoop(smallFish, startTime,
            smallFishStartTime, smallFishEndTime, smallFishInterval, "小鱼");
        StopGroup(smallFish);

        // ---- 30-45s：大鱼咬，每2秒一条 ----
        yield return DispatchLoop(bigFish, startTime,
            bigFishStartTime, bigFishEndTime, bigFishInterval, "大鱼");
        StopGroup(bigFish);

        // ---- 48s / 55s：水母攻击 ----
        foreach (float t in jellyfishAttackTimes)
        {
            yield return WaitUntil(startTime, t);
            Debug.Log($"[Timeline] {t}s：水母发起攻击");
            foreach (var jelly in jellyfish)
            {
                if (jelly != null)
                    jelly.StartBiting(1);
            }
        }

        // ---- 60s：结束，归于平静 ----
        yield return WaitUntil(startTime, totalDuration);
        StopGroup(smallFish);
        StopGroup(bigFish);
        StopGroup(jellyfish);
        Debug.Log("[Timeline] 60s 体验结束：归于平静");
        timelineRoutine = null;
    }

    // 在 [from, to) 时间段内，每隔 interval 秒轮换派出组里的一条鱼去咬一次。
    // 用绝对时间点调度，不会累积误差。
    IEnumerator DispatchLoop(FishController[] group, float startTime,
                             float from, float to, float interval, string label)
    {
        if (group == null || group.Length == 0)
            yield break;

        yield return WaitUntil(startTime, from);
        Debug.Log($"[Timeline] {from}s：{label}开始咬（每 {interval}s 一条）");

        int index = 0;
        float nextDispatch = from;
        while (Time.time - startTime < to)
        {
            if (Time.time - startTime >= nextDispatch)
            {
                var fish = group[index % group.Length];
                if (fish != null)
                    fish.StartBiting(1);
                index++;
                nextDispatch += interval;
            }
            yield return null;
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
