using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The decor catalog screen. It shows one row per DecorSlot and forwards Buy presses to the slot.
/// The player aims at a Buy button with the crosshair and presses the deal key, the same as the deal monitor.
/// </summary>
public class DecorMonitor : MonoBehaviour
{
    // One row on the screen. [Serializable] makes it appear as a group in the Inspector.
    [Serializable]
    public class Row
    {
        public DecorSlot slot;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI priceText;
        public TextMeshProUGUI buttonLabel;
        public Image buttonImage;
        public WorldSpaceUIButton buyButton;
    }

    [Header("Screen")]
    [SerializeField] private TextMeshProUGUI gpText;
    [SerializeField] private List<Row> rows = new List<Row>();

    [Header("Button colours")]
    [SerializeField] private Color buyColor = new Color(1f, 0.6f, 0.4f);        // coral FF9966
    [SerializeField] private Color disabledColor = new Color(0.85f, 0.85f, 0.85f); // light grey

    private void Start()
    {
        foreach (Row row in rows)
        {
            // Copy the loop variable so each button remembers its OWN row
            Row thisRow = row;
            row.buyButton.onClick.AddListener(() => OnBuyPressed(thisRow));
        }

        // Redraw whenever GP changes (rent, a sale, a purchase), so "Need X more" stays correct
        if (CoreEconomySystem.Instance != null)
            CoreEconomySystem.Instance.OnEconomyUpdated += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        // Stop listening when this object is destroyed (for example on Play Again)
        if (CoreEconomySystem.Instance != null)
            CoreEconomySystem.Instance.OnEconomyUpdated -= Refresh;
    }

    private void OnBuyPressed(Row row)
    {
        row.slot.TryBuy();

        // TryBuy changes GP BEFORE it marks the item as bought, so redraw once more now
        Refresh();
    }

    private void Refresh()
    {
        int gp = CoreEconomySystem.Instance != null
            ? CoreEconomySystem.Instance.GetGP("DecorMonitor")
            : 0;

        if (gpText != null) gpText.text = $"GP: {gp}";

        foreach (Row row in rows)
        {
            DecorSlot slot = row.slot;

            // Name and price come from the slot, so there is only one place to change them
            row.nameText.text = slot.ItemName;
            row.priceText.text = $"{slot.Price} GP";

            if (slot.IsBought)
            {
                row.buttonLabel.text = "BOUGHT";
                row.buttonImage.color = disabledColor;
            }
            else if (gp < slot.Price)
            {
                row.buttonLabel.text = $"Need {slot.Price - gp} more";
                row.buttonImage.color = disabledColor;
            }
            else
            {
                row.buttonLabel.text = "BUY";
                row.buttonImage.color = buyColor;
            }
        }
    }

    // The prompt line asks this when the player looks at something on the monitor
    public string GetPrompt(Collider hitCollider, string key)
    {
        foreach (Row row in rows)
        {
            // Is the thing the player is looking at THIS row's Buy button?
            if (row.buyButton.gameObject != hitCollider.gameObject) continue;

            if (row.slot.IsBought) return $"{row.slot.ItemName} is already in your shop";
            return $"Buy {row.slot.ItemName}: {row.slot.Price} GP [{key}]";
        }

        return "";
    }
}