using UnityEngine;

public class CatFactory : MonoBehaviour
{
    // Singleton so subsystems (like a Delivery Truck) can easily call it
    public static CatFactory Instance { get; private set; }

    [Header("The Blueprint")]
    public GameObject catPrefab; // Drag your Cat_Prototype cube here

    [Header("Prototype Assets (Programmer Art)")]
    // 0=Solid(Red), 1=Spotted(Blue), 2=Calico(Green), 3=Tuxedo(Black)
    public Material[] patternMaterials; 

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    /// <summary>
    /// Generates a cat at the given location. Any trait you don't pass is random.
    /// "CatBehavior?" means "a CatBehavior OR null" (null = pick one at random).
    ///     GenerateRandomCat(pos)                              → fully random
    ///     GenerateRandomCat(pos, behavior: CatBehavior.Lazy) → lazy, the rest random
    /// </summary>
    public BaseCat GenerateRandomCat(Vector3 spawnPosition,
                                     CatBehavior? behavior = null, CatBody? body = null, CatPattern? pattern = null)
    {
        GameObject newCatObj = Instantiate(catPrefab, spawnPosition, Quaternion.identity);
        return SetupCat(newCatObj, behavior, body, pattern);
    }

    // --- Helper Methods ---

    // A generic helper to pick a random item from any Enum
    private T GetRandomEnum<T>()
    {
        System.Array values = System.Enum.GetValues(typeof(T));
        return (T)values.GetValue(Random.Range(0, values.Length));
    }

    // A temporary method to visualize the body types without 3D models
    private void ApplyPrototypeScale(Transform catTransform, CatBody bodyType)
{
    Vector3 baseScale = catTransform.localScale; // keep the model's own scale (118.55)

    switch (bodyType)
    {
        case CatBody.Normal:
            break; // leave it as it is
        case CatBody.Chonky:
            catTransform.localScale = new Vector3(baseScale.x * 1.5f, baseScale.y * 1.5f, baseScale.z * 1.5f);
            break;
        case CatBody.Skinny:
            catTransform.localScale = new Vector3(baseScale.x * 0.5f, baseScale.y * 0.5f, baseScale.z * 0.5f);
            break;
    }
}

        // Add this method to your existing CatFactory.cs
    public BaseCat GenerateGiverCat(Transform handPoint)
    {
        // Spawn without a parent first, so the cat keeps its normal size
        GameObject newCatObj = Instantiate(catPrefab, handPoint.position, handPoint.rotation);
        // "true" = keep the cat's world size when attaching it to the hand
        newCatObj.transform.SetParent(handPoint, true);
        BaseCat newCat = SetupCat(newCatObj);

        // IMPORTANT: set the stats AFTER SetupCat, because InitializeCat resets them to 0
        newCat.feedingLevel = Random.Range(0, 3);      // 0, 1 or 2 (the max value is excluded)
        newCat.cleanlinessLevel = Random.Range(0, 3);  // 0, 1 or 2

        return newCat;
    }

    // Shared setup used by every way of creating a cat
    private BaseCat SetupCat(GameObject catObj,
                             CatBehavior? behavior = null, CatBody? body = null, CatPattern? pattern = null)
    {
        // 1. Roll the dice for the DNA (Enums), unless a trait was chosen.
        // "a ?? b" means "use a, but if a is null use b".
        CatBehavior randomBehavior = behavior ?? GetRandomEnum<CatBehavior>();
        CatBody randomBody = body ?? GetRandomEnum<CatBody>();
        CatPattern randomPattern = pattern ?? GetRandomEnum<CatPattern>();

        // 2. Pick the material from the Pattern enum
        BaseCat catScript = catObj.GetComponent<BaseCat>();
        Material selectedMat = patternMaterials[(int)randomPattern];

        // 3. Initialize the data inside the cat
        catScript.InitializeCat(randomBehavior, randomBody, randomPattern, selectedMat, null);

        // 4. PROTOTYPE ONLY: change the shape of the cube based on body type
        ApplyPrototypeScale(catObj.transform, randomBody);

        // 5. Rename for easy debugging in the Hierarchy
        catObj.name = $"Cat [{randomPattern} | {randomBody} | {randomBehavior}]";

        return catScript;
    }
}