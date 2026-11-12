using UnityEngine;

/// <summary>
/// Spawns pop-up numbers on the HUD. Any script can call:
///     FloatingNumbers.Show(worldPoint, FloatingNumbers.Spend("-8 GP"));
/// Put this on an empty object inside the HUD canvas (Screen Space - Overlay).
/// All the feel settings (speed, distance, colours) are here in the Inspector.
/// </summary>
public class FloatingNumbers : MonoBehaviour
{
    private static FloatingNumbers instance;

    [Header("Prefab")]
    [SerializeField] private FloatingText popupPrefab;

    [Header("Feel")]
    [SerializeField] private Vector2 startOffset = new Vector2(90f, 0f); // pixels from the cat (right of it, so it doesn't cover the tag)
    [SerializeField] private float riseDistance = 70f;                   // how far it floats up (pixels)
    [SerializeField] private float lifetime = 1.2f;                      // seconds on screen

    [Header("Colours")]
    [SerializeField] private Color spendColor = new Color(0.95f, 0.45f, 0.40f); // money going out
    [SerializeField] private Color gainColor = new Color(0.45f, 0.85f, 0.50f);  // value or money coming in

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Show a pop-up attached to a point in the world. The text can use TextMeshPro rich text.
    public static void Show(Vector3 worldPoint, string text)
    {
        if (instance == null || instance.popupPrefab == null) return;   // no pop-ups in this scene: just skip

        FloatingText popup = Instantiate(instance.popupPrefab, instance.transform);
        popup.Setup(worldPoint, text, instance.startOffset, instance.riseDistance, instance.lifetime);
    }

    // Wrap text in the "spend" or "gain" colour, e.g. Spend("-8 GP")
    public static string Spend(string text) => Wrap(text, instance != null ? instance.spendColor : Color.red);
    public static string Gain(string text) => Wrap(text, instance != null ? instance.gainColor : Color.green);

    private static string Wrap(string text, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
    }
}