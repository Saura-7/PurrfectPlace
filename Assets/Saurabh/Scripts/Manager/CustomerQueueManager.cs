using System.Collections.Generic;
using UnityEngine;

public class CustomerQueueManager : MonoBehaviour
{
    public static CustomerQueueManager Instance { get; private set; }
    private readonly string systemName = "CustomerQueueManager";

    [Header("Queue Positions")]
    public List<Transform> queueSpots;

    private List<CustomerAI> line = new List<CustomerAI>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool IsQueueFull()
    {
        return line.Count >= queueSpots.Count;
    }

    public bool JoinQueue(CustomerAI customer)
    {
        if (IsQueueFull())
        {
            Debug.LogWarning($"[SOA {systemName}] Queue is full ({line.Count}/{queueSpots.Count}). Customer '{customer.name}' cannot join.");
            return false;
        }

        line.Add(customer);
        Debug.Log($"[SOA {systemName}] Customer '{customer.name}' ({customer.myType}) joined queue at spot {line.Count - 1}.");
        UpdateLinePositions();
        return true;
    }

    public CustomerAI GetCurrentCustomerAtDesk()
    {
        if (line.Count > 0)
        {
            CustomerAI frontCustomer = line[0];
            if (frontCustomer.currentState == CustomerAI.CustomerState.WaitingAtRegister)
            {
                return frontCustomer;
            }
        }
        return null;
    }

    public void AdvanceQueue()
    {
        if (line.Count > 0)
        {
            CustomerAI finishedCustomer = line[0];
            line.RemoveAt(0);
            Debug.Log($"[SOA {systemName}] Customer '{finishedCustomer.name}' removed from queue front. Remaining in line: {line.Count}.");
            UpdateLinePositions();
        }
    }

    private void UpdateLinePositions()
    {
        for (int i = 0; i < line.Count; i++)
        {
            Vector3 targetSpot = queueSpots[i].position;
            bool isFrontOfLine = (i == 0);
            
            line[i].MoveToQueueSpot(targetSpot, isFrontOfLine);
            Debug.Log($"[SOA {systemName}] Updated position for customer '{line[i].name}' -> Queue Spot {i} ({targetSpot}).");
        }
    }
}