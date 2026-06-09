using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

/// <summary>
/// Terrain-adaptive foot placement via Unity's built-in OnAnimatorIK callback.
///
/// See FootIK_Notes.md in the same folder for a full explanation of the approach.
/// </summary>
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class FootIK : MonoBehaviour
{
    [Header("Layers")]
    [Tooltip("Layers that count as ground. Exclude the character's own physics layer to avoid self-hits.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Raycast")]
    [Tooltip("Height above the foot bone to start the downward ray. " +
             "Raise if the foot is already at or below ground during animation.")]
    [SerializeField] private float rayOriginHeight = 0.6f;

    [Tooltip("Total downward search distance from the ray origin. " +
             "Must cover the deepest step/drop you expect (origin height + step height).")]
    [SerializeField] private float rayDistance = 1.5f;

    [Header("Foot placement")]
    [Tooltip("Foot height relative to character root above which IK is released (swing phase). " +
             "Increase if IK turns off too early on tall stair steps.")]
    [SerializeField] private float swingHeightThreshold = 0.2f;

    [Header("Smoothing  (higher = snappier)")]
    [SerializeField] private float weightSmoothing   = 12f;
    [SerializeField] private float ySmoothing        = 14f;
    [SerializeField] private float rotationSmoothing = 10f;
    [SerializeField] private float bodySmoothing     = 10f;

    [Header("Body (hips) adjustment")]
    [Tooltip("Maximum downward hip shift to allow both feet to reach the ground.")]
    [SerializeField] private float maxBodyDrop = 0.3f;

    // ── per-foot state ────────────────────────────────────────────────────────
    private float leftYOffset,  rightYOffset;    // current smoothed Y corrections
    private float leftWeight,   rightWeight;
    private Quaternion leftRot  = Quaternion.identity;
    private Quaternion rightRot = Quaternion.identity;

    // ── body state ────────────────────────────────────────────────────────────
    private float smoothBodyOffset;

    private Animator animator;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        animator = GetComponent<Animator>();
        EnsureIKPassEnabled();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        // GetBoneTransform gives the world-space bone position AFTER this frame's
        // animation clip has been sampled, BEFORE IK is solved.
        Transform leftBone  = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rightBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (leftBone == null || rightBone == null) return;

        bool leftHit  = TryCast(leftBone.position,  out RaycastHit lHit);
        bool rightHit = TryCast(rightBone.position, out RaycastHit rHit);

        AdjustBody(leftHit, lHit, rightHit, rHit);

        PlaceFoot(AvatarIKGoal.LeftFoot,  leftBone,  leftHit,  lHit,
                  ref leftYOffset,  ref leftRot,  ref leftWeight);
        PlaceFoot(AvatarIKGoal.RightFoot, rightBone, rightHit, rHit,
                  ref rightYOffset, ref rightRot, ref rightWeight);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private bool TryCast(Vector3 bonePos, out RaycastHit hit)
    {
        Vector3 origin = bonePos + Vector3.up * rayOriginHeight;
        return Physics.Raycast(origin, Vector3.down, out hit,
                               rayOriginHeight + rayDistance, groundMask);
    }

    private void AdjustBody(bool lHit, RaycastHit lRay, bool rHit, RaycastHit rRay)
    {
        // Drop the hips by half the height difference between the two foot contacts
        // so that neither leg has to over-extend to reach its surface.
        float targetDrop = 0f;
        if (lHit && rHit)
        {
            float diff = Mathf.Abs(lRay.point.y - rRay.point.y);
            targetDrop = Mathf.Clamp(-diff * 0.5f, -maxBodyDrop, 0f);
        }

        smoothBodyOffset = Mathf.Lerp(smoothBodyOffset, targetDrop,
                                      Time.deltaTime * bodySmoothing);
        animator.bodyPosition += Vector3.up * smoothBodyOffset;
    }

    private void PlaceFoot(AvatarIKGoal goal, Transform bone,
                            bool grounded, RaycastHit rayHit,
                            ref float yOffset, ref Quaternion smoothRot, ref float weight)
    {
        float dt = Time.deltaTime;

        // Swing detection: foot is above the character root by more than the threshold.
        float footLocalY = bone.position.y - transform.position.y;
        bool  inSwing    = !grounded || footLocalY > swingHeightThreshold;

        if (!inSwing)
        {
            // ── Stance ────────────────────────────────────────────────────────
            // Only correct Y — X/Z always follow the animation so the foot
            // doesn't drag horizontally behind the walk cycle.
            //
            // Offset = how far the surface deviates ABOVE the character's root Y.
            // The animation already places feet correctly on flat ground (deviation=0),
            // so we only need to add the extra height when terrain rises above root.
            // Clamped to >= 0 so we never pull feet underground on descents.
            float surfaceDeviation = rayHit.point.y - transform.position.y;
            float targetOffset = Mathf.Max(0f, surfaceDeviation);
            yOffset = Mathf.Lerp(yOffset, targetOffset, dt * ySmoothing);

            // Rotate the foot to lie flat on the surface normal.
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, rayHit.normal);
            Quaternion targetRot = fwd.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(fwd, rayHit.normal)
                : Quaternion.FromToRotation(Vector3.up, rayHit.normal);
            smoothRot = Quaternion.Slerp(smoothRot, targetRot, dt * rotationSmoothing);

            weight = Mathf.Lerp(weight, 1f, dt * weightSmoothing);
        }
        else
        {
            // ── Swing ─────────────────────────────────────────────────────────
            // Reset offset INSTANTLY — any lerp here causes the foot to drag
            // from the old plant position when it next lands.
            yOffset   = 0f;
            smoothRot = bone.rotation;
            weight    = Mathf.Lerp(weight, 0f, dt * weightSmoothing);
        }

        // Build final IK target: animation X/Z + corrected Y.
        Vector3 ikPos = bone.position + Vector3.up * yOffset;

        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKRotationWeight(goal, weight);
        animator.SetIKPosition(goal, ikPos);
        animator.SetIKRotation(goal, smoothRot);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureIKPassEnabled()
    {
#if UNITY_EDITOR
        if (animator == null) return;

        var rc = animator.runtimeAnimatorController;
        if (rc is AnimatorOverrideController aoc) rc = aoc.runtimeAnimatorController;
        var ac = rc as AnimatorController;
        if (ac == null) return;

        var layers = ac.layers;
        if (layers.Length == 0 || layers[0].iKPass) return;

        layers[0].iKPass = true;
        ac.layers = layers;
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[FootIK] Enabled IK Pass on animator base layer.", this);
#endif
    }
}
