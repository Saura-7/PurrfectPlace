using System.Collections;
using UnityEngine;

/// <summary>
/// Small things a cat does on its own, so the shop feels alive even when you're not touching anything.
/// Each personality has its own habits:
///
///   Playful  → a small hop, or a gentle curious sway
///   Lazy     → "z z z", and slowly looks to one side
///   Skittish → looks left and right, and flinches when the door chimes
///   Angry    → turns its back on you for a moment, or a grumpy shake
///
/// It also NOTICES you when you come close: Playful faces you, Skittish shies away,
/// Angry gives you a side-eye, and Lazy can't be bothered.
///
/// Put this on the cat prefab next to BaseCat and CatReaction.
/// Movement (hop, shake) goes through CatReaction; this script only turns the cat,
/// so the two never fight over the same values.
/// </summary>
[RequireComponent(typeof(BaseCat))]
public class CatIdle : MonoBehaviour
{
    [Header("Seconds between idle actions (random between X and Y)")]
    [SerializeField] private Vector2 playfulGap  = new Vector2(10f, 18f);
    [SerializeField] private Vector2 lazyGap     = new Vector2(10f, 20f);
    [SerializeField] private Vector2 skittishGap = new Vector2(7f, 14f);
    [SerializeField] private Vector2 angryGap    = new Vector2(8f, 16f);

    [Header("Noticing the player")]
    [SerializeField] private float noticeDistance = 3f;     // metres: closer than this and the cat reacts to you
    [SerializeField] private float maxNoticeTurn = 60f;     // the most it will turn its head/body (degrees)
    [SerializeField] private float noticeTurnSpeed = 4f;    // how quickly it turns (higher = snappier)
    [SerializeField] private float modelFacingOffset = 0f;  // if the cat model's front isn't +Z, fix it here (degrees)

    [Header("Text pop-ups")]
    [SerializeField] private float textWithinDistance = 8f;   // only show "z z z" or "!" if the player is this close

    private BaseCat cat;
    private CatReaction reaction;
    private float nextIdleTime;
    private Coroutine turning;   // the turn currently playing, or null
    private float noticeYaw;     // how far the cat is turned toward (or away from) the player right now
    private float lastSide = 1f; // which side of the cat the player was last clearly on

    private void Awake()
    {
        cat = GetComponent<BaseCat>();
        reaction = GetComponent<CatReaction>();
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
        UpdateNotice();

        if (Time.time < nextIdleTime) return;

        if (CanAct()) DoIdle();
        ScheduleNext();   // try again later either way
    }

    // ==========================================
    // NOTICING THE PLAYER
    // ==========================================

    // Every frame: how should this cat turn because the player is near?
    //   Playful  → faces you        Skittish → shies away
    //   Angry    → side-eye         Lazy     → can't be bothered
    private void UpdateNotice()
    {
        // While an idle turn plays, or the cat is with a customer, the turn script / customer is in charge
        if (turning != null || cat.IsHeldByCustomer || transform.parent == null) { noticeYaw = 0f; return; }

        float goal = 0f;   // 0 = facing its normal way (the front of the cage)
        Camera cam = Camera.main;

        if (cam != null && cat.Behavior != CatBehavior.Lazy)
        {
            Vector3 toPlayer = cam.transform.position - transform.position;
            if (toPlayer.magnitude < noticeDistance)
            {
                // The player's direction in the CAGE's own space, flattened, as an angle from the cage's front.
                // Atan2(x, z) = how many degrees to the side the player is (0 = straight ahead).
                Vector3 local = transform.parent.InverseTransformDirection(toPlayer);
                float playerAngle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg - modelFacingOffset;

                // Which side you're on: +1 = to the cat's right, -1 = to its left.
                // Only switch sides once you're clearly (10°+) on the other side. Without this, standing
                // straight in front would flip the side every frame and the cat would jitter back and forth.
                if (Mathf.Abs(playerAngle) > 10f) lastSide = playerAngle > 0f ? 1f : -1f;
                float side = lastSide;

                switch (cat.Behavior)
                {
                    case CatBehavior.Playful:  goal = playerAngle;                       break;   // looks right at you
                    case CatBehavior.Skittish: goal = -side * 55f - playerAngle * 0.3f;  break;   // turns well away from you
                    case CatBehavior.Angry:    goal = -side * 25f;                       break;   // a small "hmph" turn: side-eye
                }
                goal = Mathf.Clamp(goal, -maxNoticeTurn, maxNoticeTurn);
            }
        }

        // Ease toward the goal. 1 - Exp(-speed × time) gives the same smoothness at any frame rate.
        float blend = 1f - Mathf.Exp(-noticeTurnSpeed * Time.deltaTime);
        noticeYaw = Mathf.Lerp(noticeYaw, goal, blend);

        transform.localRotation = Quaternion.Euler(0f, noticeYaw, 0f);
    }

