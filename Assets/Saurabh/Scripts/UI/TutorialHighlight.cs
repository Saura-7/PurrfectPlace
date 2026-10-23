using UnityEngine;

/// <summary>
/// Put this on an object the day 1 guide points at (cages, shop computer, sign, deal monitor).
/// While the tutorial's current step is one of this object's steps, a soft glow shell is shown and pulses gently.
/// The glow shell is a child object with a see-through glowing material. Artists can recolour it later.
/// </summary>
public class TutorialHighlight : MonoBehaviour
{
    [Header("When should this object glow?")]
    [SerializeField] private TutorialStep[] steps;        // e.g. Feed + Clean for the cages

    [Header("The glow")]
    [SerializeField] private GameObject highlightVisual;  // the glow shell (a child of this object)
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.03f;   // 0.03 = grows and shrinks by 3 percent

    private Vector3 baseScale;
    private bool glowing;

    private void Awake()
    {
        if (highlightVisual == null) return;

        baseScale = highlightVisual.transform.localScale;  // remember the size we set in the editor
        highlightVisual.SetActive(false);                  // start switched off
    }

    private void Update()
    {
        if (highlightVisual == null) return;

        bool shouldGlow = ShouldGlow();

        // Only switch the object on/off when the answer changes
        if (shouldGlow != glowing)
        {
            glowing = shouldGlow;
            highlightVisual.SetActive(glowing);
            if (!glowing) highlightVisual.transform.localScale = baseScale;
        }

        // Gentle pulse: Sin gives a smooth wave between -1 and 1
        if (glowing)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            highlightVisual.transform.localScale = baseScale * pulse;
        }
    }

    private bool ShouldGlow()
    {
        TutorialStep? current = TutorialNote.CurrentStep;
        if (!current.HasValue) return false;

        foreach (TutorialStep step in steps)
        {
            if (step == current.Value) return true;
        }
        return false;
    }
}