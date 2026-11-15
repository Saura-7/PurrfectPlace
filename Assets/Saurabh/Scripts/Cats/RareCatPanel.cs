using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The panel that opens when you press F on a rare cat.
///
///   Rare cat ON HOLD → name it (or roll a random name with the dice) and Keep as resident,
///                      or Put up for adoption, or Decide later
///   A RESIDENT       → Put up for adoption (back into a free cage), or Close
///
/// While it's open the game keeps running, but walking, looking and care keys are switched off
/// and the mouse is free, exactly like the developer console.
/// Put this on the HUD canvas; the panel itself starts hidden.
/// </summary>
public class RareCatPanel : MonoBehaviour
{
    private static RareCatPanel instance;

    [Header("Panel")]
    [SerializeField] private GameObject panel;              // the card that shows and hides
    [SerializeField] private TextMeshProUGUI titleText;     // "A rare cat!" or the resident's name
    [SerializeField] private TextMeshProUGUI infoText;      // what the choices mean

    [Header("Naming")]
    [SerializeField] private GameObject nameRow;            // the input + dice, hidden for residents
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button diceButton;
    [SerializeField] private int maxNameLength = 14;

    [Header("Buttons")]
    [SerializeField] private Button keepButton;             // Keep as resident
    [SerializeField] private TextMeshProUGUI keepLabel;
    [SerializeField] private Button adoptButton;            // Put up for adoption
    [SerializeField] private TextMeshProUGUI adoptLabel;
    [SerializeField] private Button closeButton;            // Decide later / Close
    [SerializeField] private TextMeshProUGUI closeLabel;

    // Cute names for the dice. Short, so they fit on the tag.
    private static readonly string[] Names =
    {
        "Biscuit", "Mochi", "Pumpkin", "Noodle", "Pepper", "Clover", "Waffles", "Miso",
        "Toffee", "Pickles", "Juniper", "Nugget", "Sprout", "Marble", "Cocoa", "Tofu",
        "Bean", "Sushi", "Maple", "Pudding", "Ginger", "Olive", "Muffin", "Peanut",
        "Hazel", "Dumpling", "Cinnamon", "Pixel", "Socks", "Butter"
    };

    public static bool IsOpen { get; private set; }

    // Same trick as the console: Esc closes the panel, and the pause menu must not see that same Esc
    private static int closedOnFrame = -10;
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedOnFrame + 1;

    private BaseCat cat;
    private readonly List<Behaviour> blocked = new List<Behaviour>();

    private void Awake()
    {
        instance = this;
        if (panel != null) panel.SetActive(false);
        if (nameInput != null) nameInput.characterLimit = maxNameLength;

        // Wire the buttons in code, so nothing needs dragging into OnClick lists
        diceButton.onClick.AddListener(RollName);
        keepButton.onClick.AddListener(Keep);
        adoptButton.onClick.AddListener(Adopt);
        closeButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (!IsOpen) return;

        // Esc, or the cat was taken away somehow: close
        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || cat == null)
            Close();
    }

    // ==========================================
    // OPEN
    // ==========================================

    public static void Open(BaseCat cat)
    {
        if (instance == null || cat == null || IsOpen || DevConsole.IsOpen || PauseMenu.IsPaused) return;
        instance.Show(cat);
    }

    private void Show(BaseCat target)
    {
        cat = target;
        bool resident = cat.IsResident;
        string traits = $"{cat.Pattern} · {cat.Body} · {cat.Behavior}";

        if (resident)
        {
            titleText.text = cat.CatName;
            infoText.text = $"{traits}\nLives here as part of the family.\n" +
                            $"Put up for adoption: it moves back into a free cage and can be adopted for {cat.CalculateFinalPrice()} GP.";
            nameRow.SetActive(false);
            keepButton.gameObject.SetActive(false);
            closeLabel.text = "Close";
        }
        else
        {
            titleText.text = "A rare cat!";
            infoText.text = $"{traits}\nKeep it as a resident: it leaves its cage, needs no care, and gets a name.\n" +
                            $"Or put it up for adoption: worth {cat.CalculateFinalPrice()} GP now.";
            nameRow.SetActive(true);
            keepButton.gameObject.SetActive(true);
            nameInput.text = RandomName();
            closeLabel.text = "Decide later";
        }

        // Explain WHY a button can't be used, right on the button
        bool spot = ResidentCats.Instance != null && ResidentCats.Instance.HasFreeSpot;
        keepButton.interactable = spot;
        keepLabel.text = spot ? "Keep as resident" : "No free resident spot";

        bool cageFree = !resident || CageManager.Instance.GetAvailableCage() != null;
        adoptButton.interactable = cageFree;
        adoptLabel.text = cageFree ? "Put up for adoption" : "No free cage";

        panel.SetActive(true);
        Juice.PopIn(panel.transform, 0.25f);
        SetOpen(true);
    }

    // ==========================================
    // THE BUTTONS
    // ==========================================

    private void RollName()
    {
        nameInput.text = RandomName();
        Juice.Punch(diceButton.transform, 0.2f);
    }

    private void Keep()
    {
        if (cat == null || ResidentCats.Instance == null) return;

        // Trim spaces; an empty box gets a random name, so no cat is ever called ""
        string chosen = nameInput.text.Trim();
        if (chosen.Length == 0) chosen = RandomName();

        if (ResidentCats.Instance.MakeResident(cat, chosen)) Close();
    }

    private void Adopt()
    {
        if (cat == null || ResidentCats.Instance == null) return;
        if (ResidentCats.Instance.PutUpForAdoption(cat)) Close();
    }

    public void Close()
    {
        if (!IsOpen) return;
        panel.SetActive(false);
        cat = null;
        SetOpen(false);
    }

    private static string RandomName() => Names[Random.Range(0, Names.Length)];

    // ==========================================
    // BLOCKING THE GAME WHILE OPEN (same as the console)
    // ==========================================

    private void SetOpen(bool open)
    {
        IsOpen = open;
        if (!open) closedOnFrame = Time.frameCount;

        if (open)
        {
            blocked.Clear();
            TryBlock(FindFirstObjectByType<PlayerMovement>());
            TryBlock(FindFirstObjectByType<CareSubsystem>());
            TryBlock(FindFirstObjectByType<ReceptionDesk>());
            TryBlock(FindFirstObjectByType<InteractionPromptUI>());
            TryBlock(FindFirstObjectByType<CinemachineInputAxisController>());   // mouse look

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            foreach (Behaviour b in blocked)
            {
                if (b != null) b.enabled = true;
            }
            blocked.Clear();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void TryBlock(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;   // only switch back on what WE switched off
        behaviour.enabled = false;
        blocked.Add(behaviour);
    }
}
