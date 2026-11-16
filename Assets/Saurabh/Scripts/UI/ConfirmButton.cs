using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// A button that needs two clicks: the first turns it red and says "Click again to confirm",
/// the second (within a few seconds) really does it. For buttons that throw away progress,
/// like Restart and Quit, so one misclick can't end a run.
///
/// Setup: put this on the Button. Leave the Button's own On Click list EMPTY,
/// and put the real action (PauseMenu.Restart, PauseMenu.QuitGame) in "On Confirmed" below.
/// </summary>
[RequireComponent(typeof(Button))]
public class ConfirmButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string confirmText = "Click again to confirm";
    [SerializeField] private Color confirmColor = new Color32(0xE8, 0x83, 0x6A, 0xFF);   // coral
    [SerializeField] private float window = 3f;   // seconds to click again before it resets

    public UnityEvent onConfirmed;   // the real action

    private Image image;
    private string normalText;
    private Color normalColor;
    private float armedUntil = -1f;   // real time until which the second click counts; -1 = not armed

    private bool Armed => armedUntil > 0f;

    private void Awake()
    {
        image = GetComponent<Button>().targetGraphic as Image;
        if (label != null) normalText = label.text;
        if (image != null) normalColor = image.color;
        GetComponent<Button>().onClick.AddListener(Click);
    }

    // Closing the menu forgets a half-made choice
    private void OnDisable() => Disarm();

    private void Update()
    {
        // unscaledTime: the game is frozen while the menu is open, but this timer must still run
        if (Armed && Time.unscaledTime > armedUntil) Disarm();
    }

    private void Click()
    {
        if (Armed)
        {
            Disarm();
            onConfirmed.Invoke();
            return;
        }

        armedUntil = Time.unscaledTime + window;
        if (label != null) label.text = confirmText;
        if (image != null) image.color = confirmColor;
    }

    private void Disarm()
    {
        armedUntil = -1f;
        if (label != null) label.text = normalText;
        if (image != null) image.color = normalColor;
    }
}