using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine.Animations.Rigging;

public static class IKTestSetup
{
    private const string ScenePath              = "Assets/FootIK_Test.unity";
    private const string CharacterLayerName     = "Character";
    private const string IdlePrefabPath         = "Assets/HomebrewIK/Demo/Models/Armature_Idle.prefab";
    private const string IdleFbxPath            = "Assets/HomebrewIK/Demo/Animations/Idle.fbx";
    private const string WalkFbxPath            = "Assets/HomebrewIK/Demo/Animations/Run.fbx";
    private const string HangIdleDaePath        = "Assets/Hanging Idle/Hanging Idle.dae";
    private const string GeneratedControllerPath = "Assets/FootIK_Demo.controller";
    private const string PlayerName             = "Player";
    private const string AutoRunPrefsKey        = "IKTestSetup.AutoRun";

    [InitializeOnLoadMethod]
    private static void AutoRunOnCompile()
    {
        if (!EditorPrefs.GetBool(AutoRunPrefsKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Run();
        };
    }

    [MenuItem("Tools/Setup")]
    public static void Run()
    {
        // Always reimport the scene from disk — never use Unity's cached (possibly dirty) version
        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        // Save other open scenes, but never save FootIK_Test (would lock in a polluted state)
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        }

        EditorSceneManager.OpenScene(ScenePath);

        // --- Physics layer ---
        int characterLayer = EnsureLayer(CharacterLayerName);

        // --- Level geometry ---
        BuildLevelGeometry();

        // Remove any previously created player first
        var existing = GameObject.Find(PlayerName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        // Clean up any stray components left on existing scene characters by previous runs
        foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            CleanupStrayComponents(a.gameObject);
            CleanupStrayComponents(a.transform.root.gameObject);
        }

        // --- Instantiate a fresh copy of Armature_Idle as the player ---
        var idlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IdlePrefabPath);
        if (idlePrefab == null)
        {
            Debug.LogError($"[Setup] Armature_Idle prefab not found at {IdlePrefabPath}");
            return;
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(idlePrefab);
        player.name = PlayerName;
        Undo.RegisterCreatedObjectUndo(player, "Create Player");
        // Spawn on top of Ledge_A (top surface at Y=1.5) so the player can walk to edges immediately
        player.transform.position = new Vector3(14f, 3.5f, 8f);
        player.transform.rotation = Quaternion.identity;

        // Assign URP materials (prefab defaults to Built-in RP which shows pink in URP)
        AssignURPMaterials(player);

        // --- Animator ---
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null) animator = player.GetComponent<Animator>();
        if (animator == null) { Debug.LogError("[Setup] No Animator on player."); return; }

        var controller = BuildDemoController();
        if (controller != null)
        {
            Undo.RecordObject(animator, "Assign Animator Controller");
            animator.runtimeAnimatorController = controller;
        }

        Undo.RecordObject(animator, "Disable Root Motion");
        animator.applyRootMotion = false;

        EnsureIKPass(animator.runtimeAnimatorController);

        // Put on Character layer so IK raycasts don't self-hit
        SetLayerRecursive(player, characterLayer);

        // --- csHomebrewIK ---
        GameObject animGO   = animator.gameObject;
        var homebrew = animGO.GetComponent<FischlWorks.csHomebrewIK>();
        if (homebrew == null) homebrew = Undo.AddComponent<FischlWorks.csHomebrewIK>(animGO);

        SerializedObject soIK = new SerializedObject(homebrew);
        soIK.FindProperty("groundLayers").intValue           = ~(1 << characterLayer);
        // Body positioning disabled — IKBodyLower owns all body-height adjustment.
        // csHomebrewIK's version crouches the body on small steps instead of just
        // raising the leg, which looks wrong. IKBodyLower only fires for large drops.
        soIK.FindProperty("enableBodyPositioning").boolValue = false;
        soIK.FindProperty("enableFootLifting").boolValue     = true;
        soIK.FindProperty("enableIKPositioning").boolValue   = true;
        soIK.FindProperty("enableIKRotating").boolValue      = true;
        soIK.FindProperty("globalWeight").floatValue         = 1f;
        soIK.FindProperty("leftFootWeight").floatValue       = 1f;
        soIK.FindProperty("rightFootWeight").floatValue      = 1f;
        soIK.FindProperty("crouchRange").floatValue          = 0.5f;
        soIK.FindProperty("rayCastRange").floatValue         = 1.5f;
        soIK.FindProperty("raySphereRadius").floatValue      = 0.05f;
        // 0.045 offset needed: Armature_Idle ankle bone sits slightly below raySphereRadius
        // height, causing feet to clip underground at 0. Verified visually.
        soIK.FindProperty("ankleHeightOffset").floatValue    = 0.045f;
        soIK.FindProperty("lengthFromHeelToToes").floatValue = 0.203f;
        soIK.ApplyModifiedProperties();

