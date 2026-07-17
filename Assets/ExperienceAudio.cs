using System.Collections;
using UnityEngine;

/// Plays the audio designed in the VR-Doctor-Fish repository
/// (clips in Assets/Resources/Audio/), following its audio/README:
///
///   drfish.mp3            Background music: starts with the experience,
///                         loops throughout, fades out at the end.
///   drfish_fish_se1/2.mp3 One-shot when a small fish nibbles (random pick).
///   drfish_big_se1.mp3    One-shot when a big fish bites.
///   drfish_jelly_se1.mp3  One-shot when the jellyfish stings.
///   (drfish_pop_se1.mp3 is reserved / unused for now.)
public class ExperienceAudio : MonoBehaviour
{
    public ExperienceTimeline timeline;

    [Header("Clips (inside Assets/Resources/<folder>/, no extension)")]
    public string audioFolder = "Audio";
    public string backgroundClip = "drfish";
    public string smallFishClip1 = "drfish_fish_se1";
    public string smallFishClip2 = "drfish_fish_se2";
    public string bigFishClip = "drfish_big_se1";
    public string jellyfishClip = "drfish_jelly_se1";

    [Header("Volumes")]
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Tooltip("Background music fade-out at the end of the experience (seconds).")]
    public float fadeOutDuration = 4f;

    AudioSource musicSource;
    AudioSource sfxSource;

    AudioClip background;
    AudioClip smallFish1;
    AudioClip smallFish2;
    AudioClip bigFish;
    AudioClip jellyfish;

    void Start()
    {
        if (timeline == null)
            timeline = FindFirstObjectByType<ExperienceTimeline>();

        background = Load(backgroundClip);
        smallFish1 = Load(smallFishClip1);
        smallFish2 = Load(smallFishClip2);
        bigFish = Load(bigFishClip);
        jellyfish = Load(jellyfishClip);

        // Two runtime AudioSources: one looping music channel,
        // one channel for overlapping one-shot effects.
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.clip = background;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;

        if (timeline == null)
        {
            Debug.LogWarning("[ExperienceAudio] ExperienceTimeline not found. Audio will not be triggered.");
            return;
        }

        timeline.PhaseChanged += OnPhaseChanged;
        Subscribe(timeline.smallFish, OnSmallFishBite);
        Subscribe(timeline.bigFish, OnBigFishBite);
        Subscribe(timeline.jellyfish, OnJellyfishBite);

        Debug.Log($"[ExperienceAudio] Ready. background={(background != null ? background.name : "MISSING")}, " +
                  $"sfx loaded={(smallFish1 != null) && (smallFish2 != null) && (bigFish != null) && (jellyfish != null)}");
    }

    void OnDestroy()
    {
        if (timeline == null)
            return;

        timeline.PhaseChanged -= OnPhaseChanged;
        Unsubscribe(timeline.smallFish, OnSmallFishBite);
        Unsubscribe(timeline.bigFish, OnBigFishBite);
        Unsubscribe(timeline.jellyfish, OnJellyfishBite);
    }

    // ---------- Phase handling ----------

    void OnPhaseChanged(ExperienceTimeline.Phase phase)
    {
        switch (phase)
        {
            case ExperienceTimeline.Phase.Intro:
                if (background != null && !musicSource.isPlaying)
                {
                    musicSource.volume = musicVolume;
                    musicSource.Play();
                    Debug.Log("[ExperienceAudio] Background music started.");
                }
                break;

            case ExperienceTimeline.Phase.Calm:
                // End of the experience: fade the music out gently.
                if (musicSource.isPlaying)
                    StartCoroutine(FadeOutMusic());
                break;
        }
    }

    IEnumerator FadeOutMusic()
    {
        float startVolume = musicSource.volume;

        for (float t = 0f; t < fadeOutDuration; t += Time.deltaTime)
        {
            musicSource.volume =
                Mathf.Lerp(startVolume, 0f, t / fadeOutDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = musicVolume;
    }

    // ---------- Bite one-shots ----------

    void OnSmallFishBite(FishController fish)
    {
        // Two nibble variations, picked at random.
        PlaySfx(Random.value < 0.5f ? smallFish1 : smallFish2);
    }

    void OnBigFishBite(FishController fish)
    {
        PlaySfx(bigFish);
    }

    void OnJellyfishBite(FishController fish)
    {
        PlaySfx(jellyfish);
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    // ---------- Loading ----------

    AudioClip Load(string clipName)
    {
        var clip = Resources.Load<AudioClip>(audioFolder + "/" + clipName);

        if (clip == null)
            Debug.LogWarning($"[ExperienceAudio] Audio clip not found: Resources/{audioFolder}/{clipName}");

        return clip;
    }

    // ---------- Subscription helpers ----------

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
}
