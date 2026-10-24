using UnityEngine;

/// <summary>
/// Turns the reputation number into a level (New shelter, Trusted, Beloved).
/// A higher level adds a donation to every adoption. It does NOT make customers come faster,
/// so doing well never makes the game more stressful.
/// Other scripts read it with ReputationLevels.Name and ReputationLevels.Donation.
/// Put this on the same object as CoreEconomySystem (or any manager object).
/// </summary>
public class ReputationLevels : MonoBehaviour
{
    private static ReputationLevels instance;

    [Header("Reputation needed for each level")]
    [SerializeField] private int trustedAt = 150;
    [SerializeField] private int belovedAt = 400;

    [Header("Donation added to every adoption (GP)")]
    [SerializeField] private int trustedDonation = 5;
    [SerializeField] private int belovedDonation = 10;

    private int lastLevel;

    // ==========================================
    // READ BY OTHER SCRIPTS
    // ==========================================

    // 0 = New shelter, 1 = Trusted, 2 = Beloved
    public static int Level
    {
        get
        {
            if (instance == null || CoreEconomySystem.Instance == null) return 0;
            return instance.LevelFor(CoreEconomySystem.Instance.GetReputation("ReputationLevels"));
        }
    }

    public static string Name
    {
        get
        {
            if (Level == 2) return "Beloved";
            if (Level == 1) return "Trusted";
            return "New shelter";
        }
    }

    // The extra GP added to each adoption right now
    public static int Donation
    {
        get
        {
            if (instance == null) return 0;
            if (Level == 2) return instance.belovedDonation;
            if (Level == 1) return instance.trustedDonation;
            return 0;
        }
    }

    // ==========================================
    // INSIDE THE LEVELS
    // ==========================================

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        lastLevel = Level;
        CoreEconomySystem.Instance.OnEconomyUpdated += CheckForLevelUp;
    }

    private void OnDestroy()
    {
        if (CoreEconomySystem.Instance != null)
            CoreEconomySystem.Instance.OnEconomyUpdated -= CheckForLevelUp;

        if (instance == this) instance = null;
    }

    private int LevelFor(int reputation)
    {
        if (reputation >= belovedAt) return 2;
        if (reputation >= trustedAt) return 1;
        return 0;
    }

    // Runs every time GP or reputation changes. Only speaks up when the level goes UP.
    private void CheckForLevelUp()
    {
        int now = Level;

        if (now > lastLevel)
        {
            int donation = now == 2 ? belovedDonation : trustedDonation;
            MessageUI.Notify($"Your shelter is now {Name}! Adopters add a +{donation} GP donation.");
        }

        lastLevel = now;
    }
}