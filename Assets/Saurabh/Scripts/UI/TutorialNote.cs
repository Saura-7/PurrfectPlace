using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The six things a new player learns on day 1
public enum TutorialStep
{
    Feed,
    Clean,
    OpenShop,
    Serve,
    Computer,
    CloseShop
}

/// <summary>
/// Day 1 to-do note in the corner of the screen.
/// Steps tick off in any order. The note hides when everything is done
/// or when the player closes the shop for the first time.
/// Other scripts call TutorialNote.Complete(step). It is safe to call even when there is no note.
/// </summary>
public class TutorialNote : MonoBehaviour
{
    private static TutorialNote instance;

    [Header("UI")]
    [SerializeField] private GameObject notePanel;       // the background panel (a child of this object)
    [SerializeField] private TextMeshProUGUI noteText;

    // Which steps are done. A HashSet can only hold each step once.
    private readonly HashSet<TutorialStep> done = new HashSet<TutorialStep>();

    // How many steps exist (counts the enum, so adding a step later updates this)
    private readonly int totalSteps = Enum.GetValues(typeof(TutorialStep)).Length;

    private bool finished;

    private void Awake()
    {
        instance = this;
        if (notePanel != null) notePanel.SetActive(true);
    }

    private void OnEnable()
    {
        // Redraw when the key names change (device switch now, rebinding in the MVP)
        InputPrompts.OnPromptsChanged += Redraw;
    }

    private void OnDisable()
    {
        // A static event outlives the scene, so we must stop listening, or it calls a destroyed object
        InputPrompts.OnPromptsChanged -= Redraw;
    }

    private void Start()
    {
        // Start, not Awake: InputPrompts and RentSystem are guaranteed to be set up by now
        Redraw();
    }

    // ==========================================
    // CALLED BY OTHER SCRIPTS
    // ==========================================

    public static void Complete(TutorialStep step)
    {
        if (instance == null || instance.finished) return;
        instance.MarkDone(step);
    }

    // ==========================================
    // INSIDE THE NOTE
    // ==========================================

    private void MarkDone(TutorialStep step)
    {
        // Add returns false if the step was already in the set, so repeats do nothing
        if (!done.Add(step)) return;

        if (done.Count == totalSteps)
        {
            Finish(true);
            return;
        }

        // Closing the shop ends day 1, so the note goes away even if some steps were skipped
        if (step == TutorialStep.CloseShop)
        {
            Finish(false);
            return;
        }

        Redraw();
    }

    private void Finish(bool allDone)
    {
        finished = true;
        if (notePanel != null) notePanel.SetActive(false);
        if (allDone) MessageUI.Notify("You're ready! Run your shop your way.");
    }

    private void Redraw()
    {
        if (noteText == null || finished) return;

        string feed  = InputPrompts.Get(PromptAction.Feed);
        string clean = InputPrompts.Get(PromptAction.Clean);
        string use   = InputPrompts.Get(PromptAction.CompleteDeal);

        string text = "<b>Today's to-do</b>\n";
        text += Line(TutorialStep.Feed,      $"Feed a cat [{feed}]");
        text += Line(TutorialStep.Clean,     $"Clean a cat [{clean}]");
        text += Line(TutorialStep.OpenShop,  $"Open the shop at the sign [{use}]");
        text += Line(TutorialStep.Serve,     $"Serve a customer at the desk [{use}]");
        text += Line(TutorialStep.Computer,  "Look at the shop computer");
        text += Line(TutorialStep.CloseShop, $"Close the shop at the sign [{use}]");

        // Rent tip: day 2 is the first rent day, and RentDue tells us how much
        if (RentSystem.Instance != null)
        {
            text += $"\n<size=85%>Tip: rent is {RentSystem.Instance.RentDue(2)} GP a day from tomorrow.</size>";
        }

        noteText.text = text;
    }

    // One line of the note: grey and crossed out when done
    private string Line(TutorialStep step, string label)
    {
        if (done.Contains(step))
            return $"<color=#9A9A9A>[x] <s>{label}</s></color>\n";

        return $"[ ] {label}\n";
    }
}