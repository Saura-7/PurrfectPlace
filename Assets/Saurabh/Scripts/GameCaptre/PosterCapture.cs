using UnityEngine;
using UnityEngine.InputSystem;

/// Poster screenshots: F12 saves a PNG, F11 hides or shows the HUD.
/// Editor only: the keys do nothing in a build.
public class PosterCapture : MonoBehaviour
{
    [SerializeField] private int superSize = 2;        // 2 = twice the Game view size (1080p becomes 4K)
    [SerializeField] private Canvas[] hideWithF11;      // the HUD canvases to hide for clean shots

    private void Update()
    {
#if UNITY_EDITOR
        Keyboard k = Keyboard.current;
        if (k == null) return;

        if (k.f11Key.wasPressedThisFrame)
        {
            foreach (Canvas c in hideWithF11)
                if (c != null) c.enabled = !c.enabled;   // turn each canvas off, or back on
        }

        if (k.f12Key.wasPressedThisFrame)
        {
            string file = $"Poster_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            ScreenCapture.CaptureScreenshot(file, superSize);   // renders the frame bigger, then saves it
            Debug.Log($"Poster shot saved: {file}");
        }
#endif
    }
}