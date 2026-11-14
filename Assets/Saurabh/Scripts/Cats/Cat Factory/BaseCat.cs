using UnityEngine;

public class BaseCat : MonoBehaviour
{
    [Header("3D Components")]
    // public MeshRenderer catMeshRenderer; // Using MeshRenderer for the prototype cube
    public Renderer[] bodyRenderers; // Every part that should receive the pattern material
    public Animator catAnimator;
    public AudioSource catAudio;

    [Header("Current Traits (DNA)")]
    [SerializeField] private CatBehavior currentBehavior;
    [SerializeField] private CatBody currentBody;
    [SerializeField] private CatPattern currentPattern;

    [Header("MVP Gameplay Stats")]
    public bool isAvailable = true; // True if waiting on a shelf. False if a customer claimed it.

    // The cat's worth and care costs now live in CareRules (one place to tune them)

    public const int MaxLevel = 6;

    [Range(0, MaxLevel)] public int feedingLevel = 0;
    [Range(0, MaxLevel)] public int cleanlinessLevel = 0;

    public bool CanUpgradeFeeding => feedingLevel < MaxLevel;
    public bool CanUpgradeCleanliness => cleanlinessLevel < MaxLevel;
    public int TotalCare => feedingLevel + cleanlinessLevel;   // 0 to 12, decides the care tier
    // The top of the cat's head in the world (used by the info tag)
    public Vector3 TopPosition
    {
        get
        {
            Bounds b = WorldBounds();
            return b.center + Vector3.up * b.extents.y;
        }
    }

    // The middle of the cat's body in the world (used by the pop-up numbers)
    public Vector3 CenterPosition => WorldBounds().center;

