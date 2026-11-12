using UnityEngine;
using TMPro;

// One shared tag that appears above whichever cat the player is looking at.
// InteractionPromptUI calls Show(cat) every frame while looking at a cat, and Hide() otherwise.
//
// The tag lives on the HUD canvas (Screen Space - Overlay), NOT in the 3D world.
// Every frame we find where the cat's head is ON SCREEN and move the tag there.
// Because it is drawn on top of the screen, cages can't cover it, and HDRP fog,
// exposure and depth of field can't wash it out.
public class CatInfoTag : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject content;          // the panel (background + text), turned on/off
    [SerializeField] private TextMeshProUGUI label;       // text inside the panel

    [Header("Placement")]
    [SerializeField] private float heightAboveCat = 0.15f; // gap above the cat's head (world units)
    [SerializeField] private float screenOffsetY = 20f;    // extra gap on screen (pixels)

    private BaseCat target;
    private string lastText = "";
    private RectTransform rect;

    private void Awake()
    {
        rect = (RectTransform)transform;
        Hide();
    }

    // Called by InteractionPromptUI while the player looks at a cat.
    public void Show(BaseCat cat)
    {
        target = cat;
        if (content != null && !content.activeSelf) content.SetActive(true);

        // Only rewrite the text when it actually changes (saves work every frame)
        string text = BuildText(cat);
        if (text != lastText)
        {
            lastText = text;
            label.text = text;
        }
    }

    // The tag shows what the cat IS (traits, tier, worth).
    // What you can DO (keys, levels, costs) is in the prompt under the crosshair.
    private string BuildText(BaseCat cat)
    {
        int care = cat.TotalCare;
        int nextTierAt = CareRules.NextTierAt(care);

        string text = $"<size=75%>{cat.Pattern} | {cat.Body} | {cat.Behavior}</size>\n";
        text += $"<b>{CareRules.ColoredName(CareRules.TierOf(care))}</b>";
        if (nextTierAt > 0)
            text += $"<size=75%>  next: {CareRules.Name(CareRules.TierOf(nextTierAt))} at {nextTierAt} total</size>";
        text += $"\nWorth {cat.CalculateFinalPrice()} GP <size=75%>(+{CareRules.ValuePerPoint} per care)</size>";
        return text;
    }

    public void Hide()
    {
        target = null;
        lastText = "";
        if (content != null) content.SetActive(false);
    }

    // LateUpdate runs after everything else moved this frame (including the cat's hop),
    // so the tag follows the cat smoothly.
    private void LateUpdate()
    {
        if (target == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Find the top of the cat using its real size in the world (BaseCat works it out)
        Vector3 top = target.TopPosition;

        // WorldToScreenPoint turns a 3D position into a pixel position on the screen.
        // x and y are pixels; z is how far in front of the camera the point is.
        Vector3 screenPoint = cam.WorldToScreenPoint(top + Vector3.up * heightAboveCat);

        // z below 0 means the point is behind the camera, so don't draw it
        if (screenPoint.z < 0f)
        {
            content.SetActive(false);
            return;
        }
        if (!content.activeSelf) content.SetActive(true);

        rect.position = new Vector3(screenPoint.x, screenPoint.y + screenOffsetY, 0f);
    }
}