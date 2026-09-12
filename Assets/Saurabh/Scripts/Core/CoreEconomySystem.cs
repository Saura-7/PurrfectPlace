using UnityEngine;
using System;

public class CoreEconomySystem : MonoBehaviour
{
    // 1. SINGLETON SETUP
    public static CoreEconomySystem Instance { get; private set; }

    // 2. STATE VARIABLES (Strictly Private)
    [SerializeField] private int currentGP = 0;
    [SerializeField] private int reputation = 0;
    [SerializeField] private int currentDay = 1;

    // Event for the UI to listen to
    public event Action OnEconomyUpdated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 3. THE AUDIT TRAIL LOGIC
    private void LogAudit(string subsystemName, string action, int value, string variableName, int newValue)
    {
        // Formats as: [AUDIT] GroomingSystem | ADD | Value: -5 | Target: GP | New Total: 45
        Debug.Log($"<b><color=#00FF00>[AUDIT]</color></b> {subsystemName} | {action} | Value: {value} | Target: {variableName} | New Total: {newValue}");
    }

    // ==========================================
    // 4. GETTERS & SETTERS
    // ==========================================

    // --- GOLD POINTS (GP) ---
    public int GetGP(string subsystemName)
    {
        // Optional: Uncomment the line below if you want to track reads as well as writes
        // LogAudit(subsystemName, "GET", 0, "GP", currentGP);
        return currentGP;
    }

    public void ModifyGP(int amount, string subsystemName)
    {
        currentGP += amount;
        
        string actionType = amount >= 0 ? "ADD" : "SUBTRACT";
        LogAudit(subsystemName, actionType, amount, "GP", currentGP);
        
        OnEconomyUpdated?.Invoke(); // Tell the UI to refresh
    }

    // Used exclusively for Loading save files
    public void OverwriteGP(int exactValue, string subsystemName)
    {
        currentGP = exactValue;
        LogAudit(subsystemName, "OVERWRITE", exactValue, "GP", currentGP);
        OnEconomyUpdated?.Invoke();
    }

    // --- REPUTATION ---
    public int GetReputation(string subsystemName) => reputation;

    public void ModifyReputation(int amount, string subsystemName)
    {
        reputation += amount;
        string actionType = amount >= 0 ? "ADD" : "SUBTRACT";
        LogAudit(subsystemName, actionType, amount, "Reputation", reputation);
        
        OnEconomyUpdated?.Invoke();
    }
}