using UnityEngine;

/// <summary>
/// The rent rules, and nothing else.
/// DayManager asks this script what is due and tells it to settle the rent at the end of a day.
/// Put this on the same object as DayManager.
/// </summary>
public class RentSystem : MonoBehaviour
{
    public static RentSystem Instance { get; private set; }

    [Header("Rent Settings")]
    [SerializeField] private int firstRentDay = 2;            // Day 1 is free
    [SerializeField] private int baseRent = 40;               // PLACEHOLDER. Set it from your own test results
    [SerializeField, Range(0, 50)] private int lateFeePercent = 10;
    [SerializeField] private int missedPaymentsToClose = 2;   // Misses in a row before the shop closes

    // What this script remembers
    private int missedPayments;   // misses in a row
    private int lateFeeOwed;      // added to the next rent after a miss
    private int extraRent;        // added by store expansions later

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    // ==========================================
    // QUESTIONS DayManager can ask
    // ==========================================

    public bool IsRentDay(int day)
    {
        return day >= firstRentDay;
    }

    // The normal rent, without any late fee
    private int DailyRent
    {
        get { return baseRent + extraRent; }
    }

    // What the player must pay at the end of this day (0 before the first rent day)
    public int RentDue(int day)
    {
        return IsRentDay(day) ? DailyRent + lateFeeOwed : 0;
    }

    // The line shown on screen during the day
    public string GetRentLabel(int day)
    {
        if (!IsRentDay(day)) return $"Rent starts on day {firstRentDay}";
        if (lateFeeOwed > 0) return $"Rent due when you close: {RentDue(day)} GP (includes late fee)";
        return $"Rent due when you close: {RentDue(day)} GP";
    }

    // ==========================================
    // CHANGES
    // ==========================================

    // For store expansions later: each upgrade can raise the daily rent
    public static void AddRent(int amount)
    {
        if (Instance == null) return;

        Instance.extraRent += amount;
        if (DayManager.Instance != null) DayManager.Instance.RefreshRentText();
    }

    /// <summary>
    /// Settles the rent at the end of a day.
    /// Returns true if the shop has to close. The message is what the summary screen shows.
    /// </summary>
    public bool SettleRent(int day, int gpNow, out string message)
    {
        // Before the first rent day there is nothing to pay
        if (!IsRentDay(day))
        {
            message = $"Rent starts on day {firstRentDay}: {DailyRent} GP per day.";
            return false;
        }

        int due = RentDue(day);

        // Case 1: the player can pay
        if (gpNow >= due)
        {
            CoreEconomySystem.Instance.ModifyGP(-due, "RentSystem");
            missedPayments = 0;
            lateFeeOwed = 0;
            message = $"Rent paid: {due} GP\nGP left: {gpNow - due}";
            return false;
        }

        // Case 2: the player cannot pay
        missedPayments++;

        // Too many misses in a row: the shop closes
        if (missedPayments >= missedPaymentsToClose)
        {
            message = $"The rent of {due} GP could not be paid. You had {gpNow} GP.";
            return true;
        }

        // First miss: a notice and a late fee on tomorrow's rent
        lateFeeOwed = Mathf.CeilToInt(DailyRent * lateFeePercent / 100f);
        message = $"The rent of {due} GP could not be paid. You had {gpNow} GP.\n" +
                  $"The landlord sent a notice. Tomorrow's rent includes a late fee of {lateFeeOwed} GP.";
        return false;
    }
}