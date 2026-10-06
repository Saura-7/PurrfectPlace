using UnityEngine;
using UnityEngine.AI;

public class CustomerAI : MonoBehaviour
{
    public enum CustomerType { Taker, Giver }
    public enum CustomerState { Entering, LookingForCat, WalkingToRegister, WaitingAtRegister, Leaving }

    public CustomerType myType;
    public CustomerState currentState;

    [Header("References")]
    public Transform handHoldPoint; 
    public BaseCat heldCat;
    
    private NavMeshAgent agent;
    private Transform receptionDesk;
    private Transform exitDoor;
    private Transform targetCage; // Used by Takers to find a cat

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        receptionDesk = GameObject.Find("ReceptionDeskTarget").transform;
        exitDoor = GameObject.Find("ExitDoorTarget").transform;
    }

    public void InitializeCustomer(CustomerType type)
    {
        myType = type;
        
        if (myType == CustomerType.Giver)
        {
            heldCat = CatFactory.Instance.GenerateGiverCat(handHoldPoint);
            ChangeState(CustomerState.WalkingToRegister);
        }
        else
        {
            ChangeState(CustomerState.Entering);
        }
    }

    private void Update()
    {
        // 1. TAKER LOGIC: Walking to a cage to grab a cat
        if (currentState == CustomerState.LookingForCat)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                GrabCatFromCage();
            }
        }
        // 2. Walking to the register (Both Types)
        else if (currentState == CustomerState.WalkingToRegister)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                ChangeState(CustomerState.WaitingAtRegister);
            }
        }
        // 3. Leaving the store (Both Types)
        else if (currentState == CustomerState.Leaving)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                Destroy(gameObject); 
            }
        }
    }

    public void ChangeState(CustomerState newState)
    {
        currentState = newState;
        switch (currentState)
        {
            case CustomerState.Entering:
                // Takers immediately transition to looking for a cat
                ChangeState(CustomerState.LookingForCat);
                break;
                
            case CustomerState.LookingForCat:
                FindTargetCatToBuy();
                break;
                
            case CustomerState.WalkingToRegister:
                agent.SetDestination(receptionDesk.position);
                break;
                
            case CustomerState.Leaving:
                agent.SetDestination(exitDoor.position);
                break;
        }
    }

    private void FindTargetCatToBuy()
    {
        // Find all cats currently in the scene
        BaseCat[] availableCats = FindObjectsByType<BaseCat>(FindObjectsSortMode.None);
        
        foreach (BaseCat cat in availableCats)
        {
            // Ensure the cat isn't already being held by another customer
            if (cat.transform.parent != handHoldPoint && cat.transform.parent != null)
            {
                targetCage = cat.transform.parent; // Assuming the cat is a child of the cage
                agent.SetDestination(targetCage.position);
                return; // Found a target, exit loop
            }
        }

        // If no cats are available in the store, the Taker leaves disappointed
        Debug.Log("No cats available to buy. Taker is leaving.");
        ChangeState(CustomerState.Leaving);
    }

    private void GrabCatFromCage()
    {
        if (targetCage != null)
        {
            BaseCat catInCage = targetCage.GetComponentInChildren<BaseCat>();
            if (catInCage != null)
            {
                // Pick up the cat
                heldCat = catInCage;
                heldCat.transform.SetParent(handHoldPoint);
                heldCat.transform.localPosition = Vector3.zero;
                heldCat.transform.localRotation = Quaternion.identity;

                // Free the cage in the Manager so Givers can use it
                if (CageManager.Instance != null)
                {
                    CageManager.Instance.FreeCage(targetCage);
                }

                ChangeState(CustomerState.WalkingToRegister);
                return;
            }
        }
        
        // Failsafe: if the cat disappeared before we got there, leave.
        ChangeState(CustomerState.Leaving);
    }

    public BaseCat CompleteTakerTransactionAndLeave()
    {
        BaseCat soldCat = heldCat;
        heldCat = null;
        ChangeState(CustomerState.Leaving);
        return soldCat;
    }

    public void RejectOfferAndLeave()
    {
        ChangeState(CustomerState.Leaving);
    }

    public BaseCat AcceptOfferAndLeave()
    {
        BaseCat boughtCat = heldCat;
        boughtCat.transform.SetParent(null); 
        heldCat = null;
        ChangeState(CustomerState.Leaving);
        return boughtCat;
    }
}