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

    [Header("Visuals (gray box now, artists' model later)")]
    [SerializeField] private GameObject itemVisual;      // the bought item, hidden at the start
    [SerializeField] private GameObject forSaleMarker;   // optional floor marker shown until bought

    // Read-only values the monitor uses to draw its rows
    public string ItemName => itemName;
    public int Price => price;
    public bool IsBought { get; private set; }

    private void Awake()
    {
        ShowBought(false);
    }

    // Returns true if the item was bought, false if the player couldn't afford it
    public bool TryBuy()
    {
        if (IsBought) return false;

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
        MessageUI.Notify($"Bought {itemName}! -{price} GP");
        return true;
    }

    private void ShowBought(bool bought)
    {
        if (itemVisual != null) itemVisual.SetActive(bought);
        if (forSaleMarker != null) forSaleMarker.SetActive(!bought);
    }
}