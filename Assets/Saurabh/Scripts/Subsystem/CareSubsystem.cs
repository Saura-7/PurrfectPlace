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
        RightShoulder = inputActions.Player.Interact3;
        LeftShoulder = inputActions.Player.Interact4;
        // interact1Action = inputActions.Player.Interact1;
        inputActions.Enable();
    }

    private void Update()
    {
        // 0 = Left Click (Feed), 1 = Right Click (Clean)
        if (LeftShoulder.WasPressedThisFrame())
        {
            TryCareForCat(isFeeding: true);
        }
        else if (RightShoulder.WasPressedThisFrame()) 
        {
            TryCareForCat(isFeeding: false);
        }
    }

    private void TryCareForCat(bool isFeeding)
    {
        if (Camera.main == null) return;

        // 1. Calculate the ray from the center of the camera
        Vector3 centerScreen = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        Ray ray = Camera.main.ScreenPointToRay(centerScreen);

        Color debugColor = Color.red; // Default color when aiming at nothing

        // 2. If the ray hits a physical object in the scene...
        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance))
        {
            // 3. Check if the object we hit is actually a Cat
            BaseCat clickedCat = hit.collider.GetComponent<BaseCat>();

            if (clickedCat != null)
            {
                // 4. Check if the cat is already maxed out on this stat
                if (isFeeding && clickedCat.feedingLevel >= 6)
                {
                    Debug.Log("Cat is already completely full!");
                    return; // Stop running code
                }
                if (!isFeeding && clickedCat.cleanlinessLevel >= 6)
                {
                    Debug.Log("Cat is already perfectly clean!");
                    return; // Stop running code
                }

                // 5. ECONOMY CHECK: Do we have $10?
                int currentMoney = CoreEconomySystem.Instance.GetGP(systemName);

                if (currentMoney >= careCost)
                {
                    // 6. PROCESS TRANSACTION: Deduct money using the Audit Trail
                    CoreEconomySystem.Instance.ModifyGP(-careCost, systemName);

                    // 7. COMMAND THE PUPPET: Upgrade the stat and play animation
                    if (isFeeding)
                    {
                        clickedCat.UpgradeFeeding();
                        Debug.Log($"Fed {clickedCat.name}! Feeding Level is now {clickedCat.feedingLevel}.");
                    }
                    else
                    {
                        clickedCat.UpgradeCleanliness();
                        Debug.Log($"Cleaned {clickedCat.name}! Cleanliness Level is now {clickedCat.cleanlinessLevel}.");
                    }

                    // Tell the cat to react visually
                    clickedCat.PlayInteractAnimation();
                }
                else
                {
                    Debug.LogWarning("Not enough GP to care for the cat!");
                }
            }
        }
    }
}