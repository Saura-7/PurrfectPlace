using System.Collections;
using UnityEngine;

/// <summary>
/// A full-screen cream colour that fades in and out between days, so the end of a day
/// feels like the end of a day: the shop fades away, the receipt appears, and the next
/// morning fades back in. It also hides the moment the shop is reset for the new day.
///
/// Setup: a full-screen Image with a CanvasGroup, as the FIRST child of the HUD canvas.
/// First child = drawn first = underneath every other HUD element, so it covers the
/// 3D world but the GP counter and the receipt still show on top of it.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private bool fadeInOnStart = true;   // the game opens by fading in from cream
    [SerializeField] private float startFadeTime = 1f;

    private static ScreenFader instance;
    private CanvasGroup group;

    private void Awake()
    {
        instance = this;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;   // never blocks clicks on buttons above or below it
        group.interactable = false;
        group.alpha = fadeInOnStart ? 1f : 0f;
    }

    private void Start()
    {
        if (fadeInOnStart) Fade(0f, startFadeTime);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// Fade to a target (1 = fully cream, 0 = clear) over some seconds.
    /// Use it with "yield return" inside a coroutine to wait until the fade has finished.
    /// Real time, so it works while the game is frozen.
    /// </summary>
    public static IEnumerator FadeTo(float target, float duration)
    {
        if (instance == null) yield break;   // no fader in this scene: skip the fade, the game still works

        CanvasGroup group = instance.group;
        float start = group.alpha;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            // SmoothStep eases in AND out: starts gently, speeds up, settles gently
            group.alpha = Mathf.SmoothStep(start, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }

    /// <summary>The same fade, started from anywhere without waiting for it.</summary>
    public static void Fade(float target, float duration)
    {
        if (instance != null) instance.StartCoroutine(FadeTo(target, duration));
    }
}