        // --- IKBodyLower ---
        IKBodyLower bodyLower = animGO.GetComponent<IKBodyLower>();
        if (bodyLower == null) bodyLower = Undo.AddComponent<IKBodyLower>(animGO);
        SerializedObject soBodyLower = new SerializedObject(bodyLower);
        soBodyLower.FindProperty("groundLayers").intValue       = ~(1 << characterLayer);
        // ankleHeight = raySphereRadius + ankleHeightOffset (must match csHomebrewIK values)
        soBodyLower.FindProperty("ankleHeight").floatValue      = 0.05f + 0.045f;
        // maxExtraLowering: 0.460 verified — enough to plant foot on tall steps
        soBodyLower.FindProperty("maxExtraLowering").floatValue = 0.460f;
        // plantedThreshold: skip body lowering if csHomebrewIK already planted both feet
        soBodyLower.FindProperty("plantedThreshold").floatValue = 0.04f;
        // gapThreshold: IK override fires when gap > this value.
        // Too low = overcorrects on gentle ramps. Too high = residual float on steps.
        soBodyLower.FindProperty("gapThreshold").floatValue     = 0.08f;
        soBodyLower.ApplyModifiedProperties();

        // --- Animation Rigging — hand IK for ledge hanging ---
        SetupHandIKRig(animGO, animator,
            out Transform leftHandTarget, out Transform rightHandTarget, out Rig handRig);

        // --- LedgeDetector ---
        LedgeDetector ledge = player.GetComponent<LedgeDetector>();
        if (ledge == null) ledge = Undo.AddComponent<LedgeDetector>(player);
        SerializedObject soLedge = new SerializedObject(ledge);
        soLedge.FindProperty("groundLayers").intValue                    = ~(1 << characterLayer);
        soLedge.FindProperty("leftHandTarget").objectReferenceValue      = leftHandTarget;
        soLedge.FindProperty("rightHandTarget").objectReferenceValue     = rightHandTarget;
        soLedge.FindProperty("handRig").objectReferenceValue             = handRig;
        soLedge.FindProperty("footIK").objectReferenceValue              = animGO.GetComponent<FischlWorks.csHomebrewIK>();
        soLedge.FindProperty("bodyLower").objectReferenceValue           = animGO.GetComponent<IKBodyLower>();
        soLedge.FindProperty("characterAnimator").objectReferenceValue   = animator;
        soLedge.ApplyModifiedProperties();

        // Wire LedgeDetector back into IKBodyLower — must happen after LedgeDetector exists
        soBodyLower.FindProperty("ledgeDetector").objectReferenceValue = ledge;
        soBodyLower.ApplyModifiedProperties();

        // --- CharacterController sized to the actual mesh bounds ---
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = Undo.AddComponent<CharacterController>(player);
        Undo.RecordObject(cc, "Size CharacterController");
        SizeCharacterController(cc, player);

        // --- SimpleCharacter ---
        SimpleCharacter sc = player.GetComponent<SimpleCharacter>();
        if (sc == null) sc = Undo.AddComponent<SimpleCharacter>(player);

        // --- IK debug menu ---
        IKDebugMenu dbg = player.GetComponent<IKDebugMenu>();
        if (dbg == null) Undo.AddComponent<IKDebugMenu>(player);

