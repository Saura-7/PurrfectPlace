using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The small, always-on life of a cat: its head looks around, its eyes blink, its tail sways.
/// This is "procedural animation": no Animator and no animation clips, just a few numbers
/// changed in code every frame.
///
/// It needs three empty "pivot" objects in the cat prefab (see the setup guide):
///   HeadPivot    → at the neck, holding the head, eyes, nose, whiskers and EyelidPivot
///   EyelidPivot  → at the TOP of the eyes (inside HeadPivot), holding catEyelids:
///                  a copy of the eyeballs wearing the fur material. Stretching it down
///                  from the top covers the eyes, like a real eyelid closing.
///   TailPivot    → at the base of the tail, holding the tail
/// Why pivots? Every part of this model rotates around the model's origin (its feet),
/// so turning catHead directly would swing it around the floor. An empty parent placed
/// at the neck gives the head a proper hinge.
///
/// If a pivot is missing, that part simply doesn't animate (no errors).
///
/// Who moves what (so scripts never fight):
///   CatFace     → only the three pivots
///   CatIdle     → only the cat's body rotation (an angry cat turning its back)
///   CatReaction → the cat's body position and scale (hops, squash)
/// </summary>
[RequireComponent(typeof(BaseCat))]
public class CatFace : MonoBehaviour
{
    [Header("Pivots (left empty = found by name)")]
    [SerializeField] private Transform headPivot;
    [SerializeField] private Transform lidPivot;
    [SerializeField] private Transform tailPivot;

    [Header("Head")]
    [SerializeField] private float noticeDistance = 3f;   // metres: closer than this and the cat looks at you
    [SerializeField] private float maxHeadYaw = 45f;      // the furthest it turns its head left/right (degrees)
    [SerializeField] private float maxHeadPitch = 20f;    // the furthest it looks up/down (degrees)
    [SerializeField] private float headSpeed = 5f;        // how quickly the head follows (higher = snappier)

    [Header("Tail")]
    [SerializeField] private Vector3 tailAxis = Vector3.up;  // the hinge the tail swings around (try (0,0,1) if it looks wrong)

    [Header("Eyes")]
    [SerializeField] private float blinkTime = 0.12f;     // seconds for one close-and-open

    private BaseCat cat;

    // The pivots' starting rotation and size, so every change is "on top of" the model's own pose
    private Quaternion headBase, tailBase;
    private Vector3 lidBase;
    private Renderer[] lidRenderers;   // hidden while the eyes are fully open
    private Renderer[] eyeRenderers;   // the eyeballs: hidden while the eyes are shut

    // HEAD: where it is pointing now (degrees)
    private float headYaw, headPitch, headTilt;

    // GLANCES: a short to-do list of head turns asked for by CatIdle ("look left 1 s, then right 1 s")
    private struct Glance { public float yaw, tilt, seconds; }
    private readonly Queue<Glance> glances = new Queue<Glance>();
    private Glance currentGlance;
    private float glanceTimer;   // seconds left on the current glance (0 = no glance)

    // EYES: 1 = fully open, 0 = closed
    private float eyeOpen = 1f;
    private float nextBlinkTime;
    private float blinkTimer;      // > 0 while a blink is playing
    private float holdClosedTimer; // > 0 while the eyes stay shut (dozing, or content when petted)

    // TAIL: a random starting point in the sway, so cats side by side don't wag in sync
    private float tailPhase;

    private void Awake()
    {
        cat = GetComponent<BaseCat>();

        if (headPivot == null) headPivot = FindDeep(transform, "HeadPivot");
        if (lidPivot == null)  lidPivot  = FindDeep(transform, "EyelidPivot");
        if (tailPivot == null) tailPivot = FindDeep(transform, "TailPivot");

        if (headPivot != null) headBase = headPivot.localRotation;
        if (tailPivot != null) tailBase = tailPivot.localRotation;
        if (lidPivot != null)
        {
            lidBase = lidPivot.localScale;
            lidRenderers = lidPivot.GetComponentsInChildren<Renderer>();
        }
        Transform eyes = FindDeep(transform, "catEyeBalls");
        eyeRenderers = eyes != null ? eyes.GetComponentsInChildren<Renderer>() : new Renderer[0];

        tailPhase = Random.Range(0f, 10f);
        ScheduleBlink();
    }

