using UnityEngine;

public class BaseCat : MonoBehaviour
{
    [Header("3D Components")]
    // public SkinnedMeshRenderer catMeshRenderer; // Required to change materials (patterns)
    public MeshRenderer catMeshRenderer; // Changed to MeshRenderer for simplicity
    public Animator catAnimator;
    public AudioSource catAudio;

    [Header("Current Traits (Read-Only)")]
    // We make these public so subsystems can read them, but use [SerializeField] to see them in Inspector
    [SerializeField] private CatBehavior currentBehavior;
    [SerializeField] private CatBody currentBody;
    [SerializeField] private CatPattern currentPattern;

    // Getters for subsystems to read the DNA
    public CatBehavior Behavior => currentBehavior;
    public CatBody Body => currentBody;
    public CatPattern Pattern => currentPattern;

    /// <summary>
    /// Called by the CatFactory immediately after spawning. 
    /// This turns the blank prefab into a specific variation.
    /// </summary>
    public void InitializeCat(CatBehavior behavior, CatBody body, CatPattern pattern, Material material, AudioClip voiceClip)
    {
        // 1. Store the DNA
        currentBehavior = behavior;
        currentBody = body;
        currentPattern = pattern;

        // 2. Apply the Visuals
        if (catMeshRenderer != null && material != null)
        {
            catMeshRenderer.material = material;
        }

        // 3. Apply the Audio
        if (catAudio != null && voiceClip != null)
        {
            catAudio.clip = voiceClip;
        }

        // 4. Update the Animator State
        // (Assuming the Animator has an Integer parameter called "BehaviorType" to determine idle animations)
        if (catAnimator != null)
        {
            catAnimator.SetInteger("BehaviorType", (int)currentBehavior);
        }

        Debug.Log($"Initialized a {currentPattern} {currentBody} cat that is {currentBehavior}.");
    }

    /// <summary>
    /// Called by Subsystems (like GroomingSubsystem) to make the cat react physically.
    /// </summary>
    public void PlayInteractAnimation()
    {
        if (catAnimator != null)
        {
            catAnimator.SetTrigger("Interact");
        }
        
        if (catAudio != null && catAudio.clip != null)
        {
            catAudio.Play();
        }
    }
}