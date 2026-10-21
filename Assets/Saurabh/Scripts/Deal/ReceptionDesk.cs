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

    [Header("Reputation Settings")]
    public int minReputationChange = 5;
    public int maxReputationChange = 10;

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

            if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Taker)
            {
                currentDealPrice = cat.CalculateFinalPrice();
                dealText.text = $"SELL CAT?\nFeed: {cat.feedingLevel}/6\nClean: {cat.cleanlinessLevel}/6\nPayout: +{currentDealPrice} GP";
                dealUIPanel.SetActive(true);

                // Debug.Log($"[SOA {systemName}] Generated Taker sale offer for cat '{cat.name}'. Payout: {currentDealPrice} GP.");
            }
            else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
            {
                // What the cat is really worth, and what this Giver asks for it
                int catValue = cat.CalculateFinalPrice();
                currentDealPrice = Mathf.Max(10, Mathf.RoundToInt(catValue * currentInteractingCustomer.AskFactor));

                dealText.text = $"BUY CAT?\nFeed: {cat.feedingLevel}/6\nClean: {cat.cleanlinessLevel}/6\nWorth about: {catValue} GP\nCost: -{currentDealPrice} GP";
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

        if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Taker)
        {
            CoreEconomySystem.Instance.ModifyGP(currentDealPrice, systemName);
            DayManager.RecordCatSold();
            currentInteractingCustomer.CompleteTakerTransactionAndLeave();
            CageManager.Instance.FreeCage(currentInteractingCustomer.HomeCage); // the cat is sold, so the cage is free now
            
            // Debug.Log($"[SOA {systemName}] ACCEPTED deal: Sold cat for +{currentDealPrice} GP.");
            MessageUI.Notify($"Deal accepted: +{currentDealPrice} GP");
        }
        else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
        {
            int playerMoney = CoreEconomySystem.Instance.GetGP(systemName);
            Transform emptyCage = CageManager.Instance.GetAvailableCage();

            if (playerMoney >= currentDealPrice && emptyCage != null)
            {
                CoreEconomySystem.Instance.ModifyGP(-currentDealPrice, systemName);
                DayManager.RecordCatBought();
                BaseCat boughtCat = currentInteractingCustomer.AcceptGiverOfferAndLeave();
                CageManager.Instance.OccupyCage(emptyCage, boughtCat);

                // Debug.Log($"[SOA {systemName}] ACCEPTED deal: Bought cat for - {currentDealPrice} GP and assigned to cage '{emptyCage.name}'.");
                MessageUI.Notify($"Deal accepted: -{currentDealPrice} GP"); 
            }
            else
            {
                // Debug.LogWarning($"[SOA {systemName}] ACCEPT failed: Player GP ({playerMoney}) < Cost ({currentDealPrice}) or no empty cage available. Auto-rejecting deal.");
                MessageUI.Notify(emptyCage == null ? "No free cage!" : "Not enough GP!");
                RejectDeal();
                return;
            }
        }

        int repGain = Random.Range(minReputationChange, maxReputationChange + 1);
        CoreEconomySystem.Instance.ModifyReputation(repGain, systemName);  
        MessageUI.Notify($"+{repGain} reputation"); 
        
        dealUIPanel.SetActive(false);
        CustomerQueueManager.Instance.AdvanceQueue();
        currentInteractingCustomer = null;
    }

    public void RejectDeal()
    {
        if (currentInteractingCustomer == null) return;

        if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Taker)
        {
            BaseCat returnedCat = currentInteractingCustomer.RejectTakerTransactionAndLeave();

            if (returnedCat != null)
            {
                CageManager.Instance.ReturnCatToCage(currentInteractingCustomer.HomeCage, returnedCat);
                // Debug.Log($"[SOA {systemName}] REJECTED Taker deal. Cat '{returnedCat.name}' placed back in cage '{currentInteractingCustomer.HomeCage.name}'.");
            }
        }
        else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
        {
            currentInteractingCustomer.RejectGiverOfferAndLeave();
            // Debug.Log($"[SOA {systemName}] REJECTED Giver deal. Customer departing with cat.");
        }

        int repLoss = Random.Range(minReputationChange, maxReputationChange + 1);
        CoreEconomySystem.Instance.ModifyReputation(-repLoss, systemName);
        DayManager.RecordRejected();
        MessageUI.Notify($"Deal rejected: -{repLoss} reputation");
        
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