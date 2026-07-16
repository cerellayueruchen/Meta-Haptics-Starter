using System.Collections;
using UnityEngine;

/// Controls the entire experience timeline (60 seconds total):
///   0s      Intro: gentle water flow, all fish swim randomly inside the bucket
///   15-30s  Small fish frenzy: dispatch one small fish every second (4 fish rotating)
///   30-45s  Big fish attacks: dispatch one big fish every 2 seconds (2 fish rotating)
///   48s/55s Jellyfish attack once at each time point
///   60s     Experience ends and returns to a calm state
public class ExperienceTimeline : MonoBehaviour
{
    [Header("Fish Groups")]
    public FishController[] smallFish;   // Four small fish (FishV1–V4)
    public FishController[] bigFish;     // Big fish (fish01 / fish02)
    public FishController[] jellyfish;   // Jellyfish

    [Header("Total Duration (seconds)")]
    public float totalDuration = 60f;

    [Header("Small Fish Frenzy")]
    public float smallFishStartTime = 15f;
    public float smallFishEndTime = 30f;
    public float smallFishInterval = 1f;   // Dispatch one fish every second

    [Header("Big Fish Attacks")]
    public float bigFishStartTime = 30f;
    public float bigFishEndTime = 45f;
    public float bigFishInterval = 2f;     // Dispatch one fish every 2 seconds

    [Header("Jellyfish Attack Times")]
    public float[] jellyfishAttackTimes = { 48f, 55f };

    [Header("General Settings")]
    public bool playOnStart = true;

    private Coroutine timelineRoutine;

    void Start()
    {
        if (playOnStart)
            Play();
    }

    /// Starts the entire experience from the beginning.
    public void Play()
    {
        if (timelineRoutine != null)
            StopCoroutine(timelineRoutine);

        timelineRoutine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        float startTime = Time.time;

        Debug.Log("[Timeline] 0s: Experience started. Gentle water flow, all fish swim randomly.");

        // ---- 15–30s: Small fish frenzy (one fish every second) ----
        yield return DispatchLoop(
            smallFish,
            startTime,
            smallFishStartTime,
            smallFishEndTime,
            smallFishInterval,
            "Small Fish"
        );

        StopGroup(smallFish);

        // ---- 30–45s: Big fish attacks (one fish every 2 seconds) ----
        yield return DispatchLoop(
            bigFish,
            startTime,
            bigFishStartTime,
            bigFishEndTime,
            bigFishInterval,
            "Big Fish"
        );

        StopGroup(bigFish);

        // ---- 48s / 55s: Jellyfish attacks ----
        foreach (float t in jellyfishAttackTimes)
        {
            yield return WaitUntil(startTime, t);

            Debug.Log($"[Timeline] {t}s: Jellyfish attack.");

            foreach (var jelly in jellyfish)
            {
                if (jelly != null)
                    jelly.StartBiting(1);
            }
        }

        // ---- 60s: Experience ends and returns to calm ----
        yield return WaitUntil(startTime, totalDuration);

        StopGroup(smallFish);
        StopGroup(bigFish);
        StopGroup(jellyfish);

        Debug.Log("[Timeline] 60s: Experience finished. Returning to a calm state.");

        timelineRoutine = null;
    }

    // During the interval [from, to), dispatch one fish from the group
    // every 'interval' seconds in a round-robin manner.
    // Uses absolute timestamps to avoid accumulated timing errors.
    IEnumerator DispatchLoop(
        FishController[] group,
        float startTime,
        float from,
        float to,
        float interval,
        string label)
    {
        if (group == null || group.Length == 0)
            yield break;

        yield return WaitUntil(startTime, from);

        Debug.Log($"[Timeline] {from}s: {label} attacks begin (one fish every {interval}s).");

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

    // Wait until the timeline reaches the absolute timestamp t (seconds).
    IEnumerator WaitUntil(float startTime, float t)
    {
        while (Time.time - startTime < t)
            yield return null;
    }
}