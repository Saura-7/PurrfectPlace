using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The one card on screen that shows any TipData. It pauses the game the first time something
/// new happens, and waits for "Got it". Playtesters missed toasts that faded on their own,
/// so new rules get a card that stays until the player has read it.
///
/// This script knows nothing about rare cats or rent: the words live in TipData assets.
/// Any script can call TipCard.Show(someTip). The card waits until nothing else is on screen
/// (pause menu, shop PC, rare cat panel, day receipt) plus a short breath, then appears.
///
/// Setup: a panel on the HUD canvas with a title, a body, an optional icon and a "Got it"
/// button. Put this script on an always-active object (not on the panel) and drag them in.
/// </summary>
public class TipCard : MonoBehaviour
{
    [SerializeField] private GameObject panel;           // what switches on and off (the dim layer, with the card inside)
    [SerializeField] private Transform card;             // optional: the card itself, so only it pops (not the dim)
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image iconImage;            // optional: hidden when a tip has no icon
    [SerializeField] private Button gotItButton;
    [SerializeField] private float delay = 1f;           // seconds of calm before a card appears

    private static TipCard instance;

    public static bool IsOpen { get; private set; }

    // The frame the card closed on, so the Esc that closes it doesn't also open the pause menu
    private static int closedOnFrame = -10;
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedOnFrame + 1;

    private readonly HashSet<TipData> shown = new HashSet<TipData>();   // each tip only once per run
    private readonly Queue<TipData> waiting = new Queue<TipData>();     // first in, first out: two at once show one after the other
    private float readyAt;                                              // real time when the next card may appear
    private PauseMenu pauseMenu;

    private void Awake()
    {
        instance = this;
        panel.SetActive(false);
        gotItButton.onClick.AddListener(Close);
    }

    private void Start()
    {
        pauseMenu = FindFirstObjectByType<PauseMenu>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        IsOpen = false;
    }

    /// <summary>Ask for a tip. Safe to call every time: each tip only ever shows once.</summary>
    public static void Show(TipData tip)
    {
        if (instance == null || tip == null) return;   // no card in the scene, or no tip dragged in: the game still works
        if (!instance.shown.Add(tip)) return;          // Add returns false if it was already in the set

        instance.waiting.Enqueue(tip);
        instance.readyAt = Time.unscaledTime + instance.delay;
    }

    private void Update()
    {
        if (IsOpen)
        {
            // The button works with the mouse; these keys close it too, so nobody gets stuck
            Keyboard k = Keyboard.current;
            if (k != null && (k.escapeKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                Close();
            return;
        }

        if (waiting.Count == 0) return;

        // Something else is on screen: keep pushing the timer back, so the card
        // appears a moment AFTER that thing closes, not in the same frame
        if (!CanShow())
        {
            readyAt = Time.unscaledTime + delay;
            return;
        }

        if (Time.unscaledTime >= readyAt) Open(waiting.Dequeue());
    }

    private static bool CanShow()
    {
        if (Time.timeScale == 0f || PauseMenu.IsPaused) return false;    // already frozen by something else
        if (DevConsole.IsOpen || RareCatPanel.IsOpen || ShopComputer.IsOpen) return false;
        if (DayManager.Instance != null && DayManager.Instance.Phase == DayPhase.Summary) return false;   // the day receipt
        return true;
    }

    private void Open(TipData tip)
    {
        titleText.text = tip.Title;
        bodyText.text = FillKeys(tip.Body);

        if (iconImage != null)
        {
            iconImage.sprite = tip.Icon;
            iconImage.gameObject.SetActive(tip.Icon != null);
        }

        panel.SetActive(true);
        Juice.PopIn(card != null ? card : panel.transform);   // pop the card if one is set, otherwise the whole panel
        IsOpen = true;

        if (pauseMenu != null) pauseMenu.FreezeGame();   // time stops, cursor free, walking and looking off
    }

    private void Close()
    {
        if (!IsOpen) return;

        panel.SetActive(false);
        IsOpen = false;
        closedOnFrame = Time.frameCount;

        if (pauseMenu != null) pauseMenu.UnfreezeGame();
        readyAt = Time.unscaledTime + delay;              // a breath before the next queued card, if any
        AudioManager.Play(Sfx.ButtonClick);
    }

    /// <summary>
    /// Swaps {Feed}, {CatOptions} and the other PromptAction names for the real keys, in bold.
    /// A new PromptAction works here automatically, because it loops over all of them.
    /// </summary>
    private static string FillKeys(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        foreach (PromptAction action in System.Enum.GetValues(typeof(PromptAction)))
        {
            text = text.Replace("{" + action + "}", $"<b>{InputPrompts.Get(action)}</b>");
        }
        return text;
    }
}