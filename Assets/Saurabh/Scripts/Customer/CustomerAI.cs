using UnityEngine;
using UnityEngine.AI;

public class CustomerAI : MonoBehaviour
{
    private readonly string systemName = "CustomerAI";

    public enum CustomerType { Taker, Giver }
    public enum CustomerState { Entering, LookingForCat, WalkingToRegister, WaitingAtRegister, Leaving }

    public CustomerType myType;
    public CustomerState currentState;

    [Header("References")]
    public Transform handHoldPoint; 
    public BaseCat heldCat;
    
    private NavMeshAgent agent;
    private Transform exitDoor;
    private Transform targetCage; 
    private bool isAtFrontOfLine = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        GameObject exitObj = GameObject.Find("ExitDoorTarget");
        if (exitObj != null) exitDoor = exitObj.transform;
    }

    public void InitializeCustomer(CustomerType type)
    {
        myType = type;
        
        if (myType == CustomerType.Giver)
        {
            heldCat = CatFactory.Instance.GenerateGiverCat(handHoldPoint);
            Debug.Log($"[SOA {systemName}] Giver initialized with cat '{heldCat.name}'. Joining queue.");
            CustomerQueueManager.Instance.JoinQueue(this);
        }
        else
        {
            Debug.Log($"[SOA {systemName}] Taker initialized. Setting state to Entering.");
            ChangeState(CustomerState.Entering);
        }
    }

    private void Update()
    {
        if (currentState == CustomerState.LookingForCat)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                GrabCatFromCage();
            }
        }
        else if (currentState == CustomerState.WalkingToRegister)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                if (isAtFrontOfLine)
                {
                    Debug.Log($"[SOA {systemName}] Customer reached register (Spot 0). Waiting for interaction.");
                    ChangeState(CustomerState.WaitingAtRegister);
                }
            }
        }
        else if (currentState == CustomerState.Leaving)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                Debug.Log($"[SOA {systemName}] Customer reached exit door. Despawning GameObject.");
                Destroy(gameObject);
            }
        }
    }

    public void ChangeState(CustomerState newState)
    {
        currentState = newState;
        SOALogger.LogState("CustomerAI", SOALogger.Colors.Customer, "STATE_CHANGE", $"{gameObject.name} state changed to {currentState}");
        switch (currentState)
        {
            case CustomerState.Entering:
                ChangeState(CustomerState.LookingForCat);
                break;
                
            case CustomerState.LookingForCat:
                FindTargetCatToBuy();
                break;
                
            case CustomerState.Leaving:
                if (exitDoor != null) agent.SetDestination(exitDoor.position);
                break;
        }
    }

    public void MoveToQueueSpot(Vector3 spotPosition, bool isAtRegister)
    {
        currentState = CustomerState.WalkingToRegister;
        isAtFrontOfLine = isAtRegister;
        agent.SetDestination(spotPosition);
        Debug.Log($"[SOA {systemName}] {gameObject.name} moving to queue spot: {spotPosition}. At register: {isAtRegister}");
    }

    private void FindTargetCatToBuy()
    {
        var occupiedPair = CageManager.Instance.GetOccupiedCageWithCat();

        if (occupiedPair.HasValue)
        {
            targetCage = occupiedPair.Value.Key;
            agent.SetDestination(targetCage.position);
            Debug.Log($"[SOA {systemName}] Taker targeting cat in cage '{targetCage.name}'.");
        }
        else
        {
            Debug.Log($"[SOA {systemName}] No occupied cages found. Taker leaving store.");
            ChangeState(CustomerState.Leaving);
        }
    }

    private void GrabCatFromCage()
    {
        if (targetCage != null)
        {
            BaseCat catInCage = targetCage.GetComponentInChildren<BaseCat>();
            if (catInCage != null)
            {
                heldCat = catInCage;
                heldCat.transform.SetParent(handHoldPoint);
                heldCat.transform.localPosition = Vector3.zero;
                heldCat.transform.localRotation = Quaternion.identity;

                Debug.Log($"[SOA {systemName}] Taker picked up cat '{heldCat.name}' from cage '{targetCage.name}'.");

                CageManager.Instance.FreeCage(targetCage);
                CustomerQueueManager.Instance.JoinQueue(this);
                return;
            }
        }
        
        Debug.LogWarning($"[SOA {systemName}] Taker reached cage but no cat was present. Leaving store.");
        ChangeState(CustomerState.Leaving);
    }

    public BaseCat CompleteTakerTransactionAndLeave()
    {
        BaseCat soldCat = heldCat;
        Debug.Log($"[SOA {systemName}] Taker transaction complete. Leaving with cat '{soldCat?.name}'.");
        ChangeState(CustomerState.Leaving);
        return soldCat;
    }

    public BaseCat RejectTakerTransactionAndLeave()
    {
        BaseCat returnedCat = heldCat;
        if (returnedCat != null)
        {
            returnedCat.transform.SetParent(null);
        }
        heldCat = null;
        Debug.Log($"[SOA {systemName}] Taker transaction rejected. Leaving empty-handed.");
        ChangeState(CustomerState.Leaving);
        return returnedCat;
    }

    public void RejectGiverOfferAndLeave()
    {
        Debug.Log($"[SOA {systemName}] Giver deal rejected. Leaving store with cat.");
        ChangeState(CustomerState.Leaving);
    }

    public BaseCat AcceptGiverOfferAndLeave()
    {
        BaseCat boughtCat = heldCat;
        if (boughtCat != null)
        {
            boughtCat.transform.SetParent(null);
        }
        heldCat = null;
        Debug.Log($"[SOA {systemName}] Giver deal accepted. Handing over cat '{boughtCat?.name}' and leaving.");
        ChangeState(CustomerState.Leaving);
        return boughtCat;
    }
}