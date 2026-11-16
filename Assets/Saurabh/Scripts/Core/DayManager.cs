using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
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
    [SerializeField] private int openingHour = 9;     // 9 = 9:00 AM
    [SerializeField] private int closingHour = 21;    // 21 = 9:00 PM (24-hour number)

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI rentText;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TextMeshProUGUI resultsText;
    [SerializeField] private GameObject nextDayButton;

    [Header("Between days")]
    [SerializeField] private float fadeTime = 0.6f;    // seconds to fade to cream at closing (and back, ×1.5)

    [Header("Day receipt")]
    [SerializeField] private float lineDelay = 0.2f;    // seconds between receipt lines
    [SerializeField] private float rentPause = 0.6f;    // the beat of suspense before the rent line

    // ==========================================
    // WHAT THIS SCRIPT REMEMBERS
    // ==========================================

    // Other scripts can read these, but only DayManager changes them
    public DayPhase Phase { get; private set; }
    public int CustomersInside { get; private set; }
    public bool LastCallExpired { get; private set; }

    private int day = 1;
    public int Day => day;   // read-only for other scripts (the developer console's "status")
    private float timeLeft;
    private int lastShownClock = -1;   // the clock time we last drew (in game minutes), so we only redraw when it changes
    private bool rentWarned;

    private float lastCallElapsed;   // seconds since closing time began
    private float countTimer;        // counts down to the next customer recount

    private int dayStartGP;
    private int dayStartReputation;

    private bool skipReceipt;     // true once the player clicks or presses a key during the receipt
    private int receiptStartFrame; // the key press that CLOSED the shop must not also skip the receipt

    // Simple stats for the summary (reset every day)
    private int catsSold;
    private int catsBought;
    private int dealsDeclined;   // the player pressed Reject
    private int dealsMissed;     // the game rejected it (not enough GP or no free cage)

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
        else if (Phase == DayPhase.Summary && Time.frameCount > receiptStartFrame && PressedAnything()) skipReceipt = true;
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

    public static void RecordDeclined()
    {
        if (Instance != null) Instance.dealsDeclined++;
    }

    public static void RecordMissed()
    {
        if (Instance != null) Instance.dealsMissed++;
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
        lastShownClock = -1;

        dayStartGP = CoreEconomySystem.Instance.GetGP(systemName);
        dayStartReputation = CoreEconomySystem.Instance.GetReputation(systemName);

        catsSold = 0;
        catsBought = 0;
        dealsDeclined = 0;
        dealsMissed = 0;

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

        MessageUI.Notify("The shop is open!", ToastKind.Good);
        TutorialNote.Complete(TutorialStep.OpenShop);
        AudioManager.Play(Sfx.ShopOpen);
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
        AudioManager.Play(Sfx.ShopClosing);
        timeLeft = 0f;
        lastCallElapsed = 0f;
        countTimer = 0f;
        LastCallExpired = false;

        // No new customers from now on. The ones already inside carry on.
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        if (spawner != null) spawner.StopSpawning();

        CustomersInside = CountCustomers();

        if (CustomersInside > 0) MessageUI.Notify("Closing time! Serve the last customers, then close the shop.", ToastKind.Warning);
        else MessageUI.Notify("Closing time! Close the shop when you are ready.", ToastKind.Warning);

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
            MessageUI.Notify("Customers are still inside. Wait for them to finish.", ToastKind.Warning);
            return;
        }

        EndDay();
    }

    // SUMMARY: rent, results, freeze
    private void EndDay()
    {
        if (Phase == DayPhase.Summary) return;

        Phase = DayPhase.Summary;
        TutorialNote.Complete(TutorialStep.CloseShop);
        AudioManager.Play(Sfx.DaySummary);
        timeLeft = 0f;

        if (OpenSign.Instance != null) OpenSign.Instance.SetOpen(false);

        // 1. Make sure nobody new arrives, and send anyone still inside home
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        if (spawner != null) spawner.StopSpawning();

        // 2. Freeze the game straight away (no walking during the fade)
        PauseMenu.PauseDisabled = true;
        PauseMenu pauseMenu = FindFirstObjectByType<PauseMenu>();
        if (pauseMenu != null) pauseMenu.FreezeGame();

        if (nextDayButton != null) nextDayButton.SetActive(false);   // shown when the receipt has finished
        resultsText.text = "";
        UpdateTimerText();

        // 3. Fade out, tidy the shop while it's hidden, then print the receipt
        StartCoroutine(EndOfDaySequence());
    }

    // The end of a day, in order. A coroutine, because each step waits for the one before.
    private IEnumerator EndOfDaySequence()
    {
        yield return ScreenFader.FadeTo(1f, fadeTime);   // the shop fades to cream

        ClearShopForNewDay();                             // customers leave while nobody can see
        endPanel.SetActive(true);
        Juice.PopIn(endPanel.transform, 0.25f);           // the receipt pops up on the cream

        yield return PrintReceipt();                      // line by line, rent part-way through
    }

    // ==========================================
    // THE DAY RECEIPT
    // ==========================================

    // Prints the day's money one line at a time, like a till receipt, then takes the rent.
    // A coroutine can pause between lines ("yield") without freezing the whole game.
    private IEnumerator PrintReceipt()
    {
        skipReceipt = false;
        receiptStartFrame = Time.frameCount;
        CoreEconomySystem eco = CoreEconomySystem.Instance;

        // Today's numbers, BEFORE rent
        int gpNow = eco.GetGP(systemName);
        int profit = gpNow - dayStartGP;
        int reputationNow = eco.GetReputation(systemName);
        int reputationChange = reputationNow - dayStartReputation;

        int earned = eco.TodayIn("ReceptionDesk");      // adoption fees + donations
        int care   = eco.TodayOut("CareSubsystem");     // feeding and cleaning
        int bought = eco.TodayOut("ReceptionDesk");     // cats bought from Givers
        int decor  = eco.TodayOut("DecorSlot");
        // Anything else (e.g. GP added with the developer console) = what's left over
        int other  = profit - (earned - care - bought - decor);

        Debug.Log($"[{systemName}] Day {day} profit before rent: {profit} GP");   // handy while tuning the rent

        // The receipt, one line at a time
        string text = $"<size=120%><b>Day {day} receipt</b></size>\n";
        yield return Show(text);

        text += "\n" + Row("GP at the start of the day", dayStartGP.ToString());   yield return Show(text);

        text += "\n" + Row("Adoptions & donations", Signed(earned), Green);   yield return Show(text);
        if (care > 0)   { text += "\n" + Row("Cat care", Signed(-care));      yield return Show(text); }
        if (bought > 0) { text += "\n" + Row("Cats bought", Signed(-bought)); yield return Show(text); }
        if (decor > 0)  { text += "\n" + Row("Decor", Signed(-decor));        yield return Show(text); }
        if (other != 0) { text += "\n" + Row("Other", Signed(other));         yield return Show(text); }

        text += "\n" + Rule;
        text += "\n" + Row("<b>Profit today</b>", $"<b>{Signed(profit)}</b>", profit >= 0 ? Green : Red);
        yield return Show(text);

        // --- RENT: a short pause, then it lands ---
        yield return Wait(rentPause);

        bool shopClosed = false;
        string rentNote = "";

        if (RentSystem.Instance == null)
        {
            text += "\n" + Row("Rent", "none in this scene");
        }
        else if (!RentSystem.Instance.IsRentDay(day))
        {
            RentSystem.Instance.SettleRent(day, gpNow, out _);   // "out _" = we don't need the message
            text += "\n" + Row("Rent", "free today", Green);
        }
        else
        {
            int due = RentSystem.Instance.RentDue(day);          // read BEFORE settling (settling changes the late fee)
            bool canPay = gpNow >= due;
            shopClosed = RentSystem.Instance.SettleRent(day, gpNow, out _);   // GP is taken HERE

            if (canPay)
            {
                text += "\n" + Row("<b>RENT</b>", $"<b>{Signed(-due)}</b>", Red);
                AudioManager.Play(Sfx.CoinsOut);
            }
            else
            {
                text += "\n" + Row($"<b>RENT</b> <color={Red}><size=80%>NOT PAID</size></color>", $"<b>{Signed(-due)}</b>", Red);
                AudioManager.Play(Sfx.PoliteNo);
                // Short and about what happens NEXT (the receipt above already shows what went wrong)
                if (!shopClosed)
                {
                    int tomorrow = RentSystem.Instance.RentDue(day + 1);   // now includes the late fee
                    int left = RentSystem.Instance.MissesLeft;
                    string warning = left <= 1 ? "Miss again and the shelter closes."
                                               : $"{left} more misses and the shelter closes.";
                    rentNote = $"\n\n<color={Red}><b>Landlord notice:</b> {tomorrow} GP tomorrow (late fee added).\n{warning}</color>";
                }
            }
        }
        resultsText.text = text;
        Juice.Punch(resultsText.transform, 0.06f);   // the "thump" of the bill landing

        // --- What's left ---
        int gpLeft = eco.GetGP(systemName);
        text += "\n" + Rule;
        text += "\n" + Row("<b>GP left</b>", $"<b>{gpLeft}</b>");
        text += rentNote;
        yield return Show(text);

        // --- The rest of the day, small ---
        text += $"\n\n<size=85%>Reputation {reputationNow} ({reputationChange:+#;-#;0})\n" +
                $"Sold {catsSold}   Bought {catsBought}   Declined {dealsDeclined}   Missed {dealsMissed}</size>";

        if (shopClosed)
            text = $"<size=120%><b>The shelter has to close for now</b></size>\n\n" + text +
                   $"\n\nYou kept the shelter open for {day} days.\nTip: keep some GP aside for the rent.";

        resultsText.text = text;
        if (nextDayButton != null) nextDayButton.SetActive(!shopClosed);
    }

    // Puts the text on screen, plays a soft tick, and waits one line's time
    private IEnumerator Show(string text)
    {
        resultsText.text = text;
        if (!skipReceipt) AudioManager.Play(Sfx.ButtonClick, 1.3f, 0.4f);   // higher and quieter than a normal click
        yield return Wait(lineDelay);
    }

    // Waits in REAL seconds (the game is frozen, so normal waits would never end).
    // Stops waiting straight away once the player has skipped.
    private IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end && !skipReceipt) yield return null;
    }

    // Any key or mouse click (this frame) — used to skip the receipt animation
    private static bool PressedAnything()
    {
        bool key = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return key || click;
    }

    // ---- Receipt formatting ----
    private const string Green = "#3FA28C";
    private const string Red = "#E8836A";
    private const string Rule = "<color=#9C978B>-----------------------------------------------------</color>";

    // One receipt row: the label on the left, the amount lined up at 70% of the width.
    // <pos=70%> is a TextMeshPro tag: "jump to 70% across before writing the rest".
    private static string Row(string label, string value, string color = null)
    {
        string amount = color == null ? value : $"<color={color}>{value}</color>";
        return $"{label}<pos=70%>{amount}";
    }

    // 30 → "+30", -8 → "-8", 0 → "0"
    private static string Signed(int value)
    {
        return value.ToString("+#;-#;0");
    }

    // ==========================================
    // DEVELOPER CONSOLE HOOKS (only called by DevCommandsCore)
    // ==========================================

    // Skips the rest of the open day. The next Update sees no time left and enters last call.
    public bool DevSkipToLastCall()
    {
        if (Phase != DayPhase.Open) return false;
        timeLeft = 0f;
        return true;
    }

    // Ends the day straight away, exactly as if the player had closed the shop
    public bool DevEndDay()
    {
        if (Phase != DayPhase.Open && Phase != DayPhase.LastCall) return false;
        EndDay();
        return true;
    }

    // Hooked up to the Next day button. Starts the next day in PREP.
    public void StartNextDay()
    {
        if (Phase != DayPhase.Summary) return;

        day++;
        if (CoreEconomySystem.Instance != null) CoreEconomySystem.Instance.ResetLedger();   // a fresh ledger every day

        PauseMenu pauseMenu = FindFirstObjectByType<PauseMenu>();
        if (pauseMenu != null) pauseMenu.UnfreezeGame();

        BeginDay();                                // the new morning is set up under the cream...
        ScreenFader.Fade(0f, fadeTime * 1.5f);     // ...then fades in, a little slower, like waking up
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
        MessageUI.Notify(warning, ToastKind.Warning);
    }

    // ==========================================
    // UI
    // ==========================================

    // Turns a number of minutes after midnight into text like "9:40 AM"
    private string ClockText(int minutesAfterMidnight)
    {
        int hour24 = minutesAfterMidnight / 60;            // whole hours
        int minute = minutesAfterMidnight % 60;            // minutes left over
        string suffix = hour24 >= 12 ? "PM" : "AM";
        int hour12 = hour24 % 12;                          // 13 -> 1, 12 -> 0
        if (hour12 == 0) hour12 = 12;                      // 0 means 12 o'clock in a 12-hour clock
        return $"{hour12}:{minute:00} {suffix}";
    }
    
    private void UpdateTimerText()
    {
        if (timerText == null) return;

        switch (Phase)
        {
            case DayPhase.Prep:
            // Shop closed, so the clock is frozen at opening time
            timerText.text = $"Day {day}   {ClockText(openingHour * 60)}";
            break;

           case DayPhase.Open:
            {
                // How far through the day we are: 0 at opening, 1 at closing
                float progress = 1f - Mathf.Clamp01(timeLeft / dayLengthSeconds);

                // Minutes after midnight, rounded down to a multiple of 10 so the clock doesn't flicker
                int openMinutes  = openingHour * 60;
                int totalMinutes = (closingHour - openingHour) * 60;
                int clock = openMinutes + Mathf.FloorToInt(progress * totalMinutes / 10f) * 10;

                if (clock == lastShownClock) return;   // only rewrite the text when the clock changes

                lastShownClock = clock;
                timerText.text = $"Day {day}   {ClockText(clock)}";
                break;
            }

            case DayPhase.LastCall:
                timerText.text = CustomersInside > 0
                    ? $"Day {day}   {ClockText(closingHour * 60)}   Finish serving"
                    : $"Day {day}   {ClockText(closingHour * 60)}   Close the shop";
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