        // --- Camera ---
        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            var camGO = new GameObject("Camera");
            Undo.RegisterCreatedObjectUndo(camGO, "Create Camera");
            cam = Undo.AddComponent<Camera>(camGO);
            Undo.AddComponent<AudioListener>(camGO);
        }

        if (cam.tag != "MainCamera")
        {
            Undo.RecordObject(cam.gameObject, "Tag MainCamera");
            cam.tag = "MainCamera";
        }

        var freeCam = cam.GetComponent<FreeCam>();
        if (freeCam != null) Undo.DestroyObjectImmediate(freeCam);

        FollowCamera follow = cam.GetComponent<FollowCamera>();
        if (follow == null) follow = Undo.AddComponent<FollowCamera>(cam.gameObject);

        // Wire camera → player and player → camera
        SerializedObject soCam = new SerializedObject(follow);
        soCam.FindProperty("target").objectReferenceValue = player.transform;
        soCam.ApplyModifiedProperties();

        SerializedObject soChar = new SerializedObject(sc);
        soChar.FindProperty("cameraTransform").objectReferenceValue  = cam.transform;
        soChar.FindProperty("ledgeDetector").objectReferenceValue    = player.GetComponent<LedgeDetector>();
        soChar.ApplyModifiedProperties();

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Setup] Done. Hit Play — WASD to move, mouse to rotate camera.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void EnsureIKPass(RuntimeAnimatorController runtimeController)
    {
        var ac = runtimeController as AnimatorController;
        if (ac == null && runtimeController is AnimatorOverrideController aoc)
            ac = aoc.runtimeAnimatorController as AnimatorController;
        if (ac == null) return;

        var layers = ac.layers;
        if (layers.Length == 0 || layers[0].iKPass) return;

        layers[0].iKPass = true;
        ac.layers = layers;
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] IK Pass enabled on animator base layer.");
    }

    private static int EnsureLayer(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer != -1) return layer;

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
        var layersProp = tagManager.FindProperty("layers");

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var slot = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[Setup] Created physics layer '{layerName}' at index {i}.");
                return i;
            }
        }

        Debug.LogWarning("[Setup] No free layer slot found.");
        return 0;
    }

    private static void SizeCharacterController(CharacterController cc, GameObject player)
    {
        // Measure actual mesh bounds so the capsule bottom aligns with the feet
        var renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (renderers.Length == 0)
        {
            // Fallback defaults
            cc.height     = 1.8f;
            cc.radius     = 0.3f;
            cc.center     = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;
            cc.skinWidth  = 0.008f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);

        // Convert to local space
        Vector3 localMin = player.transform.InverseTransformPoint(bounds.min);
        Vector3 localMax = player.transform.InverseTransformPoint(bounds.max);

        float height  = localMax.y - localMin.y;
        float radius  = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.4f;
        radius        = Mathf.Clamp(radius, 0.15f, 0.5f);
        // Center Y: midpoint between bounds, then shifted up by the sole offset so the
        // character root sinks below ground level and the foot mesh sole sits at Y=0.
        // The Armature mesh origin is ~9 cm above the foot sole — without this bias the
        // character would float that amount above the ground when the CC lands.
        const float SoleBias = 0.09f;
        float centerY = localMin.y + height * 0.5f + SoleBias;

        cc.height     = Mathf.Max(height, radius * 2f + 0.01f);
        cc.radius     = radius;
        cc.center     = new Vector3(0f, centerY, 0f);
        cc.stepOffset = Mathf.Clamp(height * 0.1f, 0.05f, 0.4f);
        cc.skinWidth  = 0.008f;

        Debug.Log($"[Setup] CharacterController sized from bounds — height:{cc.height:F2} radius:{cc.radius:F2} center.y:{cc.center.y:F2}");
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private static void CleanupStrayComponents(GameObject go)
    {
        // Only remove movement components we may have added in a previous run.
        // Never touch csHomebrewIK — the original scene characters need it.
        var sc = go.GetComponent<SimpleCharacter>();
        if (sc != null) Undo.DestroyObjectImmediate(sc);

        var cc = go.GetComponent<CharacterController>();
        if (cc != null) Undo.DestroyObjectImmediate(cc);

        var ld = go.GetComponent<LedgeDetector>();
        if (ld != null) Undo.DestroyObjectImmediate(ld);
    }

    private static AnimatorController BuildDemoController()
    {
        AnimationClip idleClip = null;
        AnimationClip runClip  = null;
        AnimationClip hangClip = null;

        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(IdleFbxPath))
            if (obj is AnimationClip c && !c.name.Contains("__preview__")) { idleClip = c; break; }

        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(WalkFbxPath))
            if (obj is AnimationClip c && !c.name.Contains("__preview__")) { runClip = c; break; }

        // Hanging Idle from Mixamo DAE — optional; controller still builds without it
        AssetDatabase.ImportAsset(HangIdleDaePath, ImportAssetOptions.ForceSynchronousImport);
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(HangIdleDaePath))
            if (obj is AnimationClip c && !c.name.Contains("__preview__")) { hangClip = c; break; }

        if (hangClip != null)
            Debug.Log($"[Setup] Found hang clip: {hangClip.name}  length={hangClip.length:F2}s");
        else
            Debug.LogWarning("[Setup] Hang Idle clip not found — Hang state will be skipped.");

        if (idleClip == null || runClip == null)
        {
            Debug.LogError("[Setup] Could not find Idle or Run clip in HomebrewIK FBXes.");
            return null;
        }

        // Reuse existing asset so the scene reference stays valid across re-runs
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(GeneratedControllerPath);
        if (ac == null)
            ac = AnimatorController.CreateAnimatorControllerAtPath(GeneratedControllerPath);

        // Clear and rebuild parameters
        while (ac.parameters.Length > 0) ac.RemoveParameter(0);
        ac.AddParameter("Speed",     AnimatorControllerParameterType.Float);
        ac.AddParameter("IsHanging", AnimatorControllerParameterType.Bool);

        // Rebuild the base layer state machine
        var layer = ac.layers[0];
        layer.iKPass = true;
        ac.layers    = new[] { layer };

        var sm = ac.layers[0].stateMachine;
        foreach (var s in sm.states) sm.RemoveState(s.state);

        // Locomotion blend tree (Idle ↔ Run)
        var locoState = sm.AddState("Locomotion", new Vector3(200, 0));
        BlendTree bt  = new BlendTree();
        AssetDatabase.AddObjectToAsset(bt, GeneratedControllerPath);
        bt.name                   = "Locomotion";
        bt.blendType              = BlendTreeType.Simple1D;
        bt.blendParameter         = "Speed";
        bt.useAutomaticThresholds = false;
        bt.AddChild(idleClip, 0f);
        bt.AddChild(runClip,  1f);
        locoState.motion = bt;
        sm.defaultState  = locoState;

        // Hang state (only added when clip is available)
        if (hangClip != null)
        {
            var hangState = sm.AddState("Hang", new Vector3(200, 120));
            hangState.motion = hangClip;

            // Locomotion → Hang
            var toHang = locoState.AddTransition(hangState);
            toHang.AddCondition(AnimatorConditionMode.If, 0, "IsHanging");
            toHang.duration            = 0.15f;
            toHang.hasExitTime         = false;

            // Hang → Locomotion
            var toLoco = hangState.AddTransition(locoState);
            toLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHanging");
            toLoco.duration            = 0.2f;
            toLoco.hasExitTime         = false;
        }

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] Built FootIK_Demo.controller.");
        return ac;
    }

    // ── Level geometry ───────────────────────────────────────────────────────

    private const string LevelRootName = "Level";

    private static void BuildLevelGeometry()
    {
        // Tear down from a previous run
        var old = GameObject.Find(LevelRootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject(LevelRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Level");

        // ── Floor ─────────────────────────────────────────────────────────────
        // 60×60 flat, top face at Y=0
        Box(root, "Floor", pos: new Vector3(0, -0.5f, 0), scale: new Vector3(60, 1, 60));

        // ── Stepped ramp (straight ahead from spawn, along +Z) ───────────────
        // Four steps leading up so the foot IK ramp/slope behaviour is visible
        Box(root, "Step_Low",    pos: new Vector3(0, 0.10f,  8f), scale: new Vector3(5, 0.20f, 3));
        Box(root, "Step_Mid",    pos: new Vector3(0, 0.25f, 13f), scale: new Vector3(5, 0.50f, 3));
        Box(root, "Step_High",   pos: new Vector3(0, 0.50f, 18f), scale: new Vector3(5, 1.00f, 3));
        Box(root, "Step_Tall",   pos: new Vector3(0, 0.75f, 23f), scale: new Vector3(5, 1.50f, 3));

        // ── Diagonal ramp (slope for foot rotation IK, to the right) ─────────
        // Tilted slab — low end at Z≈4, high end at Z≈10, rise ~1.2 m over 6 m
        var ramp = Box(root, "Ramp", pos: new Vector3(9, 0.55f, 7f), scale: new Vector3(4, 0.3f, 7));
        ramp.transform.localEulerAngles = new Vector3(-18, 0, 0);

        // ── Ledge platforms (tall drop on at least one side) ──────────────────
        // These are the primary targets for the ledge hang system.
        // "Ledge_*" — character walks onto them, approaches edge, drop > 0.8 m triggers hang.

        // Island A — 1.5 m tall, to the right (+X)
        Box(root, "Ledge_A", pos: new Vector3(14, 0.75f,  8f), scale: new Vector3(8, 1.5f, 8));

        // Island B — 2.0 m tall, further right
        Box(root, "Ledge_B", pos: new Vector3(14, 1.00f, 20f), scale: new Vector3(8, 2.0f, 8));

        // Island C — 1.2 m tall, to the left (−X)
        Box(root, "Ledge_C", pos: new Vector3(-12, 0.60f, 8f), scale: new Vector3(8, 1.2f, 8));

        // Island D — 1.8 m tall, far left — narrow, good for hang testing
        Box(root, "Ledge_D", pos: new Vector3(-12, 0.90f, 20f), scale: new Vector3(6, 1.8f, 6));

        // ── Scattered low obstacles (misc step/bump testing) ──────────────────
        Box(root, "Bump_A", pos: new Vector3( 5,  0.08f, -5f), scale: new Vector3(3, 0.16f, 3));
        Box(root, "Bump_B", pos: new Vector3(-5,  0.12f, -8f), scale: new Vector3(3, 0.24f, 4));
        Box(root, "Bump_C", pos: new Vector3( 8,  0.06f, -3f), scale: new Vector3(2, 0.12f, 2));

        EditorUtility.SetDirty(root);
        Debug.Log("[Setup] Level geometry rebuilt.");
    }

    // Creates a cube primitive parented under root. Returns the GameObject so
    // the caller can adjust rotation if needed (e.g. the ramp).
    private static GameObject Box(GameObject parent, string name, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        // Static flags so Unity can batch/lightmap the geometry
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic
                                                 | StaticEditorFlags.OccluderStatic
                                                 | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    private static void SetupHandIKRig(GameObject animGO, Animator animator,
        out Transform leftHandTarget, out Transform rightHandTarget, out Rig handRig)
    {
        leftHandTarget  = null;
        rightHandTarget = null;
        handRig         = null;

        // Tear down any rig from a previous run
        var existingRig = animGO.transform.Find("IKRig");
        if (existingRig != null) Undo.DestroyObjectImmediate(existingRig.gameObject);

        var existingRigBuilder = animGO.GetComponent<RigBuilder>();
        if (existingRigBuilder != null) Undo.DestroyObjectImmediate(existingRigBuilder);

        // RigBuilder on the Animator GO
        var rigBuilder = Undo.AddComponent<RigBuilder>(animGO);

        // IKRig child GO
        var rigGO = new GameObject("IKRig");
        Undo.RegisterCreatedObjectUndo(rigGO, "Create IKRig");
        rigGO.transform.SetParent(animGO.transform, false);
        var rig = Undo.AddComponent<Rig>(rigGO);
        rig.weight = 0f;  // blended out until a ledge grab occurs

        // Wire Rig into RigBuilder layers list
        var soRB      = new SerializedObject(rigBuilder);
        var layersProp = soRB.FindProperty("m_RigLayers");
        layersProp.ClearArray();
        layersProp.InsertArrayElementAtIndex(0);
        var layerElem = layersProp.GetArrayElementAtIndex(0);
        layerElem.FindPropertyRelative("m_Rig").objectReferenceValue = rig;
        layerElem.FindPropertyRelative("m_Active").boolValue         = true;
        soRB.ApplyModifiedProperties();

        // TwoBoneIK for each arm
        leftHandTarget  = SetupArmIK(rigGO, animator, "Left",
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        rightHandTarget = SetupArmIK(rigGO, animator, "Right",
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);

        handRig = rig;
        EditorUtility.SetDirty(animGO);
        Debug.Log("[Setup] Animation Rigging hand IK rig created.");
    }

    private static Transform SetupArmIK(GameObject rigGO, Animator animator, string side,
        HumanBodyBones upperArmBone, HumanBodyBones lowerArmBone, HumanBodyBones handBone)
    {
        Transform upperArmT = animator.GetBoneTransform(upperArmBone);
        Transform lowerArmT = animator.GetBoneTransform(lowerArmBone);
        Transform handT     = animator.GetBoneTransform(handBone);

        if (upperArmT == null || lowerArmT == null || handT == null)
        {
            Debug.LogWarning($"[Setup] {side} arm bones not found — skipping arm IK.");
            return null;
        }

        // Constraint GO under the Rig
        var constraintGO = new GameObject($"{side}HandIK");
        Undo.RegisterCreatedObjectUndo(constraintGO, $"Create {side}HandIK");
        constraintGO.transform.SetParent(rigGO.transform, false);
        var constraint = Undo.AddComponent<TwoBoneIKConstraint>(constraintGO);

        // Hand target — starts at the animated hand bone world position
        var targetGO = new GameObject($"{side}HandTarget");
        Undo.RegisterCreatedObjectUndo(targetGO, $"Create {side}HandTarget");
        targetGO.transform.SetParent(constraintGO.transform, false);
        targetGO.transform.position = handT.position;
        targetGO.transform.rotation = handT.rotation;

        // Elbow hint — placed on the elbow's "outward" side to preserve natural bend
        var hintGO = new GameObject($"{side}ElbowHint");
        Undo.RegisterCreatedObjectUndo(hintGO, $"Create {side}ElbowHint");
        hintGO.transform.SetParent(constraintGO.transform, false);
        Vector3 midPoint    = (upperArmT.position + handT.position) * 0.5f;
        Vector3 elbowOffset = (lowerArmT.position - midPoint).normalized;
        hintGO.transform.position = lowerArmT.position + elbowOffset * 0.3f;

        // Wire constraint data
        var so = new SerializedObject(constraint);
        so.FindProperty("m_Data.m_Root").objectReferenceValue              = upperArmT;
        so.FindProperty("m_Data.m_Mid").objectReferenceValue               = lowerArmT;
        so.FindProperty("m_Data.m_Tip").objectReferenceValue               = handT;
        so.FindProperty("m_Data.m_Target").objectReferenceValue            = targetGO.transform;
        so.FindProperty("m_Data.m_Hint").objectReferenceValue              = hintGO.transform;
        so.FindProperty("m_Data.m_TargetPositionWeight").floatValue        = 1f;
        so.FindProperty("m_Data.m_TargetRotationWeight").floatValue        = 0f;
        so.FindProperty("m_Data.m_HintWeight").floatValue                  = 0.5f;
        so.FindProperty("m_Data.m_MaintainTargetPositionOffset").boolValue = false;
        so.FindProperty("m_Data.m_MaintainTargetRotationOffset").boolValue = false;
        so.ApplyModifiedProperties();

        return targetGO.transform;
    }

    private static void AssignURPMaterials(GameObject player)
    {
        // Map Built-in material names to their URP equivalents
        string urpFolder = "Assets/HomebrewIK/Demo/Materials/URP";
        var urpMaterials = new System.Collections.Generic.Dictionary<string, Material>();

        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { urpFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) urpMaterials[mat.name] = mat;
        }

        if (urpMaterials.Count == 0)
        {
            Debug.LogWarning("[Setup] No URP materials found — character may appear pink.");
            return;
        }

        foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = smr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                // Strip "Built-in" prefix if present to find URP equivalent by name
                string baseName = mats[i].name.Replace(" (Built-in)", "");
                if (urpMaterials.TryGetValue(baseName, out var urp))
                    mats[i] = urp;
            }
            smr.sharedMaterials = mats;
        }
    }
}
