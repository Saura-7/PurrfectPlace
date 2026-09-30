using UnityEngine;
using UnityEngine.InputSystem;

public class CareSubsystem : MonoBehaviour
{
    private readonly string systemName = "CareSubsystem";
    
    [Header("Economy Settings")]
    public int careCost = 10; // Cost to upgrade 1 stat point
    [SerializeField] private float maxInteractionDistance = 5f; // Max distance to interact with a cat

    private InputSystem_Actions inputActions;
    private InputAction LeftShoulder;
    private InputAction RightShoulder;
    // private InputAction interact1Action;

    void OnEnable()
    {
        inputActions = new InputSystem_Actions();
        RightShoulder = inputActions.Player.FeedCat;
        LeftShoulder = inputActions.Player.CleanCat;
        // interact1Action = inputActions.Player.Interact1;
        inputActions.Enable();
    }

 private void Update()
{
    // 1. DRAW THE RAYCAST EVERY FRAME (Continuous Visualization)
    if (Camera.main != null)
    {
        // CRITICAL: This must be Camera.main.transform, not just "transform"
        Vector3 rayOrigin = Camera.main.transform.position;
        Vector3 rayDirection = Camera.main.transform.forward;
        
        // Draws a permanent blue laser in the Scene view exactly where the camera is looking
        Debug.DrawRay(rayOrigin, rayDirection * maxInteractionDistance, Color.blue);
    }

    // 2. CHECK FOR INPUTS
    if (LeftShoulder.WasPressedThisFrame())
    {
        TryCareForCat(isFeeding: false); 
    }
    else if (RightShoulder.WasPressedThisFrame()) 
    {
        TryCareForCat(isFeeding: true); 
    }
}

private void TryCareForCat(bool isFeeding)
{
    if (Camera.main == null) return;

    // 3. CREATE THE SAME RAY FOR THE ACTUAL INTERACTION
    Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);

    if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance))
    {
        Debug.Log($"Raycast hit: {hit.collider.name}");

        BaseCat clickedCat = hit.collider.GetComponent<BaseCat>();

        if (clickedCat != null)
        {
            // Economy and Care Logic
            int currentMoney = CoreEconomySystem.Instance.GetGP(systemName);

            if (currentMoney >= careCost)
            {
                CoreEconomySystem.Instance.ModifyGP(-careCost, systemName);

                if (isFeeding) { clickedCat.UpgradeFeeding(); }
                else { clickedCat.UpgradeCleanliness(); }

                clickedCat.PlayInteractAnimation();
            }
            else
            {
                Debug.LogWarning("Not enough GP!");
            }
        }
        else
        {
            Debug.LogWarning("Raycast hit an object, but it is not a Cat.");
        }
    }
    else
    {
        Debug.Log("Raycast hit nothing.");
    }
}
}