    // An invisible box around the cat, in world space.
    // We use the collider first: it's the same box the crosshair raycast hits, so it's always where the cat is.
    // (The cat prefab's ROOT has a MeshRenderer with no mesh. Its bounds never move, so they stay
    //  wherever the cat was created: fine for starter cats made in a cage, wrong for a Giver's cat made at the door.)
    private Bounds WorldBounds()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && col.enabled) return col.bounds;

        // Fallback: the first renderer that actually draws a mesh
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            MeshFilter filter = r.GetComponent<MeshFilter>();
            bool drawsSomething = !(r is MeshRenderer) || (filter != null && filter.sharedMesh != null);
            if (drawsSomething) return r.bounds;
        }
        return new Bounds(transform.position, Vector3.zero);
    }

    // True while the cat sits on the reception desk during a deal (set by CustomerAI)
    public bool OnDesk { get; set; }

    // "Belongs to a customer right now": in their hands OR on the desk in a deal.
    // Care and reactions check this, so you can't feed a cat that's being sold or bought.
    public bool IsHeldByCustomer => OnDesk || GetComponentInParent<CustomerAI>() != null;

    // ==========================================
    // MOVING THE CAT (hand, desk, cage)
    // ==========================================

    // The cat's real size in the world, remembered the first time it moves.
    // Why: a child's size is MULTIPLIED by its parent's size. If a parent is stretched
    // (a desk scaled to 3 x 1 x 1.5), the cat gets stretched too, and it keeps that shape
    // when it moves on. Re-applying the remembered size after every move undoes that.
    private Vector3 naturalSize;
    private bool hasNaturalSize;

    /// <summary>
    /// Puts the cat on a new spot (a hand, the desk, a cage) at its normal size.
    /// standOnSurface = true drops it so its feet touch the desk or cage floor below.
    /// </summary>
    public void MoveTo(Transform parent, bool standOnSurface = true)
    {
        if (!hasNaturalSize)
        {
            naturalSize = transform.lossyScale;   // lossyScale = the size you actually see in the world
            hasNaturalSize = true;
        }

        // "false" = don't try to keep the old world position/size; we set them ourselves below
        transform.SetParent(parent, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Divide by the parent's size, so (parent size x our local size) = our natural size
        Vector3 p = parent != null ? parent.lossyScale : Vector3.one;
        if (p.x != 0f && p.y != 0f && p.z != 0f)
            transform.localScale = new Vector3(naturalSize.x / p.x, naturalSize.y / p.y, naturalSize.z / p.z);

        if (standOnSurface) StandOnSurface();
    }

    // Why this is needed: the cat model's pivot (the point Unity puts on the spot) is NOT at its feet.
    // Skinny and normal cats are scaled down around that pivot, so their feet ended up in the air,
    // and on the desk the big ones sank in. Instead of trusting the pivot, we find the real
    // surface under the cat and move the cat so the bottom of its body sits exactly on it.
    private void StandOnSurface()
    {
        Bounds body = VisualBounds();
        if (body.size == Vector3.zero) return;

        // Shoot a ray straight down from the TOP of the cat. Starting at the top (not the middle)
        // means it still finds the desk when the cat has sunk halfway into it.
        Vector3 start = new Vector3(body.center.x, body.max.y, body.center.z);
        RaycastHit[] hits = Physics.RaycastAll(start, Vector3.down, body.size.y + 2f, ~0, QueryTriggerInteraction.Ignore);

        // Find the closest hit that is not part of this cat
        float closest = float.MaxValue;
        float surfaceY = 0f;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;   // our own collider
            if (hit.distance < closest)
            {
                closest = hit.distance;
                surfaceY = hit.point.y;
                found = true;
            }
        }

        if (!found) return;   // nothing below (no collider): leave the cat where the spot put it

        // Lift (or lower) the cat by the gap between its feet and the surface
        transform.position += Vector3.up * (surfaceY - body.min.y);
    }

    // The box around everything the cat draws (body, head, tail), in world space.
    // Renderer bounds update the moment the cat moves, unlike collider bounds,
    // which only update on the next physics step.
    private Bounds VisualBounds()
    {
        Bounds total = new Bounds(transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;  // skip particles etc.
            if (first) { total = r.bounds; first = false; }
            else total.Encapsulate(r.bounds);   // grow the box to include this part
        }
        return total;
    }

    // Getters for subsystems to read the DNA
    public CatBehavior Behavior => currentBehavior;
    public CatBody Body => currentBody;
    public CatPattern Pattern => currentPattern;

    /// <summary>
    /// Called by the CatFactory right after spawning.
    /// </summary>
    public void InitializeCat(CatBehavior behavior, CatBody body, CatPattern pattern, Material material, AudioClip voiceClip)
    {
        // 1. Store DNA
        currentBehavior = behavior;
        currentBody = body;
        currentPattern = pattern;
        
        // Reset MVP Stats for new cats
        isAvailable = true;
        feedingLevel = 0;
        cleanlinessLevel = 0;

        // 2. Apply Visuals & Audio
        if (material != null)
        {
            foreach (Renderer part in bodyRenderers)
            {
                if (part != null) part.sharedMaterial = material;
            }
        }
        if (catAudio != null && voiceClip != null) catAudio.clip = voiceClip;
        if (catAnimator != null) catAnimator.SetInteger("BehaviorType", (int)currentBehavior);
    }

    [ContextMenu("Fill Body Renderers")]
    private void FillBodyRenderers()
    {
        bodyRenderers = GetComponentsInChildren<Renderer>();
    }

    /// <summary>
    /// Called by Subsystems (like GroomingSubsystem) to make the cat react physically.
    /// </summary>
    public void PlayInteractAnimation()
    {
        if (catAnimator != null) catAnimator.SetTrigger("Interact");
        if (catAudio != null && catAudio.clip != null) catAudio.Play();
    }

    // ==========================================
    // MVP ECONOMY & STAT METHODS
    // ==========================================

    public void UpgradeFeeding()
    {
        if (feedingLevel < MaxLevel) feedingLevel++;
        // Debug.Log($"[BaseCat] Feeding level upgraded to {feedingLevel} for {name}");
        // MessageUI.Notify("Feeding level upgraded! to " + feedingLevel); // replaced by the pop-up number
    }

    public void UpgradeCleanliness()
    {
        if (cleanlinessLevel < MaxLevel) cleanlinessLevel++;
        // Debug.Log($"[BaseCat] Cleanliness level upgraded to {cleanlinessLevel} for {name}");
        // MessageUI.Notify("Cleanliness level upgraded! to " + cleanlinessLevel); // replaced by the pop-up number
    }

    public int CalculateFinalPrice()
    {
        // Base worth + worth per care point. The numbers are set in CareRules.
        return CareRules.Value(this);
    }
}