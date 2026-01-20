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
    [SerializeField] private bool enableTurnInPlace = true;
    [SerializeField] private float turnInPlaceInputThreshold = 0.2f;
    [SerializeField] private float turnInPlaceHoldTime = 0.2f;
    [SerializeField] private float turnInPlaceInputMax = 0.6f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -15f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float currentSpeed;
    private Transform cameraTransform;
    private Animator animator;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float minJumpAirTime = 1f;
    private int lastAnimatorStateHash;
    private float groundCheckDisableUntil;
    private float groundedSince;
    private bool jumpLocked;
    private float lastJumpTime = -10f;
    private readonly int idleHash = Animator.StringToHash("Idle");
    private readonly int walkHash = Animator.StringToHash("Walk");
    private float turnInPlaceTimer;

    // Direct Input Action references
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = Camera.main.transform;

        // Get animator from character model child
        animator = GetComponentInChildren<Animator>();

        if (minJumpAirTime < 0.6f)
        {
            minJumpAirTime = 0.6f;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[{Time.time:F2}] Jump config | minJumpAirTime={minJumpAirTime:F2}");
            Debug.Log($"[{Time.time:F2}] ThirdPersonController awake | name={gameObject.name} | id={GetInstanceID()}");
        }

        // Get the Input Actions directly
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions["Move"];
            jumpAction = playerInput.actions["Jump"];
            sprintAction = playerInput.actions["Sprint"];
        }

    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.Enable();
        if (jumpAction != null) jumpAction.Enable();
        if (sprintAction != null) sprintAction.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.Disable();
        if (jumpAction != null) jumpAction.Disable();
        if (sprintAction != null) sprintAction.Disable();
    }

    private void Update()
    {
        CheckGround();
        HandleMovement();
        HandleGravity();
        HandleJump();

        // CRITICAL: Ensure Apply Root Motion stays disabled
        if (animator != null && animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
        }

        if (enableDebugLogs)
        {
            LogAnimatorStateChange();
        }
    }

    private void CheckGround()
    {
        // Account for CharacterController center offset when calculating ground check position
        Vector3 spherePosition = transform.position + controller.center - new Vector3(0, controller.height / 2 - controller.radius, 0);
        bool groundHit = Physics.CheckSphere(spherePosition, controller.radius + groundCheckDistance, groundMask);
        bool allowGrounding = Time.time >= groundCheckDisableUntil && velocity.y <= 0.1f;
        bool wasGrounded = isGrounded;
        isGrounded = allowGrounding && groundHit;

        if (isGrounded)
        {
            if (groundedSince <= 0f)
            {
                groundedSince = Time.time;
            }

            if (Time.time - groundedSince > 0.05f && Time.time - lastJumpTime > minJumpAirTime)
            {
                jumpLocked = false;
            }
        }
        else
        {
            groundedSince = 0f;
        }

        // Jump-related log disabled for now; keep other debug logs active.

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Step down: when grounded, snap character to the ground surface
        // This helps the character "stick" to stairs and slopes instead of floating
        if (isGrounded && velocity.y <= 0)
        {
            ApplyStepDown();
        }
    }

    private void ApplyStepDown()
    {
        // Cast from the bottom of the capsule to find the actual ground surface
        Vector3 capsuleBottom = transform.position + controller.center - Vector3.up * (controller.height / 2 - controller.radius);
        float rayLength = controller.stepOffset + controller.skinWidth + 0.1f;

        // Use SphereCast to match the capsule's bottom shape
        if (Physics.SphereCast(capsuleBottom, controller.radius * 0.9f, Vector3.down, out RaycastHit hit, rayLength, groundMask))
        {
            // Calculate how far above the ground we are
            float groundY = hit.point.y;
            float characterBottomY = transform.position.y;
            float gap = characterBottomY - groundY;

            // If there's a gap and it's within step-down range, snap down
            if (gap > 0.02f && gap < controller.stepOffset)
            {
                controller.Move(Vector3.down * gap);

                if (enableDebugLogs)
                {
                    Debug.Log($"[{Time.time:F2}] Step down: gap={gap:F3}m");
                }
            }
        }
    }

    private void HandleMovement()
    {
        if (moveAction == null || cameraTransform == null) return;

        // Read movement input
        Vector2 moveInput = moveAction.ReadValue<Vector2>();

        // Get camera-relative directions
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        bool horizontalOnly = Mathf.Abs(moveInput.y) < turnInPlaceInputThreshold
            && Mathf.Abs(moveInput.x) > turnInPlaceInputThreshold
            && Mathf.Abs(moveInput.x) < turnInPlaceInputMax;

        if (enableTurnInPlace && isGrounded && horizontalOnly)
        {
            turnInPlaceTimer += Time.deltaTime;
        }
        else
        {
            turnInPlaceTimer = 0f;
        }

        bool shouldTurnInPlace = enableTurnInPlace
            && isGrounded
            && horizontalOnly
            && turnInPlaceTimer < turnInPlaceHoldTime;

        Vector3 desiredMoveDirection = shouldTurnInPlace
            ? right * Mathf.Sign(moveInput.x)
            : forward * moveInput.y + right * moveInput.x;

        // Check if sprinting
        bool isSprinting = sprintAction != null
            && sprintAction.IsPressed()
            && moveInput.sqrMagnitude > 0.01f
            && !shouldTurnInPlace;
        float targetSpeed = shouldTurnInPlace ? 0f : (isSprinting ? sprintSpeed : walkSpeed);

        // Smoothly accelerate
        if (desiredMoveDirection.magnitude > 0.1f)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

            // Rotate character
            Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0, acceleration * Time.deltaTime);
        }

        // Apply movement
        Vector3 move = shouldTurnInPlace ? Vector3.zero : desiredMoveDirection.normalized * currentSpeed;
        controller.Move(move * Time.deltaTime);

        // Update animator parameters
        if (animator != null)
        {
            // Normalize speed to 0-1 range based on walk speed
            float normalizedSpeed = currentSpeed / walkSpeed;
            animator.SetFloat("Speed", normalizedSpeed);

            if (HasParameter(animator, "IsGrounded"))
            {
                animator.SetBool("IsGrounded", isGrounded);
            }

            // Set sprint state (if parameter exists)
            if (HasParameter(animator, "IsSprinting"))
            {
                animator.SetBool("IsSprinting", isSprinting);
            }

            // Set directional blend tree parameters (for 8-directional sprint)
            if (HasParameter(animator, "Horizontal"))
            {
                animator.SetFloat("Horizontal", moveInput.x);
            }
            if (HasParameter(animator, "Vertical"))
            {
                animator.SetFloat("Vertical", moveInput.y);
            }

        }
    }

    private void HandleGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleJump()
    {
        bool groundedStable = groundedSince > 0f && Time.time - groundedSince > 0.05f;
        if (jumpAction != null && jumpAction.WasPressedThisFrame())
        {
            // Jump-related log disabled for now; keep other debug logs active.

            bool airTimeSatisfied = Time.time - lastJumpTime > minJumpAirTime;
            if (isGrounded && groundedStable && !jumpLocked && airTimeSatisfied)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                lastJumpTime = Time.time;
                groundCheckDisableUntil = Time.time + minJumpAirTime;
                isGrounded = false;
                jumpLocked = true;

                if (animator != null && HasParameter(animator, "Jump"))
                {
                    animator.SetTrigger("Jump");
                }

                // Jump-related log disabled for now; keep other debug logs active.
            }
            // Jump-related log disabled for now; keep other debug logs active.
        }
    }

    private void LateUpdate()
    {
        if (animator != null && HasParameter(animator, "VerticalVelocity"))
        {
            animator.SetFloat("VerticalVelocity", velocity.y);
        }
    }


    private void LogAnimatorStateChange()
    {
        if (animator == null) return;
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int stateHash = stateInfo.fullPathHash;
        if (stateHash != lastAnimatorStateHash)
        {
            lastAnimatorStateHash = stateHash;
            string clipName = "";
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips.Length > 0 && clips[0].clip != null)
            {
                clipName = clips[0].clip.name;
            }

            Debug.Log($"[{Time.time:F2}] Animator state: {stateInfo.shortNameHash} | clip={clipName} | normalizedTime={stateInfo.normalizedTime:F2} | grounded={isGrounded} | velocityY={velocity.y:F2}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (controller != null)
        {
            Vector3 spherePosition = transform.position + controller.center - new Vector3(0, controller.height / 2 - controller.radius, 0);
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(spherePosition, controller.radius + groundCheckDistance);
        }
    }

    private bool HasParameter(Animator animator, string paramName)
    {
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
            {
                return true;
            }
        }
        return false;
    }
}
