using UnityEngine;
using UnityEngine.InputSystem;

public class DebugSpawner : MonoBehaviour
{

    private InputSystem_Actions inputActions;
    private InputAction interactAction;
    // private InputAction interact1Action;

    void OnEnable()
    {
        inputActions = new InputSystem_Actions();
        interactAction = inputActions.Player.Interact;
        // interact1Action = inputActions.Player.Interact1;
        inputActions.Enable();
    }

    void Update()
    {
        // Press Space to spawn a new random cat at the center of the screen
        if (interactAction.WasPressedThisFrame())
        {
            CatFactory.Instance.GenerateRandomCat(Vector3.zero + new Vector3(0, 1, 0));
        }
    }
}