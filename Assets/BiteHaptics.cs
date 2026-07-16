using System.Collections;
using UnityEngine;

/// 鱼咬人 → 给 VibraForge 发震动指令。
/// 订阅 Timeline 三组鱼的 OnBite 事件，按鱼的种类发不同风格的震动。
///
/// 执行器地址映射（来自 VR-Doctor-Fish 仓库 haptics/README）：
///   左腿：前侧小执行器 0/2/4，后侧小执行器 16/18，大执行器 32
///   右腿：左腿地址 +1（前 1/3/5，后 17/19，大 33）
/// 指令格式：SendCommand(addr, mode(0停/1震), duty(0-15强度), freq(0-7频率))
public class BiteHaptics : MonoBehaviour
{
    public VibraForge vibraForge;
    public ExperienceTimeline timeline;

    [Header("每种鱼用的执行器地址（每次咬随机挑一个）")]
    public int[] smallFishAddrs = { 0, 1, 2, 3, 4, 5 };   // 前侧小执行器
    public int[] bigFishAddrs = { 32, 33 };               // 大执行器
    public int[] jellyfishAddrs = { 16, 17, 18, 19 };     // 后侧小执行器

    [Header("小鱼：轻痒的两下点触")]
    [Range(0, 15)] public int smallFishDuty = 5;
    [Range(0, 7)] public int smallFishFreq = 3;

    [Header("大鱼：一口重咬后衰减")]
    [Range(0, 15)] public int bigFishDuty = 12;
    [Range(0, 7)] public int bigFishFreq = 2;

    [Header("水母：短促的最大强度刺痛")]
    [Range(0, 15)] public int jellyfishDuty = 15;
    [Range(0, 7)] public int jellyfishFreq = 7;

    void Start()
    {
        if (timeline == null)
            timeline = FindFirstObjectByType<ExperienceTimeline>();
        if (vibraForge == null)
            vibraForge = FindFirstObjectByType<VibraForge>();

        if (timeline == null)
        {
            Debug.LogWarning("[BiteHaptics] 找不到 ExperienceTimeline，震动不会触发");
            return;
        }

        Subscribe(timeline.smallFish, OnSmallFishBite);
        Subscribe(timeline.bigFish, OnBigFishBite);
        Subscribe(timeline.jellyfish, OnJellyfishBite);
    }

    void OnDestroy()
    {
        if (timeline == null)
            return;
        Unsubscribe(timeline.smallFish, OnSmallFishBite);
        Unsubscribe(timeline.bigFish, OnBigFishBite);
        Unsubscribe(timeline.jellyfish, OnJellyfishBite);
    }

    void Subscribe(FishController[] group, System.Action<FishController> handler)
    {
        if (group == null) return;
        foreach (var fish in group)
            if (fish != null) fish.OnBite += handler;
    }

    void Unsubscribe(FishController[] group, System.Action<FishController> handler)
    {
        if (group == null) return;
        foreach (var fish in group)
            if (fish != null) fish.OnBite -= handler;
    }

    // ---------- 三种咬的震动模式 ----------

    void OnSmallFishBite(FishController fish)
    {
        StartCoroutine(SmallFishPattern(Pick(smallFishAddrs)));
    }

    void OnBigFishBite(FishController fish)
    {
        StartCoroutine(BigFishPattern(Pick(bigFishAddrs)));
    }

    void OnJellyfishBite(FishController fish)
    {
        StartCoroutine(JellyfishPattern(Pick(jellyfishAddrs)));
    }

    // 小鱼：轻轻两下点触
    IEnumerator SmallFishPattern(int addr)
    {
        Send(addr, 1, smallFishDuty, smallFishFreq);
        yield return new WaitForSeconds(0.12f);
        Send(addr, 0, 0, smallFishFreq);
        yield return new WaitForSeconds(0.08f);
        Send(addr, 1, smallFishDuty, smallFishFreq);
        yield return new WaitForSeconds(0.12f);
        Send(addr, 0, 0, smallFishFreq);
    }

    // 大鱼：重咬 0.4s，衰减 0.3s，停止
    IEnumerator BigFishPattern(int addr)
    {
        Send(addr, 1, bigFishDuty, bigFishFreq);
        yield return new WaitForSeconds(0.4f);
        Send(addr, 1, bigFishDuty / 2, bigFishFreq);
        yield return new WaitForSeconds(0.3f);
        Send(addr, 0, 0, bigFishFreq);
    }

    // 水母：0.3s 最大强度高频刺痛
    IEnumerator JellyfishPattern(int addr)
    {
        Send(addr, 1, jellyfishDuty, jellyfishFreq);
        yield return new WaitForSeconds(0.3f);
        Send(addr, 0, 0, jellyfishFreq);
    }

    // ---------- 工具 ----------

    int Pick(int[] addrs)
    {
        if (addrs == null || addrs.Length == 0)
            return 0;
        return addrs[Random.Range(0, addrs.Length)];
    }

    void Send(int addr, int mode, int duty, int freq)
    {
        if (vibraForge == null)
            return;
        vibraForge.SendCommand(addr, mode, duty, freq);
    }
}
