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

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator   = GetComponentInChildren<Animator>();
        if (animator != null)
            foreach (var p in animator.parameters)
            {
                if (p.name == "Speed")     hasSpeedParam    = true;
                if (p.name == "IsHanging") hasHangingParam  = true;
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
            if (animator != null && hasSpeedParam)
                animator.SetFloat("Speed", 0f);
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

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;

        if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);

        // Drive animation — normalize horizontal speed to 0-1
        if (animator != null && hasSpeedParam)
            animator.SetFloat("Speed", horizontal.magnitude / moveSpeed);
    }
}
