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
    /// Generates a completely random cat at the given location.
    /// </summary>
    public BaseCat GenerateRandomCat(Vector3 spawnPosition)
    {
        // 1. Roll the dice for the DNA (Enums)
        CatBehavior randomBehavior = GetRandomEnum<CatBehavior>();
        CatBody randomBody = GetRandomEnum<CatBody>();
        CatPattern randomPattern = GetRandomEnum<CatPattern>();

        // 2. Spawn the blank prototype cube
        GameObject newCatObj = Instantiate(catPrefab, spawnPosition, Quaternion.identity);
        BaseCat catScript = newCatObj.GetComponent<BaseCat>();

        // 3. Select the Material based on the Pattern enum
        Material selectedMat = patternMaterials[(int)randomPattern];

        // 4. Initialize the data inside the cat
        catScript.InitializeCat(randomBehavior, randomBody, randomPattern, selectedMat, null);

        // 5. PROTOTYPE ONLY: Change the physical shape of the cube based on Body Type
        ApplyPrototypeScale(newCatObj.transform, randomBody);

        // 6. Rename the object in the Hierarchy for easy debugging
        newCatObj.name = $"Cat [{randomPattern} | {randomBody} | {randomBehavior}]";

        return catScript;
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
        switch (bodyType)
        {
            case CatBody.Normal:
                catTransform.localScale = new Vector3(1f, 1f, 1f); // Standard Cube
                break;
            case CatBody.Chonky:
                catTransform.localScale = new Vector3(1.5f, 1f, 1.5f); // Wide Cube
                break;
            case CatBody.Skinny:
                catTransform.localScale = new Vector3(0.5f, 1f, 0.5f); // Thin Cube
                break;
        }
    }
}