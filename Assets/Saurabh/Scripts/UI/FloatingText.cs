using UnityEngine;
using TMPro;

/// <summary>
/// One pop-up number, e.g. "-8 GP  +15 worth". Lives on the FloatingText prefab.
/// It sticks to a point in the world (the cat), rises a little on screen, fades out, then deletes itself.
/// FloatingNumbers creates these; you never place one by hand.
/// </summary>
public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private CanvasGroup group;          // fades the whole pop-up at once

    private Vector3 worldPoint;      // the spot in the world it is attached to
    private Vector2 startOffset;     // where it starts, in pixels from that spot
    private float riseDistance;      // how far it floats up, in pixels
    private float lifetime;          // seconds before it disappears
    private float age;               // seconds since it appeared

    // FloatingNumbers calls this right after creating the pop-up
    public void Setup(Vector3 worldPoint, string text, Vector2 startOffset, float riseDistance, float lifetime)
    {
        this.worldPoint = worldPoint;
        this.startOffset = startOffset;
        this.riseDistance = riseDistance;
        this.lifetime = lifetime;
        label.text = text;
        UpdatePosition(0f);
    }

    private void Update()
    {
        age += Time.deltaTime;                 // Time.deltaTime = seconds since last frame (0 while paused)
        float t = age / lifetime;              // 0 at the start, 1 at the end

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        bool onScreen = UpdatePosition(t);

        // Stay fully visible for the first half, then fade out (hidden if it's behind the camera)
        group.alpha = !onScreen ? 0f : (t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f);
    }

    // Moves the pop-up. Returns false if the point is behind the camera.
    private bool UpdatePosition(float t)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);

        // Behind the camera: don't move it (Update hides it), but let the timer keep running
        if (screenPoint.z < 0f) return false;

        // Ease out: moves fast at first, then slows down (feels softer than a straight line)
        float eased = 1f - (1f - t) * (1f - t);
        float y = screenPoint.y + startOffset.y + riseDistance * eased;
        transform.position = new Vector3(screenPoint.x + startOffset.x, y, 0f);
        return true;
    }
}