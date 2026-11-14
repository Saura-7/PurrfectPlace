using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Draws the deal monitor at the reception desk. It only DISPLAYS a deal:
/// ReceptionDesk decides the prices and what Accept / Decline do, then calls one of the Show methods.
///
/// Layout (top to bottom):
///   [ HEADER bar: kind of deal, coloured ]
///   traits · care tier
///   YOU GET / YOU PAY
///   BIG NUMBER
///   details (where the number comes from, can you afford it)
///   [ ACCEPT verb ]  [ DECLINE ]  + the cost of declining
///
/// When there is no deal, the screen stays ON and shows a small idle message instead.
/// Put this script on the screen background (MonitorPanel), which is always on:
///   MonitorPanel   (this script, the cream background)
///    ├─ DealContent  (header, body, buttons; ReceptionDesk turns it on/off)
///    └─ IdleContent  (shown whenever DealContent is off)
/// </summary>
public class DealScreen : MonoBehaviour
{
    [Header("On / idle")]
    [SerializeField] private GameObject dealContent;        // the same object as ReceptionDesk's "Deal UI Panel"
    [SerializeField] private GameObject idleContent;        // shown when there is no deal
    [SerializeField] private TextMeshProUGUI idleText;      // "Waiting for customers..."

    [Header("Header")]
    [SerializeField] private Image headerBar;              // the coloured strip at the top
    [SerializeField] private TextMeshProUGUI headerTitle;  // "ADOPTION"
    [SerializeField] private TextMeshProUGUI headerNote;   // "Customer wants this cat"

    [Header("The cat")]
    [SerializeField] private TextMeshProUGUI traitsText;   // "Calico · Normal · Playful"
    [SerializeField] private TextMeshProUGUI careText;     // "Well-cared  Feed 4/6 · Clean 4/6"

    [Header("The money")]
    [SerializeField] private TextMeshProUGUI amountLabel;  // "YOU GET" / "YOU PAY"
    [SerializeField] private TextMeshProUGUI amountText;   // "+75 GP" (the biggest text on the screen)
    [SerializeField] private TextMeshProUGUI detailText;   // "Fee 60 · Donation 15" ...

    [Header("Buttons")]
    [SerializeField] private TextMeshProUGUI acceptLabel;  // the verb on the accept button: APPROVE / BUY / TAKE IN
    [SerializeField] private TextMeshProUGUI declineNote;  // "-3 reputation" under the decline button

    [Header("Colours (header bar = kind of deal)")]
    [SerializeField] private Color adoptionColor = new Color32(0xE8, 0x83, 0xA6, 0xFF);   // pink
    [SerializeField] private Color buyColor      = new Color32(0xE9, 0xA2, 0x3B, 0xFF);   // amber
    [SerializeField] private Color strayColor    = new Color32(0x9A, 0x94, 0x8A, 0xFF);   // gray
    [SerializeField] private Color gainColor     = new Color32(0x3F, 0xA2, 0x8C, 0xFF);   // green: money in
    [SerializeField] private Color costColor     = new Color32(0xE8, 0x83, 0x6A, 0xFF);   // coral: money out / a problem
    [SerializeField] private Color repColor      = new Color32(0xE8, 0x83, 0xA6, 0xFF);   // pink: reputation

    // ==========================================
    // IDLE SCREEN
    // ==========================================

    // LateUpdate runs after ReceptionDesk has opened or closed the deal this frame,
    // so the idle screen always matches. ReceptionDesk doesn't need to know the idle screen exists.
    private void LateUpdate()
    {
        if (dealContent == null || idleContent == null) return;

        bool showingDeal = dealContent.activeSelf;
        if (idleContent.activeSelf == showingDeal) idleContent.SetActive(!showingDeal);   // only change when needed

        if (!showingDeal && idleText != null)
        {
            string message = IdleMessage();
            if (idleText.text != message) idleText.text = message;   // only touch the text when it changes
        }
    }

