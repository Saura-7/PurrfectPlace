using UnityEngine;
using UnityEngine.InputSystem;

public class ReceptionDesk : MonoBehaviour
{
    private readonly string systemName = "ReceptionDesk";

    [Header("Interaction Settings")]
    public float maxInteractionDistance = 10f;

    private InputSystem_Actions inputActions;
    private InputAction CompleteDealAction;

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

    private void LateUpdate()
    {
        // 1. Draw the ray in LateUpdate so it perfectly syncs with Cinemachine
        if (Camera.main != null)
        {
            // Viewport 0.5f, 0.5f is the absolute dead-center of the screen
            Ray debugRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            Debug.DrawRay(debugRay.origin, debugRay.direction * maxInteractionDistance, Color.blue);
        }
    }

    private void OnInteractPressed()
    {
        if (Camera.main == null) return;

        // 2. Use the exact same Viewport center for the actual interaction
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance))
        {
            Debug.Log($"Raycast hit: {hit.collider.gameObject.name}");
            
            // 3. Check if the player is actually looking at THIS reception desk (or its child meshes)
            if (hit.collider.gameObject == gameObject || hit.collider.GetComponentInParent<ReceptionDesk>() != null)
            {
                ProcessTransaction();
            }
            else
            {
                Debug.Log("You must look directly at the Reception Desk to complete a deal.");
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