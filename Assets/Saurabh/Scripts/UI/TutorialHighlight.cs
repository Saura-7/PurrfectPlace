using UnityEngine;

/// <summary>
/// Put this on an object the day 1 guide points at (cages, shop computer, sign, deal monitor).
/// While the tutorial's current step is one of this object's steps, a soft glow shell is shown and pulses gently.
/// The glow fades out as the player walks up close, so it never covers the screen.
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

    [Header("Fade when the player is close")]
    [SerializeField] private float fadeStartDistance = 4f;  // farther than this: full glow
    [SerializeField] private float fadeEndDistance = 1.5f;  // closer than this: glow is gone

    // HDRP Unlit materials store their colour under this name
    private const string ColorProperty = "_UnlitColor";

    private Vector3 baseScale;
    private bool glowing;

    private Renderer glowRenderer;
    private MaterialPropertyBlock block;
    private Color baseColor = Color.white;
    private bool canFade;          // true if the material has a colour we can fade

    private void Awake()
    {
        if (highlightVisual == null) return;

        baseScale = highlightVisual.transform.localScale;  // remember the size we set in the editor

        glowRenderer = highlightVisual.GetComponentInChildren<Renderer>(true);
        if (glowRenderer != null && glowRenderer.sharedMaterial != null
            && glowRenderer.sharedMaterial.HasProperty(ColorProperty))
        {
            baseColor = glowRenderer.sharedMaterial.GetColor(ColorProperty);  // the colour and alpha we set in the editor
            block = new MaterialPropertyBlock();
            canFade = true;
        }

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

        if (!glowing) return;

        // Gentle pulse: Sin gives a smooth wave between -1 and 1
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        highlightVisual.transform.localScale = baseScale * pulse;

        ApplyDistanceFade();
    }

    // 1 = full glow (far away), 0 = invisible (right next to it)
    private void ApplyDistanceFade()
    {
        Camera cam = Camera.main;
        if (cam == null || glowRenderer == null) return;

        // Distance to the nearest point of the glow, not its centre,
        // so big objects like the desk fade when you stand beside them
        Vector3 nearest = glowRenderer.bounds.ClosestPoint(cam.transform.position);
        float distance = Vector3.Distance(cam.transform.position, nearest);

        float visibility = Mathf.InverseLerp(fadeEndDistance, fadeStartDistance, distance);

        if (canFade)
        {
            // Same colour, but the alpha (see-through amount) is scaled by visibility
            Color c = baseColor;
            c.a = baseColor.a * visibility;

            glowRenderer.GetPropertyBlock(block);
            block.SetColor(ColorProperty, c);
            glowRenderer.SetPropertyBlock(block);
        }
        else
        {
            // Fallback if the material has no fadeable colour: just hide it when close
            glowRenderer.enabled = visibility > 0.01f;
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