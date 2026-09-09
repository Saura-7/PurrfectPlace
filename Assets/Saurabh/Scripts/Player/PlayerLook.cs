using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] public float mouseSens = 150f;
    [SerializeField] Transform playerBody;
    float xRotation;
    InputSystem_Actions inputActions;
    InputAction lookAction;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; // Lock the cursor to the center of the screen
    }

    void OnEnable()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        lookAction = inputActions.Player.Look;
    }

    void Update()
    {
        Vector2 lookVector = lookAction.ReadValue<Vector2>();
        float lookX = lookVector.x * mouseSens * Time.deltaTime;
        float lookY = lookVector.y * mouseSens * Time.deltaTime;

        xRotation -= lookY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerBody.Rotate(Vector3.up * lookX);//To rotate the parent object which is the player.
        transform.localRotation = Quaternion.Euler(xRotation, 90f, 0f);
    }

    void OnDisable()
    {
        inputActions.Disable();
    }
}
