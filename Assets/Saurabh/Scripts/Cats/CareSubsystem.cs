using UnityEngine;
using UnityEngine.InputSystem;

public class CareSubsystem : MonoBehaviour
{
    private readonly string systemName = "CareSubsystem";
    
    [Header("Economy Settings")]
    // public int careCost = 10; // OLD: one flat cost. Costs now rise per level, see CareRules
    [SerializeField] private float maxInteractionDistance = 5f; // Max distance to interact with a cat

    private InputSystem_Actions inputActions;
    private InputAction LeftShoulder;
    private InputAction RightShoulder;
    private InputAction petAction;
    private InputAction optionsAction;

    [Header("Petting")]
    [SerializeField] private float petCooldown = 0.6f;   // seconds between pets, so mashing R doesn't spam hearts
    private float nextPetTime;
    // private InputAction interact1Action;

    void OnEnable()
    {
        inputActions = new InputSystem_Actions();
        RightShoulder = inputActions.Player.FeedCat;
        LeftShoulder = inputActions.Player.CleanCat;
        petAction = inputActions.Player.PetCat;
        optionsAction = inputActions.Player.CatOptions;
        // interact1Action = inputActions.Player.Interact1;
        inputActions.Enable();
    }

    // Every "new InputSystem_Actions()" must be turned off and thrown away when we're done,
    // or Unity warns "InputSystem_Actions...Disable() has not been called" (a small leak) when Play stops.
    void OnDisable()
    {
        if (inputActions == null) return;
        inputActions.Disable();
        inputActions.Dispose();
        inputActions = null;
    }

 private void Update()
{
    // 1. DRAW THE RAYCAST EVERY FRAME (Continuous Visualization)
    if (Camera.main != null)
    {
        // CRITICAL: This must be Camera.main.transform, not just "transform"
        Vector3 rayOrigin = Camera.main.transform.position;
        Vector3 rayDirection = Camera.main.transform.forward;
        
        // Draws a permanent blue laser in the Scene view exactly where the camera is looking
        Debug.DrawRay(rayOrigin, rayDirection * maxInteractionDistance, Color.blue);
    }

    // 2. CHECK FOR INPUTS
    if (LeftShoulder.WasPressedThisFrame())
    {
        TryCareForCat(isFeeding: false); 
    }
    else if (RightShoulder.WasPressedThisFrame()) 
    {
        TryCareForCat(isFeeding: true); 
    }
    else if (petAction.WasPressedThisFrame())
    {
        TryPetCat();
    }
    else if (optionsAction.WasPressedThisFrame())
    {
        TryOpenCatOptions();
    }
}

// F on a rare cat (on hold or a resident): open the keep / adopt panel
private void TryOpenCatOptions()
{
    if (Camera.main == null) return;

    Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
    if (!Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance)) return;

    BaseCat cat = hit.collider.GetComponent<BaseCat>();
    if (cat == null || cat.IsHeldByCustomer || !cat.IsRare) return;
    if (!cat.OnHold && !cat.IsResident) return;   // already up for adoption: nothing to decide

    RareCatPanel.Open(cat);
}

// Petting is free and changes no numbers: it's there so cats feel like characters, not stock
private void TryPetCat()
{
    if (Camera.main == null || Time.time < nextPetTime) return;

    Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
    if (!Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance)) return;

    BaseCat cat = hit.collider.GetComponent<BaseCat>();
    if (cat == null || cat.IsHeldByCustomer) return;
    if (!cat.TryGetComponent(out CatReaction reaction)) return;

    nextPetTime = Time.time + petCooldown;

    string said = reaction.PlayPet();          // the cat reacts in its own way and tells us what it "said"
    cat.PlayInteractAnimation();               // its own voice clip
    AudioManager.Play(Sfx.PurrClean, cat.VoicePitch, cat.VoiceVolume);
    // Only a refusal gets words: "Hiss!" is news (it didn't work). A happy pet is shown by the cat itself.
    if (said == "Hiss!") FloatingNumbers.Show(cat.CenterPosition, FloatingNumbers.Spend(said));
}

private void TryCareForCat(bool isFeeding)
{
    if (Camera.main == null) return;

    Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);

    if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance))
    {
        BaseCat clickedCat = hit.collider.GetComponent<BaseCat>();

        if (clickedCat == null) return;

        if (clickedCat.IsResident)
        {
            MessageUI.Notify($"{clickedCat.CatName} is family: no care needed, just pets.");
            return;
        }

        if (clickedCat.IsHeldByCustomer)
        {
            MessageUI.Notify("This cat is with a customer.");
            return;
        }

        // NEW: stop before spending any money if this stat is already maxed
        bool canUpgrade = isFeeding ? clickedCat.CanUpgradeFeeding : clickedCat.CanUpgradeCleanliness;
        if (!canUpgrade)
        {
            Debug.Log($"{clickedCat.name} is already at max level for this stat. No GP spent.");
            MessageUI.Notify("This stat is already at max level!");
            if (clickedCat.TryGetComponent(out CatReaction fullReaction)) fullReaction.PlayDenied();
            return;
        }

        int currentMoney = CoreEconomySystem.Instance.GetGP(systemName);
        int careCost = CareRules.NextCost(clickedCat, isFeeding);   // the price of THIS level (5, 8, 11 ...)

        if (currentMoney < careCost)
        {
            Debug.LogWarning("Not enough GP!");
            MessageUI.Notify($"Not enough GP! This care costs {careCost} GP.", ToastKind.Bad);
            if (clickedCat.TryGetComponent(out CatReaction poorReaction)) poorReaction.PlayDenied();
            return;
        }

        CoreEconomySystem.Instance.ModifyGP(-careCost, systemName);

        CareTier tierBefore = CareRules.TierOf(clickedCat.TotalCare);   // remember the tier BEFORE the upgrade

        if (isFeeding) clickedCat.UpgradeFeeding();
        else clickedCat.UpgradeCleanliness();

        ShowCarePopup(clickedCat, careCost, tierBefore);

        clickedCat.PlayInteractAnimation();
        if (clickedCat.TryGetComponent(out CatReaction reaction)) reaction.Play(isFeeding);
        AudioManager.Play(isFeeding ? Sfx.MeowFeed : Sfx.PurrClean, clickedCat.VoicePitch, clickedCat.VoiceVolume);
        TutorialNote.Complete(isFeeding ? TutorialStep.Feed : TutorialStep.Clean);
    }
    else
    {
        Debug.Log("Raycast hit nothing.");
    }
}


// The pop-up next to the cat: "-8 GP  +15 worth", plus the new tier if the cat just reached one
private void ShowCarePopup(BaseCat cat, int cost, CareTier tierBefore)
{

    // Built in two parts: Unity's C# version can't put quotes inside a {...} of another $"..." string
    string spent = FloatingNumbers.Spend("-" + cost + " GP");
    string gained = FloatingNumbers.Gain("+" + CareRules.ValuePerPoint + " worth");
    string text = spent + "  " + gained;

    CareTier tierNow = CareRules.TierOf(cat.TotalCare);
    // Celebrate only the tiers worth aiming for (Well-cared and Purrfect)
    if (tierNow != tierBefore && tierNow >= CareTier.WellCared)
        text += "\n<b>" + CareRules.ColoredName(tierNow) + "!</b>";

    FloatingNumbers.Show(cat.CenterPosition, text);

}

}