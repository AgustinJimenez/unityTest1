using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;

/// <summary>
/// Runtime overlay to tune csHomebrewIK values without leaving Play mode.
/// Up/Down arrows  — navigate entries
/// Left/Right arrows — decrease / increase value
/// Hold for repeat.
/// Attach to the Player.
/// </summary>
public class IKDebugMenu : MonoBehaviour
{
    struct Entry
    {
        public string field;
        public string label;
        public string desc;
        public float  step;
        public bool   isBool;
        public bool   isBodyLower; // read/write from bodyLower component instead of ik
    }

    private static readonly float[] Defaults =
    {
        0f, 0.05f, 0.1f, 45f, 1f, 1f, 1f, 0.075f, 1.5f, 0.5f, 0.5f, 0f, 0.5f, 0f, 1f, 1f, 1f, 1f,
        // Body Lower entries: enabled, maxExtraLowering, plantedThreshold, gapThreshold, smoothTime, speedFadeThreshold, ankleHeight
        1f, 0.46f, 0.04f, 0.05f, 0.12f, 0.8f, 0.095f,
    };

    private static readonly Entry[] Entries =
    {
        new Entry { field = "ankleHeightOffset",       label = "Ankle Height Offset",     step = 0.005f, desc = "Moves feet up (+) or down (-) relative to the ground. Go negative if feet float above the surface." },
        new Entry { field = "raySphereRadius",         label = "Ray Sphere Radius",        step = 0.005f, desc = "Size of the sphere used to detect the ground. Larger = more stable on edges. Always adds to foot height." },
        new Entry { field = "lengthFromHeelToToes",    label = "Heel To Toes Length",      step = 0.005f, desc = "Physical length of the foot mesh. Used to correct foot height on slopes — match to actual foot size." },
        new Entry { field = "maxRotationAngle",        label = "Max Rotation Angle",       step = 1f,     desc = "How much the foot can rotate to match a slope. Higher = foot tilts more aggressively on steep terrain." },
        new Entry { field = "globalWeight",            label = "Global Weight",            step = 0.05f,  desc = "Master blend between full IK (1) and no IK (0). Lower to see how much the IK is actually changing." },
        new Entry { field = "leftFootWeight",          label = "Left Foot Weight",         step = 0.05f,  desc = "IK strength for the left foot only. Set to 0 to disable left foot IK and isolate issues." },
        new Entry { field = "rightFootWeight",         label = "Right Foot Weight",        step = 0.05f,  desc = "IK strength for the right foot only. Set to 0 to disable right foot IK and isolate issues." },
        new Entry { field = "smoothTime",              label = "Smooth Time",              step = 0.005f, desc = "How quickly feet snap to IK targets. Lower = snappier but jittery. Higher = smoother but laggy." },
        new Entry { field = "rayCastRange",            label = "Ray Cast Range",           step = 0.1f,   desc = "How far down the ray looks for ground. Increase if feet lose IK on stairs or steep drops." },
        new Entry { field = "leftFootRayStartHeight",  label = "L Ray Start Height",       step = 0.05f,  desc = "Height above the left foot bone where the downward ray starts. Must be above ankle height." },
        new Entry { field = "rightFootRayStartHeight", label = "R Ray Start Height",       step = 0.05f,  desc = "Height above the right foot bone where the downward ray starts. Must be above ankle height." },
        new Entry { field = "floorRange",              label = "Floor Range",              step = 0.01f,  desc = "How high above the IK target the animated foot can go before IK lets it lift freely. Prevents foot drag." },
        new Entry { field = "crouchRange",             label = "Crouch Range",             step = 0.01f,  desc = "How much the body can lower when feet are on uneven terrain. Higher = more knee bend on slopes." },
        new Entry { field = "stretchRange",            label = "Stretch Range",            step = 0.01f,  desc = "How much the body can rise when a foot is below the character. Rarely needed on flat terrain." },
        new Entry { field = "enableBodyPositioning",   label = "Body Positioning",         isBool = true, desc = "Lowers the hips when feet are on uneven ground. Required for realistic knee bend on slopes." },
        new Entry { field = "enableFootLifting",       label = "Foot Lifting",             isBool = true, desc = "Lets the foot follow the animation upward during the swing phase instead of being pinned to ground." },
        new Entry { field = "enableIKPositioning",     label = "IK Positioning",           isBool = true, desc = "Toggles foot position correction entirely. Disable to see raw animation with no IK height adjustment." },
        new Entry { field = "enableIKRotating",        label = "IK Rotating",              isBool = true, desc = "Toggles foot rotation to match slope normals. Disable if foot angle looks wrong on flat ground." },
        new Entry { field = "_bodyLowerEnabled",       label = "Body Lower",               isBool = true, isBodyLower = true, desc = "Extra body lowering to close the IK gap when one foot is over lower ground. Toggle to see the difference." },
        new Entry { field = "maxExtraLowering",        label = "BL Max Lowering",          step = 0.01f,  isBodyLower = true, desc = "Maximum extra downward body shift IKBodyLower can apply (metres). Watch BL offset in diagnostics." },
        new Entry { field = "plantedThreshold",        label = "BL Planted Threshold",     step = 0.005f, isBodyLower = true, desc = "If all IK gaps (csHomebrewIK target - bone) are above -this, feet are planted and body lowering is skipped entirely." },
        new Entry { field = "gapThreshold",            label = "BL Gap Threshold",         step = 0.01f,  isBodyLower = true, desc = "Min gap (m) before IK target override fires. Raise if foot clips on ramps. Lower if large steps still float." },
        new Entry { field = "smoothTime",              label = "BL Smooth Time",           step = 0.01f,  isBodyLower = true, desc = "How quickly the extra body lowering ramps up/down. Lower = snappier but may jitter." },
        new Entry { field = "speedFadeThreshold",      label = "BL Speed Threshold",       step = 0.1f,   isBodyLower = true, desc = "Horizontal speed above which the body lowering fades to zero. Raise if correction should persist while walking." },
        new Entry { field = "ankleHeight",              label = "BL Ankle Height",          step = 0.005f, isBodyLower = true, desc = "Ankle height above surface (raySphereRadius + ankleHeightOffset). IK target is placed this far above the ground normal." },
    };

