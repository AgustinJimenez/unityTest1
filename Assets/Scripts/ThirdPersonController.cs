using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float turnInPlaceRotationSpeed = 0.5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private bool rotateToCameraYaw = true;
    [SerializeField] private bool enableTurnInPlace = false;
    [SerializeField] private bool logTurnInPlaceToggle = true;
    [SerializeField] private float turnInPlaceInputThreshold = 0.05f;
    [SerializeField] private float turnInPlaceInputMax = 0.95f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -15f;

    [Header("Debug Clip Play")]
    [SerializeField] private bool enableDebugPlayClip = true;
    [SerializeField] private bool debugPlayTurnInPlace = true;
    [SerializeField] private string debugTurnRightClipName = "Turn01_Right";
    [SerializeField] private string debugTurnLeftClipName = "Turn01_Left";
    [SerializeField] private string playClipStateName = "Run01_Forward";
    [SerializeField] private int playClipLayer = 0;
    [SerializeField] private bool lockMovementDuringClip = true;
    [SerializeField] private bool usePlayableGraphForDebugClip = true;
    [SerializeField] private float debugTurnAngleDegrees = 90f;

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
    [SerializeField] private bool showTurnInPlaceDebug = true;
    private bool lastShouldTurnInPlace;
    private bool isPlayingDebugClip;
    private float debugClipEndTime;
    private PlayableGraph debugGraph;
    private AnimationClipPlayable debugClipPlayable;
    private float debugTurnRemaining;
    private float debugTurnSpeed;

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
        HandleDebugClipPlay();
        HandleDebugTurnRotation();
        HandleMovement();
        HandleGravity();
        HandleJump();
        HandleDebugToggles();

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

    private void HandleDebugToggles()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            enableTurnInPlace = !enableTurnInPlace;
            if (logTurnInPlaceToggle)
            {
                Debug.Log($"Turn-in-place toggled: {enableTurnInPlace}");
            }
        }
    }

    private void HandleDebugClipPlay()
    {
        if (!enableDebugPlayClip || animator == null || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            string clipToPlay = playClipStateName;
            if (debugPlayTurnInPlace)
            {
                float inputX = moveAction != null ? moveAction.ReadValue<Vector2>().x : 1f;
                float turnSign = inputX < 0f ? -1f : 1f;
                clipToPlay = turnSign < 0f ? debugTurnLeftClipName : debugTurnRightClipName;
                debugTurnRemaining = debugTurnAngleDegrees * turnSign;
            }

            if (string.IsNullOrWhiteSpace(clipToPlay))
            {
                Debug.LogWarning("Play clip requested, but playClipStateName is empty.");
                return;
            }

            if (usePlayableGraphForDebugClip)
            {
                if (TryPlayDebugClip(clipToPlay, out float debugClipLength))
                {
                    debugClipEndTime = Time.time + debugClipLength;
                    debugTurnSpeed = Mathf.Abs(debugTurnRemaining) > 0f ? Mathf.Abs(debugTurnRemaining) / debugClipLength : 0f;
                    isPlayingDebugClip = true;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[{Time.time:F2}] Debug clip play (PlayableGraph): {clipToPlay} length={debugClipLength:F2}s");
                    }
                }
                else
                {
                    Debug.LogWarning($"Debug clip not found: {clipToPlay}");
                }

                return;
            }

            animator.Play(clipToPlay, playClipLayer, 0f);
            animator.Update(0f);

            float clipLength = 0f;
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(playClipLayer);
            if (clips.Length > 0 && clips[0].clip != null)
            {
                clipLength = clips[0].clip.length;
            }

            if (clipLength <= 0f)
            {
                clipLength = 1f;
            }

            debugClipEndTime = Time.time + clipLength;
            debugTurnSpeed = Mathf.Abs(debugTurnRemaining) > 0f ? Mathf.Abs(debugTurnRemaining) / clipLength : 0f;
            isPlayingDebugClip = true;
            if (enableDebugLogs)
            {
                Debug.Log($"[{Time.time:F2}] Debug clip play (State): {clipToPlay} length={clipLength:F2}s");
            }
        }

        if (isPlayingDebugClip && Time.time >= debugClipEndTime)
        {
            isPlayingDebugClip = false;
            debugTurnRemaining = 0f;
            debugTurnSpeed = 0f;
            StopDebugGraph();
        }
    }

    private void HandleDebugTurnRotation()
    {
        if (!isPlayingDebugClip || !debugPlayTurnInPlace)
        {
            return;
        }

        if (Mathf.Abs(debugTurnRemaining) <= 0.01f || debugTurnSpeed <= 0f)
        {
            return;
        }

        float delta = debugTurnSpeed * Time.deltaTime;
        float step = Mathf.Min(Mathf.Abs(debugTurnRemaining), delta);
        float signedStep = Mathf.Sign(debugTurnRemaining) * step;
        transform.Rotate(0f, signedStep, 0f);
        debugTurnRemaining -= signedStep;
    }
    
    private bool TryPlayDebugClip(string clipName, out float clipLength)
    {
        clipLength = 0f;
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null)
        {
            Debug.LogWarning("Animator has no RuntimeAnimatorController.");
            return false;
        }

        AnimationClip match = null;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip != null && clip.name == clipName)
            {
                match = clip;
                break;
            }
        }

        if (match == null)
        {
            return false;
        }

        StopDebugGraph();

        debugGraph = PlayableGraph.Create("DebugClipGraph");
        debugGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(debugGraph, "DebugClipOutput", animator);
        debugClipPlayable = AnimationClipPlayable.Create(debugGraph, match);
        debugClipPlayable.SetApplyFootIK(false);
        debugClipPlayable.SetApplyPlayableIK(false);
        output.SetSourcePlayable(debugClipPlayable);
        debugGraph.Play();
        clipLength = match.length;
        return true;
    }

    private void StopDebugGraph()
    {
        if (debugGraph.IsValid())
        {
            debugGraph.Destroy();
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

        if (isPlayingDebugClip && lockMovementDuringClip)
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                if (HasParameter(animator, "IsSprinting"))
                {
                    animator.SetBool("IsSprinting", false);
                }
                if (HasParameter(animator, "Horizontal"))
                {
                    animator.SetFloat("Horizontal", 0f);
                }
                if (HasParameter(animator, "Vertical"))
                {
                    animator.SetFloat("Vertical", 0f);
                }
            }
            return;
        }

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

        bool shouldTurnInPlace = enableTurnInPlace
            && isGrounded
            && horizontalOnly
            && !rotateToCameraYaw;
        if (showTurnInPlaceDebug && shouldTurnInPlace != lastShouldTurnInPlace)
        {
            Debug.Log($"TurnInPlace changed: enabled={enableTurnInPlace} horizontalOnly={horizontalOnly} shouldTurn={shouldTurnInPlace} input=({moveInput.x:F2},{moveInput.y:F2})");
        }
        lastShouldTurnInPlace = shouldTurnInPlace;

        Vector3 desiredMoveDirection = shouldTurnInPlace
            ? right * Mathf.Sign(moveInput.x)
            : forward * moveInput.y + right * moveInput.x;

        // Check if sprinting
        bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;
        bool isSprinting = sprintAction != null
            && sprintAction.IsPressed()
            && hasMoveInput
            && !shouldTurnInPlace;
        float targetSpeed = shouldTurnInPlace ? 0f : (hasMoveInput ? (isSprinting ? sprintSpeed : walkSpeed) : 0f);

        // Smoothly accelerate
        if (desiredMoveDirection.magnitude > 0.1f || rotateToCameraYaw)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

            // Rotate character
            Vector3 faceDirection = rotateToCameraYaw ? forward : desiredMoveDirection;
            if (faceDirection.sqrMagnitude < 0.001f)
            {
                faceDirection = transform.forward;
            }
            Quaternion targetRotation = Quaternion.LookRotation(faceDirection);
            float turnSpeed = shouldTurnInPlace ? turnInPlaceRotationSpeed : rotationSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0, acceleration * Time.deltaTime);
        }

        if (shouldTurnInPlace)
        {
            currentSpeed = 0f;
        }

        // Apply movement
        Vector3 move = shouldTurnInPlace ? Vector3.zero : desiredMoveDirection.normalized * currentSpeed;
        controller.Move(move * Time.deltaTime);

        // Update animator parameters
        if (animator != null)
        {
            // Normalize speed to 0-1 range based on walk speed
            float normalizedSpeed = shouldTurnInPlace ? 0f : (currentSpeed / walkSpeed);
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
                animator.SetFloat("Horizontal", shouldTurnInPlace ? 0f : moveInput.x);
            }
            if (HasParameter(animator, "Vertical"))
            {
                animator.SetFloat("Vertical", shouldTurnInPlace ? 0f : moveInput.y);
            }

        }
    }

    private void OnGUI()
    {
        if (!showTurnInPlaceDebug)
        {
            return;
        }

        string text = $"TurnInPlace: {(enableTurnInPlace ? "ON" : "OFF")} | ShouldTurn: {lastShouldTurnInPlace}\\n" +
                      $"Input: ({(moveAction != null ? moveAction.ReadValue<Vector2>().x : 0f):F2}, {(moveAction != null ? moveAction.ReadValue<Vector2>().y : 0f):F2})";
        GUI.Label(new Rect(10, 10, 500, 40), text);
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
