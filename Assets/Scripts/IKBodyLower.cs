using UnityEngine;

/// <summary>
/// Closes the IK gap when one foot is over significantly lower ground than the other.
///
/// Two-part fix:
///   1. Lowers animator.bodyPosition so the lower leg can physically reach the ground.
///   2. Overrides the IK target for that foot, bypassing csHomebrewIK's foot-lifting
///      logic (which would otherwise hold the foot at the animated bone height).
///
/// IK targets are set to hit.point + hit.normal * ankleHeight, so the ankle sits
/// correctly above sloped surfaces and doesn't clip into ramps.
///
/// Only activates when the character is nearly stationary (speed gate) to avoid
/// false corrections during the walk-cycle swing phase.
/// </summary>
[RequireComponent(typeof(Animator))]
public class IKBodyLower : MonoBehaviour
{
    [Tooltip("Max extra downward body offset this script can apply, in metres.")]
    [SerializeField] private float maxExtraLowering = 0.25f;

    [Tooltip("Minimum gap (metres) before the IK target override fires. " +
             "Prevents over-correction on gentle ramps where csHomebrewIK is sufficient.")]
    [SerializeField] private float gapThreshold = 0.05f;

    [Tooltip("How fast the correction smoothly ramps up and releases (SmoothDamp time).")]
    [SerializeField] private float smoothTime = 0.12f;

    [Tooltip("Horizontal speed above which the correction fades to zero.")]
    [SerializeField] internal float speedFadeThreshold = 0.8f;

    [Tooltip("Ankle height above the surface (raySphereRadius + ankleHeightOffset from csHomebrewIK). " +
             "The IK target is placed this far above the surface along its normal.")]
    [SerializeField] private float ankleHeight = 0.1f;

    [Tooltip("Layers considered ground. Must match csHomebrewIK groundLayers.")]
    [SerializeField] private LayerMask groundLayers = -1;

    [Tooltip("How far above the foot bone the downward ray starts.")]
    [SerializeField] private float rayStartOffset = 0.5f;

    [Tooltip("Total ray length (from start point downward).")]
    [SerializeField] private float rayLength = 1.5f;

    // Read-only diagnostics for IKDebugMenu
    public float DbgCurrentOffset  { get; private set; }
    public float DbgTargetOffset   { get; private set; }
    public float DbgWorstGap       { get; private set; }
    public float DbgHSpeed         { get; private set; }
    public float DbgLeftGap        { get; private set; }
    public float DbgRightGap       { get; private set; }
    public bool  DbgLeftOverride   { get; private set; }
    public bool  DbgRightOverride  { get; private set; }
    public float DbgBodyPosY       { get; private set; }

    private Animator            animator;
    private CharacterController cc;
    private float               currentOffset;
    private float               offsetVelocity;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        cc = GetComponentInParent<CharacterController>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        bool grounded   = cc == null || cc.isGrounded;
        float hSpeed    = cc != null ? Vector3.ProjectOnPlane(cc.velocity, Vector3.up).magnitude : 0f;
        bool canCorrect = grounded && hSpeed < speedFadeThreshold;
        DbgHSpeed = hSpeed;

        float targetOffset = 0f;

        DbgLeftOverride  = false;
        DbgRightOverride = false;

        if (canCorrect)
        {
            float leftGap  = GetGroundGap(HumanBodyBones.LeftFoot,  out Vector3 lTarget, out bool lValid);
            float rightGap = GetGroundGap(HumanBodyBones.RightFoot, out Vector3 rTarget, out bool rValid);

            DbgLeftGap  = leftGap;
            DbgRightGap = rightGap;

            float worstGap = Mathf.Min(leftGap, rightGap);
            DbgWorstGap    = worstGap;

            // Gap is naturally ~0 on flat ground because ankleHeight is subtracted from
            // the measured distance. Only fire when gap is genuinely negative (bone above target).
            if (worstGap < 0f)
                targetOffset = Mathf.Clamp(worstGap, -maxExtraLowering, 0f);

            // Override csHomebrewIK's IK target (which may use foot-lifting to hold
            // the foot at the animated bone height rather than the actual surface).
            // Target = hit.point + hit.normal * ankleHeight so foot sits correctly
            // above sloped surfaces and doesn't clip into ramps.
            // gapThreshold prevents this override from firing on gentle ramps where
            // the gap is small and csHomebrewIK's own correction is sufficient.
            if (lValid && leftGap  < -gapThreshold)
            {
                animator.SetIKPosition(AvatarIKGoal.LeftFoot, lTarget);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
                DbgLeftOverride = true;
            }
            if (rValid && rightGap < -gapThreshold)
            {
                animator.SetIKPosition(AvatarIKGoal.RightFoot, rTarget);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);
                DbgRightOverride = true;
            }
        }
        else
        {
            DbgLeftGap  = 0f;
            DbgRightGap = 0f;
            DbgWorstGap = 0f;
        }

        currentOffset = Mathf.SmoothDamp(
            currentOffset, targetOffset, ref offsetVelocity, smoothTime);

        DbgTargetOffset  = targetOffset;
        DbgCurrentOffset = currentOffset;

        if (Mathf.Abs(currentOffset) > 0.0001f)
            animator.bodyPosition += Vector3.up * currentOffset;

        DbgBodyPosY = animator.bodyPosition.y;
    }

    // Returns ankleTarget.y - uncorrectedBone.y (negative = bone above target).
    // ankleTarget = hit.point + hit.normal * ankleHeight, so on flat ground the
    // gap is naturally ~0 and no threshold is needed to suppress false positives.
    // Subtracts currentOffset from bone Y to recover the animation-pose position,
    // preventing oscillation when the correction is successfully placing the foot.
    private float GetGroundGap(HumanBodyBones bone, out Vector3 ankleTarget, out bool hitValid)
    {
        Transform t = animator.GetBoneTransform(bone);
        ankleTarget = Vector3.zero;
        hitValid = false;
        if (t == null) return 0f;

        Vector3 origin = t.position + Vector3.up * rayStartOffset;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayStartOffset + rayLength, groundLayers))
        {
            ankleTarget = hit.point + hit.normal * ankleHeight;
            hitValid = true;
            float uncorrectedBoneY = t.position.y - currentOffset;
            return ankleTarget.y - uncorrectedBoneY;
        }
        return 0f;
    }
}
