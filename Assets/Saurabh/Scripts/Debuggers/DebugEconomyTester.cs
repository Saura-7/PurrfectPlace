using UnityEngine;
using UnityEngine.InputSystem;

public class DebugEconomyTester : MonoBehaviour
{
    private readonly string systemName = "DebugTester";
    private InputSystem_Actions inputActions;
    private InputAction GetGPAction;
    private InputAction GetReputationAction;

    void OnEnable()
    {

        if(!Debug.isDebugBuild)
        {
            enabled = false;
            return;
        }   
        inputActions = new InputSystem_Actions();
        GetGPAction = inputActions.Player.MoneyIncrease;
        GetReputationAction = inputActions.Player.RepIncrease;
        inputActions.Enable();
    }

    void OnDisable()
    {
        if (inputActions != null) inputActions.Disable();
    }

    void Update()
    {
        if (CoreEconomySystem.Instance == null)
        {
            return;
        }

        if (GetGPAction.WasPressedThisFrame())
        {
            CoreEconomySystem.Instance.ModifyGP(15, systemName);
        }

        if (GetReputationAction.WasPressedThisFrame())
        {
            CoreEconomySystem.Instance.ModifyReputation(5, systemName);
        }
    }
}