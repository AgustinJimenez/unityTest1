using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

/// <summary>
/// Detects when the character approaches a ledge drop-off and activates
/// procedural hand IK so both hands grip the ledge surface.
///
/// Activation: press Left Shift while moving forward toward an edge with drop >= minHangDrop.
/// While hanging: movement and gravity are frozen (SimpleCharacter reads IsHanging).
/// Release: press S or Space to drop.
///
/// Arm IK is provided by two TwoBoneIK constraints wired up by IKTestSetup.
/// The handRig weight is blended 0→1 on grab and 1→0 on release.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class LedgeDetector : MonoBehaviour
{
    [Header("References — wired by IKTestSetup")]
    [SerializeField] public Transform              leftHandTarget;
    [SerializeField] public Transform              rightHandTarget;
    [SerializeField] public Rig                    handRig;
    [SerializeField] public FischlWorks.csHomebrewIK footIK;
    [SerializeField] public IKBodyLower            bodyLower;
    [SerializeField] public Animator               characterAnimator;

    [Header("Detection")]
    [Tooltip("Hold this key while moving forward to attempt a ledge grab.")]
    [SerializeField] private Key grabKey = Key.LeftShift;
    [Tooltip("How far in front of the character to probe for a drop.")]
    [SerializeField] private float probeDistance   = 0.5f;
    [Tooltip("Probe ray origin height above the character root.")]
    [SerializeField] private float probeRayStart   = 0.5f;
    [Tooltip("Total downward probe ray length.")]
    [SerializeField] private float probeRayLength  = 3f;
    [Tooltip("Drop below current foot level required to trigger a grab.")]
    [SerializeField] private float minHangDrop     = 0.8f;
    [Tooltip("Minimum forward speed (m/s) required before a grab can trigger.")]
    [SerializeField] private float minForwardSpeed = 0.1f;
    [Tooltip("Lateral offset from ledge centre to each hand target (half shoulder width).")]
    [SerializeField] private float shoulderWidth   = 0.3f;
    [Tooltip("Distance from character root (feet) to hand grip point when hanging. " +
             "Character is snapped this far BELOW the ledge top so arms reach upward.")]
    [SerializeField] private float hangReach       = 1.35f;
    [Tooltip("How far outside the wall face the character's centre is placed (avoids clipping).")]
    [SerializeField] private float hangBodyOffset  = 0.3f;
    [Tooltip("How far inward from the wall face edge the hand targets are placed on the ledge surface.")]
    [SerializeField] private float handInset       = 0.15f;
    [Tooltip("Layers considered ground — must match csHomebrewIK groundLayers.")]
    [SerializeField] private LayerMask groundLayers = -1;

    [Header("Blend")]
    [Tooltip("Speed at which the arm IK rig weight ramps in/out.")]
    [SerializeField] private float blendSpeed = 8f;

    private CharacterController cc;
    private float rigWeight;
    private Quaternion hangRotation;

    public bool       IsHanging     { get; private set; }
    public Quaternion HangRotation  { get; private set; }

    // Diagnostics (read by IKDebugMenu or external tools)
    public bool  DbgEdgeFound   { get; private set; }
    public float DbgDropHeight  { get; private set; }
    public float DbgForwardSpeed { get; private set; }

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (IsHanging)
        {
            // Re-apply rotation every frame — Animator root motion can override it otherwise
            transform.rotation = hangRotation;
            HandleHangInput();
        }
        else
        {
            TryGrabLedge();
        }

        // Blend rig weight smoothly in/out
        float targetWeight = IsHanging ? 1f : 0f;
        rigWeight = Mathf.MoveTowards(rigWeight, targetWeight, Time.deltaTime * blendSpeed);
        if (handRig != null)
            handRig.weight = rigWeight;
    }

    private void TryGrabLedge()
    {
        DbgEdgeFound = false;

        // Grab while key is held — safe because release requires S or Space, not key-up
        var kb = Keyboard.current;
        if (kb == null || !kb[grabKey].isPressed) return;

        // Only grab when moving forward — avoids false triggers while idle at an edge
        float fwdSpeed = Vector3.Dot(cc.velocity, transform.forward);
        DbgForwardSpeed = fwdSpeed;
        if (fwdSpeed < minForwardSpeed) return;

        // Probe downward from in front of the character to detect a drop-off
        Vector3 probeOrigin = transform.position
            + transform.forward * probeDistance
            + Vector3.up        * probeRayStart;

        if (!Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit dropHit,
                             probeRayStart + probeRayLength, groundLayers))
            return;

        // Compare detected ground level against the character's current foot level
        float footY      = transform.position.y;
        float dropHeight = footY - dropHit.point.y;
        DbgDropHeight = dropHeight;

        if (dropHeight < minHangDrop) return;

        // Find the ledge face by casting BACKWARD from just past the edge,
        // at a height just below the ledge top. The character is standing on the
        // platform, so the vertical face is outside/below the edge — a backward
        // cast from the far side of the probe finds it cleanly.
        float   ledgeTopY       = footY;
        Vector3 faceCastOrigin  = new Vector3(probeOrigin.x,
                                              ledgeTopY - 0.05f,
                                              probeOrigin.z);
        if (!Physics.Raycast(faceCastOrigin, -transform.forward, out RaycastHit faceHit,
                             probeDistance + 0.5f, groundLayers))
            return;   // no vertical face found — can't determine wall orientation

        DbgEdgeFound = true;

        // faceHit.normal points outward from the platform face (toward the player).
        // Character hangs on the outside, facing INTO the wall (chest toward wall, back outward)
        // so the third-person camera behind their back stays outside the wall.
        Vector3 wallNormal = faceHit.normal;
        Vector3 facePoint  = faceHit.point;

        // Hang position: outside the face by hangBodyOffset, hanging down by hangReach
        Vector3 hangPos = new Vector3(
            facePoint.x + wallNormal.x * hangBodyOffset,
            ledgeTopY   - hangReach,
            facePoint.z + wallNormal.z * hangBodyOffset);

        // Face INTO the wall (-wallNormal) so arms reach forward toward the ledge top
        hangRotation  = Quaternion.LookRotation(-wallNormal, Vector3.up);
        HangRotation  = hangRotation;

        Quaternion preGrabRot = transform.rotation;
        cc.enabled = false;
        transform.position = hangPos;
        transform.rotation = hangRotation;
        cc.enabled = true;

        Debug.Log($"[LedgeDetector] GRAB  wallNormal={wallNormal}  preRot={preGrabRot.eulerAngles}  hangRot={hangRotation.eulerAngles}  pos={transform.position}");

        if (characterAnimator != null) characterAnimator.applyRootMotion = false;

        // Hand targets: on the ledge top surface, inset slightly from the edge,
        // shoulder-width apart. charRight uses the updated rotation (facing into wall).
        Vector3 charRight = transform.right;
        Vector3 handBase  = new Vector3(facePoint.x - wallNormal.x * handInset,
                                         ledgeTopY,
                                         facePoint.z - wallNormal.z * handInset);

        if (leftHandTarget  != null) leftHandTarget.position  = handBase - charRight * shoulderWidth;
        if (rightHandTarget != null) rightHandTarget.position = handBase + charRight * shoulderWidth;

        SetFootIKEnabled(false);
        IsHanging = true;
    }

    private void SetFootIKEnabled(bool enabled)
    {
        if (footIK != null) footIK.enabled = enabled;
        // bodyLower stays enabled — its OnAnimatorIK must keep running so it can
        // override animator.bodyRotation during hang via the ledgeDetector reference.
    }

    private void HandleHangInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Release on S or Space — hang persists until explicitly dropped
        if (kb.sKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            IsHanging = false;
            SetFootIKEnabled(true);
            if (characterAnimator != null) characterAnimator.applyRootMotion = false;
        }
    }
}
