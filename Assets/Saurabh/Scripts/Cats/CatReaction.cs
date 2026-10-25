using System.Collections;
using UnityEngine;

// Plays a short "happy" reaction on a cat when the player feeds or cleans it.
// No Animator needed: it moves and squashes the cat's transform in code.
// Put this on the cat prefab (same object as BaseCat).
public class CatReaction : MonoBehaviour
{
    // One row of numbers per personality. Shown in the Inspector so you can tweak without code.
    [System.Serializable]
    public class Profile
    {
        public float hopHeight = 0.15f;   // how high the cat jumps (in the parent's units)
        public float duration = 0.45f;    // how long the whole reaction lasts (seconds)
        public float squash = 0.12f;      // 0.12 = 12% stretch/squash
        public float shake = 0f;          // side-to-side shake distance (used by Angry)
        public int shakeCount = 3;        // how many shakes
    }

    [Header("Personality reactions")]
    [SerializeField] private Profile playful  = new Profile { hopHeight = 0.25f, duration = 0.5f,  squash = 0.15f };
    [SerializeField] private Profile lazy     = new Profile { hopHeight = 0.05f, duration = 0.7f,  squash = 0.08f };
    [SerializeField] private Profile angry    = new Profile { hopHeight = 0.12f, duration = 0.3f,  squash = 0.1f };
    [SerializeField] private Profile skittish = new Profile { hopHeight = 0.2f,  duration = 0.3f,  squash = 0.18f };

    [Header("\"No\" reaction (not enough GP, or already full)")]
    [SerializeField] private Profile denied   = new Profile { hopHeight = 0f,    duration = 0.4f,  squash = 0f,   shake = 0.06f, shakeCount = 3 };

    [Header("Effects (Particle System prefabs)")]
    [SerializeField] private GameObject feedEffect;    // pink hearts
    [SerializeField] private GameObject cleanEffect;   // light blue bubbles
    [SerializeField] private float effectHeight = 0.6f; // how far above the cat the effect appears (world units)

    private BaseCat cat;
    private Coroutine running;
    private Vector3 basePosition;  // the cat's real position, remembered before we move it
    private Vector3 baseScale;     // the cat's real size, remembered before we squash it

    private void Awake()
    {
        cat = GetComponent<BaseCat>();
    }

    // Called by CareSubsystem after a successful feed or clean: a happy hop.
    public void Play(bool isFeeding)
    {
        Begin(GetProfile(), isFeeding ? feedEffect : cleanEffect);
    }

    // Called by CareSubsystem when care is refused (not enough GP, or the stat is already full):
    // a side-to-side shake, no hop and no hearts.
    public void PlayDenied()
    {
        Begin(denied, null);
    }

    // Shared start-up for both reactions
    private void Begin(Profile profile, GameObject effect)
    {
        // Don't wiggle a cat that a customer is carrying.
        if (cat != null && cat.IsHeldByCustomer) return;

        // If a reaction is already playing, put the cat back first.
        // This stops it drifting or growing when the player mashes the key.
        if (running != null)
        {
            StopCoroutine(running);
            Restore();
        }

        // Remember where the cat really is and how big it really is.
        basePosition = transform.localPosition;
        baseScale = transform.localScale;

        SpawnEffect(effect);
        running = StartCoroutine(ReactRoutine(profile));
    }

    private Profile GetProfile()
    {
        if (cat == null) return playful;

        switch (cat.Behavior)
        {
            case CatBehavior.Lazy:     return lazy;
            case CatBehavior.Angry:    return angry;
            case CatBehavior.Skittish: return skittish;
            default:                   return playful;
        }
    }

    private IEnumerator ReactRoutine(Profile p)
    {
        float t = 0f;

        while (t < p.duration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / p.duration);   // 0 at the start, 1 at the end

            // Hop: a smooth arc up and back down (sin goes 0 -> 1 -> 0).
            float hop = Mathf.Sin(n * Mathf.PI) * p.hopHeight;

            // Shake: left-right wobble that fades out as n goes to 1.
            float side = Mathf.Sin(n * Mathf.PI * 2f * p.shakeCount) * p.shake * (1f - n);

            transform.localPosition = basePosition + new Vector3(side, hop, 0f);

            // Squash and stretch: tall on the way up, flat on landing.
            float s = Mathf.Sin(n * Mathf.PI * 2f) * p.squash;
            transform.localScale = new Vector3(
                baseScale.x * (1f - s * 0.5f),
                baseScale.y * (1f + s),
                baseScale.z * (1f - s * 0.5f));

            yield return null;
        }

        Restore();
        running = null;
    }

    // Puts the cat exactly back to its real position and size.
    private void Restore()
    {
        transform.localPosition = basePosition;
        transform.localScale = baseScale;
    }

    private void SpawnEffect(GameObject prefab)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, transform.position + Vector3.up * effectHeight, Quaternion.identity);
        Destroy(fx, 3f);   // clean up so effects don't pile up in the scene
    }

    // If the cat is disabled or destroyed mid-reaction, don't leave it squashed.
    private void OnDisable()
    {
        if (running != null)
        {
            StopCoroutine(running);
            Restore();
            running = null;
        }
    }
}