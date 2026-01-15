using UnityEngine;

[DisallowMultipleComponent]
public class AnimatorFootIk : MonoBehaviour
{
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float raycastDistance = 1f;
    [SerializeField] private float footOffset = 0.02f;
    [SerializeField] private float hintForwardOffset = 0.2f;
    [SerializeField] private float hintUpOffset = 0.1f;
    [SerializeField] private bool rotateToSurface = true;
    [SerializeField] private float ikPositionWeight = 1f;
    [SerializeField] private float ikRotationWeight = 1f;
    [SerializeField] private float hintWeight = 1f;
    [SerializeField] private bool debug = true;

    private Animator animator;
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform leftKnee;
    private Transform rightKnee;
    private Transform leftUpperLeg;
    private Transform rightUpperLeg;

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

        SolveFoot(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, leftUpperLeg, leftKnee, leftFoot);
        SolveFoot(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, rightUpperLeg, rightKnee, rightFoot);
    }

    private void SolveFoot(AvatarIKGoal goal, AvatarIKHint hint, Transform upperLeg, Transform knee, Transform foot)
    {
        if (animator != null)
        {
            animator.SetIKPositionWeight(goal, ikPositionWeight);
            animator.SetIKRotationWeight(goal, rotateToSurface ? ikRotationWeight : 0f);
            animator.SetIKHintPositionWeight(hint, hintWeight);
        }

        Vector3 origin = foot.position + Vector3.up * raycastDistance * 0.5f;
        bool hitGround = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask);

        if (hitGround)
        {
            animator.SetIKPosition(goal, hit.point + Vector3.up * footOffset);

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
}
