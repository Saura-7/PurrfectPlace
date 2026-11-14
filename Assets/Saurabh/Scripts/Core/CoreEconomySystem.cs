using UnityEngine;
using System;
using System.Collections.Generic;

public class CoreEconomySystem : MonoBehaviour
{

    // 1. SINGLETON SETUP
    public static CoreEconomySystem Instance { get; private set; }

    // 2. STATE VARIABLES (Strictly Private)
    [SerializeField] private int currentGP = 0;
    [SerializeField] private int reputation = 0;
    // [SerializeField] private int currentDay = 1;

    // Event for the UI to listen to
    public event Action OnEconomyUpdated;

    private void Awake()
    {
   
    #if !UNITY_EDITOR && !DEVELOPMENT_BUILD
    Debug.unityLogger.logEnabled = false;
    #endif

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }


    // 3. THE AUDIT TRAIL LOGIC
    // private void LogAudit(string subsystemName, string action, int value, string variableName, int newValue)
    // {
    //     // Formats as: [AUDIT] GroomingSystem | ADD | Value: -5 | Target: GP | New Total: 45
    //     // Debug.Log($"<b><color=#00FF00>[AUDIT]</color></b> {subsystemName} | {action} | Value: {value} | Target: {variableName} | New Total: {newValue}");
    // }

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
        Tally(amount >= 0 ? $"GP in  · {subsystemName}" : $"GP out · {subsystemName}", Mathf.Abs(amount));   // for the "ledger" command
        
        string actionType = amount >= 0 ? "ADD" : "SUBTRACT";
        SOALogger.LogAudit(subsystemName, actionType, amount, "GP", currentGP);
        
        OnEconomyUpdated?.Invoke(); // Tell the UI to refresh
    }

    // Used exclusively for Loading save files
    public void OverwriteGP(int exactValue, string subsystemName)
    {
        currentGP = exactValue;
        SOALogger.LogAudit(subsystemName, "OVERWRITE", exactValue, "GP", currentGP);
        OnEconomyUpdated?.Invoke();
    }

    // ==========================================
    // LEDGER: what happened today, for testing (console command "ledger")
    // ==========================================

    // line name -> running total, e.g. "Adopted · Purrfect" -> 3, "GP out · CareSubsystem" -> 412
    private readonly Dictionary<string, int> ledger = new Dictionary<string, int>();

    // Adds to one line of today's ledger. Any script can call it: CoreEconomySystem.Tally("Stray taken in");
    public static void Tally(string line, int amount = 1)
    {
        if (Instance == null) return;
        Instance.ledger.TryGetValue(line, out int current);   // 0 if the line doesn't exist yet
        Instance.ledger[line] = current + amount;
    }

    public void ResetLedger() => ledger.Clear();

    public string LedgerText()
    {
        if (ledger.Count == 0) return "Nothing has happened yet today.";
        List<string> lines = new List<string>(ledger.Keys);
        lines.Sort();
        string text = "Today so far:";
        foreach (string line in lines) text += $"\n  {line}: {ledger[line]}";
        return text;
    }

    // --- REPUTATION ---
    public int GetReputation(string subsystemName) => reputation;

    public void ModifyReputation(int amount, string subsystemName)
    {
        reputation = Mathf.Max(0, reputation + amount);   // never drops below 0
        string actionType = amount >= 0 ? "ADD" : "SUBTRACT";
        SOALogger.LogAudit(subsystemName, actionType, amount, "Reputation", reputation);
        
        OnEconomyUpdated?.Invoke();
    }
}