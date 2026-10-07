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
                Debug.Log($"[SOA {systemName}] Raycast hit World Space UI Button: {uiButton.gameObject.name}");
                uiButton.PressButton();
                return;
            }

            if (hit.collider.gameObject == gameObject || hit.collider.GetComponentInParent<ReceptionDesk>() != null)
            {
                Debug.Log($"[SOA {systemName}] Raycast hit Reception Desk directly.");
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

                Debug.Log($"[SOA {systemName}] Generated Taker sale offer for cat '{cat.name}'. Payout: {currentDealPrice} GP.");
            }
            else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
            {
                int statPenalty = (12 - (cat.feedingLevel + cat.cleanlinessLevel)) * 2; // Each missing stat point reduces price by 2 GP
                currentDealPrice = Mathf.Max(30, 80 - statPenalty);

                dealText.text = $"BUY CAT?\nFeed: {cat.feedingLevel}/6\nClean: {cat.cleanlinessLevel}/6\nCost: -{currentDealPrice} GP";
                dealUIPanel.SetActive(true);

                Debug.Log($"[SOA {systemName}] Generated Giver purchase offer for cat '{cat.name}'. Cost: {currentDealPrice} GP.");
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
            currentInteractingCustomer.CompleteTakerTransactionAndLeave();
            
            Debug.Log($"[SOA {systemName}] ACCEPTED deal: Sold cat for +{currentDealPrice} GP.");
        }
        else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
        {
            int playerMoney = CoreEconomySystem.Instance.GetGP(systemName);
            Transform emptyCage = CageManager.Instance.GetAvailableCage();

            if (playerMoney >= currentDealPrice && emptyCage != null)
            {
                CoreEconomySystem.Instance.ModifyGP(-currentDealPrice, systemName);
                BaseCat boughtCat = currentInteractingCustomer.AcceptGiverOfferAndLeave();
                CageManager.Instance.OccupyCage(emptyCage, boughtCat);

                Debug.Log($"[SOA {systemName}] ACCEPTED deal: Bought cat for -{currentDealPrice} GP and assigned to cage '{emptyCage.name}'.");
            }
            else
            {
                Debug.LogWarning($"[SOA {systemName}] ACCEPT failed: Player GP ({playerMoney}) < Cost ({currentDealPrice}) or no empty cage available. Auto-rejecting deal.");
                RejectDeal();
                return;
            }
        }

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
            Transform emptyCage = CageManager.Instance.GetAvailableCage();

            if (returnedCat != null && emptyCage != null)
            {
                CageManager.Instance.OccupyCage(emptyCage, returnedCat);
                Debug.Log($"[SOA {systemName}] REJECTED Taker deal. Cat '{returnedCat.name}' placed back in cage '{emptyCage.name}'.");
            }
        }
        else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
        {
            currentInteractingCustomer.RejectGiverOfferAndLeave();
            Debug.Log($"[SOA {systemName}] REJECTED Giver deal. Customer departing with cat.");
        }

        dealUIPanel.SetActive(false);
        CustomerQueueManager.Instance.AdvanceQueue();
        currentInteractingCustomer = null;
    }
}