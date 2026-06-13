using UnityEngine;
using UnityEngine.InputSystem;

public class FollowCamera : MonoBehaviour
{
    [SerializeField] public Transform target;
    [SerializeField] private Vector3 targetOffset     = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float   distance         = 4f;
    [SerializeField] private float   mouseSensitivity = 0.2f;
    [SerializeField] private float   minPitch         = -20f;
    [SerializeField] private float   maxPitch         = 60f;

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        yaw = transform.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Camera look is suspended while the Escape menu is open (cursor is unlocked),
        // but the camera keeps following the target.
        if (!GameMenu.IsOpen)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw   += delta.x * mouseSensitivity;
            pitch -= delta.y * mouseSensitivity;
            pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focusPoint  = target.position + targetOffset;
        transform.position  = focusPoint - rotation * Vector3.forward * distance;
        transform.rotation  = rotation;
    }
}
