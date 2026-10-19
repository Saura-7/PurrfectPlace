using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

/// <summary>
/// Pause menu: Resume, Restart, Quit.
/// Put this on its own always-active object (NOT on the pause panel itself),
/// or it stops running when the panel is hidden.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [SerializeField] private GameObject pausePanel;

    private InputSystem_Actions inputActions;

    // The gameplay scripts we switch off while the game is paused
    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputActions.Player.Pause.Enable();
    }

    private void OnDisable()
    {
        inputActions.Player.Pause.Disable();
    }

    private void Start()
    {
        // Always begin in a clean, unpaused state
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        // Safety: never leave the game frozen if this object is destroyed while paused
        Time.timeScale = 1f;
        IsPaused = false;
    }

    private void Update()
    {
        if (inputActions.Player.Pause.WasPressedThisFrame())
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    // ==========================================
    // BUTTON METHODS (hooked up in the Inspector)
    // ==========================================

    public void Pause()
    {
        if (IsPaused) return;

        IsPaused = true;
        Time.timeScale = 0f;
        pausePanel.SetActive(true);

        CollectBehaviours();
        SetBehavioursEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;
        pausePanel.SetActive(false);

        SetBehavioursEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Restart()
    {
        // IMPORTANT: unfreeze time first, or the reloaded scene starts frozen
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        Debug.Log("Game quit");
#else
        Application.Quit();
#endif
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private void CollectBehaviours()
    {
        pausedBehaviours.Clear();

        AddIfFound(FindFirstObjectByType<PlayerMovement>());
        AddIfFound(FindFirstObjectByType<CareSubsystem>());
        AddIfFound(FindFirstObjectByType<ReceptionDesk>());
        AddIfFound(FindFirstObjectByType<InteractionPromptUI>());
        AddIfFound(FindFirstObjectByType<CinemachineInputAxisController>()); // mouse and stick look
    }

    private void AddIfFound(Behaviour behaviour)
    {
        if (behaviour != null) pausedBehaviours.Add(behaviour);
    }

    private void SetBehavioursEnabled(bool value)
    {
        foreach (Behaviour behaviour in pausedBehaviours)
        {
            if (behaviour != null) behaviour.enabled = value;
        }
    }
}