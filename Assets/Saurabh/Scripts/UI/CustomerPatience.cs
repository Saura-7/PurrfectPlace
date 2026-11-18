using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A customer at the front of the line slowly runs out of patience if nobody serves them.
/// When it runs out, they walk out, and it counts as a missed deal (−3 reputation).
///
/// It only counts down when ALL of these are true:
///   - it's day 3 or later (days 1 and 2 stay calm while the player learns)
///   - the shop is open (Open or Last call)
///   - this customer is waiting at the desk, at the front of the line
///   - the player hasn't started their deal yet (once the deal screen is up, the clock stops)
///
/// What the player sees:
///   - a small bar over the customer's head, once half the patience is gone
///   - a warning toast at a quarter left
///   - the walk-out toast when it hits zero
/// </summary>
[RequireComponent(typeof(CustomerAI))]
public class CustomerPatience : MonoBehaviour
{
    [Header("Rules")]
    [SerializeField] private float patienceSeconds = 60f;   // how long they'll wait at the desk without being served
    [SerializeField] private int startDay = 3;              // no patience before this day
    [SerializeField] private float showBarBelow = 1f;       // the bar appears when patience drops under this (1 = right away)
    [SerializeField] private float warnBelow = 0.25f;       // the warning toast fires under this

    [Header("Bar (a small world-space canvas above the head)")]
    [SerializeField] private GameObject barRoot;   // the canvas object: shown and hidden
    [SerializeField] private Image barFill;        // a Sliced Image stretched across the bar (anchors 0,0 to 1,1)
    [SerializeField] private Color fullColor = new Color32(0x3F, 0xA2, 0x8C, 0xFF);   // teal: happy to wait
    [SerializeField] private Color calmColor = new Color32(0xE9, 0xA2, 0x3B, 0xFF);   // amber
    [SerializeField] private Color angryColor = new Color32(0xE8, 0x83, 0x6A, 0xFF);  // coral
    [SerializeField] private TipData patienceTip;   // shown the first time any patience bar appears

    private CustomerAI customer;
    private float patience = 1f;   // 1 = full, 0 = they leave
    private bool warned;           // the warning toast only fires once
    private bool gone;             // they've walked out: stop doing anything

    private void Awake()
    {
        customer = GetComponent<CustomerAI>();
        if (barRoot != null) barRoot.SetActive(false);
    }

    private void Update()
    {
        if (gone) return;

        // Waiting = at the front of the desk on a patience day. Counting = waiting AND not being served yet.
        bool waiting = IsWaitingAtDesk();
        bool counting = waiting && !ReceptionDesk.Instance.IsAttending(customer);   // walking away un-freezes it

        if (counting)
        {
            // Time.deltaTime is 0 while paused, so patience freezes with the game
            patience -= Time.deltaTime / patienceSeconds;

            if (!warned && patience <= warnBelow)
            {
                warned = true;
                MessageUI.Notify("A customer is getting impatient at the desk", ToastKind.Warning);
            }

            if (patience <= 0f)
            {
                gone = true;
                ShowBar(false);
                ReceptionDesk.Instance.CustomerWalkedOut(customer);
                return;
            }
        }

        // The bar stays up while you serve them, frozen, so you can see the clock stopped
        ShowBar(waiting && patience <= showBarBelow);
        if (barFill != null)
        {
            // Shrink the fill from the right by moving its right anchor: 1 = full width, 0.5 = half.
            // (Unlike a Filled image, this keeps the rounded corners of a Sliced sprite sharp.)
            Vector2 max = barFill.rectTransform.anchorMax;
            barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(patience), max.y);
            // Teal (full) → amber (half) → coral (about to leave).
            // Two blends: the top half goes amber→teal, the bottom half coral→amber.
            barFill.color = patience > 0.5f
                ? Color.Lerp(calmColor, fullColor, (patience - 0.5f) / 0.5f)
                : Color.Lerp(angryColor, calmColor, patience / 0.5f);
        }
    }

    private void LateUpdate()
    {
        // "Billboard": turn the bar to face the camera, so it's never seen edge-on or mirrored
        if (barRoot != null && barRoot.activeSelf && Camera.main != null)
            barRoot.transform.rotation = Camera.main.transform.rotation;
    }

    // Is this customer standing at the desk, at the front, on a day with patience?
    // (Whether the clock actually ticks also depends on whether the player is serving them.)
    private bool IsWaitingAtDesk()
    {
        DayManager days = DayManager.Instance;
        if (days == null || days.Day < startDay) return false;
        if (days.Phase != DayPhase.Open && days.Phase != DayPhase.LastCall) return false;

        if (customer.currentState != CustomerAI.CustomerState.WaitingAtRegister) return false;
        if (customer.IsSayingGoodbye) return false;
        if (CustomerQueueManager.Instance == null || CustomerQueueManager.Instance.GetCurrentCustomerAtDesk() != customer) return false;
        if (ReceptionDesk.Instance == null) return false;

        return true;
    }

    private void ShowBar(bool show)
    {
        if (barRoot == null || barRoot.activeSelf == show) return;
        barRoot.SetActive(show);
        if (show)
        {
            Juice.PopIn(barRoot.transform);   // a small pop so the eye notices it
            TipCard.Show(patienceTip);       // only does anything the very first time
        }
    }
}