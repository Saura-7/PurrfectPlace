using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One toast: a small card with a coloured bar on the left and a message.
/// It pops in, waits, then fades away and destroys itself.
/// MessageUI makes these from the Toast prefab, so you never place one in the scene by hand.
/// </summary>
public class ToastItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;   // the message
    [SerializeField] private Image accent;            // the thin coloured bar that shows the kind of message
    [SerializeField] private CanvasGroup group;       // lets us fade the whole card (background, bar and text) at once

    public string Message { get; private set; }       // MessageUI reads this to spot repeated messages
    public bool IsLeaving { get; private set; }        // true once it has started fading away

    private int count = 1;        // how many times this same message arrived in a row
    private float timeLeft;       // seconds until it starts to leave

    /// <summary>Called once, right after MessageUI creates the toast.</summary>
    public void Setup(string message, Color color, float life)
    {
        Message = message;
        text.text = message;
        accent.color = color;
        timeLeft = life;

        group.alpha = 1f;
        Juice.PopIn(transform);   // grows from nothing with a small bounce
    }

    /// <summary>The same message came again: show "×2" and bump the card instead of stacking a copy.</summary>
    public void Repeat(float life)
    {
        count++;
        text.text = $"{Message}  ×{count}";
        timeLeft = life;          // restart the timer so the player has time to read it
        Juice.Punch(transform);
    }

    /// <summary>Starts fading out and shrinking. The card destroys itself when done.</summary>
    public void Dismiss()
    {
        if (IsLeaving) return;    // already on its way out
        IsLeaving = true;
        Juice.Fade(group, 0f, 0.25f);
        Juice.PopOut(transform, 0.25f, destroyAfter: true);
    }

    private void Update()
    {
        if (IsLeaving) return;

        // unscaledDeltaTime ignores Time.timeScale, so toasts still leave while the
        // game is frozen (the day summary sets timeScale to 0).
        timeLeft -= Time.unscaledDeltaTime;
        if (timeLeft <= 0f) Dismiss();
    }
}
