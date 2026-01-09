using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    [Header("Camera Settings")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float zoomSpeed = 2f;

    [Header("Rotation")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gamepadSensitivity = 100f;
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float minVerticalAngle = -40f;
    [SerializeField] private float maxVerticalAngle = 70f;

    [Header("Collision")]
    [SerializeField] private float collisionOffset = 0.3f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private Vector2 lookInput;
    private float rotationX = 0f;
    private float rotationY = 0f;
    private Vector3 currentVelocity;
    private bool isGamepad;

    private void Start()
    {
        // Hide cursor in play mode
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize rotation to current camera rotation
        Vector3 angles = transform.eulerAngles;
        rotationX = angles.y;
        rotationY = angles.x;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        HandleRotation();
        HandlePosition();
    }

    private void HandleRotation()
    {
        // Determine sensitivity based on input device
        float sensitivity = isGamepad ? gamepadSensitivity : mouseSensitivity;

        // Apply rotation input
        rotationX += lookInput.x * sensitivity * Time.deltaTime;
        rotationY -= lookInput.y * sensitivity * Time.deltaTime;

        // Clamp vertical rotation
        rotationY = Mathf.Clamp(rotationY, minVerticalAngle, maxVerticalAngle);
    }

    private void HandlePosition()
    {
        // Calculate desired position
        Vector3 targetPosition = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(rotationY, rotationX, 0);
        Vector3 desiredPosition = targetPosition - rotation * Vector3.forward * distance;

        // Check for collision
        Vector3 direction = desiredPosition - targetPosition;
        if (Physics.Raycast(targetPosition, direction.normalized, out RaycastHit hit, distance, collisionMask))
        {
            desiredPosition = hit.point + hit.normal * collisionOffset;
        }

        // Smoothly move camera
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, rotationSmoothTime);
        transform.LookAt(targetPosition);
    }

    // Input System callbacks
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();

        // Detect if using gamepad (analog stick has larger values compared to mouse delta)
        isGamepad = context.control.device is UnityEngine.InputSystem.Gamepad;
    }

    public void OnScroll(InputAction.CallbackContext context)
    {
        // Handle mouse scroll wheel for zoom (optional)
        Vector2 scrollValue = context.ReadValue<Vector2>();
        distance = Mathf.Clamp(distance - scrollValue.y * zoomSpeed * 0.1f, minDistance, maxDistance);
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize target offset
        if (target != null)
        {
            Vector3 targetPosition = target.position + targetOffset;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetPosition, 0.3f);
        }
    }
}
