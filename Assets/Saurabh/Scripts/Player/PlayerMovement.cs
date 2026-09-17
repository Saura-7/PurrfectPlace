using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    CharacterController controller;
    float walkSpeed = 10f;
    float gravity = -24.5f;
    float jumpHeight = 1.5f;

    InputSystem_Actions inputSystemActions;
    InputAction moveAction;
    InputAction sprintAction;
    InputAction jumpAction;
    Vector3 move;
    float verticalVelocity;

    enum MovementState
    {
        Walking,
        Running,
        Idle,
    }

    [SerializeField] MovementState currentState;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        inputSystemActions = new InputSystem_Actions();
        moveAction = inputSystemActions.Player.Move;
        sprintAction = inputSystemActions.Player.Sprint;
        jumpAction = inputSystemActions.Player.Jump;

        inputSystemActions.Enable();
    }

    void OnDisable()
    {
        inputSystemActions.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 inputVector = moveAction.ReadValue<Vector2>();
        float inputX = inputVector.x;
        float inputZ = inputVector.y;

        //Movement
        move = transform.right * inputX + transform.forward * inputZ;
        move.Normalize();
        StateSwitcher();
    }

    #region State Logic
    void StateSwitcher()
    {
        if(move.magnitude > 0.1f)
        {
            if(sprintAction.IsPressed())
            {
                currentState = MovementState.Running;
            }
            else
            {
                currentState = MovementState.Walking;
            }
        }
        else
        {
            currentState = MovementState.Idle;
        }

        MovePlayer();
    }

    void MovePlayer()
    {
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpAction.WasPressedThisFrame())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        verticalVelocity += gravity * Time.deltaTime;

        float movementSpeed = 0f;
        switch(currentState)
        {
            case MovementState.Walking:
                movementSpeed = walkSpeed;
                break;
            case MovementState.Running:
                movementSpeed = walkSpeed * 2f;
                break;
        }

        Vector3 velocity = move * movementSpeed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }   
    #endregion State Logic
}