    // ==========================================
    // WHEN A CAT MAY ACT
    // ==========================================

    private bool CanAct()
    {
        return Time.timeScale > 0f                              // not paused or on the summary
               && !cat.IsHeldByCustomer                        // not in someone's arms or on the desk
               && (reaction == null || !reaction.IsPlaying)    // not mid-hop from feeding or petting
               && turning == null;                             // not already turning
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

        switch (cat.Behavior)
        {
            case CatBehavior.Playful:
                if (coin && reaction != null) reaction.PlayIdleHop();
                else Turn(new[] { 20f, -20f, 0f }, new[] { 0.7f, 0.9f, 0.7f }, 0f); // a gentle, curious sway
                break;

            case CatBehavior.Lazy:
                ShowText("z z z");
                Turn(new[] { 25f, 0f }, new[] { 1.2f, 1.2f }, 1.5f);               // a slow, sleepy look aside
                break;

            case CatBehavior.Skittish:
                Turn(new[] { 35f, -35f, 0f }, new[] { 0.2f, 0.3f, 0.2f }, 0.3f);   // a quick nervous look around
                break;

            case CatBehavior.Angry:
                if (coin) Turn(new[] { 160f, 0f }, new[] { 0.4f, 0.6f }, 2.5f);    // turns its back, then turns round again
                else if (reaction != null) reaction.PlayDenied();                // a grumpy shake
                break;
        }
    }

    // Skittish cats jump when someone walks in
    private void OnSoundPlayed(Sfx sound)
    {
        if (sound != Sfx.DoorChime || cat == null || cat.Behavior != CatBehavior.Skittish) return;
        if (cat.IsHeldByCustomer || reaction == null || reaction.IsPlaying) return;

        reaction.PlayFlinch();
        ShowText("!");
    }

    // ==========================================
    // TURNING (the only thing this script moves)
    // ==========================================

    // Turns the cat through a list of angles (degrees around its up axis, measured from where it started).
    // holdAtFirst = how long to pause after the first turn (e.g. an angry cat holding its back to you).
    private void Turn(float[] angles, float[] durations, float holdAtFirst)
    {
        turning = StartCoroutine(TurnRoutine(angles, durations, holdAtFirst));
    }

    private IEnumerator TurnRoutine(float[] angles, float[] durations, float holdAtFirst)
    {
        Transform startParent = transform.parent;
        Quaternion startRotation = transform.localRotation;
        float from = 0f;

        for (int i = 0; i < angles.Length; i++)
        {
            float to = angles[i];
            float t = 0f;

            while (t < durations[i])
            {
                // Picked up or moved mid-turn: stop, and let the new spot decide its rotation
                if (transform.parent != startParent) { turning = null; yield break; }

                t += Time.deltaTime;
                float eased = Juice.EaseOut(Mathf.Clamp01(t / durations[i]));
                transform.localRotation = startRotation * Quaternion.Euler(0f, Mathf.Lerp(from, to, eased), 0f);
                yield return null;
            }

            from = to;
            if (i == 0 && holdAtFirst > 0f) yield return new WaitForSeconds(holdAtFirst);
        }

        // Always finish facing exactly the way it started
        if (transform.parent == startParent) transform.localRotation = startRotation;
        turning = null;
        // Carry on noticing from where the cat now faces, so there's no snap
        noticeYaw = Mathf.DeltaAngle(0f, transform.localEulerAngles.y);
    }

    private void ShowText(string text)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (Vector3.Distance(cam.transform.position, transform.position) > textWithinDistance) return;

        FloatingNumbers.Show(cat.TopPosition, text);
    }
}