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
    DaySummary      // the summary screen appears
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

    private AudioSource musicSource;
    private AudioSource[] voices;       // a small pool of speakers for effects
    private int nextVoice;

    // Lets us find a sound's row quickly by its name
    private readonly Dictionary<Sfx, SfxEntry> lookup = new Dictionary<Sfx, SfxEntry>();

    private void Awake()
    {
        instance = this;

        // The music speaker
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;      // 0 = 2D sound: same volume wherever the player stands
        musicSource.volume = musicVolume;

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

    public static void Play(Sfx sound)
    {
        if (instance == null) return;
        instance.PlayInternal(sound);
    }

    // ==========================================
    // INSIDE THE MANAGER
    // ==========================================

    private void PlayInternal(Sfx sound)
    {
        // No row for this sound, or no clip assigned yet: do nothing instead of breaking
        if (!lookup.TryGetValue(sound, out SfxEntry entry) || entry.clip == null) return;

        // Take the next speaker in the pool. After the last one we go back to the first
        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        voice.clip = entry.clip;
        voice.volume = sfxVolume * entry.volume;
        voice.pitch = entry.randomPitch
            ? 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation)
            : 1f;
        voice.Play();
    }
}