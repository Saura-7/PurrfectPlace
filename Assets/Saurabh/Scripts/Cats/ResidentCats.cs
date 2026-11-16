using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rare cats the player chose to keep. A resident:
///   - leaves its cage (the cage is free again) and lives on a resident spot
///   - has a name, needs no care, can be petted
///   - brings adopters a tiny bit more often (see CustomerSpawner), never as much as selling it would
///   - can be put up for adoption later (back into a free cage)
///
/// The number of resident spots in the scene is the limit (2 spots = at most 2 residents).
/// Put this on Managers.
/// </summary>
public class ResidentCats : MonoBehaviour
{
    public static ResidentCats Instance { get; private set; }

    [Tooltip("Empty objects where residents live (a cushion, a shelf). The number of spots = the most residents you can have")]
    [SerializeField] private List<Transform> spots = new List<Transform>();

    // Which spot each resident sits on
    private readonly Dictionary<Transform, BaseCat> residents = new Dictionary<Transform, BaseCat>();

    // How many residents live in the shop (0 if there is no ResidentCats in the scene)
    public static int Count => Instance != null ? Instance.residents.Count : 0;

    public bool HasFreeSpot => FreeSpot() != null;
    public int MaxResidents => spots.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Keeps a rare cat as a resident. Returns false if every spot is taken.</summary>
    public bool MakeResident(BaseCat cat, string catName)
    {
        Transform spot = FreeSpot();
        if (cat == null || spot == null) return false;

        // Out of its cage: the cage is free for a new cat
        Transform cage = CageManager.Instance.CageOf(cat);
        if (cage != null) CageManager.Instance.FreeCage(cage);

        cat.IsResident = true;
        cat.OnHold = false;
        cat.CatName = catName;
        cat.MoveTo(spot);                     // keeps its size and stands it on the surface (see BaseCat)
        residents[spot] = cat;

        Juice.Squash(cat.transform);
        MessageUI.Notify($"{catName} lives here now!", ToastKind.Heart);
        AudioManager.Play(Sfx.ResidentJoins);
        return true;
    }

    /// <summary>
    /// Makes a rare cat available to adopters: a resident moves back into a free cage,
    /// an on-hold cat simply comes off hold. Returns false if a resident has no free cage to go to.
    /// </summary>
    public bool PutUpForAdoption(BaseCat cat)
    {
        if (cat == null) return false;

        if (cat.IsResident)
        {
            Transform cage = CageManager.Instance.GetAvailableCage();
            if (cage == null) return false;   // every cage is full

            Transform spot = SpotOf(cat);
            if (spot != null) residents.Remove(spot);

            cat.IsResident = false;
            CageManager.Instance.OccupyCage(cage, cat);
        }

        cat.OnHold = false;
        string who = string.IsNullOrEmpty(cat.CatName) ? "The rare cat" : cat.CatName;
        MessageUI.Notify($"{who} is looking for a home.", ToastKind.Info);
        return true;
    }

    private Transform FreeSpot()
    {
        foreach (Transform spot in spots)
        {
            if (spot != null && !residents.ContainsKey(spot)) return spot;
        }
        return null;
    }

    private Transform SpotOf(BaseCat cat)
    {
        foreach (var pair in residents)
        {
            if (pair.Value == cat) return pair.Key;
        }
        return null;
    }
}
