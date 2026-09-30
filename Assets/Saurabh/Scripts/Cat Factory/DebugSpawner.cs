using UnityEngine;
using UnityEngine.InputSystem;

public class DebugSpawner : MonoBehaviour
{

    private InputSystem_Actions inputActions;
    private InputAction SpawnCatAction;
    // private InputAction interact1Action;

    void OnEnable()
    {
        inputActions = new InputSystem_Actions();
        SpawnCatAction = inputActions.Player.SpawnCat;
        // interact1Action = inputActions.Player.Interact1;
        inputActions.Enable();
    }

    void Update()
    {
        // Press Space to spawn a new random cat at the center of the screen
        if (SpawnCatAction.WasPressedThisFrame())
        {
            CatFactory.Instance.GenerateRandomCat(Vector3.zero + new Vector3(0, 1, 0));
        }
    }
}