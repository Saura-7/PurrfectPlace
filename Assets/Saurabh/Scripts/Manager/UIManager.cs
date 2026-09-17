using UnityEngine;
using TMPro; // Required for TextMeshPro

public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI gpText;
    public TextMeshProUGUI reputationText;

    private readonly string systemName = "UIManager";

    private void Start()
    {
        // 1. Subscribe to the Core Economy event
        // Whenever ModifyGP or ModifyReputation is called, RefreshUI will run automatically.
        CoreEconomySystem.Instance.OnEconomyUpdated += RefreshUI;

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

        // Update the visual text
        gpText.text = $"Gold Points: {currentGP}";
        reputationText.text = $"Reputation: {currentReputation}";
    }
}