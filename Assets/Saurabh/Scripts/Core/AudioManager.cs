using System.Collections.Generic;
using UnityEngine;

// Every sound effect in the game has a name here. Add a new name to add a new sound.
public enum Sfx
{
    DoorChime,      // a customer walks in
    MeowFeed,       // the player feeds a cat
    PurrClean,      // the player cleans a cat
    CoinsIn,        // a Taker adopts a cat (GP goes up)
    CoinsOut,       // the player buys a cat or decor (GP goes down)
    PoliteNo,       // a deal is declined or missed
    ButtonClick,    // any world-space button
    ShopOpen,       // the sign is turned to Open
    ShopClosing,    // last call starts
    DaySummary,     // the summary screen appears
    // New sounds go at the END only: the Inspector stores each row's sound as a number (its position here)
    RareCat,        // a rare cat is put on the desk
    ResidentJoins   // a rare cat becomes a resident
}

/// <summary>
/// Plays all the music and sound effects. Other scripts only need one line:
///     AudioManager.Play(Sfx.CoinsIn);
/// It is safe to call even when a scene has no AudioManager (nothing happens).
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    // One row in the Inspector: which sound, which clip, how loud
    [System.Serializable]
    public class SfxEntry
    {
        public Sfx sound;
        public AudioClip clip;
        [Range(0f, 2f)] public float volume = 1f;
        public bool randomPitch = true;     // a tiny pitch change each time, so repeats don't sound robotic
    }

    [Header("Music")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;

    [Header("Sound effects")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
    [SerializeField] private float pitchVariation = 0.05f;   // 0.05 means 5 percent higher or lower
    [SerializeField] private int voiceCount = 6;             // how many effects can play at the same time
    [SerializeField] private List<SfxEntry> effects = new List<SfxEntry>();

    // The player's own volume settings (0 to 1), on top of the mix levels above. Saved between sessions.
    // "static" so the pause menu's sliders can set them without finding this object.
    public static float MusicSetting { get; private set; } = 1f;
    public static float SfxSetting { get; private set; } = 1f;
    private const string MusicKey = "MusicVolume";
    private const string SfxKey = "SfxVolume";

    private AudioSource musicSource;
    private AudioLowPassFilter musicFilter;   // muffles the music (cuts the high sounds) while paused

    [Header("Muffled music while paused")]
    [SerializeField] private float muffledCutoff = 900f;   // Hz: lower = more muffled ("through a wall")
    [SerializeField] private float muffleSpeed = 8f;       // how quickly it muffles and clears
    private const float ClearCutoff = 22000f;              // Hz: everything a person can hear passes = no muffle
    private static bool muffled;
    
    private AudioSource[] voices;       // a small pool of speakers for effects
    private int nextVoice;

    // Lets us find a sound's row quickly by its name
    private readonly Dictionary<Sfx, SfxEntry> lookup = new Dictionary<Sfx, SfxEntry>();

    private void Awake()
    {
        instance = this;
        MusicSetting = PlayerPrefs.GetFloat(MusicKey, 1f);   // the saved value, or full volume on first launch
        SfxSetting = PlayerPrefs.GetFloat(SfxKey, 1f);

        // The music speaker lives on its OWN child object. A low-pass filter affects every AudioSource
        // on the same object, so this keeps the muffle on the music only (button clicks stay crisp).
        GameObject musicObject = new GameObject("Music");
        musicObject.transform.SetParent(transform, false);
        musicSource = musicObject.AddComponent<AudioSource>();
        musicFilter = musicObject.AddComponent<AudioLowPassFilter>();
        musicFilter.cutoffFrequency = ClearCutoff;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;      // 0 = 2D sound: same volume wherever the player stands
        musicSource.volume = musicVolume * MusicSetting;

        // The effect speakers. Each one has its own pitch, so one effect's pitch never changes another
        voices = new AudioSource[Mathf.Max(1, voiceCount)];
        for (int i = 0; i < voices.Length; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
            voices[i].spatialBlend = 0f;
        }

        // Build the lookup table from the Inspector rows
        foreach (SfxEntry entry in effects)
        {
            lookup[entry.sound] = entry;
        }

        if (musicClip != null)
        {
            musicSource.clip = musicClip;
            musicSource.Play();
        }
    }

    private void OnDestroy()
    {
        // Only clear it if it is still us (a static variable outlives the scene)
        if (instance == this) instance = null;
    }

    // ==========================================
    // CALLED BY OTHER SCRIPTS
    // ==========================================

    // Anything can listen for sounds, e.g. skittish cats flinch at the door chime.
    // It fires even if no clip is set yet, so behaviour doesn't depend on audio being finished.
    // Called by the pause menu's sliders. Music changes straight away; effects from their next play.
    public static void SetMusicVolume(float value)
    {
        MusicSetting = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, MusicSetting);
        if (instance != null) instance.musicSource.volume = instance.musicVolume * MusicSetting;
    }

    public static void SetSfxVolume(float value)
    {
        SfxSetting = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, SfxSetting);
    }

    /// <summary>Muffle the music (the pause menu calls this). It glides there over a moment.</summary>
    public static void SetMuffled(bool on) => muffled = on;

    private void Update()
    {
        if (musicFilter == null) return;

        float target = muffled ? muffledCutoff : ClearCutoff;
        // Unscaled: the game is frozen while paused, but the muffle still has to move.
        float blend = 1f - Mathf.Exp(-muffleSpeed * Time.unscaledDeltaTime);
        musicFilter.cutoffFrequency = Mathf.Lerp(musicFilter.cutoffFrequency, target, blend);
    }

    public static event System.Action<Sfx> OnSoundPlayed;

    public static void Play(Sfx sound) => Play(sound, 1f, 1f);

    /// <summary>
    /// Same as Play, but higher/lower and louder/quieter. Used for cat voices:
    /// one meow clip, and each personality sounds different (pitch 1.3 = higher, 0.85 = lower).
    /// </summary>
    public static void Play(Sfx sound, float pitch, float volume)
    {
        OnSoundPlayed?.Invoke(sound);
        if (instance == null) return;
        instance.PlayInternal(sound, pitch, volume);
    }

    // ==========================================
    // INSIDE THE MANAGER
    // ==========================================

    private void PlayInternal(Sfx sound, float pitchScale, float volumeScale)
    {
        // No row for this sound, or no clip assigned yet: do nothing instead of breaking
        if (!lookup.TryGetValue(sound, out SfxEntry entry) || entry.clip == null) return;

        // Take the next speaker in the pool. After the last one we go back to the first
        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        voice.clip = entry.clip;
        voice.volume = sfxVolume * SfxSetting * entry.volume * volumeScale;
        float pitch = entry.randomPitch
            ? 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation)
            : 1f;
        voice.pitch = pitch * pitchScale;   // the small random wobble, then the cat's own voice on top
        voice.Play();
    }
}