using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Plays the haptic patterns designed in the VR-Doctor-Fish repository
/// (StreamingAssets/haptics/*.json, one {"time","addr","mode","duty","freq"}
/// command per line).
///
/// Unity does NOT decide any vibration itself anymore: it only schedules the
/// commands from the JSON files and forwards each one, as JSON, to the
/// Python / VibraForge bridge through VibraForge.SendCommand.
/// The vibration design lives entirely in the pattern files.
///
/// Timeline mapping:
///   0s    welcome_experience.json (one-shot), then idle_water.json loops
///   15s   small_fish_nibble.json loops during the small fish frenzy
///   30s   idle_water.json loops again; every big fish bite overlays big_fish_bite.json
///   45s+  every jellyfish sting overlays jellyfish_sting.json
///   60s   everything stops, idle_water.json loops (calm water)
public class BiteHaptics : MonoBehaviour
{
    public VibraForge vibraForge;
    public ExperienceTimeline timeline;

    [Header("Pattern files (inside Assets/StreamingAssets/<folder>/)")]
    public string patternFolder = "haptics";
    public string welcomeFile = "welcome_experience.json";
    public string idleFile = "idle_water.json";
    public string nibbleFile = "small_fish_nibble.json";
    public string bigBiteFile = "big_fish_bite.json";
    public string stingFile = "jellyfish_sting.json";

    [Header("Playback")]
    [Tooltip("Pause between repetitions of a looping pattern (seconds).")]
    public float loopGap = 0.2f;

    [System.Serializable]
    class HapticCommand
    {
        public float time;
        public int addr;
        public int mode;
        public int duty;
        public int freq;
    }

    class Pattern
    {
        public string name;
        public List<HapticCommand> commands = new List<HapticCommand>();
        public HashSet<int> addrs = new HashSet<int>();
    }

    Pattern welcome;
    Pattern idle;
    Pattern nibble;
    Pattern bigBite;
    Pattern sting;

    Coroutine background;       // Current background pattern (welcome/idle/nibble)
    Pattern backgroundPattern;  // Whose actuators get switched off when stopped

    void Start()
    {
        if (timeline == null)
            timeline = FindFirstObjectByType<ExperienceTimeline>();

        if (vibraForge == null)
            vibraForge = FindFirstObjectByType<VibraForge>();

        welcome = Load(welcomeFile);
        idle = Load(idleFile);
        nibble = Load(nibbleFile);
        bigBite = Load(bigBiteFile);
        sting = Load(stingFile);

        if (timeline == null)
        {
            Debug.LogWarning("[BiteHaptics] ExperienceTimeline not found. Haptic feedback will not be triggered.");
            return;
        }

        timeline.PhaseChanged += OnPhaseChanged;

        // Per-bite one-shots. Small fish are covered by the nibble loop,
        // so only big fish and jellyfish trigger individual patterns.
        Subscribe(timeline.bigFish, OnBigFishBite);
        Subscribe(timeline.jellyfish, OnJellyfishBite);
    }

    void OnDestroy()
    {
        if (timeline == null)
            return;

        timeline.PhaseChanged -= OnPhaseChanged;
        Unsubscribe(timeline.bigFish, OnBigFishBite);
        Unsubscribe(timeline.jellyfish, OnJellyfishBite);
    }

    // ---------- Phase handling ----------

    void OnPhaseChanged(ExperienceTimeline.Phase phase)
    {
        switch (phase)
        {
            case ExperienceTimeline.Phase.Intro:
                // Welcome wave once, then calm water.
                StopBackground();
                backgroundPattern = welcome;
                background = StartCoroutine(WelcomeThenIdle());
                break;

            case ExperienceTimeline.Phase.SmallFishFrenzy:
                StartLoop(nibble);
                break;

            case ExperienceTimeline.Phase.BigFishAttacks:
            case ExperienceTimeline.Phase.JellyfishAttacks:
            case ExperienceTimeline.Phase.Calm:
                // Calm water between and after the attacks.
                if (backgroundPattern != idle)
                    StartLoop(idle);
                break;
        }
    }

    IEnumerator WelcomeThenIdle()
    {
        yield return PlayPattern(welcome, false);

        backgroundPattern = idle;
        yield return PlayPattern(idle, true);
    }

    // ---------- Bite one-shots ----------

    void OnBigFishBite(FishController fish)
    {
        StartCoroutine(PlayPattern(bigBite, false));
    }

    void OnJellyfishBite(FishController fish)
    {
        StartCoroutine(PlayPattern(sting, false));
    }

    // ---------- Pattern playback ----------

    void StartLoop(Pattern pattern)
    {
        StopBackground();
        backgroundPattern = pattern;
        background = StartCoroutine(PlayPattern(pattern, true));
    }

    void StopBackground()
    {
        if (background != null)
        {
            StopCoroutine(background);
            background = null;
        }

        // Make sure nothing keeps vibrating after a loop is interrupted.
        if (backgroundPattern != null)
            AllOff(backgroundPattern);

        backgroundPattern = null;
    }

    IEnumerator PlayPattern(Pattern pattern, bool loop)
    {
        if (pattern == null || pattern.commands.Count == 0)
            yield break;

        do
        {
            float startTime = Time.time;

            foreach (var cmd in pattern.commands)
            {
                float wait = cmd.time - (Time.time - startTime);

                if (wait > 0f)
                    yield return new WaitForSeconds(wait);

                Send(cmd);
            }

            if (loop)
                yield return new WaitForSeconds(loopGap);

        } while (loop);
    }

    void AllOff(Pattern pattern)
    {
        if (vibraForge == null)
            return;

        foreach (int addr in pattern.addrs)
            vibraForge.SendCommand(addr, 0, 0, 0);
    }

    void Send(HapticCommand cmd)
    {
        if (vibraForge == null)
            return;

        vibraForge.SendCommand(cmd.addr, cmd.mode, cmd.duty, cmd.freq);
    }

    // ---------- Loading ----------

    Pattern Load(string file)
    {
        var pattern = new Pattern { name = file };

        string path = Path.Combine(
            Application.streamingAssetsPath, patternFolder, file);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[BiteHaptics] Pattern file not found: {path}");
            return pattern;
        }

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim().TrimEnd(',');

            if (line.Length == 0 || !line.StartsWith("{"))
                continue;

            var cmd = JsonUtility.FromJson<HapticCommand>(line);

            if (cmd != null)
            {
                pattern.commands.Add(cmd);
                pattern.addrs.Add(cmd.addr);
            }
        }

        // Commands are scheduled by absolute time within the pattern.
        pattern.commands.Sort((a, b) => a.time.CompareTo(b.time));

        Debug.Log($"[BiteHaptics] Loaded {pattern.commands.Count} commands from {file}");

        return pattern;
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
