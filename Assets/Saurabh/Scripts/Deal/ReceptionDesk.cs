using UnityEngine;
using UnityEngine.InputSystem;

public class ReceptionDesk : MonoBehaviour
{
    private readonly string systemName = "ReceptionDesk";

    [Header("Interaction Settings")]
    public float maxInteractionDistance = 10f;

    [Header("UI References")]
    public GameObject dealUIPanel; 
    public TMPro.TextMeshProUGUI dealText; 

    private InputSystem_Actions inputActions;
    private InputAction CompleteDealAction;

    private CustomerAI currentInteractingCustomer;
    private int currentDealPrice;

    [Header("Reputation from care (care = feed + clean, 0 to 12)")]
    [SerializeField] private int wellCaredFrom = 8;          // care at or above this counts as well cared for
    [SerializeField] private int averageFrom = 4;            // care at or above this (but below well cared) is average
    [SerializeField] private int wellCaredAdoptionRep = 8;   // reputation for adopting out a well-cared cat
    [SerializeField] private int averageAdoptionRep = 3;     // reputation for an average cat
    [SerializeField] private int neglectedAdoptionRep = 0;   // reputation for a neglected cat (no penalty)
    [SerializeField] private int giverRescueRep = 3;         // reputation for taking in a Giver's cat
    [SerializeField] private int missedDealRep = 1;          // reputation LOST when the shop cannot complete a deal

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
        if (CompleteDealAction.WasPressedThisFrame())
        {
            OnInteractPressed();
        }
    }

    private void OnInteractPressed()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance))
        {
            WorldSpaceUIButton uiButton = hit.collider.GetComponent<WorldSpaceUIButton>();
            if (uiButton != null)
            {
                // Debug.Log($"[SOA {systemName}] Raycast hit World Space UI Button: {uiButton.gameObject.name}");
                uiButton.PressButton();
                return;
            }

            if (hit.collider.gameObject == gameObject || hit.collider.GetComponentInParent<ReceptionDesk>() != null)
            {
                // Debug.Log($"[SOA {systemName}] Raycast hit Reception Desk directly.");
                ProcessTransaction();
            }
        }
    }

    private void ProcessTransaction()
    {
        currentInteractingCustomer = CustomerQueueManager.Instance.GetCurrentCustomerAtDesk();

        if (currentInteractingCustomer != null && currentInteractingCustomer.heldCat != null)
        {
            BaseCat cat = currentInteractingCustomer.heldCat;

            // e.g. "Calico | Chonky | Lazy" in a slightly smaller size, then a new line
            string traits = $"<size=80%>{cat.Pattern} | {cat.Body} | {cat.Behavior}</size>\n";

            if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Taker)
            {
                currentDealPrice = cat.CalculateFinalPrice();
                int donation = ReputationLevels.Donation;
                dealText.text = $"ADOPTION?\n{traits}Feed: {cat.feedingLevel}/6\nClean: {cat.cleanlinessLevel}/6\nAdoption fee: +{currentDealPrice} GP";
                if (donation > 0) dealText.text += $"\nDonation ({ReputationLevels.Name}): +{donation} GP";
                dealUIPanel.SetActive(true);
            }
            else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
            {
                // What the cat is really worth, and what this Giver asks for it
                int catValue = cat.CalculateFinalPrice();

                if (currentInteractingCustomer.IsStray)
                {
                    // A stray finder asks for nothing
                    currentDealPrice = 0;
                    dealText.text = $"TAKE IN A STRAY?\n{traits}Feed: {cat.feedingLevel}/6\nClean: {cat.cleanlinessLevel}/6\nFound outside\nCost: Free";
                }
                else
                {
                    currentDealPrice = Mathf.Max(10, Mathf.RoundToInt(catValue * currentInteractingCustomer.AskFactor));
                    dealText.text = $"BUY CAT?\n{traits}Feed: {cat.feedingLevel}/6\nClean: {cat.cleanlinessLevel}/6\nWorth about: {catValue} GP\nCost: -{currentDealPrice} GP";
                }

                dealUIPanel.SetActive(true);
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
            repGain = AdoptionReputation(care);

            // A trusted shelter gets a donation on top of the adoption fee
            int donation = ReputationLevels.Donation;

            CoreEconomySystem.Instance.ModifyGP(currentDealPrice + donation, systemName);
            AudioManager.Play(Sfx.CoinsIn);
            DayManager.RecordCatSold();
            currentInteractingCustomer.CompleteTakerTransactionAndLeave();
            CageManager.Instance.FreeCage(currentInteractingCustomer.HomeCage); // the cat is adopted, so the cage is free now

            MessageUI.Notify(donation > 0
                ? $"Adopted! +{currentDealPrice} GP and a +{donation} GP donation"
                : $"Adopted! +{currentDealPrice} GP");

            if (care >= wellCaredFrom) MessageUI.Notify("A happy adoption!");
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
                BaseCat boughtCat = currentInteractingCustomer.AcceptGiverOfferAndLeave();
                CageManager.Instance.OccupyCage(emptyCage, boughtCat);

                repGain = giverRescueRep;   // you gave a cat a safe place
                MessageUI.Notify(currentDealPrice > 0 ? $"Deal accepted: -{currentDealPrice} GP" : "Stray taken in!");
            }
            else
            {
                MessageUI.Notify(emptyCage == null ? "No free cage!" : "Not enough GP!");
                HandleReject(true);
                return;
            }
        }

        if (repGain > 0)
        {
            CoreEconomySystem.Instance.ModifyReputation(repGain, systemName);
            MessageUI.Notify($"+{repGain} reputation");
        }

        TutorialNote.Complete(TutorialStep.Serve);

        dealUIPanel.SetActive(false);
        CustomerQueueManager.Instance.AdvanceQueue();
        currentInteractingCustomer = null;
    }

    // How much reputation an adoption earns, from how well the cat was cared for
    private int AdoptionReputation(int care)
    {
        if (care >= wellCaredFrom) return wellCaredAdoptionRep;
        if (care >= averageFrom) return averageAdoptionRep;
        return neglectedAdoptionRep;
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

        // Saying no is free. Only a deal the shop could not complete costs reputation
        if (forcedByGame)
        {
            CoreEconomySystem.Instance.ModifyReputation(-missedDealRep, systemName);
            DayManager.RecordMissed();
            MessageUI.Notify($"Deal missed: -{missedDealRep} reputation");
        }
        else
        {
            DayManager.RecordDeclined();
            MessageUI.Notify("Deal declined");
        }

        AudioManager.Play(Sfx.PoliteNo);
        TutorialNote.Complete(TutorialStep.Serve);

        dealUIPanel.SetActive(false);
        CustomerQueueManager.Instance.AdvanceQueue();
        currentInteractingCustomer = null;
    }

    public void CancelCurrentDeal()
    {
        currentInteractingCustomer = null;
        if (dealUIPanel != null) dealUIPanel.SetActive(false);
    }
}