    /// Slow motion (0.2x). Toggled by the O key or the Escape menu — both go through
    /// this property so the time scale always matches the flag.
    public bool SlowMotion
    {
        get => slowMotion;
        set
        {
            slowMotion          = value;
            Time.timeScale      = value ? SlowScale : 1f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }
    }

    [Tooltip("Show the top-left diagnostics overlay (and foot gizmos). Toggle from the Escape menu.")]
    public bool showDiagnostics = false;

    [Tooltip("Show the right-side IK tuning panel (arrow-key driven). Toggle from the Escape menu.")]
    public bool showTuningPanel = false;

    private FischlWorks.csHomebrewIK ik;
    private IKBodyLower bodyLower;
    private Animator anim;
    private CharacterController cc;
    private int     selected     = 0;
    private float   holdTimer    = 0f;
    private bool    holding      = false;
    private Vector2 scrollPos;
    private bool    slowMotion   = false;
    private const float HoldDelay   = 0.35f;
    private const float HoldRate    = 0.07f;
    private const float SlowScale   = 0.2f;

    private void Awake()
    {
        ik        = GetComponentInChildren<FischlWorks.csHomebrewIK>();
        bodyLower = GetComponentInChildren<IKBodyLower>();
        anim      = GetComponentInChildren<Animator>();
        cc        = GetComponent<CharacterController>();
        if (ik == null)
            Debug.LogWarning("[IKDebugMenu] No csHomebrewIK found on this GameObject or its children.");
    }

