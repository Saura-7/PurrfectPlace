using UnityEngine;
using UnityEngine.InputSystem;

public class ReceptionDesk : MonoBehaviour
{
    private readonly string systemName = "ReceptionDesk";

    [Header("Interaction Settings")]
    public float maxInteractionDistance = 10f;

    private InputSystem_Actions inputActions;

    private void OnEnable()
    {
        inputActions = new InputSystem_Actions();

        // Bind to 'Interact' action (E on Keyboard / Button North on Gamepad)
        inputActions.Player.Interact.started += OnInteractPressed;

        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Interact.started -= OnInteractPressed;
            inputActions.Player.Disable();
        }
    }

    private void OnInteractPressed(InputAction.CallbackContext context)
    {
        if (Camera.main == null) return;

        // 1. Unified Center-Screen Raycast (Works for Mouse & Gamepad)
        Vector3 centerScreen = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        Ray ray = Camera.main.ScreenPointToRay(centerScreen);

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance))
        {
            // 2. Check if the player is aiming directly at this Reception Desk
            if (hit.collider.gameObject == gameObject || hit.collider.GetComponent<ReceptionDesk>() != null)
            {
                ProcessTransaction();
            }
        }
    }

    private void ProcessTransaction()
    {
        // Find a customer currently standing at the register
        CustomerAI waitingCustomer = FindWaitingCustomer();

        if (waitingCustomer != null)
        {
            // 1. Signal customer to leave and retrieve the cat puppet reference
            BaseCat soldCat = waitingCustomer.CompleteTransactionAndLeave();

            if (soldCat != null)
            {
                // 2. Calculate price based on stats (50 base + 20 per feeding/cleanliness point)
                int payout = soldCat.CalculateFinalPrice();

                // 3. Deposit earnings through SOA Central Bank & generate Audit Log
                CoreEconomySystem.Instance.ModifyGP(payout, systemName);

                // 4. Clean up sold cat instance from the scene
                Destroy(soldCat.gameObject);

                Debug.Log($"[SOA ReceptionDesk] Transaction complete! Sold {soldCat.name} for {payout} GP.");
            }
        }
        else
        {
            Debug.Log("[SOA ReceptionDesk] No customer is waiting at the desk.");
        }
    }

    private CustomerAI FindWaitingCustomer()
    {
        CustomerAI[] customers = FindObjectsByType<CustomerAI>(FindObjectsSortMode.None);
        foreach (var customer in customers)
        {
            if (customer.currentState == CustomerAI.CustomerState.WaitingAtRegister)
            {
                return customer;
            }
        }
        return null;
    }
}