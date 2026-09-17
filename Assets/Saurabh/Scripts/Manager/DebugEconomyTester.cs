using UnityEngine;
using UnityEngine.InputSystem;

public class DebugEconomyTester : MonoBehaviour
{
    private readonly string systemName = "DebugTester";
    private InputSystem_Actions inputActions;
    private InputAction interactAction;
    private InputAction interact1Action;

    void OnEnable()
    {
        inputActions = new InputSystem_Actions();
        interactAction = inputActions.Player.Interact;
        interact1Action = inputActions.Player.Interact1;
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void Update()
    {
        if (CoreEconomySystem.Instance == null)
        {
            return;
        }

        if (interactAction.WasPressedThisFrame())
        {
            CoreEconomySystem.Instance.ModifyGP(15, systemName);
        }

        if (interact1Action.WasPressedThisFrame())
        {
            CoreEconomySystem.Instance.ModifyReputation(5, systemName);
        }
    }
}