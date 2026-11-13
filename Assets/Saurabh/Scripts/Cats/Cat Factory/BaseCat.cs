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

    public bool IsHeldByCustomer => GetComponentInParent<CustomerAI>() != null; 

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