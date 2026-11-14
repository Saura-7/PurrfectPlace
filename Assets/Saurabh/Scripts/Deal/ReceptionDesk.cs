using UnityEngine;
using UnityEngine.InputSystem;

public class ReceptionDesk : MonoBehaviour
{
    private readonly string systemName = "ReceptionDesk";

    [Header("Interaction Settings")]
    public float maxInteractionDistance = 10f;

    [Header("Cat on the desk")]
    [SerializeField] private Transform deskCatSpot;   // where the customer puts the cat during a deal

    [Header("UI References")]
    public GameObject dealUIPanel; 
    [SerializeField] private DealScreen dealScreen;   // draws the deal on the monitor (header, big number, buttons)

    private InputSystem_Actions inputActions;
    private InputAction CompleteDealAction;

    private CustomerAI currentInteractingCustomer;
    private int currentDealPrice;

    [Header("Reputation from deals")]
    // Care tiers and their adoption reputation moved to CareRules (one place for every care number)
    [SerializeField] private int giverRescueRep = 3;         // reputation for taking in a Giver's cat
    // [SerializeField] private int missedDealRep = 1;          // reputation LOST when the shop cannot complete a deal
    [SerializeField] private int noDealRep = 3;              // reputation LOST for every "no", chosen or forced

    private void OnEnable()
    {
        inputActions = new InputSystem_Actions();
        CompleteDealAction = inputActions.Player.CompleteDeal;
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        if (inputActions != null) inputActions.Player.Disable();
    }

    private void Update()
    {
        // The Interact action is bound to E and the left mouse button (and the gamepad trigger),
        // so one check covers every way the player can interact.
        if (CompleteDealAction.WasPressedThisFrame())
        {
            OnInteractPressed();
        }
    }

    private void OnInteractPressed()
    {
        if (Camera.main == null) return;

        // While a menu or the day summary is open the cursor is free.
        // Ignore input then, so clicking a menu button can't also press something in the world behind it.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance))
        {
            // 1. A world-space button (Accept / Reject, Buy, the sign)
            WorldSpaceUIButton uiButton = hit.collider.GetComponent<WorldSpaceUIButton>();
            if (uiButton != null)
            {
                uiButton.PressButton();
                return;
            }

            // 2. A customer: talking to the one at the front of the line starts the deal.
            //    GetComponentInParent also finds the customer when you click the cat they're holding.
            CustomerAI customer = hit.collider.GetComponentInParent<CustomerAI>();
            if (customer != null)
            {
                if (customer == CustomerQueueManager.Instance.GetCurrentCustomerAtDesk()) ProcessTransaction();
                return;   // someone further back in the line: nothing happens yet
            }

            // 3. The desk itself still works too: serve the customer
            if (hit.collider.gameObject == gameObject || hit.collider.GetComponentInParent<ReceptionDesk>() != null)
            {
                ProcessTransaction();
            }
        }
    }

