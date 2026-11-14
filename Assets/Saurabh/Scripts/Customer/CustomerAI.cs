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

    [Header("Talking")]
    // A collider around the customer's body, so the player can look at them and click.
    // It is only switched ON while they wait at the desk. Walking customers never block your
    // view of a cat, and the player can only "talk" to someone who is ready for a deal.
    [SerializeField] private Collider talkCollider;
    
    private NavMeshAgent agent;
    private Transform exitDoor;
    private Transform targetCage; 
    private bool isAtFrontOfLine = false;

    private Transform doorChimePoint;
    private bool hasChimed;                    // the bell rings once per customer
    private const float doorChimeDistance = 2f;   // metres from the marker (sideways only) to trigger the chime
    
    // How greedy a Giver is: 0.85 = asks 15% less than the cat is worth, 1.35 = asks 35% more.
    // Average 1.1: re-selling a cat WITHOUT caring for it loses a little, so care is where profit comes from.
    [Header("Giver asking price (x the cat's worth)")]
    [SerializeField] private float minAskFactor = 0.85f;
    [SerializeField] private float maxAskFactor = 1.35f;
    public float AskFactor { get; private set; }

    // A Stray finder is a Giver who found a cat outside and asks for nothing
    public bool IsStray { get; private set; }

    public Transform HomeCage => targetCage;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (talkCollider != null) talkCollider.enabled = false;   // nobody is at the desk yet
        agent.avoidancePriority = Random.Range(40, 60);
        
        GameObject exitObj = GameObject.Find("ExitDoorTarget");
        if (exitObj != null) exitDoor = exitObj.transform;
        GameObject doorObj = GameObject.Find("DoorChimePoint");
        if (doorObj != null) doorChimePoint = doorObj.transform;
        AskFactor = Random.Range(minAskFactor, maxAskFactor);
    }

    // isStray is optional, so old calls like InitializeCustomer(type) still work
    public void InitializeCustomer(CustomerType type, bool isStray = false)
    {
        myType = type;
        IsStray = isStray && type == CustomerType.Giver;
        
        if (myType == CustomerType.Giver)
        {
            if (CustomerQueueManager.Instance.IsQueueFull())
            {
                Debug.LogWarning($"[SOA {systemName}] Queue full. Giver leaving without entering.");
                ChangeState(CustomerState.Leaving);
                return;
            }

            heldCat = CatFactory.Instance.GenerateGiverCat(handHoldPoint);

            // A stray was living outside, so it arrives hungry and dirty
            if (IsStray)
            {
                heldCat.feedingLevel = 0;
                heldCat.cleanlinessLevel = 0;
            }
            // Debug.Log($"[SOA {systemName}] Giver initialized with cat '{heldCat.name}'. Joining queue.");
            CustomerQueueManager.Instance.JoinQueue(this);
        }
        else
        {
            if (CustomerQueueManager.Instance.IsQueueFull())
            {
                Debug.LogWarning($"[SOA {systemName}] Queue full. Taker leaving without a cat.");
                ChangeState(CustomerState.Leaving);
                return;
            }
            // Debug.Log($"[SOA {systemName}] Taker initialized. Setting state to Entering.");
            ChangeState(CustomerState.Entering);
        }
    }

    private void Update()
    {
        // Ring the bell once, when a customer walking in gets near the door
        if (!hasChimed && doorChimePoint != null && currentState != CustomerState.Leaving)
        {
            // Compare only left/right and forward/back. Ignore height,
            // because the marker is on the floor and the customer's pivot is higher up.
            Vector3 offset = transform.position - doorChimePoint.position;
            offset.y = 0f;

            if (offset.magnitude <= doorChimeDistance)
            {
                hasChimed = true;
                AudioManager.Play(Sfx.DoorChime);
            }
        }


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
                    // Debug.Log($"[SOA {systemName}] Customer reached register (Spot 0). Waiting for interaction.");
                    ChangeState(CustomerState.WaitingAtRegister);
                }
            }
        }
        else if (currentState == CustomerState.Leaving)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                // Debug.Log($"[SOA {systemName}] Customer reached exit door. Despawning GameObject.");
                Destroy(gameObject);
            }
        }
    }

    public void ChangeState(CustomerState newState)
    {
        // If we change states before reaching the cage (e.g. leaving early), release reservation
        if (currentState == CustomerState.LookingForCat && newState == CustomerState.Leaving && heldCat == null)
        {
            CageManager.Instance.UnreserveCage(targetCage);
        }

        currentState = newState;

        // Clickable only while waiting at the desk (see talkCollider above)
        if (talkCollider != null) talkCollider.enabled = currentState == CustomerState.WaitingAtRegister;
        // Debug.Log($"[SOA {systemName}] {gameObject.name} state changed to: {currentState}");
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
                agent.avoidancePriority = Random.Range(70, 90); // leaving customers give way to everyone else
                if (exitDoor != null) agent.SetDestination(exitDoor.position);
                break;
        }
    }

    public void MoveToQueueSpot(Vector3 spotPosition, bool isAtRegister)
    {
        currentState = CustomerState.WalkingToRegister;
        isAtFrontOfLine = isAtRegister;
        agent.SetDestination(spotPosition);
        // Debug.Log($"[SOA {systemName}] {gameObject.name} moving to queue spot: {spotPosition}. At register: {isAtRegister}");
    }

    private void FindTargetCatToBuy()
{
    // Ask CageManager for an occupied cage that no other Taker has claimed yet
    var reservedPair = CageManager.Instance.ReserveUnclaimedCatCage();

    if (reservedPair.HasValue)
    {
        targetCage = reservedPair.Value.Key;
        agent.SetDestination(targetCage.position);
        // Debug.Log($"[SOA {systemName}] Taker reserved and targeted cat in cage '{targetCage.name}'.");
    }
    else
    {
        // Debug.Log($"[SOA {systemName}] No unreserved cats available. Taker leaving store.");
        ChangeState(CustomerState.Leaving);
    }
}

    private void GrabCatFromCage()
    {
        if (CustomerQueueManager.Instance.IsQueueFull())
        {
            Debug.LogWarning($"[SOA {systemName}] Queue full. Taker leaving without a cat.");
            ChangeState(CustomerState.Leaving);
            return;
        }

        if (targetCage != null)
        {
            BaseCat catInCage = targetCage.GetComponentInChildren<BaseCat>();
            if (catInCage != null)
            {
                heldCat = catInCage;
                heldCat.MoveTo(handHoldPoint, standOnSurface: false);   // in the hands: no floor to stand on

                // Debug.Log($"[SOA {systemName}] Taker picked up cat '{heldCat.name}' from cage '{targetCage.name}'.");

                // CageManager.Instance.FreeCage(targetCage);
                CustomerQueueManager.Instance.JoinQueue(this);
                return;
            }
        }
        
        // Debug.LogWarning($"[SOA {systemName}] Taker reached cage but no cat was present. Leaving store.");
        SOALogger.LogState("CustomerAI", SOALogger.Colors.Customer, "WARNING", $"Taker reached cage but no cat was present. Leaving store.");
        ChangeState(CustomerState.Leaving);
    }

    // ==========================================
    // THE CAT ON THE DESK DURING A DEAL
    // ==========================================

    // The deal opened: the customer sets the cat down on the desk so the player can look at it
    public void PutCatOnDesk(Transform spot)
    {
        if (heldCat == null || spot == null) return;
        PlaceCat(spot, true);   // feet on the desk
        heldCat.OnDesk = true;
    }

    // The customer picks the cat back up (adopted, or a Giver who was turned down)
    public void TakeCatBack()
    {
        if (heldCat == null || !heldCat.OnDesk) return;
        heldCat.OnDesk = false;
        PlaceCat(handHoldPoint, false);   // back in the hands
    }

    // Moves the cat into a new parent and snaps it to that spot
    private void PlaceCat(Transform parent, bool standOnSurface)
    {
        heldCat.MoveTo(parent, standOnSurface);   // MoveTo also keeps the cat its normal size (see BaseCat)
    }

    public BaseCat CompleteTakerTransactionAndLeave()
    {
        TakeCatBack();   // the adopter picks the cat up from the desk and leaves with it
        BaseCat soldCat = heldCat;
        // Debug.Log($"[SOA {systemName}] Taker transaction complete. Leaving with cat '{soldCat?.name}'.");
        ChangeState(CustomerState.Leaving);
        return soldCat;
    }

    public BaseCat RejectTakerTransactionAndLeave()
    {
        BaseCat returnedCat = heldCat;
        if (returnedCat != null)
        {
            returnedCat.OnDesk = false;   // the shop keeps it: it goes back to its cage
            returnedCat.transform.SetParent(null);
        }
        heldCat = null;
        // Debug.Log($"[SOA {systemName}] Taker transaction rejected. Leaving empty-handed.");
        ChangeState(CustomerState.Leaving);
        return returnedCat;
    }

    public void RejectGiverOfferAndLeave()
    {
        TakeCatBack();   // the Giver takes their cat back home
        // Debug.Log($"[SOA {systemName}] Giver deal rejected. Leaving store with cat.");
        ChangeState(CustomerState.Leaving);
    }

    public BaseCat AcceptGiverOfferAndLeave()
    {
        BaseCat boughtCat = heldCat;
        if (boughtCat != null)
        {
            boughtCat.OnDesk = false;   // now it's the shop's cat: it goes into a cage
            boughtCat.transform.SetParent(null);
        }
        heldCat = null;
        // Debug.Log($"[SOA {systemName}] Giver deal accepted. Handing over cat '{boughtCat?.name}' and leaving.");
        ChangeState(CustomerState.Leaving);
        return boughtCat;
    }

    public void DismissForNewDay()
    {
        // A cat left on the desk goes back into the customer's hands first,
        // so it isn't left behind when this customer is removed
        TakeCatBack();

        if (myType == CustomerType.Taker)
        {
            bool waitingWithCat = heldCat != null &&
                (currentState == CustomerState.WalkingToRegister || currentState == CustomerState.WaitingAtRegister);

            if (waitingWithCat)
            {
                // The cat was never sold, so it goes back to its own enclosure
                BaseCat cat = heldCat;
                heldCat = null;
                CageManager.Instance.ReturnCatToCage(HomeCage, cat);
            }
            else if (currentState == CustomerState.LookingForCat)
            {
                // Still walking to the enclosure, so release the reservation
                CageManager.Instance.UnreserveCage(targetCage);
            }
        }

        Destroy(gameObject); 
    }

    private void OnDestroy()
    {
        // If this customer disappears while still walking to a reserved cage, release it
        if (heldCat == null && currentState == CustomerState.LookingForCat && CageManager.Instance != null)
        {
            CageManager.Instance.UnreserveCage(targetCage);
        }
    }
}