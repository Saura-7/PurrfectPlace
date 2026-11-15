using UnityEngine;

/// <summary>
/// One arrow on the HUD that shows the player where to go next.
///
///   Target ON screen  → a small marker bobs just above it, pointing down
///   Target OFF screen → the arrow sits on the screen edge, pointing the way to turn
///   Close to target   → it fades away (the glow and the look-at prompt take over)
///
/// What it points at:
///   Day 1   → the current tutorial step's object (the same objects that glow)
///   Later   → only gentle hints: a customer has been waiting at the desk for a while,
///             or it's closing time and the shop is empty (point at the sign)
///
/// Put this on the HUD canvas. The arrow graphic must point RIGHT when its rotation is 0.
/// </summary>
public class GuideArrow : MonoBehaviour
{
    [Header("The arrow on the HUD")]
    [SerializeField] private RectTransform arrow;      // the badge that moves and turns
    [SerializeField] private CanvasGroup group;        // fades the whole arrow in and out

    [Header("Placement (pixels)")]
    [SerializeField] private float edgeMargin = 80f;   // how far from the screen edge the off-screen arrow sits
    [SerializeField] private float heightAboveTarget = 50f;   // marker gap above the target's top
    [SerializeField] private float bobAmount = 8f;     // how far it bobs
    [SerializeField] private float bobSpeed = 4f;

    [Header("When to hide")]
    [SerializeField] private float hideDistance = 2.5f;    // metres: closer than this and on screen = hide
    [SerializeField] private float fadeSpeed = 6f;         // how fast it fades in and out

    [Header("Hints after day 1")]
    [SerializeField] private bool hintsAfterDayOne = true;
    [SerializeField] private float customerWaitBeforeHint = 15f;   // seconds a customer waits before we point at them
    [SerializeField] private float closingIdleBeforeHint = 4f;     // seconds of empty shop at closing time before we point at the sign
    [SerializeField] private float customerHeadroom = 0.3f;        // metres above the customer's head

    private float customerWaitTimer;
    private float closingTimer;
    private CustomerAI waitingCustomer;    // the customer the wait timer belongs to

    private void Awake()
    {
        if (group != null) group.alpha = 0f;   // start hidden
    }

