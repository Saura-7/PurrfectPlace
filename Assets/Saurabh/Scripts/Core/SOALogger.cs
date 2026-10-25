using UnityEngine;

public static class SOALogger
{
    // Subsystem Color Palette (Hex Codes)
    public static class Colors
    {
        public const string Economy       = "#00FF00"; // Green
        public const string Reception     = "#FFD700"; // Gold
        public const string Customer      = "#FF69B4"; // Pink
        public const string Cage          = "#FFA500"; // Orange
        public const string Queue         = "#1E90FF"; // Blue
        public const string Grooming      = "#00FFFF"; // Cyan
    }

    /// <summary>
    /// Logs strict financial and reputation transactions using the [AUDIT] tag.
    /// </summary>
    public static void LogAudit(string sourceSubsystem, string action, int value, string target, int newTotal)
    {
        string formattedValue = value >= 0 ? $"+{value}" : value.ToString();
        string log = $"[AUDIT] <color={Colors.Economy}> {sourceSubsystem} </color>| {action} | Value: {formattedValue} | Target: {target} | New Total: {newTotal}";
        Debug.Log(log);
    }

    /// <summary>
    /// Logs subsystem state transitions, events, and operations using the [StateChange] tag.
    /// </summary>
    public static void LogState(string subsystemName, string colorHex, string action, string message)
    {
        string log = $"[StateChange] <color={colorHex}> {subsystemName} </color>| {action} | {message}";
        Debug.Log(log);
    }

    /// <summary>
    /// Logs subsystem warnings with formatted colors.
    /// </summary>
    public static void LogWarning(string subsystemName, string colorHex, string message)
    {
            string log = $"[WARNING]> <color={colorHex}> {subsystemName} </color | {message}";
            Debug.LogWarning(log);
    }
}