    private void OnDestroy()
    {
        // Ensure timeScale is restored if play mode exits while slow-mo is active
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private void Update()
    {
        if (ik == null) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // Slow-mo toggle stays available even with all panels hidden
        if (kb.oKey.wasPressedThisFrame)
            SlowMotion = !SlowMotion;

        // Tuning keys only act when the panel is visible and the Escape menu is closed,
        // so values can't change invisibly.
        if (!showTuningPanel || GameMenu.IsOpen) return;

        // Navigate up / down
        if (kb.upArrowKey.wasPressedThisFrame)
            selected = (selected - 1 + Entries.Length) % Entries.Length;
        if (kb.downArrowKey.wasPressedThisFrame)
            selected = (selected + 1) % Entries.Length;

        // Change value — left / right with hold repeat
        int dir = 0;
        bool leftHeld  = kb.leftArrowKey.isPressed;
        bool rightHeld = kb.rightArrowKey.isPressed;

        if (kb.leftArrowKey.wasPressedThisFrame)  { dir = -1; holding = false; holdTimer = 0f; }
        if (kb.rightArrowKey.wasPressedThisFrame) { dir =  1; holding = false; holdTimer = 0f; }

        if ((leftHeld || rightHeld) && dir == 0)
        {
            holdTimer += Time.unscaledDeltaTime;
            float threshold = holding ? HoldRate : HoldDelay;
            if (holdTimer >= threshold)
            {
                dir = rightHeld ? 1 : -1;
                holding = true;
                holdTimer = 0f;
            }
        }

        if (!leftHeld && !rightHeld) { holding = false; holdTimer = 0f; }

        if (dir != 0) ApplyChange(dir);

        if (kb.rKey.wasPressedThisFrame) ResetSelected();
        if (kb.pKey.wasPressedThisFrame) PrintSnapshot();
    }

    private void ResetSelected()
    {
        var entry = Entries[selected];

        if (entry.field == "_bodyLowerEnabled")
        {
            if (bodyLower != null) bodyLower.enabled = Defaults[selected] >= 1f;
            return;
        }

        if (entry.isBodyLower)
        {
            SetBodyLowerField(entry.field, Defaults[selected]);
            return;
        }

        var field = typeof(FischlWorks.csHomebrewIK)
            .GetField(entry.field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return;

        if (entry.isBool)
            field.SetValue(ik, Defaults[selected] >= 1f);
        else
            field.SetValue(ik, Defaults[selected]);
    }

    private void ApplyChange(int dir)
    {
        var entry = Entries[selected];

        if (entry.field == "_bodyLowerEnabled")
        {
            if (bodyLower != null) bodyLower.enabled = !bodyLower.enabled;
            return;
        }

        if (entry.isBodyLower)
        {
            var blField = typeof(IKBodyLower)
                .GetField(entry.field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (blField == null || bodyLower == null) return;
            float current = (float)blField.GetValue(bodyLower);
            blField.SetValue(bodyLower, Mathf.Max(0f, current + dir * entry.step));
            return;
        }

        var field = typeof(FischlWorks.csHomebrewIK)
            .GetField(entry.field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return;

        if (entry.isBool)
        {
            field.SetValue(ik, !(bool)field.GetValue(ik));
        }
        else
        {
            float current = (float)field.GetValue(ik);
            float next    = current + dir * entry.step;

            // Clamp to sensible ranges
            if (entry.field == "ankleHeightOffset")       next = Mathf.Clamp(next, -0.15f,  0.125f);
            else if (entry.field == "raySphereRadius")    next = Mathf.Clamp(next,  0.01f,  0.1f);
            else if (entry.field == "globalWeight" ||
                     entry.field == "leftFootWeight"  ||
                     entry.field == "rightFootWeight") next = Mathf.Clamp01(next);
            else                                          next = Mathf.Max(0f, next);

            field.SetValue(ik, next);
        }
    }

    private void SetBodyLowerField(string fieldName, float value)
    {
        if (bodyLower == null) return;
        var f = typeof(IKBodyLower).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        f?.SetValue(bodyLower, value);
    }

    private void PrintSnapshot()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== IK SNAPSHOT ===");

        // csHomebrewIK fields
        sb.AppendLine("-- csHomebrewIK --");
        foreach (var e in Entries)
        {
            if (e.isBodyLower || e.field == "_bodyLowerEnabled") continue;
            var f = typeof(FischlWorks.csHomebrewIK)
                .GetField(e.field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) continue;
            object val = f.GetValue(ik);
            sb.AppendLine($"  {e.label,-26} = {(e.isBool ? ((bool)val ? "ON" : "OFF") : ((float)val).ToString("F4"))}");
        }

        // Bone/IK target positions
        Transform hips    = anim.GetBoneTransform(HumanBodyBones.Hips);
        Transform lFoot   = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rFoot   = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        Transform lKnee   = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform rKnee   = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        Vector3 lIKTarget = ik._LeftFootIKPositionTarget;
        Vector3 rIKTarget = ik._RightFootIKPositionTarget;
        float hipsY  = hips  != null ? hips.position.y  : 0f;
        float lBoneY = lFoot != null ? lFoot.position.y : 0f;
        float rBoneY = rFoot != null ? rFoot.position.y : 0f;
        float lKneeY = lKnee != null ? lKnee.position.y : 0f;
        float rKneeY = rKnee != null ? rKnee.position.y : 0f;
        sb.AppendLine("-- Bone & IK Targets --");
        sb.AppendLine($"  hips Y    = {hipsY:F4}   bodyPos Y = {anim.bodyPosition.y:F4}   root Y = {transform.position.y:F4}");
        sb.AppendLine($"  L foot Y  = {lBoneY:F4}   knee Y = {lKneeY:F4}   IK target Y = {lIKTarget.y:F4}   gap = {(lIKTarget.y - lBoneY):+0.0000;-0.0000;0.0000}");
        sb.AppendLine($"  R foot Y  = {rBoneY:F4}   knee Y = {rKneeY:F4}   IK target Y = {rIKTarget.y:F4}   gap = {(rIKTarget.y - rBoneY):+0.0000;-0.0000;0.0000}");
        sb.AppendLine($"  foot diff = {Mathf.Abs(lBoneY - rBoneY):F4}   hip→L = {(hipsY - lBoneY):F4}   hip→R = {(hipsY - rBoneY):F4}");

        // CharacterController
        if (cc != null)
        {
            float ccBottomY = transform.position.y + cc.center.y - cc.height * 0.5f;
            sb.AppendLine("-- CharacterController --");
            sb.AppendLine($"  grounded = {cc.isGrounded}   bottom Y = {ccBottomY:F4}   velocity = {cc.velocity}");
        }

        // IKBodyLower
        if (bodyLower != null)
        {
            sb.AppendLine("-- IKBodyLower --");
            sb.AppendLine($"  enabled       = {bodyLower.enabled}");
            sb.AppendLine($"  needsHelp     = {bodyLower.DbgNeedsHelp}   ikGapL = {bodyLower.DbgIKGapL:+0.0000;-0.0000;0.0000}   ikGapR = {bodyLower.DbgIKGapR:+0.0000;-0.0000;0.0000}");
            sb.AppendLine($"  worstGap      = {bodyLower.DbgRaycastWorstGap:+0.0000;-0.0000;0.0000}");
            sb.AppendLine($"  leftGap       = {bodyLower.DbgLeftGap:+0.0000;-0.0000;0.0000}   override = {bodyLower.DbgLeftOverride}");
            sb.AppendLine($"  rightGap      = {bodyLower.DbgRightGap:+0.0000;-0.0000;0.0000}   override = {bodyLower.DbgRightOverride}");
            sb.AppendLine($"  targetOffset  = {bodyLower.DbgTargetOffset:+0.0000;-0.0000;0.0000}");
            sb.AppendLine($"  currentOffset = {bodyLower.DbgCurrentOffset:+0.0000;-0.0000;0.0000}");
            sb.AppendLine($"  bodyPosY      = {bodyLower.DbgBodyPosY:F4}");
            sb.AppendLine($"  hSpeed        = {bodyLower.DbgHSpeed:F4}   threshold = {bodyLower.speedFadeThreshold:F4}");
        }

        sb.AppendLine("===================");
        Debug.Log(sb.ToString());
    }

    private void OnGUI()
    {
        if (ik == null) return;
        if (showTuningPanel) DrawTuningPanel();
        if (showDiagnostics) DrawDiagnostics();
    }

    // CheckSphere instead of cc.isGrounded — stable under any timeScale
    private bool ComputeGrounded()
    {
        if (cc == null) return false;
        Vector3 feetPos = transform.position + cc.center
                        + Vector3.down * (cc.height * 0.5f - cc.radius + 0.02f);
        return Physics.CheckSphere(feetPos, cc.radius + 0.05f,
                   ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore);
    }

    private void DrawTuningPanel()
    {
        bool grounded = ComputeGrounded();

        int   pad   = 10;
        int   lh    = 22;
        int   descH = 52;
        int   ccH   = cc != null ? (3 * lh + pad * 2) : 0;
        float width = 360f;
        float x     = Screen.width - width - pad;
        float y     = pad;

        // How much vertical space the non-scrolling parts need
        int titleH    = lh + pad * 2;          // title row + top/bottom padding
        int footerH   = (ccH > 0 ? ccH + pad : 0) + descH + pad;
        float maxPanelH = Screen.height - pad * 2;
        float entriesContentH = Entries.Length * lh;
        float entriesViewH  = Mathf.Min(entriesContentH, maxPanelH - titleH - footerH);
        float totalPanelH   = titleH + entriesViewH + footerH;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };
        GUIStyle normal = new GUIStyle(GUI.skin.label)
            { normal = { textColor = Color.white } };
        GUIStyle active = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, normal = { textColor = Color.cyan } };

        // ── Main background ──────────────────────────────────────────────────
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(x, y, width, totalPanelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // ── Title ────────────────────────────────────────────────────────────
        GUI.Label(new Rect(x + pad, y + pad, width - pad * 2, lh),
                  "IK DEBUG  [↑↓ navigate  ←→ change  R reset]", titleStyle);

        // ── Scrollable entries ───────────────────────────────────────────────
        float listY = y + titleH;

        // Auto-scroll to keep selected row visible
        float selTop = selected * lh;
        float selBot = selTop + lh;
        if (selTop < scrollPos.y)
            scrollPos.y = selTop;
        else if (selBot > scrollPos.y + entriesViewH)
            scrollPos.y = selBot - entriesViewH;

        Rect viewRect    = new Rect(x, listY, width, entriesViewH);
        Rect contentRect = new Rect(0, 0, width - 16, entriesContentH);
        scrollPos = GUI.BeginScrollView(viewRect, scrollPos, contentRect, false, false);

        for (int i = 0; i < Entries.Length; i++)
        {
            var    e      = Entries[i];
            string valStr;

            if (e.field == "_bodyLowerEnabled")
            {
                valStr = bodyLower != null ? (bodyLower.enabled ? "ON" : "OFF") : "—";
            }
            else if (e.isBodyLower)
            {
                if (bodyLower == null) { valStr = "—"; }
                else
                {
                    var blField = typeof(IKBodyLower)
                        .GetField(e.field, BindingFlags.NonPublic | BindingFlags.Instance);
                    valStr = blField != null ? ((float)blField.GetValue(bodyLower)).ToString("F3") : "—";
                }
            }
            else
            {
                var field = typeof(FischlWorks.csHomebrewIK)
                    .GetField(e.field, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) continue;
                object val = field.GetValue(ik);
                valStr = e.isBool ? ((bool)val ? "ON" : "OFF") : ((float)val).ToString("F3");
            }

            string prefix = i == selected ? "▶ " : "  ";
            GUI.Label(new Rect(pad, i * lh, width - pad * 2, lh),
                      $"{prefix}{e.label,-26} {valStr}",
                      i == selected ? active : normal);
        }

        GUI.EndScrollView();

        // ── CC panel ─────────────────────────────────────────────────────────
        float afterListY = listY + entriesViewH;
        if (cc != null)
        {
            float ccY = afterListY + pad;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x, ccY, width, ccH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle ccTitle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.6f, 0.9f, 1f) } };
            GUIStyle ccVal = new GUIStyle(GUI.skin.label)
                { normal = { textColor = Color.white } };

            GUI.Label(new Rect(x + pad, ccY + pad,          width, lh), "CHARACTER CONTROLLER", ccTitle);
            GUI.Label(new Rect(x + pad, ccY + pad + lh,     width, lh),
                $"  Skin Width   {cc.skinWidth:F4}   [direct edit in Inspector]", ccVal);
            GUI.Label(new Rect(x + pad, ccY + pad + lh * 2, width, lh),
                $"  Step Offset  {cc.stepOffset:F4}   Grounded: {(grounded ? "YES" : "NO")}", ccVal);
        }