    // LateUpdate: after the player and camera have moved this frame, so the arrow never lags behind
    private void LateUpdate()
    {
        if (arrow == null || group == null) return;

        Camera cam = Camera.main;
        bool show = false;

        // Paused or on the day summary (time frozen): no arrow
        if (cam != null && Time.timeScale > 0f && TryGetTarget(out Vector3 target))
        {
            show = PlaceArrow(cam, target);
        }

        // Fade toward visible or hidden. MoveTowards changes alpha by at most fadeSpeed × time each frame.
        float goal = show ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, goal, fadeSpeed * Time.unscaledDeltaTime);
    }

    // ==========================================
    // WHAT TO POINT AT
    // ==========================================

    private bool TryGetTarget(out Vector3 point)
    {
        point = Vector3.zero;

        // Day 1: follow the tutorial
        if (TutorialNote.IsRunning)
        {
            TutorialStep? step = TutorialNote.CurrentStep;
            if (!step.HasValue) return false;

            // Feed and Clean: point at a CAT that still needs it, not just any cage (a cage can be empty)
            if (step == TutorialStep.Feed || step == TutorialStep.Clean)
            {
                if (TryNearestCatNeeding(step.Value, out point)) return true;
            }
            return TryNearestHighlight(step.Value, out point);
        }

        if (!hintsAfterDayOne) return false;

        // Later days: a customer who has waited at the desk for a while
        if (TryWaitingCustomer(out point)) return true;

        // Later days: closing time and nobody inside → the sign
        DayManager day = DayManager.Instance;
        bool shopEmptyAtClosing = day != null && day.Phase == DayPhase.LastCall && day.CustomersInside == 0;
        closingTimer = shopEmptyAtClosing ? closingTimer + Time.deltaTime : 0f;
        if (closingTimer >= closingIdleBeforeHint)
            return TryNearestHighlight(TutorialStep.CloseShop, out point);   // the sign is the CloseShop highlight

        return false;
    }

    // The nearest caged cat that can still be fed (or cleaned), so the arrow never points at an empty cage
    private bool TryNearestCatNeeding(TutorialStep step, out Vector3 point)
    {
        point = Vector3.zero;
        if (CageManager.Instance == null || Camera.main == null) return false;

        Vector3 player = Camera.main.transform.position;
        float best = float.MaxValue;

        foreach (BaseCat cat in CageManager.Instance.CatsInCages)
        {
            if (cat == null || cat.IsHeldByCustomer) continue;

            bool needsIt = step == TutorialStep.Feed ? cat.CanUpgradeFeeding : cat.CanUpgradeCleanliness;
            if (!needsIt) continue;

            Vector3 top = cat.TopPosition;
            float d = (top - player).sqrMagnitude;
            if (d < best) { best = d; point = top; }
        }
        return best < float.MaxValue;
    }

    // Of all the objects that glow for this step (e.g. 3 cages for Feed), point at the nearest one
    private bool TryNearestHighlight(TutorialStep step, out Vector3 point)
    {
        point = Vector3.zero;
        Vector3 player = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        float best = float.MaxValue;

        foreach (TutorialHighlight h in TutorialHighlight.All)
        {
            if (!h.Covers(step)) continue;
            Vector3 p = h.GuidePoint;
            float d = (p - player).sqrMagnitude;   // squared distance: fine for "which is closer", and faster
            if (d < best) { best = d; point = p; }
        }
        return best < float.MaxValue;
    }

    private bool TryWaitingCustomer(out Vector3 point)
    {
        point = Vector3.zero;
        CustomerAI front = CustomerQueueManager.Instance != null
            ? CustomerQueueManager.Instance.GetCurrentCustomerAtDesk()
            : null;

        // Restart the timer whenever a different customer (or nobody) is at the front
        if (front != waitingCustomer)
        {
            waitingCustomer = front;
            customerWaitTimer = 0f;
        }
        if (front == null) return false;

        // Already dealing with them (their cat is on the desk): no hint, and the wait starts over
        if (front.heldCat != null && front.heldCat.OnDesk)
        {
            customerWaitTimer = 0f;
            return false;
        }

        customerWaitTimer += Time.deltaTime;
        if (customerWaitTimer < customerWaitBeforeHint) return false;

        // Just above the customer's head (the top of everything they draw)
        Renderer body = front.GetComponentInChildren<Renderer>();
        float top = body != null ? body.bounds.max.y : front.transform.position.y + 2f;
        point = new Vector3(front.transform.position.x, top + customerHeadroom, front.transform.position.z);
        return true;
    }

    // ==========================================
    // WHERE TO DRAW IT
    // ==========================================

    // Returns false when the arrow should hide (we're right next to the target)
    private bool PlaceArrow(Camera cam, Vector3 target)
    {
        // WorldToScreenPoint: x and y are pixels, z is the distance in front of the camera
        Vector3 sp = cam.WorldToScreenPoint(target);
        bool behind = sp.z < 0f;

        Vector2 centre = new Vector2(Screen.width, Screen.height) * 0.5f;
        float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmount;

        bool onScreen = !behind
                        && sp.x > edgeMargin && sp.x < Screen.width - edgeMargin
                        && sp.y > edgeMargin && sp.y < Screen.height - edgeMargin;

        if (onScreen)
        {
            // Close and visible: the glow and the look-at prompt are enough
            if (Vector3.Distance(cam.transform.position, target) < hideDistance) return false;

            // A marker above the target, pointing down (-90°), bobbing up and down
            arrow.position = new Vector3(sp.x, sp.y + heightAboveTarget + bob, 0f);
            arrow.rotation = Quaternion.Euler(0f, 0f, -90f);
            return true;
        }

        // Off screen: which way from the centre of the screen is the target?
        Vector2 dir = new Vector2(sp.x, sp.y) - centre;
        if (behind) dir = -dir;   // behind the camera, the screen position comes out mirrored, so flip it
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;   // exactly behind us: point down ("turn around")
        dir.Normalize();

        // Slide from the centre along that direction until we hit the edge (minus the margin).
        // Whichever edge we reach first (side or top/bottom) decides how far we go.
        float halfW = centre.x - edgeMargin;
        float halfH = centre.y - edgeMargin;
        float scaleX = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
        float scaleY = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
        Vector2 edge = centre + dir * Mathf.Min(scaleX, scaleY);

        // Point the arrow along the direction (Atan2 gives the angle of a direction, in radians)
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrow.rotation = Quaternion.Euler(0f, 0f, angle);

        // Bob toward the edge, like it's nudging you to turn
        arrow.position = new Vector3(edge.x, edge.y, 0f) + (Vector3)(dir * bob);
        return true;
    }
}