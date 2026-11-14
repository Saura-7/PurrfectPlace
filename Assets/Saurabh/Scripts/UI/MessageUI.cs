using System.Collections.Generic;
using UnityEngine;

// What a message is about. Each kind gets its own colour bar, so the player can
// tell good news from a problem before reading a single word.
public enum ToastKind
{
    Info,      // gray:  neutral tips and phase changes ("Prep time...")
    Good,      // green: money in, a purchase done
    Warning,   // amber: something needs attention soon (closing time, rent)
    Bad,       // red:   it didn't work or you lost something (not enough GP, deal missed)
    Heart      // pink:  adoptions and reputation, the emotional moments
}

/// <summary>
/// Shows short messages as toasts at the top centre of the screen.
/// Call it from any script, exactly as before:
///
///     MessageUI.Notify("Not enough GP!", ToastKind.Bad);
///     MessageUI.Notify("The shop is open!");          // no kind given = Info
/// </summary>
public class MessageUI : MonoBehaviour
{
    public static MessageUI Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] private RectTransform container;   // the "Toasts" object with the Vertical Layout Group
    [SerializeField] private ToastItem toastPrefab;     // the Toast prefab

    [Header("Timing")]
    [SerializeField] private float showTime = 2.5f;     // seconds a normal toast stays
    [SerializeField] private float problemTimeMultiplier = 1.5f;  // warnings and problems stay longer, so they get read
    [SerializeField] private int maxToasts = 3;          // more than this and the oldest leaves early

    [Header("Colours (bar on the left of each toast)")]
    [SerializeField] private Color infoColor    = new Color32(0x9A, 0x94, 0x8A, 0xFF);
    [SerializeField] private Color goodColor    = new Color32(0x3F, 0xA2, 0x8C, 0xFF);
    [SerializeField] private Color warningColor = new Color32(0xE9, 0xA2, 0x3B, 0xFF);
    [SerializeField] private Color badColor     = new Color32(0xE8, 0x5D, 0x4A, 0xFF);
    [SerializeField] private Color heartColor   = new Color32(0xE8, 0x83, 0xA6, 0xFF);

    // The toasts on screen right now, oldest first
    private readonly List<ToastItem> toasts = new List<ToastItem>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // "kind = ToastKind.Info" is a default value: old calls with only a message still work unchanged
    public static void Notify(string message, ToastKind kind = ToastKind.Info)
    {
        if (Instance != null) Instance.Show(message, kind);
    }

    private void Show(string message, ToastKind kind)
    {
        if (container == null || toastPrefab == null) return;

        // Forget toasts that are already fading out or were destroyed
        toasts.RemoveAll(t => t == null || t.IsLeaving);

        float life = (kind == ToastKind.Warning || kind == ToastKind.Bad) ? showTime * problemTimeMultiplier : showTime;

        // Same message as the newest toast? Bump that one ("×2") instead of stacking a copy.
        // This stops spam when the player clicks a locked thing five times.
        ToastItem newest = toasts.Count > 0 ? toasts[toasts.Count - 1] : null;
        if (newest != null && newest.Message == message)
        {
            newest.Repeat(life);
            return;
        }

        // Make a new toast inside the container. The Vertical Layout Group stacks it under the others.
        ToastItem toast = Instantiate(toastPrefab, container);
        toast.Setup(message, ColorOf(kind), life);
        toasts.Add(toast);

        // Too many on screen: the oldest leaves early
        while (toasts.Count > maxToasts)
        {
            toasts[0].Dismiss();
            toasts.RemoveAt(0);
        }
    }

    private Color ColorOf(ToastKind kind)
    {
        switch (kind)
        {
            case ToastKind.Good:    return goodColor;
            case ToastKind.Warning: return warningColor;
            case ToastKind.Bad:     return badColor;
            case ToastKind.Heart:   return heartColor;
            default:                return infoColor;
        }
    }
}