using System.Collections;
using UnityEngine;

/// Sends haptic commands to VibraForge when fish bite the user.
/// Subscribes to the OnBite events from the three fish groups controlled
/// by the Timeline and triggers different vibration patterns.
///
/// Actuator address mapping (from VR-Doctor-Fish haptics/README):
///   Left leg: front small actuators 0/2/4, rear small actuators 16/18, large actuator 32
///   Right leg: left leg addresses +1 (front 1/3/5, rear 17/19, large 33)
/// Command format:
///   SendCommand(addr, mode(0=Off, 1=Vibrate), duty(0-15 intensity), freq(0-7 frequency))
public class BiteHaptics : MonoBehaviour
{
    public VibraForge vibraForge;
    public ExperienceTimeline timeline;

    [Header("Actuator addresses for each fish type (randomly selected for each bite)")]
    public int[] smallFishAddrs = { 0, 1, 2, 3, 4, 5 };   // Front small actuators
    public int[] bigFishAddrs = { 32, 33 };               // Large actuators
    public int[] jellyfishAddrs = { 16, 17, 18, 19 };     // Rear small actuators

    [Header("Small fish: two gentle tickling pulses")]
    [Range(0, 15)] public int smallFishDuty = 5;
    [Range(0, 7)] public int smallFishFreq = 3;

    [Header("Big fish: one strong bite followed by a fade-out")]
    [Range(0, 15)] public int bigFishDuty = 12;
    [Range(0, 7)] public int bigFishFreq = 2;

    [Header("Jellyfish: short, high-intensity sting")]
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
            Debug.LogWarning("[BiteHaptics] ExperienceTimeline not found. Haptic feedback will not be triggered.");
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
            if (fish != null)
                fish.OnBite += handler;
    }

    void Unsubscribe(FishController[] group, System.Action<FishController> handler)
    {
        if (group == null) return;

        foreach (var fish in group)
            if (fish != null)
                fish.OnBite -= handler;
    }

    // ---------- Haptic patterns for different fish types ----------

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

    // Small fish: two light vibration pulses
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

    // Big fish: strong bite, gradual fade, then stop
    IEnumerator BigFishPattern(int addr)
    {
        Send(addr, 1, bigFishDuty, bigFishFreq);
        yield return new WaitForSeconds(0.4f);

        Send(addr, 1, bigFishDuty / 2, bigFishFreq);
        yield return new WaitForSeconds(0.3f);

        Send(addr, 0, 0, bigFishFreq);
    }

    // Jellyfish: short, high-frequency sting
    IEnumerator JellyfishPattern(int addr)
    {
        Send(addr, 1, jellyfishDuty, jellyfishFreq);
        yield return new WaitForSeconds(0.3f);

        Send(addr, 0, 0, jellyfishFreq);
    }

    // ---------- Utility methods ----------

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