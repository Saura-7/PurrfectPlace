using UnityEngine;
using System.Collections.Generic;

public class CageManager : MonoBehaviour
{
    public static CageManager Instance { get; private set; }
    private readonly string systemName = "CageManager";

    [Header("Store Cages")]
    public List<Transform> allCages; 

    private Dictionary<Transform, BaseCat> occupiedCages = new Dictionary<Transform, BaseCat>();
    // NEW: HashSet to track cages already claimed by an incoming Taker
    private HashSet<Transform> reservedCages = new HashSet<Transform>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool HasEmptyCage()
    {
        return occupiedCages.Count < allCages.Count;
    }

    public Transform GetAvailableCage()
    {
        foreach (Transform cage in allCages)
        {
            if (!occupiedCages.ContainsKey(cage)) return cage;
        }
        return null;
    }

    public void OccupyCage(Transform cage, BaseCat cat)
    {
        if (!occupiedCages.ContainsKey(cage))
        {
            occupiedCages.Add(cage, cat);
            
            cat.transform.SetParent(cage);
            cat.transform.localPosition = Vector3.zero;
            cat.transform.localRotation = Quaternion.identity;

            // Debug.Log($"[SOA {systemName}] Cage '{cage.name}' occupied by cat '{cat.name}'. Occupied: {occupiedCages.Count}/{allCages.Count}");
            SOALogger.LogState("CageManager", SOALogger.Colors.Cage, "OCCUPY", $"Cage '{cage.name}' assigned to cat '{cat.name}'. Total Occupied: {occupiedCages.Count}/{allCages.Count}");
        }
    }

    public void FreeCage(Transform cage)
    {
        reservedCages.Remove(cage); // always clear it; safe even if it isn't in the set
        if (occupiedCages.ContainsKey(cage))
        {
            occupiedCages.Remove(cage);
            
            // Debug.Log($"[SOA {systemName}] Cage '{cage.name}' freed. Occupied: {occupiedCages.Count}/{allCages.Count}");
            SOALogger.LogState("CageManager", SOALogger.Colors.Cage, "FREE", $"Cage '{cage.name}' freed. Total Occupied: {occupiedCages.Count}/{allCages.Count}");
        }
    
    }

    // UPDATED: Finds an occupied cage that is NOT already reserved by another Taker
    public KeyValuePair<Transform, BaseCat>? ReserveUnclaimedCatCage()
    {
        foreach (var pair in occupiedCages)
        {
            Transform cage = pair.Key;
            BaseCat cat = pair.Value;

            // Check that the cage has a cat AND is not reserved by another Taker
            if (cat != null && !reservedCages.Contains(cage))
            {
                reservedCages.Add(cage); // Instantly reserve this cage for the caller
                // Debug.Log($"[SOA {systemName}] Cage '{cage.name}' reserved for incoming Taker.");
                SOALogger.LogState("CageManager", SOALogger.Colors.Cage, "RESERVE", $"Cage '{cage.name}' reserved for incoming Taker.");
                return pair;
            }
        }
        return null; // No unreserved cats available
    }

    // NEW: Unreserves a cage if a Taker leaves or cancels their walk to the cage
    public void UnreserveCage(Transform cage)
    {
        if (cage != null && reservedCages.Contains(cage))
        {
            reservedCages.Remove(cage);
            // Debug.Log($"[SOA {systemName}] Reservation released for cage '{cage.name}'.");
            SOALogger.LogState("CageManager", SOALogger.Colors.Cage, "UNRESERVE", $"Reservation released for cage '{cage.name}'.");
        }
    }

    // Puts a returned cat back into the cage it came from
    public void ReturnCatToCage(Transform cage, BaseCat cat)
    {
        if (cage == null || cat == null || !occupiedCages.ContainsKey(cage)) return;

        occupiedCages[cage] = cat;
        reservedCages.Remove(cage); // the cat is back on the shelf and can be bought again

        cat.transform.SetParent(cage);
        cat.transform.localPosition = Vector3.zero;
        cat.transform.localRotation = Quaternion.identity;
    }
}