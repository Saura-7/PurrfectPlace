using UnityEngine;

/// <summary>
/// One fixed spot in the play area where a decor item can be bought.
/// The DecorMonitor calls TryBuy() when the player presses Buy on the screen.
/// Beta version of decor: the full game replaces this with free pick-up-and-place.
/// </summary>
public class DecorSlot : MonoBehaviour
{
    [Header("Item")]
    [SerializeField] private string itemName = "Cat Scratcher";
    [SerializeField] private int price = 100;

    [Header("Optional")]
    [SerializeField] private DecorSlot requires;   // must be bought first (e.g. the 5th cage needs the 4th)
    [SerializeField] private Transform addsCage;   // for cage items: the cage point cats sit on once bought

    [Header("Visuals (gray box now, artists' model later)")]
    [SerializeField] private GameObject itemVisual;      // the bought item, hidden at the start
    [SerializeField] private GameObject forSaleMarker;   // optional floor marker shown until bought

    // Read-only values the monitor uses to draw its rows
    public string ItemName => itemName;
    public int Price => price;
    public bool IsBought { get; private set; }
    public bool IsAvailable => requires == null || requires.IsBought;   // can it be bought yet?
    public string RequiresName => requires != null ? requires.ItemName : "";

    private void Awake()
    {
        ShowBought(false);
    }

    // Returns true if the item was bought, false if the player couldn't afford it
    public bool TryBuy()
    {
        if (IsBought) return false;

        if (!IsAvailable)
        {
            MessageUI.Notify($"Buy {requires.ItemName} first");
            return false;
        }

        int gp = CoreEconomySystem.Instance.GetGP("DecorSlot");

        if (gp < price)
        {
            MessageUI.Notify($"Need {price} GP for {itemName}");
            return false;
        }

        // A negative amount means spending. This is the GP sink: the money leaves the game for good
        CoreEconomySystem.Instance.ModifyGP(-price, "DecorSlot");
        AudioManager.Play(Sfx.CoinsOut);

        IsBought = true;
        ShowBought(true);

        // A cage item also becomes a real cage the shop can use
        if (addsCage != null) CageManager.Instance.AddCage(addsCage);
        MessageUI.Notify($"Bought {itemName}! -{price} GP");
        return true;
    }

    private void ShowBought(bool bought)
    {
        if (itemVisual != null) itemVisual.SetActive(bought);
        if (forSaleMarker != null) forSaleMarker.SetActive(!bought);
    }
}