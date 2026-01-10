using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    [Header("Camera Settings")]
    [SerializeField] private float distance = 1.25f;
    [SerializeField] private float minDistance = 0.3f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float zoomSpeed = 15f;

    [Header("Rotation")]
    [SerializeField] private float mouseSensitivity = 32f;
    [SerializeField] private float gamepadSensitivity = 1600f;
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float minVerticalAngle = -40f;
    [SerializeField] private float maxVerticalAngle = 70f;

    [Header("Collision")]
    [SerializeField] private float collisionOffset = 0.3f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private Vector3 currentVelocity;

    // Direct Input Action reference
    private InputAction lookAction;
    private PlayerInput playerInput;

    private void Start()
    {
        // Hide and lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize rotation
        Vector3 angles = transform.eulerAngles;
        rotationX = angles.y;
        rotationY = angles.x;

        // Find player input
        if (target != null)
        {
            playerInput = target.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                lookAction = playerInput.actions["Look"];
                lookAction.Enable();
            }
        }
    }

    private void OnDisable()
    {
        if (lookAction != null)
        {
            lookAction.Disable();
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleZoom();
        HandleRotation();
        HandlePosition();
    }

    private void HandleZoom()
    {
        // Read mouse scroll wheel input
        float scroll = UnityEngine.InputSystem.Mouse.current?.scroll.ReadValue().y ?? 0f;

        if (scroll != 0f)
        {
            // Scroll up = zoom in (decrease distance), scroll down = zoom out (increase distance)
            // Mouse scroll typically gives values around 120/-120, so normalize it
            distance -= (scroll / 120f) * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    private void HandleRotation()
    {
        if (lookAction == null) return;

        // Read look input
        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        // Detect input device and apply sensitivity
        bool isGamepad = lookAction.activeControl?.device is Gamepad;
        float sensitivity = isGamepad ? gamepadSensitivity : mouseSensitivity;

        // Apply rotation
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

    private void OnDrawGizmosSelected()
    {
        if (target != null)
        {
            Vector3 targetPosition = target.position + targetOffset;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetPosition, 0.3f);
        }
    }
}
