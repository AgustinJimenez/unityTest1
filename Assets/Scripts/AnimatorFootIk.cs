using UnityEngine;

[DisallowMultipleComponent]
public class AnimatorFootIk : MonoBehaviour
{
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float raycastDistance = 1.5f;
    [SerializeField] private float footOffset = 0.05f;
    [SerializeField] private float hintForwardOffset = 0.2f;
    [SerializeField] private float hintUpOffset = 0.1f;
    [SerializeField] private bool rotateToSurface = true;
    [SerializeField] private float ikPositionWeight = 1f;
    [SerializeField] private float ikRotationWeight = 1f;
    [SerializeField] private float hintWeight = 1f;
    [SerializeField] private bool debug = true;

    [Header("Movement Detection")]
    [SerializeField] private float speedThreshold = 0.1f;
    [SerializeField] private float movingIkWeight = 0.3f;

    [Header("Pelvis Adjustment")]
    [SerializeField] private bool adjustPelvis = true;
    [SerializeField] private float pelvisOffsetSpeed = 5f;
    [SerializeField] private float maxPelvisOffset = 0.3f;

    private Animator animator;
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform leftKnee;
    private Transform rightKnee;
    private Transform leftUpperLeg;
    private Transform rightUpperLeg;
    private Transform pelvis;
    private float currentPelvisOffset;
    private float leftFootIkOffset;
    private float rightFootIkOffset;
    private float currentSpeed;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            pelvis = animator.GetBoneTransform(HumanBodyBones.Hips);
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || leftFoot == null || rightFoot == null)
        {
            return;
        }

        if (debug && !animator.isHuman)
        {
            Debug.LogWarning("[AnimatorFootIk] Animator is not humanoid; IK will be ignored.");
        }

        // Get current speed from animator to reduce IK weight when moving
        currentSpeed = animator.GetFloat("Speed");

        // Calculate foot offsets first
        leftFootIkOffset = CalculateFootOffset(leftFoot);
        rightFootIkOffset = CalculateFootOffset(rightFoot);

        // Adjust pelvis based on lowest foot
        if (adjustPelvis)
        {
            AdjustPelvis();
        }

        SolveFoot(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, leftUpperLeg, leftKnee, leftFoot, leftFootIkOffset);
        SolveFoot(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, rightUpperLeg, rightKnee, rightFoot, rightFootIkOffset);
    }

    private float CalculateFootOffset(Transform foot)
    {
        Vector3 origin = foot.position + Vector3.up * raycastDistance * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask))
        {
            // Return how much the foot needs to move down (negative) or up (positive)
            return hit.point.y - foot.position.y + footOffset;
        }
        return 0f;
    }

    private void AdjustPelvis()
    {
        // Reduce pelvis adjustment when moving to allow natural animation
        bool isMoving = currentSpeed > speedThreshold;
        float pelvisWeight = isMoving ? movingIkWeight : 1f;

        // Find the lowest offset (most negative means foot needs to go down the most)
        float lowestOffset = Mathf.Min(leftFootIkOffset, rightFootIkOffset);

        // Only lower pelvis, never raise it above original position
        float targetPelvisOffset = Mathf.Clamp(lowestOffset * pelvisWeight, -maxPelvisOffset, 0f);

        // Smoothly interpolate
        currentPelvisOffset = Mathf.Lerp(currentPelvisOffset, targetPelvisOffset, Time.deltaTime * pelvisOffsetSpeed);

        // Apply pelvis offset
        if (pelvis != null)
        {
            Vector3 pelvisPos = pelvis.position;
            pelvisPos.y += currentPelvisOffset;
            pelvis.position = pelvisPos;
        }
    }

    private void SolveFoot(AvatarIKGoal goal, AvatarIKHint hint, Transform upperLeg, Transform knee, Transform foot, float preCalculatedOffset)
    {
        if (animator != null)
        {
            // Reduce IK weight when moving to allow natural foot lift animation
            bool isMoving = currentSpeed > speedThreshold;
            float effectivePositionWeight = isMoving ? movingIkWeight : ikPositionWeight;
            float effectiveRotationWeight = isMoving ? movingIkWeight : ikRotationWeight;

            animator.SetIKPositionWeight(goal, effectivePositionWeight);
            animator.SetIKRotationWeight(goal, rotateToSurface ? effectiveRotationWeight : 0f);
            animator.SetIKHintPositionWeight(hint, isMoving ? movingIkWeight : hintWeight);
        }

        Vector3 origin = foot.position + Vector3.up * raycastDistance * 0.5f;
        bool hitGround = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask);

        if (hitGround)
        {
            // Apply position with pelvis compensation
            Vector3 targetPos = hit.point + Vector3.up * footOffset;

            // Compensate for pelvis offset so feet stay planted
            if (adjustPelvis)
            {
                targetPos.y -= currentPelvisOffset;
            }

            animator.SetIKPosition(goal, targetPos);

            Quaternion targetRotation = rotateToSurface
                ? Quaternion.LookRotation(transform.forward, hit.normal)
                : foot.rotation;
            animator.SetIKRotation(goal, targetRotation);
        }

        if (upperLeg != null && knee != null)
        {
            Vector3 thighDir = (knee.position - upperLeg.position).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, thighDir).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.ProjectOnPlane(transform.right, thighDir).normalized;
            }

            Vector3 hintPos = knee.position + forward * hintForwardOffset + Vector3.up * hintUpOffset;
            animator.SetIKHintPosition(hint, hintPos);
        }
    }

    // Debug variables to store raycast results for Gizmos
    private Vector3 leftFootOrigin, rightFootOrigin;
    private Vector3 leftFootHitPoint, rightFootHitPoint;
    private Vector3 leftFootIkTarget, rightFootIkTarget;
    private bool leftFootHit, rightFootHit;

    private void LateUpdate()
    {
        if (!debug || leftFoot == null || rightFoot == null) return;

        // Store debug info for left foot
        leftFootOrigin = leftFoot.position + Vector3.up * raycastDistance * 0.5f;
        leftFootHit = Physics.Raycast(leftFootOrigin, Vector3.down, out RaycastHit leftHit, raycastDistance, groundMask);
        if (leftFootHit)
        {
            leftFootHitPoint = leftHit.point;
            leftFootIkTarget = leftHit.point + Vector3.up * footOffset;
        }

        // Store debug info for right foot
        rightFootOrigin = rightFoot.position + Vector3.up * raycastDistance * 0.5f;
        rightFootHit = Physics.Raycast(rightFootOrigin, Vector3.down, out RaycastHit rightHit, raycastDistance, groundMask);
        if (rightFootHit)
        {
            rightFootHitPoint = rightHit.point;
            rightFootIkTarget = rightHit.point + Vector3.up * footOffset;
        }

        // Draw debug lines (visible in Scene view and Game view with Gizmos on)
        DrawDebugVisualization();
    }

    private void DrawDebugVisualization()
    {
        // Left foot - cyan ray, green hit point, blue IK target
        if (leftFootHit)
        {
            Debug.DrawLine(leftFootOrigin, leftFootHitPoint, Color.green);
            Debug.DrawLine(leftFoot.position, leftFootIkTarget, Color.yellow);
            // Draw cross at hit point
            Debug.DrawLine(leftFootHitPoint + Vector3.left * 0.05f, leftFootHitPoint + Vector3.right * 0.05f, Color.cyan);
            Debug.DrawLine(leftFootHitPoint + Vector3.forward * 0.05f, leftFootHitPoint + Vector3.back * 0.05f, Color.cyan);
            // Draw cross at IK target
            Debug.DrawLine(leftFootIkTarget + Vector3.left * 0.08f, leftFootIkTarget + Vector3.right * 0.08f, Color.blue);
            Debug.DrawLine(leftFootIkTarget + Vector3.forward * 0.08f, leftFootIkTarget + Vector3.back * 0.08f, Color.blue);
        }
        else
        {
            Debug.DrawRay(leftFootOrigin, Vector3.down * raycastDistance, Color.red);
        }

        // Right foot - magenta ray, green hit point, red IK target
        if (rightFootHit)
        {
            Debug.DrawLine(rightFootOrigin, rightFootHitPoint, Color.green);
            Debug.DrawLine(rightFoot.position, rightFootIkTarget, Color.yellow);
            // Draw cross at hit point
            Debug.DrawLine(rightFootHitPoint + Vector3.left * 0.05f, rightFootHitPoint + Vector3.right * 0.05f, Color.magenta);
            Debug.DrawLine(rightFootHitPoint + Vector3.forward * 0.05f, rightFootHitPoint + Vector3.back * 0.05f, Color.magenta);
            // Draw cross at IK target
            Debug.DrawLine(rightFootIkTarget + Vector3.left * 0.08f, rightFootIkTarget + Vector3.right * 0.08f, Color.red);
            Debug.DrawLine(rightFootIkTarget + Vector3.forward * 0.08f, rightFootIkTarget + Vector3.back * 0.08f, Color.red);
        }
        else
        {
            Debug.DrawRay(rightFootOrigin, Vector3.down * raycastDistance, Color.red);
        }

        // Draw current foot positions
        Debug.DrawLine(leftFoot.position, leftFoot.position + Vector3.up * 0.1f, Color.blue);
        Debug.DrawLine(rightFoot.position, rightFoot.position + Vector3.up * 0.1f, Color.red);

        // Draw pelvis offset
        if (pelvis != null && adjustPelvis && Mathf.Abs(currentPelvisOffset) > 0.001f)
        {
            Debug.DrawLine(pelvis.position, pelvis.position + Vector3.down * Mathf.Abs(currentPelvisOffset), Color.white);
        }
    }

    private void OnDrawGizmos()
    {
        if (!debug || !Application.isPlaying) return;
        if (leftFoot == null || rightFoot == null) return;

        // Left foot - cyan/blue
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(leftFootOrigin, 0.02f);
        if (leftFootHit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(leftFootOrigin, leftFootHitPoint);
            Gizmos.DrawWireSphere(leftFootHitPoint, 0.03f);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(leftFootIkTarget, 0.05f);
            // Draw line from current foot to IK target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(leftFoot.position, leftFootIkTarget);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(leftFootOrigin, leftFootOrigin + Vector3.down * raycastDistance);
        }

        // Right foot - magenta/red
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(rightFootOrigin, 0.02f);
        if (rightFootHit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(rightFootOrigin, rightFootHitPoint);
            Gizmos.DrawWireSphere(rightFootHitPoint, 0.03f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(rightFootIkTarget, 0.05f);
            // Draw line from current foot to IK target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(rightFoot.position, rightFootIkTarget);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(rightFootOrigin, rightFootOrigin + Vector3.down * raycastDistance);
        }

        // Draw pelvis offset indicator
        if (pelvis != null && adjustPelvis)
        {
            Gizmos.color = Color.white;
            Vector3 pelvisBase = pelvis.position;
            Gizmos.DrawLine(pelvisBase, pelvisBase + Vector3.down * Mathf.Abs(currentPelvisOffset));
        }
    }
}
