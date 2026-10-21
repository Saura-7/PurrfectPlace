using UnityEngine;
using TMPro;

// The four parts of a day. DayManager is always in exactly one of them.
public enum DayPhase
{
    Prep,       // Shop closed. Timer frozen. The player can look after the cats.
    Open,       // Customers arrive and the timer runs.
    LastCall,   // The timer ran out. No new customers. Customers already inside finish up.
    Summary     // The day is over. Rent is taken and the results are shown.
}

/// <summary>
/// Runs the day: which phase we are in, the timer, and the summary screen.
/// The rent rules live in RentSystem, which sits on the same object.
/// </summary>
public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    private readonly string systemName = "DayManager";

    // ==========================================
    // SETTINGS (change these in the Inspector)
    // ==========================================

    [Header("Day Settings")]
    [SerializeField] private float dayLengthSeconds = 300f;   // 5 minutes of open shop
    [SerializeField] private float lastCallSeconds = 60f;     // After this long, the player may close even with customers inside
    [SerializeField] private bool requireOpenSign = true;     // Untick to open and close the shop automatically (handy for testing)

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI rentText;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TextMeshProUGUI resultsText;
    [SerializeField] private GameObject nextDayButton;

    // ==========================================
    // WHAT THIS SCRIPT REMEMBERS
    // ==========================================

    // Other scripts can read these, but only DayManager changes them
    public DayPhase Phase { get; private set; }
    public int CustomersInside { get; private set; }
    public bool LastCallExpired { get; private set; }

    private int day = 1;
    private float timeLeft;
    private int lastShownSeconds = -1;
    private bool rentWarned;

    private float lastCallElapsed;   // seconds since closing time began
    private float countTimer;        // counts down to the next customer recount

    private int dayStartGP;
    private int dayStartReputation;

    // Simple stats for the summary (reset every day)
    private int catsSold;
    private int catsBought;
    private int dealsRejected;

    // ==========================================
    // UNITY EVENTS
    // ==========================================

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
        if (RentSystem.Instance == null)
        {
            Debug.LogWarning($"[{systemName}] No RentSystem found. Add a RentSystem component to the DayManager object. The game will run without rent.");
        }

        BeginDay();
    }

    private void Update()
    {
        // Time.deltaTime is 0 while the game is paused, so every timer here stops by itself
        if (Phase == DayPhase.Open) UpdateOpenPhase();
        else if (Phase == DayPhase.LastCall) UpdateLastCallPhase();
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

    // ==========================================
    // THE SIGN
    // ==========================================

    // The sign calls this when the player presses it. What it means depends on the phase.
    public void OnSignPressed()
    {
        switch (Phase)
        {
            case DayPhase.Prep:
                OpenShop();
                break;
            case DayPhase.Open:
                MessageUI.Notify("The shop stays open until closing time.");
                break;
            case DayPhase.LastCall:
                TryCloseShop();
                break;
        }
    }

    // The text shown when the player looks at the sign
    public string GetSignPrompt(string key)
    {
        switch (Phase)
        {
            case DayPhase.Prep:
                return $"Open the shop [{key}]";

            case DayPhase.LastCall:
                if (CustomersInside == 0) return $"Close the shop [{key}]";
                if (LastCallExpired) return $"Close anyway [{key}]";
                return "Customers are still inside";

            default:
                return "";
        }
    }

    // ==========================================
    // THE PHASES
    // ==========================================

    // PREP: the day starts here
    private void BeginDay()
    {
        Phase = DayPhase.Prep;
        CustomersInside = 0;
        LastCallExpired = false;
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

        RefreshRentText();
        UpdateTimerText();

        if (UsesSign())
        {
            MessageUI.Notify("Prep time: look after your cats, then open the shop.");
        }
        else
        {
            OpenShop();
        }
    }

    // PREP -> OPEN
    private void OpenShop()
    {
        if (Phase != DayPhase.Prep) return;

        Phase = DayPhase.Open;

        if (OpenSign.Instance != null) OpenSign.Instance.SetOpen(true);

        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        if (spawner != null) spawner.StartSpawning();

        MessageUI.Notify("The shop is open!");
        UpdateTimerText();
    }

    // OPEN: count down, warn about rent, then move to closing time
    private void UpdateOpenPhase()
    {
        timeLeft -= Time.deltaTime;
        UpdateTimerText();   // only rewrites the text when the second changes
        WarnAboutRent();

        if (timeLeft <= 0f) EnterLastCall();
    }

    // OPEN -> LAST CALL
    private void EnterLastCall()
    {
        Phase = DayPhase.LastCall;
        timeLeft = 0f;
        lastCallElapsed = 0f;
        countTimer = 0f;
        LastCallExpired = false;

        // No new customers from now on. The ones already inside carry on.
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        if (spawner != null) spawner.StopSpawning();

        CustomersInside = CountCustomers();

        if (CustomersInside > 0) MessageUI.Notify("Closing time! Serve the last customers, then close the shop.");
        else MessageUI.Notify("Closing time! Close the shop when you are ready.");

        UpdateTimerText();
    }

    // LAST CALL: wait for the customers to finish
    private void UpdateLastCallPhase()
    {
        lastCallElapsed += Time.deltaTime;

        // Recount the customers twice a second (cheaper than every frame)
        countTimer -= Time.deltaTime;
        if (countTimer <= 0f)
        {
            countTimer = 0.5f;

            int count = CountCustomers();
            if (count != CustomersInside)
            {
                CustomersInside = count;
                UpdateTimerText();
            }
        }

        // After the safety limit, the player may close even with customers inside
        if (!LastCallExpired && lastCallElapsed >= lastCallSeconds)
        {
            LastCallExpired = true;
            if (CustomersInside > 0) MessageUI.Notify("You can close the shop now, even with customers inside.");
        }

        // Testing without a sign: close automatically
        if (!UsesSign() && (CustomersInside == 0 || LastCallExpired)) EndDay();
    }

    // LAST CALL -> SUMMARY, when the player presses the sign
    private void TryCloseShop()
    {
        if (Phase != DayPhase.LastCall) return;

        CustomersInside = CountCustomers();

        if (CustomersInside > 0 && !LastCallExpired)
        {
            MessageUI.Notify("Customers are still inside. Wait for them to finish.");
            return;
        }

        EndDay();
    }

    // SUMMARY: rent, results, freeze
    private void EndDay()
    {
        if (Phase == DayPhase.Summary) return;

        Phase = DayPhase.Summary;
        timeLeft = 0f;

        if (OpenSign.Instance != null) OpenSign.Instance.SetOpen(false);

        // 1. Make sure nobody new arrives, and send anyone still inside home
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

        // 3. Rent (RentSystem decides what happens)
        bool shopClosed = false;
        string rentSection;

        if (RentSystem.Instance != null)
        {
            shopClosed = RentSystem.Instance.SettleRent(day, gpNow, out rentSection);
        }
        else
        {
            rentSection = "There is no rent system in this scene.";
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

        UpdateTimerText();
    }

    // Hooked up to the Next day button. Starts the next day in PREP.
    public void StartNextDay()
    {
        if (Phase != DayPhase.Summary) return;

        day++;

        PauseMenu pauseMenu = FindFirstObjectByType<PauseMenu>();
        if (pauseMenu != null) pauseMenu.UnfreezeGame();

        BeginDay();
    }

    // ==========================================
    // HELPERS
    // ==========================================

    // True when the player uses the sign to open and close the shop
    private bool UsesSign()
    {
        return requireOpenSign && OpenSign.Instance != null;
    }

    // Everyone in the shop, including customers who are on their way out
    private int CountCustomers()
    {
        return FindObjectsByType<CustomerAI>(FindObjectsSortMode.None).Length;
    }

    // Fallback: sends anyone still inside home, so the next day feels like a new day.
    // Cats stay in their enclosures, because they are the shop's stock.
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

        CustomersInside = 0;
    }

    // Gives the player a fair warning before the rent is taken
    private void WarnAboutRent()
    {
        if (rentWarned) return;
        if (RentSystem.Instance == null || !RentSystem.Instance.IsRentDay(day)) return;
        if (timeLeft > 30f) return;

        rentWarned = true;

        int due = RentSystem.Instance.RentDue(day);
        int gp = CoreEconomySystem.Instance.GetGP(systemName);

        string warning = $"Rent is due when you close the shop: {due} GP.";
        if (gp < due) warning += $" You have {gp} GP.";
        MessageUI.Notify(warning);
    }

    // ==========================================
    // UI
    // ==========================================

    private void UpdateTimerText()
    {
        if (timerText == null) return;

        switch (Phase)
        {
            case DayPhase.Prep:
                timerText.text = $"Day {day}   Prep time";
                break;

            case DayPhase.Open:
            {
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
                if (seconds == lastShownSeconds) return;   // only rewrite the text when the second changes

                lastShownSeconds = seconds;
                timerText.text = $"Day {day}   Time left: {seconds / 60}:{seconds % 60:00}";
                break;
            }

            case DayPhase.LastCall:
                timerText.text = CustomersInside > 0
                    ? $"Day {day}   Closing time: serve the last customers"
                    : $"Day {day}   Closing time: close the shop";
                break;

            case DayPhase.Summary:
                timerText.text = $"Day {day}   Closed";
                break;
        }
    }

    // Public because RentSystem calls it when the rent changes
    public void RefreshRentText()
    {
        if (rentText == null) return;

        rentText.text = RentSystem.Instance != null ? RentSystem.Instance.GetRentLabel(day) : "";
    }
}