using UnityEngine;
using TMPro;

public class InteractionPromptUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private CatInfoTag infoTag;   // the floating tag above cats (optional)

    [Header("Distances")]
    [SerializeField] private float catDistance = 5f;
    [SerializeField] private float deskDistance = 10f;

    private string lastText = "";
    private BaseCat lookedAtCat;   // the cat under the crosshair this frame (null if none)

    private void Update()
    {
        string newText = GetPromptText();

        // Only touch the UI when the text actually changes
        if (newText != lastText)
        {
            lastText = newText;
            promptText.text = newText;
        }

        // Show the floating tag on the cat we look at, hide it otherwise
        if (infoTag != null)
        {
            if (lookedAtCat != null) infoTag.Show(lookedAtCat);
            else infoTag.Hide();
        }
    }

    // Unity calls OnDisable automatically when this script is switched off (for example when the game freezes)
    private void OnDisable()
    {
        lastText = "";
        lookedAtCat = null;

        // Wipe the text so an old prompt doesn't stay on screen over the pause menu or summary
        if (promptText != null) promptText.text = "";
        if (infoTag != null) infoTag.Hide();
    }

    private string GetPromptText()
    {
        lookedAtCat = null;   // start each frame with "not looking at a cat"

        Camera cam = Camera.main;
        if (cam == null) return "";

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        float maxDistance = Mathf.Max(catDistance, deskDistance);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance)) return "";

        // 1. Looking at a cat
        BaseCat cat = hit.collider.GetComponent<BaseCat>();
        if (cat != null && hit.distance <= catDistance && !cat.IsHeldByCustomer)
        {
            lookedAtCat = cat;   // the floating tag shows this cat's traits and worth

            string feed = InputPrompts.Get(PromptAction.Feed);
            string clean = InputPrompts.Get(PromptAction.Clean);

            // The screen prompt now only shows what you can DO, on one line
            return $"Feed [{feed}] {cat.feedingLevel}/{BaseCat.MaxLevel}     " +
                   $"Clean [{clean}] {cat.cleanlinessLevel}/{BaseCat.MaxLevel}";
        }

        OpenSign sign = hit.collider.GetComponentInParent<OpenSign>();
        if (sign != null && hit.distance <= deskDistance)
        {
            // The sign asks DayManager what to say, because the answer depends on the phase
            return sign.GetPrompt(InputPrompts.Get(PromptAction.CompleteDeal));
        }

        // Looking at the decor monitor
        DecorMonitor decorMonitor = hit.collider.GetComponentInParent<DecorMonitor>();
        if (decorMonitor != null && hit.distance <= deskDistance)
        {
            TutorialNote.Complete(TutorialStep.Computer);
            return decorMonitor.GetPrompt(hit.collider, InputPrompts.Get(PromptAction.CompleteDeal));
        }

        // 2. Looking at an Accept / Reject button
        if (hit.distance <= deskDistance && hit.collider.GetComponent<WorldSpaceUIButton>() != null)
        {
            return $"Choose [{InputPrompts.Get(PromptAction.CompleteDeal)}]";
        }

        // 3. Looking at the reception desk
        if (hit.distance <= deskDistance && hit.collider.GetComponentInParent<ReceptionDesk>() != null)
        {
            bool customerWaiting = CustomerQueueManager.Instance != null
                                   && CustomerQueueManager.Instance.GetCurrentCustomerAtDesk() != null;

            return customerWaiting
                ? $"Serve customer [{InputPrompts.Get(PromptAction.CompleteDeal)}]"
                : "No customer waiting";
        }

        return "";
    }
}