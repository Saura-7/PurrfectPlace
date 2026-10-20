using UnityEngine;

/// <summary>
/// The Open / Closed sign. The player looks at it and presses the deal key to open the shop.
/// Customers only arrive after the sign is switched on, and the day timer only runs while it is open.
///
/// Setup: an empty object with a BoxCollider, this script, and a WorldSpaceUIButton (added automatically).
/// Put the visuals in two child objects WITHOUT colliders.
/// When the artists deliver a final sign, swap the child models and keep this object.
/// </summary>
[RequireComponent(typeof(WorldSpaceUIButton))]
public class OpenSign : MonoBehaviour
{
    public static OpenSign Instance { get; private set; }

    [Header("Visuals (swap these for the artists' models later)")]
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openVisual;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // The sign reuses the same button the deal screen uses, so the existing interaction key works
        GetComponent<WorldSpaceUIButton>().onClick.AddListener(OnPressed);

        SetOpen(false);
    }

    private void OnPressed()
    {
        if (IsOpen)
        {
            MessageUI.Notify("The shop is already open.");
            return;
        }

        if (DayManager.Instance != null) DayManager.Instance.OpenShop();
    }

    // Called by DayManager. Does not start the day by itself.
    public void SetOpen(bool open)
    {
        IsOpen = open;

        if (closedVisual != null) closedVisual.SetActive(!open);
        if (openVisual != null) openVisual.SetActive(open);
    }
}