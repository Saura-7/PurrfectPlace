using UnityEngine;

/// <summary>
/// A cat taking a proper nap: it sinks into a loaf, breathes slowly, keeps its eyes shut,
/// droops its head, stills its tail and lets out the odd "z". After a while it gets up again.
/// Any reaction (feeding, petting, a door chime for a skittish cat) wakes it straight away.
///
/// Who moves what while napping:
///   CatNap      → the cat's body scale and height (the loaf and the breathing)
///   CatFace     → still the head, eyes and tail; it asks IsAsleep to know how to look
///   CatReaction → calls WakeUp() BEFORE it remembers the cat's size, so the two never fight
/// If something else moves the cat (a customer picks it up, it becomes a resident),
/// the nap just stops and leaves the new spot's pose alone.
/// </summary>
[RequireComponent(typeof(BaseCat))]
public class CatNap : MonoBehaviour
{
    [Header("The loaf")]
    [SerializeField] private float settle = 0.07f;          // 7% flatter while asleep
    [SerializeField] private float spread = 0.03f;          // 3% wider, like a cat melting into the floor
    [SerializeField] private float easeTime = 1.2f;         // seconds to settle down, and to get up again

    [Header("Breathing")]
    [SerializeField] private float breathDepth = 0.015f;    // how much the body swells per breath
    [SerializeField] private float breathsPerSecond = 0.3f; // slow and deep

    [Header("Zzz")]
    [SerializeField] private float zEvery = 3f;             // seconds between floating "z"s (0 = none)

    private BaseCat cat;
    private bool napping;
    private float napLeft;       // seconds of sleep left (then it gets up)
    private float weight;        // 0 = awake pose, 1 = fully settled loaf
    private float nextZ;
    private float breathPhase;   // random, so napping cats side by side don't breathe in sync

    // The pose before the nap, given back exactly when it ends
    private Transform napParent;
    private Vector3 napPosition, napScale;
    private float footDepth;     // world distance from the cat's pivot down to the bottom of its body

    /// <summary>True from lying down until fully up again (CatIdle waits for this).</summary>
    public bool IsNapping => napping;

    /// <summary>True while actually asleep, not while getting up (CatFace shuts the eyes for this).</summary>
    public bool IsAsleep => napping && napLeft > 0f;

    private void Awake()
    {
        cat = GetComponent<BaseCat>();
        breathPhase = Random.Range(0f, 10f);
    }

    /// <summary>Lie down for some seconds. Ignored if already napping or held by a customer.</summary>
    public void StartNap(float seconds)
    {
        if (napping || cat.IsHeldByCustomer) return;

        napping = true;
        napLeft = seconds;
        weight = 0f;
        nextZ = Time.time + 1.5f;

        napParent = transform.parent;
        napPosition = transform.localPosition;
        napScale = transform.localScale;
        footDepth = transform.position.y - cat.VisualBounds().min.y;
    }

    /// <summary>Wake up instantly and stand exactly as before. Safe to call when not napping.</summary>
    public void WakeUp()
    {
        if (!napping) return;

        if (transform.parent == napParent)
        {
            transform.localPosition = napPosition;
            transform.localScale = napScale;
        }
        napping = false;
        weight = 0f;
    }

    private void Update()
    {
        if (!napping) return;

        // Moved by someone else (picked up, made a resident): their spot decides the pose now
        if (transform.parent != napParent || cat.IsHeldByCustomer)
        {
            napping = false;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // paused: freeze mid-breath with everything else

        napLeft -= dt;

        // Ease towards the loaf while asleep, and back to standing once the time is up
        float goal = napLeft > 0f ? 1f : 0f;
        weight = Mathf.MoveTowards(weight, goal, dt / easeTime);

        if (napLeft <= 0f && weight <= 0f)
        {
            WakeUp();   // fully up: hand back the exact pose
            return;
        }

        ApplyPose();

        if (zEvery > 0f && IsAsleep && Time.time >= nextZ)
        {
            nextZ = Time.time + zEvery;
            Vector3 top = cat.VisualBounds().max;
            FloatingNumbers.Show(new Vector3(transform.position.x, top.y, transform.position.z),
                                 "<size=70%><i><color=#9FB8C8>z</color></i></size>");
        }
    }

    private void ApplyPose()
    {
        float w = Mathf.SmoothStep(0f, 1f, weight);   // eases in and out, so lying down isn't robotic

        // A sine wave: slowly in... and out. Only while asleep (times w).
        float breath = Mathf.Sin((Time.time + breathPhase) * breathsPerSecond * Mathf.PI * 2f) * breathDepth * w;

        float sy = 1f - settle * w + breath;           // height: flatter, plus the breath
        float sxz = 1f + spread * w + breath * 0.5f;   // width: a little wider, half the breath

        transform.localScale = new Vector3(napScale.x * sxz, napScale.y * sy, napScale.z * sxz);

        // Scaling happens around the pivot, which sits above the feet. Squashing down would
        // lift the feet off the floor, so lower the cat by exactly the amount the feet rose.
        transform.localPosition = napPosition;
        transform.position += Vector3.down * footDepth * (1f - sy);
    }
}