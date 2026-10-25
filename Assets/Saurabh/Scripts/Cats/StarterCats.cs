using UnityEngine;

/// <summary>
/// Puts a few cats in the enclosures when the game starts,
/// so the first prep phase has something to care for.
/// Runs once per game (Start only runs when the scene loads).
/// </summary>
public class StarterCats : MonoBehaviour
{
    [Header("Starter Cats")]
    [SerializeField] private int catCount = 2;
    [SerializeField, Range(0, BaseCat.MaxLevel)] private int startFeeding = 0;
    [SerializeField, Range(0, BaseCat.MaxLevel)] private int startCleanliness = 0;

    private void Start()
    {
        for (int i = 0; i < catCount; i++)
        {
            // 1. Find a free enclosure. Stop if every enclosure is already full.
            Transform cage = CageManager.Instance.GetAvailableCage();
            if (cage == null) break;

            // 2. Make a random cat at the enclosure's position
            BaseCat cat = CatFactory.Instance.GenerateRandomCat(cage.position);

            // 3. Set its care levels AFTER it is made, because InitializeCat resets them to 0
            cat.feedingLevel = startFeeding;
            cat.cleanlinessLevel = startCleanliness;

            // 4. Register it as stock and place it inside the enclosure
            CageManager.Instance.OccupyCage(cage, cat);
        }
    }
}