        // ── Description ──────────────────────────────────────────────────────
        float descY = afterListY + (ccH > 0 ? ccH + pad : 0) + pad;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(x, descY, width, descH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle desc = new GUIStyle(GUI.skin.label)
            { wordWrap = true, normal = { textColor = new Color(1f, 1f, 0.6f) } };
        GUI.Label(new Rect(x + pad, descY + 4, width - pad * 2, descH - 8),
                  Entries[selected].desc, desc);
    }

    // Diagnostics panel — anchored to top-left so it's always visible
    private void DrawDiagnostics()
    {
        if (anim != null)
        {
            bool  grounded = ComputeGrounded();
            int   pad      = 10;
            int   lh       = 22;
            float width    = 360f;
            int   diagLines = bodyLower != null ? 15 : 10;
            float diagH     = diagLines * lh + pad * 2;
            float diagX     = pad;
            float diagY     = pad;

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(diagX, diagY, width, diagH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle diagTitle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold, normal = { textColor = Color.green } };
            GUIStyle diagVal = new GUIStyle(GUI.skin.label)
                { normal = { textColor = Color.white } };
            GUIStyle diagWarn = new GUIStyle(GUI.skin.label)
                { normal = { textColor = new Color(1f, 0.5f, 0.2f) } };

            GUI.Label(new Rect(diagX + pad, diagY + pad, width, lh), "DIAGNOSTICS", diagTitle);

            // Row 0: current animation state
            var clips = anim.GetCurrentAnimatorClipInfo(0);
            string clipName = clips.Length > 0 ? clips[0].clip.name : "—";
            float  clipWeight = clips.Length > 0 ? clips[0].weight : 0f;
            var nextClips = anim.GetNextAnimatorClipInfo(0);
            string nextStr = nextClips.Length > 0 ? $"  → {nextClips[0].clip.name} ({nextClips[0].weight:F2})" : "";
            string slowTag = slowMotion ? "  [SLOW x0.2]" : "";
            GUI.Label(new Rect(diagX + pad, diagY + pad + lh, width, lh),
                $"  ANIM: {clipName} ({clipWeight:F2}){nextStr}{slowTag}",
                new GUIStyle(GUI.skin.label) { normal = { textColor = slowMotion ? new Color(1f, 0.7f, 0.2f) : new Color(0.4f, 1f, 1f) } });

            Transform hips   = anim.GetBoneTransform(HumanBodyBones.Hips);
            Transform lFoot  = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rFoot  = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform lKnee  = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform rKnee  = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Vector3 lIKTarget = ik._LeftFootIKPositionTarget;
            Vector3 rIKTarget = ik._RightFootIKPositionTarget;

            float hipsY   = hips  != null ? hips.position.y  : 0f;
            float lBoneY  = lFoot != null ? lFoot.position.y : 0f;
            float rBoneY  = rFoot != null ? rFoot.position.y : 0f;
            float lKneeY  = lKnee != null ? lKnee.position.y : 0f;
            float rKneeY  = rKnee != null ? rKnee.position.y : 0f;
            float lGap    = lIKTarget.y - lBoneY;
            float rGap    = rIKTarget.y - rBoneY;

            // CharacterController grounding info — uses CheckSphere computed at top of OnGUI
            float ccBottomY = cc != null ? (transform.position.y + cc.center.y - cc.height * 0.5f) : 0f;

            float rowBase = diagY + pad + lh * 2;

            // Row 0-1: feet
            GUI.Label(new Rect(diagX + pad, rowBase,      width, lh), $"  L foot Y: {lBoneY:F3}  knee: {lKneeY:F3}  IK tgt: {lIKTarget.y:F3}", diagVal);
            GUI.Label(new Rect(diagX + pad, rowBase + lh, width, lh), $"  R foot Y: {rBoneY:F3}  knee: {rKneeY:F3}  IK tgt: {rIKTarget.y:F3}", diagVal);

            // Row 2-3: IK gaps
            string lGapStr = $"  L gap (IK-bone): {lGap:+0.0000;-0.0000;0.0000}  {(Mathf.Abs(lGap) > 0.005f ? "^ pull" : "ok")}";
            string rGapStr = $"  R gap (IK-bone): {rGap:+0.0000;-0.0000;0.0000}  {(Mathf.Abs(rGap) > 0.005f ? "^ pull" : "ok")}";
            GUI.Label(new Rect(diagX + pad, rowBase + lh * 2, width, lh), lGapStr, Mathf.Abs(lGap) > 0.005f ? diagWarn : diagVal);
            GUI.Label(new Rect(diagX + pad, rowBase + lh * 3, width, lh), rGapStr, Mathf.Abs(rGap) > 0.005f ? diagWarn : diagVal);

            // Row 4: hips + animator bodyPosition
            float bodyPosY = bodyLower != null ? bodyLower.DbgBodyPosY : anim.bodyPosition.y;
            GUI.Label(new Rect(diagX + pad, rowBase + lh * 4, width, lh),
                $"  hips Y: {hipsY:F3}   bodyPos Y: {bodyPosY:F3}   root Y: {transform.position.y:F3}", diagVal);

            // Row 5: CC
            string groundedStr = grounded ? "YES" : "NO";
            GUIStyle groundedStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = grounded ? Color.green : new Color(1f,0.3f,0.3f) } };
            GUI.Label(new Rect(diagX + pad, rowBase + lh * 5, width, lh),
                $"  CC grounded: {groundedStr}   bottom Y: {ccBottomY:F3}", groundedStyle);

