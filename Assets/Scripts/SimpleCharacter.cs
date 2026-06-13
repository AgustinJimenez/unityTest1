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

    private CharacterController controller;
    private Animator animator;
    private float verticalVelocity;
    private bool hasSpeedParam;
    private bool hasHangingParam;
    private bool hasIsGroundedParam;
    private bool hasVerticalVelocityParam;
    private bool hasTimeInAirParam;
    private float  timeInAir;
    private string prevClipName = "";

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator   = GetComponentInChildren<Animator>();
        if (animator != null)
            foreach (var p in animator.parameters)
            {
                if (p.name == "Speed")           hasSpeedParam           = true;
                if (p.name == "IsHanging")       hasHangingParam         = true;
                if (p.name == "IsGrounded")       hasIsGroundedParam       = true;
                if (p.name == "VerticalVelocity") hasVerticalVelocityParam  = true;
                if (p.name == "TimeInAir")        hasTimeInAirParam         = true;
            }
    }

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

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;

        Vector3 horizontal = Vector3.zero;
        if (input.sqrMagnitude > 0.01f)
        {
            Vector3 camFwd   = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 camRight = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
                : Vector3.right;

            horizontal = (camFwd * input.y + camRight * input.x).normalized * moveSpeed;
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

        if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);

        // Drive animation
        if (animator != null && hasSpeedParam)
            animator.SetFloat("Speed", horizontal.magnitude / moveSpeed);
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
