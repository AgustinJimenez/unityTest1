using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleCharacter : MonoBehaviour
{
    [SerializeField] public Transform    cameraTransform;
    [SerializeField] public LedgeDetector ledgeDetector;
    [SerializeField] private float moveSpeed  = 4f;
    [SerializeField] private float turnSpeed  = 15f;
    [SerializeField] private float gravity    = -15f;
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("Crouch / Crawl (C key)")]
    [Tooltip("Tap C to toggle crouch; hold C to lay down into crawl (release to come back to crouch).")]
    [SerializeField] private float crouchSpeed   = 2f;
    [SerializeField] private float crawlSpeed    = 1.2f;
    [Tooltip("Hold C at least this long to drop from crouch into crawl.")]
    [SerializeField] private float crawlHoldTime = 0.35f;
    [Tooltip("Capsule height as a fraction of standing height.")]
    [SerializeField] private float crouchHeightFactor = 0.6f;
    [SerializeField] private float crawlHeightFactor  = 0.35f;

    private enum Stance { Standing, Crouching, Crawling }

    private CharacterController controller;
    private Animator animator;
    private float verticalVelocity;
    private bool hasSpeedParam;
    private bool hasHangingParam;
    private bool hasIsGroundedParam;
    private bool hasVerticalVelocityParam;
    private bool hasTimeInAirParam;
    private bool hasCrouchParam, hasCrawlParam, hasMoveXParam;
    private float  timeInAir;
    private string prevClipName = "";

    private Stance stance = Stance.Standing;
    private float  standHeight, standCenterY;
    private float  cHoldTimer;
    private bool   enteredCrawlThisHold;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        standHeight  = controller.height;
        standCenterY = controller.center.y;
        RefreshAnimator();
    }

    // Re-grab the active Animator (e.g. after CharacterSwitcher swaps the model)
    // and re-detect which controller parameters exist.
    public void RefreshAnimator()
    {
        animator = GetComponentInChildren<Animator>();
        hasSpeedParam = hasHangingParam = hasIsGroundedParam = hasVerticalVelocityParam =
            hasTimeInAirParam = hasCrouchParam = hasCrawlParam = hasMoveXParam = false;
        if (animator != null)
            foreach (var p in animator.parameters)
            {
                if (p.name == "Speed")           hasSpeedParam           = true;
                if (p.name == "IsHanging")       hasHangingParam         = true;
                if (p.name == "IsGrounded")       hasIsGroundedParam       = true;
                if (p.name == "VerticalVelocity") hasVerticalVelocityParam  = true;
                if (p.name == "TimeInAir")        hasTimeInAirParam         = true;
                if (p.name == "IsCrouching")      hasCrouchParam            = true;
                if (p.name == "IsCrawling")       hasCrawlParam             = true;
                if (p.name == "MoveX")            hasMoveXParam             = true;
            }
    }

    // Tap C toggles crouch; holding C past crawlHoldTime lays the character down to
    // crawl; releasing from crawl returns to crouch.
    private void UpdateStance(Keyboard keyboard)
    {
        if (keyboard.cKey.wasPressedThisFrame) { cHoldTimer = 0f; enteredCrawlThisHold = false; }
        if (keyboard.cKey.isPressed)
        {
            cHoldTimer += Time.deltaTime;
            if (cHoldTimer >= crawlHoldTime && stance != Stance.Crawling)
            {
                stance = Stance.Crawling;
                enteredCrawlThisHold = true;
            }
        }
        if (keyboard.cKey.wasReleasedThisFrame)
        {
            if (enteredCrawlThisHold) stance = Stance.Crouching;                 // came up from crawl
            else stance = stance == Stance.Standing ? Stance.Crouching : Stance.Standing; // tap toggle
            enteredCrawlThisHold = false;
        }

        // Smoothly resize the capsule, keeping its base at the feet.
        float targetH = stance == Stance.Crawling ? standHeight * crawlHeightFactor
                      : stance == Stance.Crouching ? standHeight * crouchHeightFactor
                      : standHeight;
        controller.height = Mathf.Lerp(controller.height, targetH, Time.deltaTime * 10f);
        controller.center = new Vector3(controller.center.x,
            standCenterY - (standHeight - controller.height) * 0.5f, controller.center.z);

        if (animator != null)
        {
            if (hasCrouchParam) animator.SetBool("IsCrouching", stance == Stance.Crouching);
            if (hasCrawlParam)  animator.SetBool("IsCrawling",  stance == Stance.Crawling);
        }
    }

    private float CurrentMoveSpeed() =>
        stance == Stance.Crawling ? crawlSpeed : stance == Stance.Crouching ? crouchSpeed : moveSpeed;

    private void Update()
    {
        // Freeze locomotion while hanging — LedgeDetector owns movement in that state
        bool isHanging = ledgeDetector != null && ledgeDetector.IsHanging;
        if (animator != null && hasHangingParam)
            animator.SetBool("IsHanging", isHanging);

        if (isHanging)
        {
            verticalVelocity = 0f;
            controller.Move(Vector3.zero);
            if (animator != null && hasSpeedParam)           animator.SetFloat("Speed", 0f);
            if (animator != null && hasIsGroundedParam)       animator.SetBool("IsGrounded", true);
            if (animator != null && hasVerticalVelocityParam) animator.SetFloat("VerticalVelocity", 0f);
            if (animator != null && hasTimeInAirParam)        animator.SetFloat("TimeInAir", 0f);
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        UpdateStance(keyboard);
        float speed = CurrentMoveSpeed();

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;

        // While crawling, MoveX feeds the crawl blend tree so the body leans into
        // sideways crawl; standing/crouching strafe is handled by the move vector.
        if (animator != null && hasMoveXParam)
            animator.SetFloat("MoveX", stance == Stance.Crawling ? input.x : 0f);

        Vector3 horizontal = Vector3.zero;
        if (input.sqrMagnitude > 0.01f)
        {
            Vector3 camFwd   = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 camRight = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
                : Vector3.right;

            horizontal = (camFwd * input.y + camRight * input.x).normalized * speed;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(horizontal), Time.deltaTime * turnSpeed);
        }

        // Physics.CheckSphere is reliable regardless of Time.timeScale — cc.isGrounded
        // depends on Move() contact which flickers when deltaTime is tiny (slow-mo).
        Vector3 feetPos = transform.position
                        + controller.center
                        + Vector3.down * (controller.height * 0.5f - controller.radius + 0.02f);
        bool isGrounded = Physics.CheckSphere(feetPos, controller.radius + 0.05f,
                              ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            timeInAir = 0f;
        }
        else
        {
            timeInAir += Time.deltaTime;
        }

        if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame
            && stance == Stance.Standing)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);

        // Drive animation
        if (animator != null && hasSpeedParam)
            animator.SetFloat("Speed", horizontal.magnitude / speed);
        if (animator != null && hasIsGroundedParam)
            animator.SetBool("IsGrounded", isGrounded);
        if (animator != null && hasVerticalVelocityParam)
            animator.SetFloat("VerticalVelocity", verticalVelocity);
        if (animator != null && hasTimeInAirParam)
            animator.SetFloat("TimeInAir", timeInAir);

        if (animator != null)
        {
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            string clip = clips.Length > 0 ? clips[0].clip.name : "none";
            if (clip != prevClipName)
            {
                // Only log jump-related changes — Idle↔Run churn is noise.
                bool jumpRelated = clip.Contains("Jump") || clip.Contains("Fall")
                                || prevClipName.Contains("Jump") || prevClipName.Contains("Fall");
                if (jumpRelated)
                    Debug.Log($"[Anim] {prevClipName} → {clip}" +
                              $"  t={Time.time:F2}  grounded={isGrounded}  vv={verticalVelocity:F2}" +
                              $"  timeInAir={timeInAir:F2}  speed={horizontal.magnitude:F2}  scale={Time.timeScale:F1}");
                prevClipName = clip;
            }
        }

    }
}