            // Row 6: foot-to-foot height diff
            float footDiff = Mathf.Abs(lBoneY - rBoneY);
            GUI.Label(new Rect(diagX + pad, rowBase + lh * 6, width, lh),
                $"  foot diff: {footDiff:F3}   L-R: {(lBoneY - rBoneY):+0.000;-0.000;0.000}", diagVal);

            // Row 7: hip-to-foot distances (leg stretch)
            float lLegLen = hips != null && lFoot != null ? hipsY - lBoneY : 0f;
            float rLegLen = hips != null && rFoot != null ? hipsY - rBoneY : 0f;
            GUI.Label(new Rect(diagX + pad, rowBase + lh * 7, width, lh),
                $"  hip→L foot: {lLegLen:F3}   hip→R foot: {rLegLen:F3}", diagVal);

            if (bodyLower != null)
            {
                GUIStyle blStyle  = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 1f, 0.6f) } };
                GUIStyle blActive = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.9f, 0.2f) } };
                GUIStyle blSkip   = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };

                if (!bodyLower.enabled)
                {
                    GUI.Label(new Rect(diagX + pad, rowBase + lh * 8, width, lh), "  BL: disabled", blSkip);
                }
                else
                {
                    // Row 8: needsHelp + ikGaps from homebrew
                    string needsStr = bodyLower.DbgNeedsHelp ? "NEEDS HELP" : "skip (planted)";
                    string row8 = $"  BL: {needsStr}   ikGapL={bodyLower.DbgIKGapL:+0.000;-0.000;0.000}  ikGapR={bodyLower.DbgIKGapR:+0.000;-0.000;0.000}";
                    GUI.Label(new Rect(diagX + pad, rowBase + lh * 8, width, lh), row8, bodyLower.DbgNeedsHelp ? blActive : blSkip);

                    // Row 9: body offset
                    string row9 = $"  BL: tgt={bodyLower.DbgTargetOffset:+0.000;-0.000;0.000}  cur={bodyLower.DbgCurrentOffset:+0.000;-0.000;0.000}";
                    GUI.Label(new Rect(diagX + pad, rowBase + lh * 9, width, lh), row9, blStyle);

                    // Row 10-11: per-foot gaps + overrides
                    string lOvr = bodyLower.DbgLeftOverride  ? " [OVR]" : "";
                    string rOvr = bodyLower.DbgRightOverride ? " [OVR]" : "";
                    string row10 = $"  BL gaps: L={bodyLower.DbgLeftGap:+0.000;-0.000;0.000}{lOvr}  R={bodyLower.DbgRightGap:+0.000;-0.000;0.000}{rOvr}";
                    bool anyOvr = bodyLower.DbgLeftOverride || bodyLower.DbgRightOverride;
                    GUI.Label(new Rect(diagX + pad, rowBase + lh * 10, width, lh), row10, anyOvr ? blActive : blStyle);

                    // Row 11: speed + worstGap
                    string row11 = $"  BL: spd={bodyLower.DbgHSpeed:F2}  (thr={bodyLower.speedFadeThreshold:F1})  worst={bodyLower.DbgRaycastWorstGap:+0.000;-0.000;0.000}";
                    GUI.Label(new Rect(diagX + pad, rowBase + lh * 11, width, lh), row11, blStyle);
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showDiagnostics) return;

        // ── CharacterController bottom ──────────────────────────────────────
        if (cc != null)
        {
            Vector3 bottom = transform.position + cc.center - Vector3.up * (cc.height * 0.5f - cc.radius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(bottom, cc.radius);

            // Ground plane at transform.position.y
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            DrawWireDisc(transform.position, 0.5f);
        }

        if (anim == null || ik == null) return;

        // ── Foot bones (actual animated position) ──────────────────────────
        Transform lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);

        Gizmos.color = Color.red;
        if (lFoot != null) Gizmos.DrawWireSphere(lFoot.position, 0.015f);
        if (rFoot != null) Gizmos.DrawWireSphere(rFoot.position, 0.015f);

        // ── IK targets (where IK wants to move the ankle) ──────────────────
        Vector3 lTarget = ik._LeftFootIKPositionTarget;
        Vector3 rTarget = ik._RightFootIKPositionTarget;

        if (lFoot != null) { lTarget.x = lFoot.position.x; lTarget.z = lFoot.position.z; }
        if (rFoot != null) { rTarget.x = rFoot.position.x; rTarget.z = rFoot.position.z; }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(lTarget, 0.015f);
        Gizmos.DrawWireSphere(rTarget, 0.015f);

        // Lines connecting bone to IK target so the gap is obvious
        Gizmos.color = Color.cyan;
        if (lFoot != null) Gizmos.DrawLine(lFoot.position, lTarget);
        if (rFoot != null) Gizmos.DrawLine(rFoot.position, rTarget);
    }

    private static void DrawWireDisc(Vector3 center, float radius)
    {
        int   segments = 32;
        float step     = 360f / segments;
        Vector3 prev   = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float   angle = i * step * Mathf.Deg2Rad;
            Vector3 next  = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
