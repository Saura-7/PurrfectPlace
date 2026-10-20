using UnityEngine;
using TMPro;

/// <summary>
/// Runs the days and the rent.
/// - Every day starts in a PREP phase: the shop is closed, the day timer is frozen, and the player can care for the cats.
/// - The player opens the shop at the Open sign. Customers arrive and the timer runs.
/// - At the end of the day everyone goes home, the game freezes, and a summary is shown.
/// - Day 1 is free. From the first rent day, rent is taken at the end of every day.
/// - Missing rent once brings a notice and a late fee. Missing it twice in a row closes the shop.
/// </summary>
public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    private readonly string systemName = "DayManager";

    [Header("Day Settings")]
    [SerializeField] private float dayLengthSeconds = 300f;   // 5 minutes of open shop
    [SerializeField] private bool requireOpenSign = true;     // Untick to open the shop automatically (handy for testing)

    [Header("Rent Settings")]
    [SerializeField] private int firstRentDay = 2;            // Day 1 is free
    [SerializeField] private int baseRent = 40;               // PLACEHOLDER. Set it from your own test results
    [SerializeField, Range(0, 50)] private int lateFeePercent = 10;
    [SerializeField] private int missedPaymentsToClose = 2;   // Misses in a row before the shop closes

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI rentText;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TextMeshProUGUI resultsText;
    [SerializeField] private GameObject nextDayButton;

    // Day state
    private int day = 1;
    private float timeLeft;
    private int lastShownSeconds = -1;
    private bool shopOpen;
    private bool dayEnded;
    private bool rentWarned;

    // Money state for the current day
    private int dayStartGP;
    private int dayStartReputation;

    // Rent state
    private int missedPayments;   // misses in a row
    private int lateFeeOwed;      // added to the next rent after a miss
    private int extraRent;        // added by store expansions later

    // Simple stats for the summary (reset every day)
    private int catsSold;
    private int catsBought;
    private int dealsRejected;

    // What is due tonight (0 before the first rent day)
    private int RentToday => day >= firstRentDay ? baseRent + extraRent + lateFeeOwed : 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        BeginDay();
    }

    private void Update()
    {
        // The day timer only runs while the shop is open
        if (dayEnded || !shopOpen) return;

        // Time.deltaTime is 0 while the game is paused, so the timer stops by itself
        timeLeft -= Time.deltaTime;
        UpdateTimerText();

        // Give the player a fair warning before the rent is taken
        if (!rentWarned && day >= firstRentDay && timeLeft <= 30f)
        {
            rentWarned = true;
            int gp = CoreEconomySystem.Instance.GetGP(systemName);
            string warning = $"Rent is due in 30 seconds: {RentToday} GP";
            if (gp < RentToday) warning += $". You have {gp} GP.";
            MessageUI.Notify(warning);
        }

        if (timeLeft <= 0f) EndDay();
    }

    // ==========================================
    // CALLED FROM OTHER SCRIPTS (safe even if there is no DayManager in the scene)
    // ==========================================

    public static void RecordCatSold()
    {
        if (Instance != null) Instance.catsSold++;
    }

    public static void RecordCatBought()
    {
        if (Instance != null) Instance.catsBought++;
    }

    public static void RecordRejected()
    {
        if (Instance != null) Instance.dealsRejected++;
    }

    // For store expansions later: each upgrade can raise the daily rent
    public static void AddRent(int amount)
    {
        if (Instance == null) return;
        Instance.extraRent += amount;
        Instance.UpdateRentText();
    }

    // ==========================================
    // DAY FLOW
    // ==========================================

    private void BeginDay()
    {
        dayEnded = false;
        shopOpen = false;
        rentWarned = false;
        timeLeft = dayLengthSeconds;
        lastShownSeconds = -1;

        dayStartGP = CoreEconomySystem.Instance.GetGP(systemName);
        dayStartReputation = CoreEconomySystem.Instance.GetReputation(systemName);

        catsSold = 0;
        catsBought = 0;
        dealsRejected = 0;

        if (endPanel != null) endPanel.SetActive(false);

        // The shop starts the day closed, with no customers arriving
        if (OpenSign.Instance != null) OpenSign.Instance.SetOpen(false);

        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        if (spawner != null) spawner.StopSpawning();

        UpdateRentText();
        UpdateTimerText();

        // If there is no sign in the scene (or you turned the requirement off), open straight away
        if (requireOpenSign && OpenSign.Instance != null)
        {
            MessageUI.Notify("Prep time: look after your cats, then open the shop.");
        }
        else
        {
            OpenShop();
        }
    }

    // Called by the Open sign
    public void OpenShop()
    {
        if (shopOpen || dayEnded) return;

        shopOpen = true;

        if (OpenSign.Instance != null) OpenSign.Instance.SetOpen(true);

        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        if (spawner != null) spawner.StartSpawning();

        MessageUI.Notify("The shop is open!");
        UpdateTimerText();
    }

    private void EndDay()
    {
        dayEnded = true;
        shopOpen = false;
        timeLeft = 0f;
        UpdateTimerText();

        if (OpenSign.Instance != null) OpenSign.Instance.SetOpen(false);

        // 1. Stop new customers, and send everyone home so tomorrow feels like a new day
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        if (spawner != null) spawner.StopSpawning();

        ClearShopForNewDay();

        // 2. Work out the day's results BEFORE rent is taken
        int gpNow = CoreEconomySystem.Instance.GetGP(systemName);
        int reputationNow = CoreEconomySystem.Instance.GetReputation(systemName);
        int profit = gpNow - dayStartGP;
        int reputationChange = reputationNow - dayStartReputation;

        // Handy while tuning the rent: look for these lines in the Console
        Debug.Log($"[{systemName}] Day {day} profit before rent: {profit} GP");

        // 3. Rent
        bool shopClosed = false;
        string rentSection;

        if (day < firstRentDay)
        {
            rentSection = $"Rent starts on day {firstRentDay}: {baseRent + extraRent} GP per day.";
        }
        else
        {
            int due = RentToday;

            if (gpNow >= due)
            {
                CoreEconomySystem.Instance.ModifyGP(-due, systemName);
                missedPayments = 0;
                lateFeeOwed = 0;
                rentSection = $"Rent paid: {due} GP\nGP left: {gpNow - due}";
            }
            else
            {
                missedPayments++;

                if (missedPayments >= missedPaymentsToClose)
                {
                    shopClosed = true;
                    rentSection = $"The rent of {due} GP could not be paid. You had {gpNow} GP.";
                }
                else
                {
                    lateFeeOwed = Mathf.CeilToInt((baseRent + extraRent) * lateFeePercent / 100f);
                    rentSection = $"The rent of {due} GP could not be paid. You had {gpNow} GP.\n" +
                                  $"The landlord sent a notice. Tomorrow's rent includes a late fee of {lateFeeOwed} GP.";
                }
            }
        }

        // 4. Build the summary text
        string title = shopClosed ? "The shelter has to close for now" : $"Day {day} complete";

        string stats =
            $"Profit today: {profit} GP\n" +
            $"Reputation: {reputationNow} ({reputationChange:+#;-#;0})\n" +
            $"Cats sold: {catsSold}\n" +
            $"Cats bought: {catsBought}\n" +
            $"Deals rejected: {dealsRejected}";

        string closing = shopClosed
            ? $"\n\nYou kept the shelter open for {day} days.\nTip: keep some GP aside for the rent."
            : "";

        resultsText.text = $"{title}\n\n{stats}\n\n{rentSection}{closing}";

        // 5. Freeze the game and show the summary
        PauseMenu.PauseDisabled = true;
        PauseMenu pauseMenu = FindFirstObjectByType<PauseMenu>();
        if (pauseMenu != null) pauseMenu.FreezeGame();

        if (nextDayButton != null) nextDayButton.SetActive(!shopClosed);
        endPanel.SetActive(true);
    }

    // Everyone goes home. Cats stay in their enclosures, because they are the shop's stock.
    private void ClearShopForNewDay()
    {
        ReceptionDesk desk = FindFirstObjectByType<ReceptionDesk>();
        if (desk != null) desk.CancelCurrentDeal();

        CustomerAI[] customers = FindObjectsByType<CustomerAI>(FindObjectsSortMode.None);
        foreach (CustomerAI customer in customers)
        {
            customer.DismissForNewDay();
        }

        if (CustomerQueueManager.Instance != null) CustomerQueueManager.Instance.ClearQueue();
    }

    // Hooked up to the Next day button
    public void StartNextDay()
    {
        day++;

        PauseMenu pauseMenu = FindFirstObjectByType<PauseMenu>();
        if (pauseMenu != null) pauseMenu.UnfreezeGame();

        // The new day starts in the prep phase, with the shop closed
        BeginDay();
    }

    // ==========================================
    // UI
    // ==========================================

    private void UpdateTimerText()
    {
        if (timerText == null) return;

        if (!shopOpen && !dayEnded)
        {
            timerText.text = $"Day {day}   Prep time";
            lastShownSeconds = -1;
            return;
        }

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
        if (seconds == lastShownSeconds) return; // only rewrite the text when the second changes

        lastShownSeconds = seconds;
        timerText.text = $"Day {day}   Time left: {seconds / 60}:{seconds % 60:00}";
    }

    private void UpdateRentText()
    {
        if (rentText == null) return;

        if (day < firstRentDay)
        {
            rentText.text = $"Rent starts on day {firstRentDay}";
        }
        else if (lateFeeOwed > 0)
        {
            rentText.text = $"Rent due tonight: {RentToday} GP (includes late fee)";
        }
        else
        {
            rentText.text = $"Rent due tonight: {RentToday} GP";
        }
    }
}