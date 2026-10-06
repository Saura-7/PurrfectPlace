using UnityEngine;
using System.Collections.Generic;

public class CageManager : MonoBehaviour
{
    public static CageManager Instance { get; private set; }

    [Header("Store Cages")]
    public List<Transform> allCages; // Drag all your cage spawn points here in the Inspector
    private List<Transform> occupiedCages = new List<Transform>();

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
            if (!occupiedCages.Contains(cage)) return cage;
        }
        return null;
    }

    public void OccupyCage(Transform cage, BaseCat cat)
    {
        if (!occupiedCages.Contains(cage))
        {
            occupiedCages.Add(cage);
            cat.transform.position = cage.position;
            cat.transform.rotation = cage.rotation;
        }
    }

    public void FreeCage(Transform cage)
    {
        if (occupiedCages.Contains(cage)) occupiedCages.Remove(cage);
    }
}