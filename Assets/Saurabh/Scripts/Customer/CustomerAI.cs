using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CustomerAI : MonoBehaviour
{
    // Added 'WaitingForCat' as the default state
    public enum CustomerState { WaitingForCat, WalkingToCat, WalkingToRegister, WaitingAtRegister, Leaving }
    
    [Header("State")]
    public CustomerState currentState = CustomerState.WaitingForCat;

    [Header("References")]
    public Transform receptionDesk;
    public Transform exitDoor;
    private NavMeshAgent agent;
    private BaseCat targetCat;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        currentState = CustomerState.WaitingForCat;
    }

    private void Update()
    {
        switch (currentState)
        {
            case CustomerState.WaitingForCat:
                // Keep looking every frame until a cat is spawned by debug input
                FindRandomCat();
                break;

            case CustomerState.WalkingToCat:
                // Guard clause: if the cat was destroyed or lost, go back to waiting
                if (targetCat == null)
                {
                    currentState = CustomerState.WaitingForCat;
                    return;
                }

                // If we reached the cat
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    PickUpCat();
                }
                break;

            case CustomerState.WalkingToRegister:
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    currentState = CustomerState.WaitingAtRegister;
                }
                break;

            case CustomerState.WaitingAtRegister:
                // Customer waits for player to click the reception desk
                break;

            case CustomerState.Leaving:
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    Destroy(gameObject); // Despawn out the door
                }
                break;
        }
    }

    private void FindRandomCat()
    {
        BaseCat[] allCats = FindObjectsByType<BaseCat>(FindObjectsSortMode.None);
        
        // Filter for cats that exist AND are available (not claimed by another customer)
        List<BaseCat> availableCats = new List<BaseCat>();
        foreach (BaseCat cat in allCats)
        {
            if (cat.isAvailable)
            {
                availableCats.Add(cat);
            }
        }

        // Only proceed if at least one available cat exists
        if (availableCats.Count > 0)
        {
            targetCat = availableCats[Random.Range(0, availableCats.Count)];
            targetCat.isAvailable = false; // Reserve this cat
            
            currentState = CustomerState.WalkingToCat;
            agent.SetDestination(targetCat.transform.position);
        }
    }

    private void PickUpCat()
    {
        // Guard clause to prevent NullReferenceException
        if (targetCat == null) return;

        // Parent the cat to the customer so it moves with them
        targetCat.transform.SetParent(this.transform);
        targetCat.transform.localPosition = new Vector3(0, 1, 1); // Position in front of customer

        currentState = CustomerState.WalkingToRegister;
        
        if (receptionDesk != null)
        {
            agent.SetDestination(receptionDesk.position);
        }
    }

    public BaseCat CompleteTransactionAndLeave()
    {
        BaseCat soldCat = targetCat;
        targetCat = null;
        
        currentState = CustomerState.Leaving;
        if (exitDoor != null)
        {
            agent.SetDestination(exitDoor.position);
        }
        
        return soldCat;
    }
}