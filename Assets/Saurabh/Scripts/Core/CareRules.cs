using UnityEngine;

// The four care tiers. "care" = feeding level + cleanliness level (0 to 12).
public enum CareTier { Neglected, Average, WellCared, Purrfect }

/// <summary>
/// Every care number in the game lives here, so you can tune them in ONE Inspector.
/// What care costs, what it adds to a cat's worth, where each tier starts, and the reputation each tier earns.
/// Other scripts only ask questions (CareRules.NextCost, CareRules.Value, CareRules.TierOf ...).
/// Put this on the same manager object as CoreEconomySystem and ReputationLevels.
/// </summary>
public class CareRules : MonoBehaviour
{
    private static CareRules instance;

    [Header("Cat worth")]
    [SerializeField] private int baseValue = 30;        // worth of a cat with no care
    [SerializeField] private int valuePerPoint = 15;    // worth added by each care point

    [Header("Care cost (rises with each level)")]
    [SerializeField] private int firstLevelCost = 5;    // cost of level 1
    [SerializeField] private int costStepPerLevel = 3;  // each level after that costs this much more
    // With 5 and 3: level 1 = 5, 2 = 8, 3 = 11, 4 = 14, 5 = 17, 6 = 20

    [Header("Where each tier starts (feed + clean)")]
    [SerializeField] private int averageFrom = 4;
    [SerializeField] private int wellCaredFrom = 8;
    [SerializeField] private int purrfectFrom = 12;

    [Header("Reputation for adopting out a cat of each tier")]
    [SerializeField] private int neglectedRep = 0;
    [SerializeField] private int averageRep = 3;
    [SerializeField] private int wellCaredRep = 8;
    [SerializeField] private int purrfectRep = 12;

    [Header("Tier colours on the cat tag and deal screen")]
    [SerializeField] private Color neglectedColor = new Color(0.75f, 0.75f, 0.75f);
    [SerializeField] private Color averageColor = new Color(0.85f, 0.65f, 0.25f);
    [SerializeField] private Color wellCaredColor = new Color(0.35f, 0.70f, 0.35f);
    [SerializeField] private Color purrfectColor = new Color(0.90f, 0.45f, 0.65f);

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Finds the rules even if another script asks before our Awake has run
    private static CareRules I
    {
        get
        {
            if (instance == null) instance = FindFirstObjectByType<CareRules>();
            if (instance == null) Debug.LogError("[CareRules] No CareRules in the scene. Add it to the manager object.");
            return instance;
        }
    }

    // ==========================================
    // COST
    // ==========================================

    // What it costs to reach this level (1 to 6)
    public static int CostForLevel(int level)
    {
        return I.firstLevelCost + I.costStepPerLevel * (level - 1);
    }

    // What the NEXT feed (or clean) on this cat would cost. 0 if the stat is already maxed.
    public static int NextCost(BaseCat cat, bool feeding)
    {
        int current = feeding ? cat.feedingLevel : cat.cleanlinessLevel;
        if (current >= BaseCat.MaxLevel) return 0;
        return CostForLevel(current + 1);
    }

    // ==========================================
    // WORTH
    // ==========================================

    public static int ValuePerPoint => I.valuePerPoint;

    public static int Value(BaseCat cat)
    {
        return I.baseValue + cat.TotalCare * I.valuePerPoint;
    }

    // ==========================================
    // TIERS
    // ==========================================

    public static CareTier TierOf(int care)
    {
        if (care >= I.purrfectFrom) return CareTier.Purrfect;
        if (care >= I.wellCaredFrom) return CareTier.WellCared;
        if (care >= I.averageFrom) return CareTier.Average;
        return CareTier.Neglected;
    }

    // Reputation for adopting out a cat with this much care
    public static int ReputationFor(int care)
    {
        switch (TierOf(care))
        {
            case CareTier.Purrfect: return I.purrfectRep;
            case CareTier.WellCared: return I.wellCaredRep;
            case CareTier.Average: return I.averageRep;
            default: return I.neglectedRep;
        }
    }

    // How much care the next tier needs (-1 when the cat is already Purrfect)
    public static int NextTierAt(int care)
    {
        if (care < I.averageFrom) return I.averageFrom;
        if (care < I.wellCaredFrom) return I.wellCaredFrom;
        if (care < I.purrfectFrom) return I.purrfectFrom;
        return -1;
    }

    public static string Name(CareTier tier)
    {
        switch (tier)
        {
            case CareTier.Purrfect: return "Purrfect";
            case CareTier.WellCared: return "Well-cared";
            case CareTier.Average: return "Average";
            default: return "Neglected";
        }
    }

    // The tier name wrapped in its colour, ready for a TextMeshPro label
    public static string ColoredName(CareTier tier)
    {
        Color c;
        switch (tier)
        {
            case CareTier.Purrfect: c = I.purrfectColor; break;
            case CareTier.WellCared: c = I.wellCaredColor; break;
            case CareTier.Average: c = I.averageColor; break;
            default: c = I.neglectedColor; break;
        }
        return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{Name(tier)}</color>";
    }
}