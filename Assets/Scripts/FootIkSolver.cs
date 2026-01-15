using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class FootIkSolver : MonoBehaviour
{
    [SerializeField] private Transform leftTarget;
    [SerializeField] private Transform rightTarget;
    [SerializeField] private Transform leftHint;
    [SerializeField] private Transform rightHint;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float raycastDistance = 1f;
    [SerializeField] private float footOffset = 0.02f;
    [SerializeField] private float hintForwardOffset = 0.2f;
    [SerializeField] private float hintUpOffset = 0.1f;
    [SerializeField] private bool rotateToSurface = false;
    [SerializeField] private bool debugRig = true;
    [SerializeField] private float debugInterval = 1f;

    private Animator animator;
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform leftKnee;
    private Transform rightKnee;
    private Transform leftUpperLeg;
    private Transform rightUpperLeg;
    private bool rigVerified;
    private float debugTimer;

    public void SetTargets(Transform left, Transform right, Transform leftHintTransform, Transform rightHintTransform)
    {
        leftTarget = left;
        rightTarget = right;
        leftHint = leftHintTransform;
        rightHint = rightHintTransform;
    }

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

    private void Update()
    {
        if (!rigVerified)
        {
            rigVerified = true;
            VerifyRig();
        }

        if (leftTarget == null || rightTarget == null || leftHint == null || rightHint == null)
        {
            return;
        }

        if (leftFoot == null || rightFoot == null)
        {
            return;
        }

        UpdateFootTarget(leftFoot, leftTarget);
        UpdateFootTarget(rightFoot, rightTarget);

        UpdateHint(leftUpperLeg, leftKnee, leftHint);
        UpdateHint(rightUpperLeg, rightKnee, rightHint);
    }

    private void LateUpdate()
    {
        if (!debugRig || leftFoot == null || rightFoot == null || leftTarget == null || rightTarget == null)
        {
            return;
        }

        debugTimer += Time.deltaTime;
        if (debugTimer < debugInterval)
        {
            return;
        }

        debugTimer = 0f;
        float leftDistance = Vector3.Distance(leftFoot.position, leftTarget.position);
        float rightDistance = Vector3.Distance(rightFoot.position, rightTarget.position);
        Debug.Log($"[FootIkSolver] Distances L={leftDistance:F3} R={rightDistance:F3}");
    }

    private void UpdateFootTarget(Transform foot, Transform target)
    {
        Vector3 origin = foot.position + Vector3.up * raycastDistance * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask))
        {
            target.position = hit.point + Vector3.up * footOffset;
            target.rotation = rotateToSurface
                ? Quaternion.LookRotation(transform.forward, hit.normal)
                : foot.rotation;
        }
        else
        {
            target.position = foot.position;
            target.rotation = foot.rotation;
        }
    }

    private void UpdateHint(Transform upperLeg, Transform knee, Transform hint)
    {
        if (knee == null || upperLeg == null)
        {
            return;
        }

        Vector3 basePosition = knee.position;
        Vector3 thighDir = (knee.position - upperLeg.position).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, thighDir).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.ProjectOnPlane(transform.right, thighDir).normalized;
        }

        Vector3 offset = forward * hintForwardOffset + Vector3.up * hintUpOffset;
        hint.position = basePosition + offset;
    }

    private void OnDrawGizmosSelected()
    {
        if (leftKnee != null && leftHint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(leftKnee.position, leftHint.position);
        }

        if (rightKnee != null && rightHint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(rightKnee.position, rightHint.position);
        }
    }

    private void VerifyRig()
    {
        RigBuilder rigBuilder = animator != null ? animator.GetComponent<RigBuilder>() : null;
        if (rigBuilder == null)
        {
            rigBuilder = GetComponentInParent<RigBuilder>();
        }

        Rig rig = GetComponentInChildren<Rig>();
        if (rigBuilder != null && rig != null)
        {
            rigBuilder.enabled = true;
            var layers = new System.Collections.Generic.List<RigLayer>(rigBuilder.layers);
            bool hasLayer = layers.Exists(layer => layer.rig == rig);
            if (!hasLayer)
            {
                layers.Add(new RigLayer(rig) { active = true });
                rigBuilder.layers = layers;
            }

            rigBuilder.Build();
        }

        if (debugRig)
        {
            string builderState = rigBuilder == null ? "none" : $"enabled={rigBuilder.enabled}, layers={rigBuilder.layers.Count}";
            string rigState = rig == null ? "none" : $"enabled={rig.enabled}, weight={rig.weight}";
            Debug.Log($"[FootIkSolver] RigBuilder={builderState}, Rig={rigState}");

            if (rigBuilder != null && animator != null)
            {
                Debug.Log($"[FootIkSolver] RigBuilderHost={rigBuilder.gameObject.name} AnimatorHost={animator.gameObject.name} sameGO={rigBuilder.gameObject == animator.gameObject}");
                for (int i = 0; i < rigBuilder.layers.Count; i++)
                {
                    RigLayer layer = rigBuilder.layers[i];
                    string rigName = layer.rig == null ? "null" : layer.rig.name;
                    Debug.Log($"[FootIkSolver] Layer[{i}] rig={rigName} active={layer.active}");
                }
            }

            TwoBoneIKConstraint left = GetComponentInChildren<TwoBoneIKConstraint>(true);
            if (left != null)
            {
                var data = left.data;
                Debug.Log($"[FootIkSolver] LeftIK weight={left.weight} target={NameOrNull(data.target)} hint={NameOrNull(data.hint)}");
                Debug.Log($"[FootIkSolver] LeftIK weights pos={data.targetPositionWeight} rot={data.targetRotationWeight} hint={data.hintWeight}");
                LogBoneParentCheck("LeftIK.root", data.root);
                LogBoneParentCheck("LeftIK.mid", data.mid);
                LogBoneParentCheck("LeftIK.tip", data.tip);
            }

            if (animator != null)
            {
                Debug.Log($"[FootIkSolver] Animator={animator.name}");
            }
        }
    }

    private static string NameOrNull(Transform value)
    {
        return value == null ? "null" : value.name;
    }

    private void LogBoneParentCheck(string label, Transform bone)
    {
        if (bone == null || animator == null)
        {
            return;
        }

        bool isChild = bone.IsChildOf(animator.transform);
        Debug.Log($"[FootIkSolver] {label} childOfAnimator={isChild}");
    }
}
