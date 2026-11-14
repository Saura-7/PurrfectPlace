using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The juice toolkit: small, reusable animations that make things feel alive.
/// Every polish effect in the game calls these, so all motion shares one style.
///
///     Juice.Punch(transform);          // grow a little, then settle back (GP counter, buttons)
///     Juice.PopIn(transform);          // appear from nothing with a small overshoot (toasts, a new cage)
///     Juice.Squash(transform);         // squash and stretch (a cat reacting)
///     Juice.Fade(canvasGroup, 0f);     // fade UI in or out
///
/// You never add this component yourself: it creates a hidden helper object the first time it's used.
/// </summary>
public class Juice : MonoBehaviour
{
    // ==========================================
    // SETTINGS
    // ==========================================

    // Players who dislike motion can switch this on (saved between sessions).
    // Every animation then jumps straight to its end state.
    public static bool ReducedMotion
    {
        get => PlayerPrefs.GetInt("ReducedMotion", 0) == 1;
        set => PlayerPrefs.SetInt("ReducedMotion", value ? 1 : 0);
    }

    // ==========================================
    // THE HIDDEN HELPER
    // ==========================================

    // Coroutines can only run on a MonoBehaviour in the scene, but a static class has none.
    // So the first time anyone calls Juice, we make one invisible object to run them on.
    private static Juice runner;
    private static Juice Runner
    {
        get
        {
            if (runner == null)
            {
                GameObject go = new GameObject("[Juice]");
                runner = go.AddComponent<Juice>();
            }
            return runner;
        }
    }

    // One running animation per object. Starting a new one on the same object stops the old one.
    private readonly Dictionary<Object, Coroutine> running = new Dictionary<Object, Coroutine>();

    // The object's real size, remembered while an animation is running on it.
    // Without this, punching an object WHILE it's already punched would make it grow a little more
    // every time (100% -> 115% -> 132% ...). We always animate around the real size.
    // It is forgotten when the animation ends, because an object's real size can change later
    // (a cat moved into a new parent gets a new local scale).
    private readonly Dictionary<Transform, Vector3> baseScales = new Dictionary<Transform, Vector3>();

    private void OnDestroy()
    {
        if (runner == this) runner = null;
    }

    // ==========================================
    // PUBLIC MOVES
    // ==========================================

    /// <summary>Grows by "amount" (0.15 = 15%) and settles back, like a heartbeat.</summary>
    public static void Punch(Transform target, float amount = 0.15f, float duration = 0.25f, bool unscaledTime = true)
    {
        if (target == null) return;
        Vector3 baseScale = Runner.BaseScale(target);
        Runner.Play(target, Runner.ScaleRoutine(target, duration, unscaledTime, t =>
        {
            // Goes up fast, comes back down slowly: 0 -> 1 -> 0 over the animation
            float bump = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.5f);
            return baseScale * (1f + amount * bump);
        }, baseScale));
    }

    /// <summary>Appears from size 0 to its real size with a small overshoot.</summary>
    public static void PopIn(Transform target, float duration = 0.3f, bool unscaledTime = true)
    {
        if (target == null) return;
        Vector3 baseScale = Runner.BaseScale(target);
        target.localScale = Vector3.zero;
        Runner.Play(target, Runner.ScaleRoutine(target, duration, unscaledTime, t => baseScale * EaseOutBack(t), baseScale));
    }

    /// <summary>Shrinks to 0, then optionally destroys the object.</summary>
    public static void PopOut(Transform target, float duration = 0.2f, bool destroyAfter = false, bool unscaledTime = true)
    {
        if (target == null) return;
        Vector3 baseScale = Runner.BaseScale(target);
        Runner.Play(target, Runner.PopOutRoutine(target, baseScale, duration, destroyAfter, unscaledTime));
    }

    /// <summary>Squashes flat and wide, then stretches back (for cats and soft things).</summary>
    public static void Squash(Transform target, float amount = 0.2f, float duration = 0.3f, bool unscaledTime = false)
    {
        if (target == null) return;
        Vector3 baseScale = Runner.BaseScale(target);
        Runner.Play(target, Runner.ScaleRoutine(target, duration, unscaledTime, t =>
        {
            // A damped wobble: flat and wide first, then a little tall, then settle
            float wobble = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
            return new Vector3(baseScale.x * (1f + amount * wobble),
                               baseScale.y * (1f - amount * wobble),
                               baseScale.z * (1f + amount * wobble));
        }, baseScale));
    }

    /// <summary>Fades a whole UI group to the given alpha (0 = invisible, 1 = fully visible).</summary>
    public static void Fade(CanvasGroup group, float to, float duration = 0.25f, bool unscaledTime = true)
    {
        if (group == null) return;
        Runner.Play(group, Runner.FadeRoutine(group, to, duration, unscaledTime));
    }

    // ==========================================
    // EASING (see study note 13)
    // ==========================================

    // Ease-out: fast start, gentle stop
    public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    // Ease-out-back: overshoots a little past 1, then settles. Feels bouncy and friendly.
    public static float EaseOutBack(float t)
    {
        const float s = 1.70158f;   // how far it overshoots (this standard value gives about 10%)
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    // ==========================================
    // HOW THE MOVES RUN
    // ==========================================

    private Vector3 BaseScale(Transform target)
    {
        if (!baseScales.TryGetValue(target, out Vector3 scale))
        {
            scale = target.localScale;
            baseScales[target] = scale;
        }
        return scale;
    }

    // Stops any animation already running on this object, then starts the new one
    private void Play(Object key, IEnumerator routine)
    {
        if (running.TryGetValue(key, out Coroutine old) && old != null) StopCoroutine(old);
        running[key] = StartCoroutine(routine);
    }

    // Runs "scaleAt(t)" every frame for t from 0 to 1, then snaps to the final size
    private IEnumerator ScaleRoutine(Transform target, float duration, bool unscaledTime,
                                     System.Func<float, Vector3> scaleAt, Vector3 finalScale)
    {
        if (!ReducedMotion)
        {
            float time = 0f;
            while (time < duration)
            {
                if (target == null) yield break;           // the object was destroyed mid-animation
                time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                target.localScale = scaleAt(Mathf.Clamp01(time / duration));
                yield return null;                          // wait one frame
            }
        }
        if (target != null) target.localScale = finalScale;
        running.Remove(target);
        baseScales.Remove(target);   // finished: the next animation measures the size fresh
    }

    private IEnumerator PopOutRoutine(Transform target, Vector3 baseScale, float duration, bool destroyAfter, bool unscaledTime)
    {
        if (!ReducedMotion)
        {
            float time = 0f;
            while (time < duration)
            {
                if (target == null) yield break;
                time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                target.localScale = baseScale * (1f - EaseOut(t));
                yield return null;
            }
        }
        running.Remove(target);
        if (target == null) yield break;
        if (destroyAfter)
        {
            baseScales.Remove(target);
            Destroy(target.gameObject);
        }
        else target.localScale = Vector3.zero;   // keep the real size remembered, so a PopIn can bring it back
    }

    private IEnumerator FadeRoutine(CanvasGroup group, float to, float duration, bool unscaledTime)
    {
        float from = group.alpha;
        if (!ReducedMotion)
        {
            float time = 0f;
            while (time < duration)
            {
                if (group == null) yield break;
                time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, EaseOut(Mathf.Clamp01(time / duration)));
                yield return null;
            }
        }
        if (group != null) group.alpha = to;
        running.Remove(group);
    }
}