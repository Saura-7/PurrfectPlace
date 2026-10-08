using UnityEngine;
using System.Collections.Generic;

public class CageManager : MonoBehaviour
{
    public static CageManager Instance { get; private set; }
    private readonly string systemName = "CageManager";

    [Header("Store Cages")]
    public List<Transform> allCages; 

    private Dictionary<Transform, BaseCat> occupiedCages = new Dictionary<Transform, BaseCat>();

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

            SOALogger.LogState("CageManager", SOALogger.Colors.Cage, "OCCUPY", $"Cage '{cage.name}' assigned to cat '{cat.name}'. Total Occupied: {occupiedCages.Count}/{allCages.Count}");        
        }
    }

    public void FreeCage(Transform cage)
    {
        if (occupiedCages.ContainsKey(cage))
        {
            occupiedCages.Remove(cage);
            SOALogger.LogState("CageManager", SOALogger.Colors.Cage, "FREE", $"Cage '{cage.name}' freed. Occupied: {occupiedCages.Count}/{allCages.Count}");
        }
    }

    public KeyValuePair<Transform, BaseCat>? GetOccupiedCageWithCat()
    {
        foreach (var pair in occupiedCages)
        {
            if (pair.Value != null)
            {
                return pair;
            }
        }
        return null;
    }
}