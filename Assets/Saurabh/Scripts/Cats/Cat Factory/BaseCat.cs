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

    public const int MaxLevel = 6;

    [Range(0, MaxLevel)] public int feedingLevel = 0;
    [Range(0, MaxLevel)] public int cleanlinessLevel = 0;

    public bool CanUpgradeFeeding => feedingLevel < MaxLevel;
    public bool CanUpgradeCleanliness => cleanlinessLevel < MaxLevel;

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
        MessageUI.Notify("Feeding level upgraded! to " + feedingLevel);
    }

    public void UpgradeCleanliness()
    {
        if (cleanlinessLevel < MaxLevel) cleanlinessLevel++;
        // Debug.Log($"[BaseCat] Cleanliness level upgraded to {cleanlinessLevel} for {name}");
        MessageUI.Notify("Cleanliness level upgraded! to " + cleanlinessLevel);
    }

    public int CalculateFinalPrice()
    {
        // Base price is 50. Each stat point (max 12 total) adds 10 to the value.
        // Debug.Log($"[BaseCat] Final price calculated for {name}: 50 + ({feedingLevel} * 10) + ({cleanlinessLevel} * 10)");
        return 50 + (feedingLevel * 10) + (cleanlinessLevel * 10);
    }
}