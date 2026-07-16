using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Drives the VR Doctor Fish timed experience.
/// Stage order, fixed durations and the Calm-stage music fade are controlled
/// here. Procedural events (nibbles, stings) and the big fish bite
/// choreography are owned by their own controllers, which listen to the
/// stage events raised by this manager.
///
/// Timeline (total 2:01):
///   0:00-0:12  Welcome / Entering the Water
///   0:12-0:57  Small Fish
///   0:57-1:19  Big Fish
///   1:19-1:41  Jellyfish
///   1:41-2:01  Calm / Recovery (music fades 1:51-1:59)
///   2:01       Session Complete
/// </summary>
public class ExperienceStateManager : MonoBehaviour
{
    public enum Stage
    {
        Idle,       // Before Start Experience is activated
        Welcome,    // Scene 1: Welcome / Entering the Water
        SmallFish,  // Scene 2: Small Fish
        BigFish,    // Scene 3: Big Fish
        Jellyfish,  // Scene 4: Jellyfish
        Calm,       // Scene 5: Calm / Recovery
        Complete    // Timed sequence finished, creatures keep swimming gently
    }

    [Header("Fixed Stage Durations (seconds)")]
    public float welcomeDuration = 12f;
    public float smallFishDuration = 45f;
    public float bigFishDuration = 22f;
    public float jellyfishDuration = 22f;
    public float calmDuration = 20f;

    [Header("Transitions")]
    [Tooltip("Lighting, fog and water appearance blend over this time instead of changing instantly.")]
    public float stageBlendDuration = 2.5f;

    [Header("Music")]
    public AudioSource backgroundMusic;
    [Tooltip("Music fades in over this time from 0:00.")]
    public float musicFadeInDuration = 2f;
    [Tooltip("Music fade-out starts this long after the Calm stage begins (1:51 overall).")]
    public float musicFadeOutDelayIntoCalm = 10f;
    [Tooltip("Music fades out over this time (1:51-1:59).")]
    public float musicFadeOutDuration = 8f;

    [Header("Audio")]
    [Tooltip("Played once at 0:00 as the virtual water appears.")]
    public AudioSource waterEntrySound;

    [Header("Stage Events")]
    public UnityEvent onWelcome;
    public UnityEvent onSmallFish;
    public UnityEvent onBigFish;
    public UnityEvent onJellyfish;
    public UnityEvent onCalm;
    public UnityEvent onComplete;

    public Stage CurrentStage { get; private set; } = Stage.Idle;

    /// <summary>Seconds since Start Experience was activated. Zero while idle.</summary>
    public float ElapsedTime { get; private set; }

    private Coroutine sequenceRoutine;
    private float musicBaseVolume = 1f;

    void Awake()
    {
        if (backgroundMusic != null)
            musicBaseVolume = backgroundMusic.volume;
    }

    /// <summary>Begins the timed sequence. Called by the Start Experience trigger.</summary>
    public void StartExperience()
    {
        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);

        sequenceRoutine = StartCoroutine(RunSequence());
    }

    /// <summary>Stops the sequence and returns to idle, ready for a restart.</summary>
    public void ResetExperience()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        if (backgroundMusic != null)
        {
            backgroundMusic.Stop();
            backgroundMusic.volume = musicBaseVolume;
        }

        ElapsedTime = 0f;
        CurrentStage = Stage.Idle;
    }

    IEnumerator RunSequence()
    {
        ElapsedTime = 0f;

        // 0:00 - water entry sound and music fade-in
        if (waterEntrySound != null)
            waterEntrySound.Play();

        if (backgroundMusic != null)
        {
            backgroundMusic.volume = 0f;
            backgroundMusic.Play();
            StartCoroutine(FadeMusic(0f, musicBaseVolume, musicFadeInDuration));
        }

        yield return RunStage(Stage.Welcome, onWelcome, welcomeDuration);
        yield return RunStage(Stage.SmallFish, onSmallFish, smallFishDuration);
        yield return RunStage(Stage.BigFish, onBigFish, bigFishDuration);
        yield return RunStage(Stage.Jellyfish, onJellyfish, jellyfishDuration);

        // Calm stage owns the music fade-out (starts 1:51, ends 1:59)
        CurrentStage = Stage.Calm;
        onCalm.Invoke();

        float calmElapsed = 0f;
        bool fadeStarted = false;

        while (calmElapsed < calmDuration)
        {
            calmElapsed += Time.deltaTime;
            ElapsedTime += Time.deltaTime;

            if (!fadeStarted && calmElapsed >= musicFadeOutDelayIntoCalm)
            {
                fadeStarted = true;
                if (backgroundMusic != null)
                    StartCoroutine(FadeMusic(backgroundMusic.volume, 0f, musicFadeOutDuration));
            }

            yield return null;
        }

        // 2:01 - session complete, creatures keep swimming gently
        CurrentStage = Stage.Complete;
        onComplete.Invoke();
        sequenceRoutine = null;
    }

    IEnumerator RunStage(Stage stage, UnityEvent stageEvent, float duration)
    {
        CurrentStage = stage;
        stageEvent.Invoke();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ElapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator FadeMusic(float from, float to, float duration)
    {
        if (backgroundMusic == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            backgroundMusic.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        backgroundMusic.volume = to;

        if (Mathf.Approximately(to, 0f))
            backgroundMusic.Stop();
    }
}
