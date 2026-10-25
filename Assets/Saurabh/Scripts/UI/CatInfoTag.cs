using UnityEngine;
using TMPro;

// One shared floating tag that appears above whichever cat the player is looking at.
// InteractionPromptUI calls Show(cat) every frame while looking at a cat, and Hide() otherwise.
public class CatInfoTag : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject content;          // the Canvas child (turned on/off)
    [SerializeField] private TextMeshProUGUI label;       // text inside the canvas

    [Header("Placement")]
    [SerializeField] private float heightAboveCat = 0.3f; // gap between the cat's head and the tag (world units)

    [Header("Size")]
    [SerializeField] private float sizePerMeter = 0.0015f; // bigger = larger tag
    [SerializeField] private float minDistance = 1f;       // closer than this, stop shrinking
    [SerializeField] private float maxDistance = 5f;       // farther than this, stop growing

    private BaseCat target;
    private string lastText = "";

    private void Awake()
    {
        Hide();
    }

    // Called by InteractionPromptUI while the player looks at a cat.
    public void Show(BaseCat cat)
    {
        target = cat;
        if (content != null && !content.activeSelf) content.SetActive(true);

        // Only rewrite the text when it actually changes (saves work every frame)
        string text = $"<size=70%>{cat.Pattern} | {cat.Body} | {cat.Behavior}</size>\n" +
                      $"Worth {cat.CalculateFinalPrice()} GP";

        if (text != lastText)
        {
            lastText = text;
            label.text = text;
        }
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

        // Find the top of the cat using its real size in the world.
        // This works even though the cat model has a huge scale (about 118).
        Renderer rend = target.GetComponentInChildren<Renderer>();
        Vector3 top = rend != null
            ? rend.bounds.center + Vector3.up * rend.bounds.extents.y
            : target.transform.position;

        transform.position = top + Vector3.up * heightAboveCat;

        // Turn the tag so its front faces the player.
        // (A world-space canvas shows its front along +Z, so we point +Z away from the camera.)
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        // Keep the tag a readable size at any distance.
        float distance = Mathf.Clamp(Vector3.Distance(cam.transform.position, transform.position), minDistance, maxDistance);
        transform.localScale = Vector3.one * (distance * sizePerMeter);
    }
}