using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float acceleration = 10f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -15f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private Vector2 moveInput;
    private bool sprintInput;
    private float currentSpeed;
    private Transform cameraTransform;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        CheckGround();
        HandleMovement();
        HandleGravity();
    }

    private void CheckGround()
    {
        // Ground check using raycast from center of capsule
        Vector3 spherePosition = transform.position - new Vector3(0, controller.height / 2 - controller.radius, 0);
        isGrounded = Physics.CheckSphere(spherePosition, controller.radius + groundCheckDistance, groundMask);

        // Reset velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }

    private void HandleMovement()
    {
        // Get input relative to camera direction
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // Flatten the vectors to only move on horizontal plane
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        // Calculate movement direction
        Vector3 desiredMoveDirection = forward * moveInput.y + right * moveInput.x;

        // Calculate target speed based on sprint
        float targetSpeed = sprintInput ? sprintSpeed : walkSpeed;

        // Smoothly accelerate to target speed
        if (desiredMoveDirection.magnitude > 0.1f)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

            // Rotate character to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            // Decelerate when no input
            currentSpeed = Mathf.Lerp(currentSpeed, 0, acceleration * Time.deltaTime);
        }

        // Apply movement
        Vector3 move = desiredMoveDirection.normalized * currentSpeed;
        controller.Move(move * Time.deltaTime);
    }

    private void HandleGravity()
    {
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // Input System callbacks
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintInput = context.ReadValueAsButton();
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize ground check sphere
        if (controller != null)
        {
            Vector3 spherePosition = transform.position - new Vector3(0, controller.height / 2 - controller.radius, 0);
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(spherePosition, controller.radius + groundCheckDistance);
        }
    }
}