    private void Update()
    {
        // Time.deltaTime is 0 while paused, so the cat freezes with the rest of the game
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateHead(dt);
        UpdateEyes(dt);
        UpdateTail();
    }

    // ==========================================
    // HEAD
    // ==========================================

    /// <summary>Queue a head turn: yaw = left/right, tilt = the curious sideways head tilt, held for some seconds.</summary>
    public void QueueGlance(float yaw, float tilt, float seconds)
    {
        glances.Enqueue(new Glance { yaw = yaw, tilt = tilt, seconds = seconds });
    }

    /// <summary>True while the head is busy with glances (CatIdle waits for it).</summary>
    public bool IsGlancing => glanceTimer > 0f || glances.Count > 0;

    private void UpdateHead(float dt)
    {
        if (headPivot == null) return;

        // Pick up the next glance from the to-do list when the last one ends
        if (glanceTimer > 0f) glanceTimer -= dt;
        if (glanceTimer <= 0f && glances.Count > 0)
        {
            currentGlance = glances.Dequeue();
            glanceTimer = currentGlance.seconds;
        }

        float goalYaw = 0f, goalPitch = 0f, goalTilt = 0f;

        if (cat.IsHeldByCustomer)
        {
            // In a customer's arms or on the desk: look straight ahead, and forget any glances
            glances.Clear();
            glanceTimer = 0f;
        }
        else if (glanceTimer > 0f)
        {
            goalYaw = currentGlance.yaw;
            goalTilt = currentGlance.tilt;
        }
        else
        {
            NoticePlayer(ref goalYaw, ref goalPitch);
        }

        // Ease toward the goal. 1 - Exp(-speed × time) gives the same smoothness at any frame rate.
        // Lazy cats move their head at half speed.
        float speed = cat.Behavior == CatBehavior.Lazy ? headSpeed * 0.5f : headSpeed;
        float blend = 1f - Mathf.Exp(-speed * dt);
        headYaw   = Mathf.Lerp(headYaw,   goalYaw,   blend);
        headPitch = Mathf.Lerp(headPitch, goalPitch, blend);
        headTilt  = Mathf.Lerp(headTilt,  goalTilt,  blend);

        // Euler(x, y, z): x = nod (pitch), y = turn (yaw), z = tilt (roll)
        headPivot.localRotation = headBase * Quaternion.Euler(headPitch, headYaw, headTilt);
    }

    // Where the head wants to point because the player is near. Each personality reacts its own way.
    private void NoticePlayer(ref float yaw, ref float pitch)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 toPlayer = cam.transform.position - headPivot.position;
        if (toPlayer.magnitude > noticeDistance) return;

        // The player's direction in the CAT's own space (InverseTransformDirection ignores the cat's scale).
        // Atan2(x, z) = degrees to the side (0 = straight ahead). Atan2(y, flat) = degrees up.
        Vector3 local = transform.InverseTransformDirection(toPlayer);
        float sideAngle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float upAngle = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;

        switch (cat.Behavior)
        {
            case CatBehavior.Playful:   // looks right at you
                yaw = sideAngle; pitch = -upAngle;   // minus: in Unity a positive X turn tips the head DOWN
                break;
            case CatBehavior.Lazy:      // half-heartedly glances your way
                yaw = sideAngle * 0.4f; pitch = -upAngle * 0.3f;
                break;
            case CatBehavior.Skittish:  // won't meet your eyes: looks down and away
                yaw = -Mathf.Sign(sideAngle) * 35f; pitch = 10f;
                break;
            case CatBehavior.Angry:     // side-eye: head turned away a little, but still tracking you
                yaw = sideAngle * 0.3f - Mathf.Sign(sideAngle) * 20f;
                break;
        }

