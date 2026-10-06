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

        // Firing Raycast from center crosshair
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance))
        {
            // 1. Check if we hit a World Space UI Button (Accept/Reject)
            WorldSpaceUIButton uiButton = hit.collider.GetComponent<WorldSpaceUIButton>();
            if (uiButton != null)
            {
                uiButton.PressButton();
                return;
            }

            // 2. Check if we hit the Reception Desk directly
            if (hit.collider.gameObject == gameObject || hit.collider.GetComponentInParent<ReceptionDesk>() != null)
            {
                ProcessTransaction();
            }
        }
    }

    private void ProcessTransaction()
    {
        currentInteractingCustomer = FindWaitingCustomer();

        if (currentInteractingCustomer != null)
        {
            if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Taker)
            {
                BaseCat soldCat = currentInteractingCustomer.CompleteTakerTransactionAndLeave();
                int payout = soldCat.CalculateFinalPrice();
                CoreEconomySystem.Instance.ModifyGP(payout, systemName);
                Destroy(soldCat.gameObject);
            }
            else if (currentInteractingCustomer.myType == CustomerAI.CustomerType.Giver)
            {
                PresentGiverDeal(currentInteractingCustomer);
            }
        }
    }

    private void PresentGiverDeal(CustomerAI giver)
    {
        BaseCat offeredCat = giver.heldCat;
        int statPenalty = (12 - (offeredCat.feedingLevel + offeredCat.cleanlinessLevel)) * 5; 
        currentDealPrice = Mathf.Max(10, 50 - statPenalty);

        dealText.text = $"Buy this cat?\nFeed: {offeredCat.feedingLevel}/6\nClean: {offeredCat.cleanlinessLevel}/6\nPrice: {currentDealPrice} GP";
        dealUIPanel.SetActive(true);
    }

    public void AcceptGiverDeal()
    {
        if (currentInteractingCustomer == null) return;

        int playerMoney = CoreEconomySystem.Instance.GetGP(systemName);
        Transform emptyCage = CageManager.Instance.GetAvailableCage();

        if (playerMoney >= currentDealPrice && emptyCage != null)
        {
            CoreEconomySystem.Instance.ModifyGP(-currentDealPrice, systemName);
            BaseCat boughtCat = currentInteractingCustomer.AcceptOfferAndLeave();
            CageManager.Instance.OccupyCage(emptyCage, boughtCat);
        }
        else
        {
            currentInteractingCustomer.RejectOfferAndLeave();
        }

        dealUIPanel.SetActive(false);
        currentInteractingCustomer = null;
    }

    public void RejectGiverDeal()
    {
        if (currentInteractingCustomer == null) return;

        dealUIPanel.SetActive(false);
        currentInteractingCustomer.RejectOfferAndLeave();
        currentInteractingCustomer = null;
    }

    private CustomerAI FindWaitingCustomer()
    {
        CustomerAI[] customers = FindObjectsByType<CustomerAI>(FindObjectsSortMode.None);
        foreach (var customer in customers)
        {
            if (customer.currentState == CustomerAI.CustomerState.WaitingAtRegister) return customer;
        }
        return null;
    }
}