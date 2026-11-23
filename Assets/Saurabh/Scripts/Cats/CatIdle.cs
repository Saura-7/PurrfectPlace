using System.Collections;
using UnityEngine;

/// <summary>
/// The occasional habits a cat does on its own, so the shop feels alive even when you're not touching anything.
/// (The always-on life — blinking, tail, looking at you — is in CatFace. This script is the rarer, bigger moments.)
///
///   Playful  → a small hop, or a curious head tilt
///   Lazy     → dozes off for a few seconds
///   Skittish → glances left and right, and flinches when the door chimes
///   Angry    → turns its back on you for a moment, or a grumpy look away
///
/// Habits are spaced far apart on purpose: a shop full of cats that move all the time
/// is distracting, and a habit you see rarely feels special.
///
/// Who moves what: this script only turns the cat's BODY (the angry back-turn).
/// Head turns are asked of CatFace; hops go through CatReaction.
/// </summary>
[RequireComponent(typeof(BaseCat))]
public class CatIdle : MonoBehaviour
{
    [Header("Seconds between idle habits (random between X and Y)")]
    [SerializeField] private Vector2 playfulGap  = new Vector2(20f, 36f);
    [SerializeField] private Vector2 lazyGap     = new Vector2(20f, 40f);
    [SerializeField] private Vector2 skittishGap = new Vector2(14f, 28f);
    [SerializeField] private Vector2 angryGap    = new Vector2(16f, 32f);

    [Header("Naps")]
    [SerializeField] private float lazyNapChance = 0.6f;     // a lazy cat's habit is a nap 60% of the time
    [SerializeField] private float otherNapChance = 0.15f;   // playful and grumpy cats nap now and then
    [SerializeField] private Vector2 napLength = new Vector2(20f, 40f);   // seconds asleep

    private BaseCat cat;
    private CatReaction reaction;
    private CatFace face;
    private CatNap nap;
    private float nextIdleTime;
    private Coroutine turning;   // the body turn currently playing, or null

    private void Awake()
    {
        cat = GetComponent<BaseCat>();
        reaction = GetComponent<CatReaction>();
        face = GetComponent<CatFace>();
        nap = GetComponent<CatNap>();
    }

    private void OnEnable()
    {
        AudioManager.OnSoundPlayed += OnSoundPlayed;
        ScheduleNext();
    }

    private void OnDisable()
    {
        // A static event outlives this cat: stop listening, or it would call a destroyed object
        AudioManager.OnSoundPlayed -= OnSoundPlayed;
    }

    private void Update()
    {
        if (Time.time < nextIdleTime) return;

        if (CanAct()) DoIdle();
        ScheduleNext();   // try again later either way
    }

    // ==========================================
    // WHEN A CAT MAY ACT
    // ==========================================

    private bool CanAct()
    {
        return Time.timeScale > 0f                              // not paused or on the summary
               && !cat.IsHeldByCustomer                        // not in someone's arms or on the desk
               && (reaction == null || !reaction.IsPlaying)    // not mid-hop from feeding or petting
               && (face == null || !face.IsGlancing)           // head not already busy
               && turning == null                              // not already turning
               && (nap == null || !nap.IsNapping);             // not asleep
    }

    private void ScheduleNext()
    {
        Vector2 gap = GapFor(cat != null ? cat.Behavior : CatBehavior.Playful);
        nextIdleTime = Time.time + Random.Range(gap.x, gap.y);
    }

    private Vector2 GapFor(CatBehavior behavior)
    {
        switch (behavior)
        {
            case CatBehavior.Lazy:     return lazyGap;
            case CatBehavior.Skittish: return skittishGap;
            case CatBehavior.Angry:    return angryGap;
            default:                   return playfulGap;
        }
    }

    // ==========================================
    // THE HABITS
    // ==========================================

    private void DoIdle()
    {
        bool coin = Random.value < 0.5f;   // a coin flip, so each cat has two habits, not one

        // Sometimes the habit is a proper nap instead (skittish cats never: they stay on guard)
        if (nap != null && Random.value < NapChance(cat.Behavior))
        {
            nap.StartNap(Random.Range(napLength.x, napLength.y));
            return;
        }

        switch (cat.Behavior)
        {
            case CatBehavior.Playful:
                if (coin && reaction != null) reaction.PlayIdleHop();
                else if (face != null) face.QueueGlance(15f, 18f, 1.2f);   // the curious head tilt
                break;

            case CatBehavior.Lazy:
                if (face != null) face.Doze(Random.Range(3f, 5f));        // eyes slowly close... and open again
                break;

            case CatBehavior.Skittish:
                if (face != null)                                           // a quick nervous look around
                {
                    face.QueueGlance(35f, 0f, 0.4f);
                    face.QueueGlance(-35f, 0f, 0.4f);
                }
                break;

            case CatBehavior.Angry:
                if (coin) Turn(120f, 0.6f, 2f, 0.8f);                       // turns its back, then turns round again
                else if (face != null) face.QueueGlance(-40f, 0f, 2f);      // a long, grumpy look away
                break;
        }
    }

    private float NapChance(CatBehavior behavior)
    {
        switch (behavior)
        {
            case CatBehavior.Lazy:     return lazyNapChance;
            case CatBehavior.Skittish: return 0f;
            default:                   return otherNapChance;
        }
    }

    // Skittish cats jump when someone walks in
    private void OnSoundPlayed(Sfx sound)
    {
        if (sound != Sfx.DoorChime || cat == null || cat.Behavior != CatBehavior.Skittish) return;
        if (cat.IsHeldByCustomer || reaction == null || reaction.IsPlaying) return;

        reaction.PlayFlinch();
    }

    // ==========================================
    // BODY TURN (the only thing this script moves)
    // ==========================================

    // Turns the body to "angle" (degrees around its up axis) over "turnTime", holds, then turns back.
    private void Turn(float angle, float turnTime, float hold, float backTime)
    {
        turning = StartCoroutine(TurnRoutine(angle, turnTime, hold, backTime));
    }

    private IEnumerator TurnRoutine(float angle, float turnTime, float hold, float backTime)
    {
        Transform startParent = transform.parent;
        Quaternion startRotation = transform.localRotation;

        // There and back: the same loop, run twice with different numbers
        float[] from = { 0f, angle };
        float[] to = { angle, 0f };
        float[] time = { turnTime, backTime };

        for (int i = 0; i < 2; i++)
        {
            float t = 0f;
            while (t < time[i])
            {
                // Picked up or moved mid-turn: stop, and let the new spot decide its rotation
                if (transform.parent != startParent) { turning = null; yield break; }

                t += Time.deltaTime;
                float eased = Juice.EaseOut(Mathf.Clamp01(t / time[i]));
                transform.localRotation = startRotation * Quaternion.Euler(0f, Mathf.Lerp(from[i], to[i], eased), 0f);
                yield return null;
            }
            if (i == 0) yield return new WaitForSeconds(hold);
        }

        // Always finish facing exactly the way it started
        if (transform.parent == startParent) transform.localRotation = startRotation;
        turning = null;
    }
}