using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The six things a new player learns on day 1, in the order we teach them.
// IMPORTANT: this ORDER matters. CurrentStep picks the first step in this list that is not done yet.
public enum TutorialStep
{
    Feed,
    Clean,
    Computer,
    OpenShop,
    Serve,
    CloseShop
}

/// <summary>
/// Day 1 guide. Shows ONE objective line in the corner of the screen, e.g. "Feed your cats [E]  (1/6)".
/// The same CurrentStep also drives the glow on the target object (see TutorialHighlight),
/// so the text and the glow always agree.
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

    // The step we last drew. If CurrentStep changes, we redraw.
    private TutorialStep? shownStep;
    private bool drewOnce;

    // ==========================================
    // WHAT THE PLAYER SHOULD DO RIGHT NOW
    // ==========================================

    // The step the player should do right now, or null if there is nothing to do yet
    // (for example, the shop is open but no customer has arrived) or the tutorial is over.
    // "TutorialStep?" means "a TutorialStep OR null".
    public static TutorialStep? CurrentStep
    {
        get
        {
            if (instance == null || instance.finished) return null;
            return instance.FindCurrentStep();
        }
    }

    private TutorialStep? FindCurrentStep()
    {
        // Walk the steps in enum order and return the first one that is not done AND can be done now.
        // Checking "can be done now" for every step (not just the first not-done one) means a skipped
        // step can never block the others. Example: at last call, if Serve was skipped, Close still shows.
        foreach (TutorialStep step in Enum.GetValues(typeof(TutorialStep)))
        {
            if (done.Contains(step)) continue;
            if (IsAvailable(step)) return step;
        }
        return null;
    }

    // Can the player do this step at this moment?
    private bool IsAvailable(TutorialStep step)
    {
        DayManager day = DayManager.Instance;

        switch (step)
        {
            case TutorialStep.OpenShop:
                return day != null && day.Phase == DayPhase.Prep;

            case TutorialStep.Serve:
                // Only while a customer is standing at the desk
                return CustomerQueueManager.Instance != null
                       && CustomerQueueManager.Instance.GetCurrentCustomerAtDesk() != null;

            case TutorialStep.CloseShop:
                // Only once the timer has run out
                return day != null && day.Phase == DayPhase.LastCall;

            default:
                // Feed, Clean and Computer can be done any time
                return true;
        }
    }

    // ==========================================
    // UNITY CALLBACKS
    // ==========================================

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
        // Start, not Awake: InputPrompts is guaranteed to be set up by now
        Redraw();
    }

    private void Update()
    {
        if (finished) return;

        // "A customer arrived" and "last call started" don't call us, so we check each frame.
        // Only redraw when the answer actually changed.
        TutorialStep? now = FindCurrentStep();
        if (!drewOnce || now != shownStep) Redraw();
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

        // Closing the shop ends day 1, so the guide goes away even if some steps were skipped
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

        TutorialStep? current = FindCurrentStep();
        shownStep = current;
        drewOnce = true;

        string text = current.HasValue ? StepLine(current.Value) : WaitingLine();

        // No text means nothing useful to say, so hide the panel instead of showing an empty box
        if (notePanel != null) notePanel.SetActive(text != "");
        noteText.text = text;
    }

    private string StepLine(TutorialStep step)
    {
        string feed  = InputPrompts.Get(PromptAction.Feed);
        string clean = InputPrompts.Get(PromptAction.Clean);
        string use   = InputPrompts.Get(PromptAction.CompleteDeal);

        string label;
        switch (step)
        {
            case TutorialStep.Feed:     label = $"Feed a cat [{feed}]"; break;
            case TutorialStep.Clean:    label = $"Clean a cat [{clean}]"; break;
            case TutorialStep.Computer: label = "Look at the shop computer"; break;
            case TutorialStep.OpenShop: label = $"Open the shop at the sign [{use}]"; break;
            case TutorialStep.Serve:    label = $"Serve the customer at the desk [{use}]"; break;
            default:                    label = $"Close the shop at the sign [{use}]"; break;
        }

        // Counter: how many are done, plus the one we are on
        return $"<b>{label}</b>  <size=80%>({done.Count + 1}/{totalSteps})</size>";
    }

    // Shown when no step can be done right now
    private string WaitingLine()
    {
        DayManager day = DayManager.Instance;
        if (day == null || day.Phase != DayPhase.Open) return "";

        return done.Contains(TutorialStep.Serve)
            ? "Keep the shop running until closing time..."
            : "Waiting for your first customer...";
    }
}