    private string IdleMessage()
    {
        bool customerWaiting = CustomerQueueManager.Instance != null
                               && CustomerQueueManager.Instance.GetCurrentCustomerAtDesk() != null;
        if (customerWaiting) return $"Next customer, please!\n<size=60%>Talk to them [{InputPrompts.Get(PromptAction.CompleteDeal)}]</size>";

        DayManager day = DayManager.Instance;
        if (day != null && day.Phase == DayPhase.Prep) return "Shop closed\n<size=60%>Open the shop at the sign</size>";
        return "Waiting for customers...";
    }

    // ==========================================
    // THE THREE KINDS OF DEAL
    // ==========================================

    /// <summary>A Taker wants to adopt one of your cats.</summary>
    public void ShowAdoption(BaseCat cat, int fee, int donation, int rep, int declineRep)
    {
        SetHeader("ADOPTION", "Customer wants this cat", adoptionColor);
        SetCat(cat);

        int total = fee + donation;
        SetAmount("YOU GET", $"+{total} GP", gainColor);

        // Where the total comes from, then the reputation on its own line
        string details = donation > 0 ? $"Fee {fee} · Donation {donation}" : $"Adoption fee {fee}";
        if (rep > 0) details += $"\n<color={Hex(repColor)}><b>+{rep} reputation</b></color>";
        detailText.text = details;

        SetButtons("APPROVE", declineRep);
    }

    /// <summary>A Giver wants to sell you a cat.</summary>
    public void ShowPurchase(BaseCat cat, int price, int worth, int gp, int freeCages, int declineRep)
    {
        SetHeader("BUY A CAT", "Giver is selling", buyColor);
        SetCat(cat);
        SetAmount("YOU PAY", $"-{price} GP", costColor);

        // Show both numbers and let the player compare. We never say "good deal" or "bad deal".
        detailText.text = $"Worth now: about {worth} GP\n{CanYouLine(gp, price, freeCages)}";

        SetButtons("BUY", declineRep);
    }

    /// <summary>Someone found a stray outside and gives it to you for free.</summary>
    public void ShowStray(BaseCat cat, int gp, int freeCages, int declineRep)
    {
        SetHeader("STRAY FOUND", "Found outside", strayColor);
        SetCat(cat);
        SetAmount("YOU PAY", "Free", gainColor);
        detailText.text = $"Arrives hungry and dirty\n{CanYouLine(gp, 0, freeCages)}";
        SetButtons("TAKE IN", declineRep);
    }

    // ==========================================
    // SHARED PIECES
    // ==========================================

    private void SetHeader(string title, string note, Color color)
    {
        headerTitle.text = title;
        headerNote.text = note;
        headerBar.color = color;
    }

    private void SetCat(BaseCat cat)
    {
        traitsText.text = $"{cat.Pattern} · {cat.Body} · {cat.Behavior}";
        careText.text = $"<b>{CareRules.ColoredName(CareRules.TierOf(cat.TotalCare))}</b>   " +
                        $"Feed {cat.feedingLevel}/{BaseCat.MaxLevel} · Clean {cat.cleanlinessLevel}/{BaseCat.MaxLevel}";
    }

    private void SetAmount(string label, string amount, Color color)
    {
        amountLabel.text = label;
        amountText.text = amount;
        amountText.color = color;
    }

    private void SetButtons(string acceptVerb, int declineRep)
    {
        acceptLabel.text = acceptVerb;
        declineNote.text = declineRep > 0 ? $"-{declineRep} reputation" : "";
    }

    // "You have 160 GP · 1 free cage", with any problem in coral, so the player sees it BEFORE pressing Buy
    private string CanYouLine(int gp, int price, int freeCages)
    {
        string money = gp >= price ? $"You have {gp} GP" : $"<color={Hex(costColor)}>Only {gp} GP</color>";
        string space = freeCages > 0
            ? $"{freeCages} free cage{(freeCages == 1 ? "" : "s")}"
            : $"<color={Hex(costColor)}>No free cage</color>";
        return $"{money} · {space}";
    }

    // TextMeshPro colour tags need "#RRGGBB" text, so this turns a Color into that
    private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}