        yaw = Mathf.Clamp(yaw, -maxHeadYaw, maxHeadYaw);
        pitch = Mathf.Clamp(pitch, -maxHeadPitch, maxHeadPitch);
    }

    // ==========================================
    // EYES
    // ==========================================

    // The eyes are only ever fully OPEN or fully SHUT. A half-closed lid looked like a flat disc
    // on this model (it's a squashed copy of the eyeball, not a real eyelid), so we don't use it.

    /// <summary>Close the eyes for a while (a lazy cat dozing off).</summary>
    public void Doze(float seconds) { holdClosedTimer = seconds; }

    /// <summary>Eyes shut in contentment, the way a cat closes its eyes when it likes being petted.</summary>
    public void Content(float seconds) { holdClosedTimer = seconds; }

    private void UpdateEyes(float dt)
    {
        if (lidPivot == null) return;

        float goal = 1f;   // open

        if (holdClosedTimer > 0f)
        {
            holdClosedTimer -= dt;
            goal = 0f;
        }
        else if (blinkTimer > 0f)
        {
            // A blink: closes for the first half, opens for the second half
            blinkTimer -= dt;
            goal = blinkTimer > blinkTime * 0.5f ? 0f : 1f;
        }
        else if (Time.time >= nextBlinkTime)
        {
            blinkTimer = blinkTime;
            ScheduleBlink();
        }

        // Blinks are fast (speed 40), dozing off is slow (speed 4)
        float speed = holdClosedTimer > 0f ? 4f : 40f;
        eyeOpen = Mathf.Lerp(eyeOpen, goal, 1f - Mathf.Exp(-speed * dt));

        // The lid grows DOWN from the top of the eyes (that's why EyelidPivot sits at the top).
        // eyeOpen 1 → lid height 0 (eyes open). eyeOpen 0 → lid full height (eyes shut).
        float lid = 1f - eyeOpen;
        lidPivot.localScale = new Vector3(lidBase.x, lidBase.y * Mathf.Max(lid, 0.001f), lidBase.z);

        // A scale of almost 0 can still show a thin line, so hide the lids when the eyes are open
        bool show = lid > 0.02f;
        foreach (Renderer r in lidRenderers) r.enabled = show;

        // Once the lid has (almost) fully covered the eye, hide the eyeball underneath.
        // The lid is a copy of the eyeball, so from some angles the eyeball pokes through it;
        // with the eyeball switched off, there's nothing left to poke through.
        // (Not earlier: half-way, a hidden eyeball would show the hole in the head.)
        bool shut = lid > 0.9f;
        foreach (Renderer r in eyeRenderers) r.enabled = !shut;
    }

    private void ScheduleBlink()
    {
        // Skittish cats blink a lot, lazy cats rarely
        Vector2 gap = cat != null && cat.Behavior == CatBehavior.Skittish ? new Vector2(1.5f, 3.5f)
                    : cat != null && cat.Behavior == CatBehavior.Lazy     ? new Vector2(4f, 8f)
                    : new Vector2(2.5f, 6f);
        nextBlinkTime = Time.time + Random.Range(gap.x, gap.y);
    }

    // ==========================================
    // TAIL
    // ==========================================

    private void UpdateTail()
    {
        if (tailPivot == null) return;

        // amount = how far it swings (degrees), speed = swings per second
        float amount, speed;
        switch (cat.Behavior)
        {
            case CatBehavior.Lazy:     amount = 5f;  speed = 0.2f; break;   // barely moves
            case CatBehavior.Skittish: amount = 6f;  speed = 1.2f; break;   // small, nervous twitch
            case CatBehavior.Angry:    amount = 16f; speed = 0.3f; break;   // slow, wide swish: "annoyed"
            default:                   amount = 12f; speed = 0.6f; break;   // Playful: a relaxed, happy sway
        }

        // A sine wave goes smoothly from -1 to 1 and back. Mixing in a second, faster wave
        // (20% of it) stops the sway looking like a perfect metronome.
        float t = (Time.time + tailPhase) * speed * Mathf.PI * 2f;
        float wave = Mathf.Sin(t) * 0.8f + Mathf.Sin(t * 2.3f) * 0.2f;

        tailPivot.localRotation = tailBase * Quaternion.AngleAxis(wave * amount, tailAxis);
    }

    // ==========================================
    // HELPERS
    // ==========================================

    // transform.Find only looks one level down; this looks through every child, grandchild, ...
    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}