    private void ProcessTransaction()
    {
        currentInteractingCustomer = CustomerQueueManager.Instance.GetCurrentCustomerAtDesk();
        if (currentInteractingCustomer != null && currentInteractingCustomer.IsSayingGoodbye) return;   // already adopted, just leaving
        bool panelWasOpen = dealUIPanel.activeSelf;   // clicking again shouldn't replay the animation

        if (currentInteractingCustomer != null && currentInteractingCustomer.heldCat != null)
        {
            BaseCat cat = currentInteractingCustomer.heldCat;

            // Turn the panel on FIRST, so its text objects are active when we fill them in
            dealUIPanel.SetActive(true);

            if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Taker)
            {
                currentDealPrice = cat.CalculateFinalPrice();
                int donation = ReputationLevels.Donation;
                int rep = CareRules.ReputationFor(cat.TotalCare);
                dealScreen.ShowAdoption(cat, currentDealPrice, donation, rep, noDealRep);
            }
            else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
            {
                // What the player has, so the screen can warn BEFORE they press Buy
                int gp = CoreEconomySystem.Instance.GetGP(systemName);
                int freeCages = CageManager.Instance.allCages.Count - CageManager.Instance.CatCount;

                if (currentInteractingCustomer.IsStray)
                {
                    // A stray finder asks for nothing
                    currentDealPrice = 0;
                    dealScreen.ShowStray(cat, gp, freeCages, noDealRep);
                }
                else
                {
                    // What the cat is really worth, and what this Giver asks for it
                    int catValue = cat.CalculateFinalPrice();
                    currentDealPrice = Mathf.Max(10, Mathf.RoundToInt(catValue * currentInteractingCustomer.AskFactor));
                    dealScreen.ShowPurchase(cat, currentDealPrice, catValue, gp, freeCages, noDealRep);
                }
            }

            // Both kinds of customer set the cat down on the desk while you decide
            currentInteractingCustomer.PutCatOnDesk(deskCatSpot);

            if (!panelWasOpen)
            {
                Juice.PopIn(dealUIPanel.transform, 0.25f);                    // the deal screen pops open
                Juice.Squash(currentInteractingCustomer.heldCat.transform);   // the cat lands softly on the desk
            }
        }
        else
        {
            Debug.LogWarning($"[SOA {systemName}] Interaction attempt failed: No valid customer standing at front of register line.");
        }
    }

    public void AcceptDeal()
    {
        if (currentInteractingCustomer == null) return;

        int repGain = 0;   // how much reputation this deal earns (0 = none)

        if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Taker)
        {
            // Read the cat's care BEFORE the Taker leaves with it
            BaseCat adoptedCat = currentInteractingCustomer.heldCat;
            int care = adoptedCat != null ? adoptedCat.feedingLevel + adoptedCat.cleanlinessLevel : 0;
            CatBehavior personality = adoptedCat != null ? adoptedCat.Behavior : CatBehavior.Playful;
            repGain = AdoptionReputation(care);

            // A trusted shelter gets a donation on top of the adoption fee
            int donation = ReputationLevels.Donation;

            CoreEconomySystem.Instance.ModifyGP(currentDealPrice + donation, systemName);
            CoreEconomySystem.Tally($"Adopted · {CareRules.Name(CareRules.TierOf(care))}");
            CoreEconomySystem.Tally("Adoption fees (GP)", currentDealPrice);
            if (donation > 0) CoreEconomySystem.Tally("Donations (GP)", donation);
            AudioManager.Play(Sfx.CoinsIn);
            DayManager.RecordCatSold();
            currentInteractingCustomer.CompleteTakerTransactionAndLeave();
            CageManager.Instance.FreeCage(currentInteractingCustomer.HomeCage); // the cat is adopted, so the cage is free now

            MessageUI.Notify(donation > 0
                ? $"Adopted! +{currentDealPrice} GP and a +{donation} GP donation"
                : $"Adopted! +{currentDealPrice} GP", ToastKind.Good);

            // One heart toast in the cat's own voice; the best-cared cats get a little praise in front
            CareTier tier = CareRules.TierOf(care);
            string praise = tier == CareTier.Purrfect ? "A purrfect adoption! "
                          : tier == CareTier.WellCared ? "A happy adoption! "
                          : "";
            MessageUI.Notify(praise + GoodbyeLine(personality), ToastKind.Heart);
        }
        else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
        {
            int playerMoney = CoreEconomySystem.Instance.GetGP(systemName);
            Transform emptyCage = CageManager.Instance.GetAvailableCage();

            // A free stray can always be taken in, even if GP is negative
            bool canAfford = currentDealPrice == 0 || playerMoney >= currentDealPrice;

            if (canAfford && emptyCage != null)
            {
                if (currentDealPrice > 0)
                {
                    CoreEconomySystem.Instance.ModifyGP(-currentDealPrice, systemName);
                    AudioManager.Play(Sfx.CoinsOut);
                }
                DayManager.RecordCatBought();
                CoreEconomySystem.Tally(currentDealPrice > 0 ? "Bought from a Giver" : "Stray taken in");
                if (currentDealPrice > 0) CoreEconomySystem.Tally("Paid to Givers (GP)", currentDealPrice);
                BaseCat boughtCat = currentInteractingCustomer.AcceptGiverOfferAndLeave();
                CageManager.Instance.OccupyCage(emptyCage, boughtCat);

                repGain = giverRescueRep;   // you gave a cat a safe place
                MessageUI.Notify(currentDealPrice > 0 ? $"Deal accepted: -{currentDealPrice} GP" : "Stray taken in!", ToastKind.Good);
            }
            else
            {
                MessageUI.Notify(emptyCage == null ? "No free cage!" : "Not enough GP!", ToastKind.Bad);
                HandleReject(true);
                return;
            }
        }

        if (repGain > 0)
        {
            CoreEconomySystem.Instance.ModifyReputation(repGain, systemName);
            MessageUI.Notify($"+{repGain} reputation", ToastKind.Heart);
        }

        TutorialNote.Complete(TutorialStep.Serve);

        dealUIPanel.SetActive(false);
        CustomerQueueManager.Instance.AdvanceQueue();
        currentInteractingCustomer = null;
    }

    // How each personality leaves the shop
    private static string GoodbyeLine(CatBehavior personality)
    {
        switch (personality)
        {
            case CatBehavior.Lazy:     return "The lazy one barely woke up to leave.";
            case CatBehavior.Skittish: return "The shy one peeked back once, then left happy.";
            case CatBehavior.Angry:    return "The grumpy one pretended not to care. It cared.";
            default:                   return "The playful one bounced off with its new family!";
        }
    }

    // How much reputation an adoption earns, from how well the cat was cared for
    private int AdoptionReputation(int care)
    {
        return CareRules.ReputationFor(care);
    }

    // The Reject button on the deal monitor calls this. The player chose to say no.
    public void RejectDeal()
    {
        HandleReject(false);
    }

    // The shared rejection logic. forcedByGame is true when the game rejects the deal for the player.
    private void HandleReject(bool forcedByGame)
    {
        if (currentInteractingCustomer == null) return;
        CoreEconomySystem.Tally(forcedByGame ? "Refused · couldn't" : "Refused · chose to");

        if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Taker)
        {
            BaseCat returnedCat = currentInteractingCustomer.RejectTakerTransactionAndLeave();

            if (returnedCat != null)
            {
                CageManager.Instance.ReturnCatToCage(currentInteractingCustomer.HomeCage, returnedCat);
            }
        }
        else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
        {
            currentInteractingCustomer.RejectGiverOfferAndLeave();
        }

        // A lost deal is a lost deal: every "no" costs reputation, chosen or forced
        CoreEconomySystem.Instance.ModifyReputation(-noDealRep, systemName);

        if (forcedByGame)
        {
            DayManager.RecordMissed();      // counted as "missed" in the day summary
            MessageUI.Notify($"Deal missed: -{noDealRep} reputation", ToastKind.Bad);
        }
        else
        {
            DayManager.RecordDeclined();    // counted as "declined" in the day summary
            MessageUI.Notify($"Deal declined: -{noDealRep} reputation", ToastKind.Bad);
        }

        AudioManager.Play(Sfx.PoliteNo);
        TutorialNote.Complete(TutorialStep.Serve);

        dealUIPanel.SetActive(false);
        CustomerQueueManager.Instance.AdvanceQueue();
        currentInteractingCustomer = null;
    }

    public void CancelCurrentDeal()
    {
        if (currentInteractingCustomer != null) currentInteractingCustomer.TakeCatBack();
        currentInteractingCustomer = null;
        if (dealUIPanel != null) dealUIPanel.SetActive(false);
    }
}