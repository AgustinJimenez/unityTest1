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

    [Tooltip("If every foot's IK gap (csHomebrewIK target Y - bone Y) is above this value, " +
             "csHomebrewIK already planted both feet — skip body lowering entirely. " +
             "Requires csHomebrewIK to be earlier in the component list so its SetIKPosition " +
             "calls are visible when this script reads GetIKPosition.")]
    [SerializeField] private float plantedThreshold = 0.04f;

    [Tooltip("Minimum gap (metres) before the IK target override fires. " +
             "Prevents over-correction on gentle ramps where csHomebrewIK is sufficient.")]
    [SerializeField] private float gapThreshold = 0.05f;

    [Tooltip("How fast the correction smoothly ramps up and releases (SmoothDamp time).")]
    [SerializeField] private float smoothTime = 0.12f;

    [Tooltip("Horizontal speed above which the correction fades to zero.")]
    [SerializeField] public float speedFadeThreshold = 0.8f;

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
    public float DbgCurrentOffset      { get; private set; }
    public float DbgTargetOffset       { get; private set; }
    public float DbgRaycastWorstGap    { get; private set; }
    public float DbgHSpeed             { get; private set; }
    public float DbgLeftGap        { get; private set; }
    public float DbgRightGap       { get; private set; }
    public bool  DbgLeftOverride   { get; private set; }
    public bool  DbgRightOverride  { get; private set; }
    public float DbgBodyPosY       { get; private set; }
    public bool  DbgNeedsHelp      { get; private set; }
    public float DbgIKGapL         { get; private set; }
    public float DbgIKGapR         { get; private set; }

    // Optional — when set, foot IK is suppressed during hang
    [SerializeField] public LedgeDetector ledgeDetector;


    private Animator                  animator;
    private CharacterController       cc;
    private FischlWorks.csHomebrewIK  homebrew;
    private float                     currentOffset;
    private float                     offsetVelocity;
    private int                       prevAirStateHash = 0;
    private bool                      homebrewBodyPositioningDefault = true;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        cc       = GetComponentInParent<CharacterController>();
        homebrew = GetComponent<FischlWorks.csHomebrewIK>();
        if (homebrew != null)
            homebrewBodyPositioningDefault = homebrew._EnableBodyPositioning;
    }

    private void Update()
    {
        if (homebrew == null) return;

        // Gate homebrew's body positioning to grounded-in-Locomotion frames.
        // ApplyBodyIK shifts animator.bodyPosition unconditionally — zeroing the foot
        // IK weights in OnAnimatorIK does NOT suppress it, and its smoothed buffers lag
        // during jumps, which makes the mesh sink on ascent and pop up at touchdown.
        // Set from Update because it runs before this frame's IK pass; the animator
        // state info here is one frame stale, which only delays the gate by a frame.
        bool hanging      = ledgeDetector != null && ledgeDetector.IsHanging;
        bool inLocomotion = animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion")
                         && !animator.IsInTransition(0);
        homebrew._EnableBodyPositioning =
            homebrewBodyPositioningDefault && !hanging && inLocomotion && IsGrounded();
    }

    // CheckSphere instead of cc.isGrounded — the latter flickers when deltaTime is
    // tiny (slow-motion) because it depends on Move() contact each frame.
    private bool IsGrounded()
    {
        if (cc == null) return true;
        Vector3 feetPos = cc.transform.position + cc.center
                        + Vector3.down * (cc.height * 0.5f - cc.radius + 0.02f);
        return Physics.CheckSphere(feetPos, cc.radius + 0.05f,
                   ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        // While hanging, skip foot IK entirely.
        // transform.rotation is already held at HangRotation every frame by LedgeDetector.Update —
        // no need to override animator.bodyRotation here (it causes double-rotation fighting).
        bool hanging = ledgeDetector != null && ledgeDetector.IsHanging;
        if (hanging)
        {
            DbgBodyPosY = animator.bodyPosition.y;
            if (homebrew != null && homebrew.enabled) homebrew.enabled = false;
            // Homebrew only gets disabled now, so on the grab frame it has already run
            // and set full weights toward ground targets — zero them or the feet get
            // pulled toward the ground for one frame while the body snaps to the wall.
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot,  0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot,  0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            return;
        }

        bool grounded = IsGrounded();

        // Keep homebrew always enabled so its internal IK targets stay fresh even during
        // the jump. IKBodyLower zeroes the weights when IK shouldn't be visible, which
        // suppresses homebrew's effect without letting its state go stale.
        if (homebrew != null && !homebrew.enabled) homebrew.enabled = true;

        // Only apply foot IK when FULLY in Locomotion (not mid-transition).
        bool inLocomotion = animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion")
                         && !animator.IsInTransition(0);
        bool enableFootIK = grounded && inLocomotion;

        if (!enableFootIK)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            int curHash   = stateInfo.shortNameHash
                          ^ (animator.IsInTransition(0)
                                ? animator.GetNextAnimatorStateInfo(0).shortNameHash : 0);
            if (curHash != prevAirStateHash)
            {
                prevAirStateHash = curHash;
                Transform lFoot  = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rFoot  = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform lKnee  = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform rKnee  = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                Transform lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                string stateName = stateInfo.IsName("Locomotion") ? "Loco"
                                 : stateInfo.IsName("JumpBegin")  ? "Begin"
                                 : stateInfo.IsName("JumpAir")    ? "Air"
                                 : stateInfo.IsName("JumpLand")   ? "Land"
                                 : stateInfo.IsName("Fall")       ? "Fall"
                                 : stateInfo.IsName("Hang")       ? "Hang"
                                 : $"h{stateInfo.shortNameHash}";
                string next = animator.IsInTransition(0)
                    ? $"→{animator.GetNextAnimatorStateInfo(0).shortNameHash}" : "";
                float normTime = stateInfo.normalizedTime % 1f;
                Debug.Log($"[AirLegs] t={Time.time:F2}  state={stateName}({normTime:F2}){next}  " +
                          $"grnd={grounded}  loco={inLocomotion}  ikEN={enableFootIK}  root={transform.position.y:F3}  " +
                          $"LFoot={lFoot?.position.y:F3} LKnee={lKnee?.position.y:F3} LThigh={lThigh?.position.y:F3}  " +
                          $"RFoot={rFoot?.position.y:F3} RKnee={rKnee?.position.y:F3} RThigh={rThigh?.position.y:F3}");
            }
        }
        else
        {
            prevAirStateHash = 0; // reset so next air entry logs fresh
        }
        float hSpeed    = cc != null ? Vector3.ProjectOnPlane(cc.velocity, Vector3.up).magnitude : 0f;
        bool canCorrect = enableFootIK && hSpeed < speedFadeThreshold;
        DbgHSpeed = hSpeed;

        float targetOffset = 0f;

        DbgLeftOverride  = false;
        DbgRightOverride = false;

        if (canCorrect)
        {
            Transform lBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            float leftGap  = GetGroundGap(lBone, out Vector3 lTarget, out bool lValid);
            float rightGap = GetGroundGap(rBone, out Vector3 rTarget, out bool rValid);

            DbgLeftGap  = leftGap;
            DbgRightGap = rightGap;

            // Raw raycast gap — for reference only; may hit geometry below the walking
            // surface on steps/edges. Not used to drive body lowering.
            DbgRaycastWorstGap = Mathf.Min(leftGap, rightGap);

            // Use csHomebrewIK's IK targets to measure the actual residual gap —
            // how far the foot bone still is from where csHomebrewIK wants it.
            // This is far more accurate than IKBodyLower's own deep raycasts, which
            // can hit geometry well below the actual walking surface.
            float ikGapL = 0f, ikGapR = 0f;
            if (homebrew != null)
            {
                ikGapL = lBone != null ? homebrew._LeftFootIKPositionTarget.y  - lBone.position.y : 0f;
                ikGapR = rBone != null ? homebrew._RightFootIKPositionTarget.y - rBone.position.y : 0f;
            }
            DbgIKGapL = ikGapL;
            DbgIKGapR = ikGapR;

            // needsHelp: at least one foot has a residual gap csHomebrewIK can't close alone.
            bool needsHelp = ikGapL < -plantedThreshold || ikGapR < -plantedThreshold;
            DbgNeedsHelp = needsHelp;

            if (needsHelp)
            {
                // Drive body lowering from the actual IK gap, not the deep raycast gap.
                // This means the body stops lowering exactly when the foot is planted —
                // no over-lowering due to deep raycast artifacts.
                float worstIKGap = Mathf.Min(ikGapL, ikGapR);
                targetOffset = Mathf.Clamp(worstIKGap, -maxExtraLowering, 0f);

                // Override csHomebrewIK's IK target for the foot that still can't reach.
                // Uses IKBodyLower's own raycast target (hit.point + normal * ankleHeight)
                // to bypass csHomebrewIK's foot-lifting when the gap is large.
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
        }
        else
        {
            DbgLeftGap         = 0f;
            DbgRightGap        = 0f;
            DbgRaycastWorstGap = 0f;
        }

        currentOffset = Mathf.SmoothDamp(
            currentOffset, targetOffset, ref offsetVelocity, smoothTime);

        DbgTargetOffset  = targetOffset;
        DbgCurrentOffset = currentOffset;

        if (Mathf.Abs(currentOffset) > 0.0001f)
            animator.bodyPosition += Vector3.up * currentOffset;

        // When IK is not active (air / transitions), zero the weights so homebrew's effect
        // is invisible — but homebrew itself keeps running to maintain fresh IK targets.
        // This avoids knee flip: IK weight is always binary (0 or 1), never partial.
        if (!enableFootIK)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot,  0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot,  0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
        }

        DbgBodyPosY = animator.bodyPosition.y;
    }

    // Returns ankleTarget.y - uncorrectedBone.y (negative = foot is above target = needs to come down).
    // ankleTarget = hit.point + hit.normal * ankleHeight, so on flat ground the gap is ~0
    // and no extra threshold is needed to suppress false positives.
    // Subtracts currentOffset from bone Y to recover the animation-pose position,
    // preventing oscillation when the correction is already placing the foot correctly.
    // The gap value itself is not used to drive body lowering — only lTarget/rTarget
    // (the world-space IK override position) is used from the out parameter.
    private float GetGroundGap(Transform t, out Vector3 ankleTarget, out bool hitValid)
    {
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
