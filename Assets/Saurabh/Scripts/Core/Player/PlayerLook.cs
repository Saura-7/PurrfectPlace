using UnityEngine;
using Unity.Cinemachine;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] Transform playerBody;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; // Lock the cursor to the center of the screen
    }

    void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(UpdatePlayerBodyRotation);
    }

    void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(UpdatePlayerBodyRotation);
    }

    void UpdatePlayerBodyRotation(CinemachineBrain brain)
    {
        if (playerBody == null || brain == null || brain.OutputCamera == null || brain.OutputCamera.transform != transform) return;

        Vector3 cameraForward = transform.forward;
        cameraForward.y = 0f;
        if (cameraForward.sqrMagnitude < 0.0001f) return;

        playerBody.rotation = Quaternion.LookRotation(cameraForward, Vector3.up);
    }
}
