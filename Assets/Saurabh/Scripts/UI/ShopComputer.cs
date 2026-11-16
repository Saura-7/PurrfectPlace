using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sit at the shop PC: press the interact key on the monitor and the camera glides in until the
/// screen fills the view. The mouse cursor appears, the player stops moving, and you click the
/// screen's buttons with the mouse. Esc (or the Log off button) glides back out.
///
/// How the zoom works: a second Cinemachine camera ("PC camera") sits in front of the screen,
/// switched OFF. Switching it ON makes Cinemachine blend from the player's view to it; switching
/// it off blends back. The blend length is set on the Main Camera's CinemachineBrain.
///
/// How clicking works: the screen's buttons are WorldSpaceUIButtons (each has a BoxCollider).
/// While sitting here, a left click fires a ray from the MOUSE position and presses whatever
/// button it hits. That's the same button the old "aim and press E" used, just aimed with the mouse.
///
/// Setup: put this on the monitor's root. "Use zone" is a BoxCollider covering the whole screen,
/// slightly in front of it, so looking anywhere at the screen offers "Use computer".
/// </summary>
public class ShopComputer : MonoBehaviour
{
    [Header("Zoom")]
    [SerializeField] private CinemachineCamera pcCamera;   // the camera in front of the screen (starts switched off)

    [Header("Screen")]
    [SerializeField] private Collider useZone;              // covers the screen: "Use computer [E]"
    [SerializeField] private WorldSpaceUIButton logOffButton; // optional on-screen button that leaves the PC
    [SerializeField] private float clickDistance = 5f;      // how far the mouse ray reaches

    [Header("Hidden while sitting here")]
    [SerializeField] private GameObject crosshair;          // the centre dot means nothing when you use the mouse

    public static bool IsOpen { get; private set; }

    // Same trick as the console: the Esc that leaves the PC must not also open the pause menu
    private static int closedOnFrame = -10;
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedOnFrame + 1;

    private readonly List<Behaviour> blocked = new List<Behaviour>();
    private int openedOnFrame;   // the key press that sat you down must not also click a button

    private void Awake()
    {
        if (pcCamera != null) pcCamera.gameObject.SetActive(false);
        if (logOffButton != null) logOffButton.onClick.AddListener(Leave);
    }

    // ReceptionDesk calls this when the player presses interact while looking at the monitor
    public void SitDown()
    {
        if (IsOpen || PauseMenu.IsPaused || DevConsole.IsOpen || RareCatPanel.IsOpen) return;
        if (DayManager.Instance != null && DayManager.Instance.Phase == DayPhase.Summary) return;

        IsOpen = true;
        openedOnFrame = Time.frameCount;

        if (pcCamera != null) pcCamera.gameObject.SetActive(true);   // Cinemachine blends to it
        if (useZone != null) useZone.enabled = false;                // so mouse clicks reach the buttons behind it
        if (crosshair != null) crosshair.SetActive(false);

        // Stop walking, looking around and the other "aim and press" systems while seated
        blocked.Clear();
        TryBlock(FindFirstObjectByType<PlayerMovement>());
        TryBlock(FindFirstObjectByType<CareSubsystem>());
        TryBlock(FindFirstObjectByType<ReceptionDesk>());
        TryBlock(FindFirstObjectByType<InteractionPromptUI>());
        TryBlock(FindFirstObjectByType<CinemachineInputAxisController>());   // mouse look

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        TutorialNote.Complete(TutorialStep.Computer);
    }

    // Esc or the Log off button
    public void Leave() => Stand(true);

    // backToGame = false when the day summary takes over (it keeps the cursor free and the crosshair hidden)
    private void Stand(bool backToGame)
    {
        if (!IsOpen) return;
        IsOpen = false;
        closedOnFrame = Time.frameCount;

        if (pcCamera != null) pcCamera.gameObject.SetActive(false);  // Cinemachine blends back to the player
        if (useZone != null) useZone.enabled = true;
        if (crosshair != null && backToGame) crosshair.SetActive(true);

        foreach (Behaviour b in blocked)
        {
            if (b != null) b.enabled = true;
        }
        blocked.Clear();

        if (backToGame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (!IsOpen) return;

        // The day summary can start while you're seated (closing time): stand up,
        // but leave the cursor free for the summary's buttons
        if (DayManager.Instance != null && DayManager.Instance.Phase == DayPhase.Summary)
        {
            Stand(false);
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Leave();
            return;
        }

        if (Time.frameCount > openedOnFrame && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            ClickAtMouse();
    }

    // Fire a ray from the mouse position into the world; press the screen button it hits
    private void ClickAtMouse()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, clickDistance)) return;

        WorldSpaceUIButton button = hit.collider.GetComponent<WorldSpaceUIButton>();
        if (button != null && button.transform.IsChildOf(transform))   // only buttons on THIS screen
            button.PressButton();
    }

    private void TryBlock(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;   // only switch back on what WE switched off
        behaviour.enabled = false;
        blocked.Add(behaviour);
    }
}