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

        LogAudioDiagnostics();

        // The experience starts at 0s, so start the music right away instead
        // of waiting for the Intro phase event. This keeps the music playing
        // even if the timeline reference or event wiring is broken.
        if (background != null)
        {
            musicSource.Play();
            Debug.Log("[ExperienceAudio] Background music started.");
        }

        if (timeline == null)
        {
            Debug.LogWarning("[ExperienceAudio] ExperienceTimeline not found. Bite sound effects will not be triggered.");
            return;
        }

        timeline.PhaseChanged += OnPhaseChanged;
        Subscribe(timeline.smallFish, OnSmallFishBite);
        Subscribe(timeline.bigFish, OnBigFishBite);
        Subscribe(timeline.jellyfish, OnJellyfishBite);

        Debug.Log($"[ExperienceAudio] Ready. background={(background != null ? background.name : "MISSING")}, " +
                  $"sfx loaded={(smallFish1 != null) && (smallFish2 != null) && (bigFish != null) && (jellyfish != null)}");
    }

    /// One-shot startup report so a silent run can be diagnosed from the
    /// Console (Editor) or logcat (Quest) without guessing.
    void LogAudioDiagnostics()
    {
        var listener = FindFirstObjectByType<AudioListener>();

        Debug.Log(
            "[ExperienceAudio] Diagnostics: " +
            $"background={Describe(background)}, " +
            $"smallFish1={Describe(smallFish1)}, smallFish2={Describe(smallFish2)}, " +
            $"bigFish={Describe(bigFish)}, jellyfish={Describe(jellyfish)}, " +
            $"listener={(listener != null ? listener.gameObject.name : "NONE")}, " +
            $"listenerVolume={AudioListener.volume}, listenerPaused={AudioListener.pause}, " +
            $"musicVolume={musicVolume}, outputSampleRate={AudioSettings.outputSampleRate}, " +
            $"speakerMode={AudioSettings.speakerMode}");

        if (listener == null)
            Debug.LogError("[ExperienceAudio] No AudioListener in the scene. Nothing will be audible.");

#if UNITY_EDITOR
        // A clip that fails to load in the Editor is usually a Git LFS
        // pointer file left behind by a checkout without git-lfs. Look at
        // the source file on disk and say so explicitly.
        if (background == null)
        {
            string path = System.IO.Path.Combine(
                Application.dataPath, "Resources", audioFolder, backgroundClip + ".mp3");

            if (System.IO.File.Exists(path))
            {
                var info = new System.IO.FileInfo(path);

                if (info.Length < 1024)
                    Debug.LogError(
                        $"[ExperienceAudio] {path} is only {info.Length} bytes - it is still a Git LFS " +
                        "pointer, not real audio. Pull the latest default branch (the fixed clips are " +
                        "committed as regular blobs) and let Unity reimport Assets/Resources/Audio.");
                else
                    Debug.LogError(
                        $"[ExperienceAudio] {path} exists ({info.Length} bytes) but Unity did not import " +
                        "it as an AudioClip. Right-click Assets/Resources/Audio and choose Reimport.");
            }
            else
            {
                Debug.LogError($"[ExperienceAudio] Source file not found: {path}");
            }
        }
#endif
    }

    static string Describe(AudioClip clip)
    {
        return clip != null ? $"{clip.name}({clip.length:F1}s)" : "MISSING";
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
