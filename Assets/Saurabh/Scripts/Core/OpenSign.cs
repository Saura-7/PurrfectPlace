using UnityEngine;

/// <summary>
/// The Open / Closed sign. It is only a button.
/// When the player presses it, it tells DayManager, and DayManager decides what that means
/// (open the shop during prep, close the shop at closing time).
///
/// Setup: an empty object with a BoxCollider and this script (WorldSpaceUIButton is added automatically).
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
        if (DayManager.Instance != null) DayManager.Instance.OnSignPressed();
    }

    // Called by DayManager to change how the sign looks
    public void SetOpen(bool open)
    {
        IsOpen = open;

        if (closedVisual != null) closedVisual.SetActive(!open);
        if (openVisual != null) openVisual.SetActive(open);
    }

    // The text shown when the player looks at the sign
    public string GetPrompt(string key)
    {
        return DayManager.Instance != null ? DayManager.Instance.GetSignPrompt(key) : "";
    }
}