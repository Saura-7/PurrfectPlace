using UnityEngine;
using TMPro; // Required for TextMeshPro

/// <summary>
/// Keeps the money corner of the HUD up to date: GP, reputation, and the "+30" change.
/// GP doesn't jump to its new value: it rolls there over a short time and bumps,
/// so every sale and every cost FEELS like money moving (the "cha-ching" moment).
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI gpText;
    public TextMeshProUGUI reputationText;

    [Header("Rolling GP counter")]
    [SerializeField] private float rollTime = 0.4f;               // seconds to count to the new value
    [SerializeField] private TextMeshProUGUI gpChangeText;         // the small "+30" beside GP (optional)
    [SerializeField] private float changeShowTime = 1.2f;          // how long "+30" stays before fading
    [SerializeField] private Color gainColor = new Color32(0x3F, 0xA2, 0x8C, 0xFF);   // green
    [SerializeField] private Color lossColor = new Color32(0xE8, 0x5D, 0x4A, 0xFF);   // red

    private readonly string systemName = "UIManager";

    // The counter
    private bool started;          // false until the first refresh (so the game doesn't start by rolling up from 0)
    private int targetGP;          // the real GP
    private float rollFrom;        // the number shown when the current roll began
    private float rollProgress = 1f;   // 0 = roll just started, 1 = finished

    // The "+30" label
    private int changeTotal;       // adds up changes that come quickly one after another
    private float changeTimer;     // seconds left before the label fades

    private void Start()
    {
        // 1. Subscribe to the Core Economy event
        // Whenever ModifyGP or ModifyReputation is called, RefreshUI will run automatically.
        CoreEconomySystem.Instance.OnEconomyUpdated += RefreshUI;

        if (gpChangeText != null) gpChangeText.alpha = 0f;   // hidden until money changes

        // 2. Force an initial update so the text doesn't say "New Text" when the game starts
        RefreshUI();
    }

    private void OnDestroy()
    {
        // CRITICAL: Always unsubscribe when this object is destroyed (e.g., changing scenes)
        // If the team forgets this, Unity will try to update a deleted UI element and throw NullReferenceExceptions.
        if (CoreEconomySystem.Instance != null)
        {
            CoreEconomySystem.Instance.OnEconomyUpdated -= RefreshUI;
        }
    }

    private void RefreshUI()
    {
        // Use the strict getter method required by your SOA pattern
        int currentGP = CoreEconomySystem.Instance.GetGP(systemName);
        int currentReputation = CoreEconomySystem.Instance.GetReputation(systemName);

        reputationText.text = $"Rep {currentReputation} · {ReputationLevels.Name}";

        if (!started)
        {
            // First refresh: just show the number, no animation
            started = true;
            targetGP = currentGP;
            gpText.text = $"{currentGP} GP";
            return;
        }

        int delta = currentGP - targetGP;
        if (delta == 0) return;   // only reputation changed

        // Start a new roll from whatever number is on screen right now
        rollFrom = ShownGP();
        targetGP = currentGP;
        rollProgress = 0f;

        Juice.Punch(gpText.transform, 0.12f);
        ShowChange(delta);
    }

    private void Update()
    {
        // unscaledDeltaTime: keeps counting while the game is frozen (rent is paid on the day summary)
        float dt = Time.unscaledDeltaTime;

        if (rollProgress < 1f)
        {
            rollProgress = Mathf.Min(1f, rollProgress + dt / rollTime);
            gpText.text = $"{ShownGP()} GP";
        }

        if (gpChangeText != null && changeTimer > 0f)
        {
            changeTimer -= dt;
            // Fully visible most of the time, fades over the last 0.3 seconds
            gpChangeText.alpha = Mathf.Clamp01(changeTimer / 0.3f);
            if (changeTimer <= 0f) changeTotal = 0;   // next change starts a new total
        }
    }

    // The number on screen right now, part-way between rollFrom and targetGP.
    // Ease-out: counts fast at first and slows down as it arrives.
    private int ShownGP()
    {
        return Mathf.RoundToInt(Mathf.Lerp(rollFrom, targetGP, Juice.EaseOut(rollProgress)));
    }

    // Shows "+30" (green) or "-8" (red) beside the GP.
    // Changes close together add up: an adoption fee and a donation become one "+45".
    private void ShowChange(int delta)
    {
        if (gpChangeText == null) return;

        changeTotal += delta;
        changeTimer = changeShowTime;

        gpChangeText.text = changeTotal > 0 ? $"+{changeTotal}" : changeTotal.ToString();
        gpChangeText.color = changeTotal >= 0 ? gainColor : lossColor;
        gpChangeText.alpha = 1f;   // set AFTER color, because color includes alpha
        Juice.Punch(gpChangeText.transform, 0.2f);
    }
}