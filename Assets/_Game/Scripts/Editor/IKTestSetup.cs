using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine.Animations.Rigging;

public static class IKTestSetup
{
    private const string ScenePath              = "Assets/_Game/Scenes/FootIK_Test.unity";
    private const string CharacterLayerName     = "Character";
    private const string IdlePrefabPath         = "Assets/ThirdParty/HomebrewIK/Demo/Models/Armature_Idle.prefab";
    private const string IdleFbxPath            = "Assets/ThirdParty/HomebrewIK/Demo/Animations/Idle.fbx";
    private const string WalkFbxPath            = "Assets/ThirdParty/HomebrewIK/Demo/Animations/Run.fbx";
    private const string HangIdleDaePath        = "Assets/ThirdParty/Hanging Idle/Hanging Idle.dae";
    private const string JumpBeginFbxPath       = "Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Jump01 - Begin.fbx";
    // Fall01 is the pack's airborne hold loop, meant to sit between Begin and Land for
    // variable-height jumps. Do NOT use HumanM@Jump01.fbx here: it is the FULL jump
    // (takeoff+air+land, looping), so long airtime visibly restarts the jump at the peak.
    private const string JumpAirFbxPath         = "Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Fall01.fbx";
    private const string JumpLandFbxPath        = "Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Jump01 - Land.fbx";
    // Kimodo-generated crawl motions (Humanoid, retargeted via KimodoImport).
    private const string KimodoFolder         = "Assets/_Game/Resources/KimodoMotions/";
    // Crouch uses authored DoubleL clips, not Kimodo: text-to-motion could not produce
    // a stable held crouch idle (it kept yielding waist-bends / kneels / sprawls). The
    // DoubleL "One Hand Up" crouch set is a proper feet-flat half-crouch — Idle for the
    // held pose, Crouch_F for the slow forward sneak — and both are already Humanoid.
    private const string DoubleLCrouchFolder  = "Assets/ThirdParty/DoubleL/One Hand Up/Movement/Crouch/";
    private const string CrouchIdleFbxPath    = DoubleLCrouchFolder + "Idle/Idle/OneHand_Up_Crouch_Idle_1.fbx";
    private const string CrouchWalkFbxPath    = DoubleLCrouchFolder + "Base/OneHand_Up_Crouch_F.fbx";
    private const string CrawlIdleFbxPath     = KimodoFolder + "KimodoCrawlIdle.fbx";
    private const string CrawlForwardFbxPath  = KimodoFolder + "KimodoCrawlForward.fbx";
    private const string CrawlLeftFbxPath     = KimodoFolder + "KimodoCrawlLeft.fbx";
    private const string CrawlRightFbxPath    = KimodoFolder + "KimodoCrawlRight.fbx";
    // Alternate playable characters for the in-menu Character switcher (Humanoid models).
    private const string DoubleLCharacterPrefab = "Assets/ThirdParty/DoubleL/Model/Armature (1).prefab";
    private const string RPGCharacterPrefab     = "Assets/ThirdParty/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Prefabs/Character/RPG-Character.prefab";
    private const string CasualManFbxPath       = "Assets/ThirdParty/CasualMan/casualman_rigged.fbx";
    private const string GeneratedControllerPath = "Assets/_Game/Animation/FootIK_Demo.controller";
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
    public static void Run() => RunInternal(force: false);

    [MenuItem("Tools/Setup (Force Rebuild)")]
    public static void RunForced() => RunInternal(force: true);

    private static void RunInternal(bool force)
    {
        // Skip the rebuild — and its fileID churn — when nothing that affects the
        // generated controller/scene has changed since the last successful build.
        if (!force && BuildIsUpToDate())
        {
            Debug.Log("[Setup] No relevant changes since last build — skipped. " +
                      "Use Tools/Setup (Force Rebuild) to rebuild anyway.");
            return;
        }

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
        player.transform.position = new Vector3(14f, 13.5f, 8f);
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
        if (dbg == null) dbg = Undo.AddComponent<IKDebugMenu>(player);

        // --- Escape menu (toggles the debug overlays) ---
        GameMenu menu = player.GetComponent<GameMenu>();
        if (menu == null) menu = Undo.AddComponent<GameMenu>(player);

        // --- Character switcher (runtime model swap, listed in the menu) ---
        CharacterSwitcher switcher = player.GetComponent<CharacterSwitcher>();
        if (switcher == null) switcher = Undo.AddComponent<CharacterSwitcher>(player);
        var charEntries = new System.Collections.Generic.List<CharacterSwitcher.Entry>
        {
            new CharacterSwitcher.Entry { name = "Mannequin", prefab = null, scale = 1f },
        };
        foreach (var pair in new[] {
            ("DoubleL",    DoubleLCharacterPrefab),
            ("Casual Man", CasualManFbxPath) })
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Item2);
            if (pf != null)
                charEntries.Add(new CharacterSwitcher.Entry { name = pair.Item1, prefab = pf, scale = 1f });
            else
                Debug.LogWarning($"[Setup] Character prefab not found: {pair.Item2}");
        }
        // 'animator' is on the original model child; that GameObject is the index-0 model.
        switcher.Configure(controller, animator.gameObject, charEntries);

        SerializedObject soMenu = new SerializedObject(menu);
        soMenu.FindProperty("ikDebugMenu").objectReferenceValue       = dbg;
        soMenu.FindProperty("characterSwitcher").objectReferenceValue = switcher;
        soMenu.ApplyModifiedProperties();

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
        EditorPrefs.SetString(BuildHashKey, ComputeBuildHash());
        Debug.Log("[Setup] Done. Hit Play — WASD to move, mouse to rotate camera.");
    }

    // ── Build-up-to-date gate ─────────────────────────────────────────────────
    // A build is "up to date" when this script's source and the input animation
    // assets are unchanged since the last successful build. Re-running setup then
    // is a no-op — which avoids re-serializing the controller/scene with fresh
    // fileIDs, the source of the large no-op git diffs. AutoRunOnCompile also
    // benefits: editing any other script no longer triggers a full rebuild.

    private static string BuildHashKey => "IKTestSetup.BuildHash:" + Application.dataPath;

    private static bool BuildIsUpToDate()
    {
        // If the controller is missing, a rebuild is always needed.
        if (!System.IO.File.Exists(GeneratedControllerPath)) return false;
        return EditorPrefs.GetString(BuildHashKey, "") == ComputeBuildHash();
    }

    private static string ComputeBuildHash()
    {
        var sb = new System.Text.StringBuilder();

        // This script defines the entire controller/scene structure, so any edit to
        // it (a transition value, a state, geometry) must invalidate the build.
        try { sb.Append(System.IO.File.ReadAllText(SourceFilePath())); }
        catch { sb.Append(System.Guid.NewGuid().ToString()); } // unreadable -> never matches

        // Swapping an input clip (e.g. the air animation) must invalidate it too.
        foreach (var p in new[] {
            IdlePrefabPath, IdleFbxPath, WalkFbxPath, HangIdleDaePath,
            JumpBeginFbxPath, JumpAirFbxPath, JumpLandFbxPath,
            CrouchIdleFbxPath, CrouchWalkFbxPath, CrawlIdleFbxPath,
            CrawlForwardFbxPath, CrawlLeftFbxPath, CrawlRightFbxPath,
            DoubleLCharacterPrefab, RPGCharacterPrefab, CasualManFbxPath })
            sb.Append(p).Append('=').Append(AssetDatabase.AssetPathToGUID(p)).Append(';');

        using (var md5 = System.Security.Cryptography.MD5.Create())
            return System.Convert.ToBase64String(
                md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString())));
    }

    // Compile-time absolute path of this source file — used to detect edits.
    private static string SourceFilePath(
        [System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

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
        // Step offset must clear the tallest stair step (0.45 m grand staircase).
        // Using 0.25× height gives ~0.45 m for a 1.8 m character.
        cc.stepOffset = Mathf.Clamp(height * 0.25f, 0.1f, 0.55f);
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

    private static AnimationClip LoadFirstClip(string path)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            if (obj is AnimationClip c && !c.name.Contains("__preview__")) return c;
        return null;
    }

    private static AnimatorController BuildDemoController()
    {
        var idleClip      = LoadFirstClip(IdleFbxPath);
        var runClip       = LoadFirstClip(WalkFbxPath);

        AssetDatabase.ImportAsset(HangIdleDaePath, ImportAssetOptions.ForceSynchronousImport);
        var hangClip      = LoadFirstClip(HangIdleDaePath);

        var jumpBeginClip = LoadFirstClip(JumpBeginFbxPath);
        var jumpAirClip   = LoadFirstClip(JumpAirFbxPath);
        var jumpLandClip  = LoadFirstClip(JumpLandFbxPath);

        if (idleClip == null || runClip == null)
        {
            Debug.LogError("[Setup] Could not find Idle or Run clip in HomebrewIK FBXes.");
            return null;
        }

        if (hangClip      == null) Debug.LogWarning("[Setup] Hang Idle clip not found — Hang state skipped.");
        if (jumpBeginClip == null) Debug.LogWarning("[Setup] Jump Begin clip not found — Jump states skipped.");

        // Reuse existing asset so the scene reference stays valid across re-runs
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(GeneratedControllerPath);
        if (ac == null)
            ac = AnimatorController.CreateAnimatorControllerAtPath(GeneratedControllerPath);

        // Clear and rebuild parameters
        while (ac.parameters.Length > 0) ac.RemoveParameter(0);
        ac.AddParameter("Speed",          AnimatorControllerParameterType.Float);
        ac.AddParameter("IsHanging",      AnimatorControllerParameterType.Bool);
        ac.AddParameter("IsGrounded",       AnimatorControllerParameterType.Bool);
        ac.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
        ac.AddParameter("TimeInAir",        AnimatorControllerParameterType.Float);
        ac.AddParameter("IsCrouching",      AnimatorControllerParameterType.Bool);
        ac.AddParameter("IsCrawling",       AnimatorControllerParameterType.Bool);
        ac.AddParameter("MoveX",            AnimatorControllerParameterType.Float);

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

        // Jump states
        bool hasJump = jumpBeginClip != null && jumpAirClip != null && jumpLandClip != null;
        if (hasJump)
        {
            var jumpBeginState = sm.AddState("JumpBegin", new Vector3(500, -80));
            var jumpAirState   = sm.AddState("JumpAir",   new Vector3(500,   0));
            var jumpLandState  = sm.AddState("JumpLand",  new Vector3(500,  80));

            jumpBeginState.motion = jumpBeginClip;
            jumpAirState.motion   = jumpAirClip;
            jumpLandState.motion  = jumpLandClip;

            // Locomotion → JumpBegin  (just left the ground going up)
            var locoToBegin = locoState.AddTransition(jumpBeginState);
            locoToBegin.AddCondition(AnimatorConditionMode.IfNot,   0,    "IsGrounded");
            locoToBegin.AddCondition(AnimatorConditionMode.Greater, 0.5f, "VerticalVelocity");
            locoToBegin.hasExitTime = false;
            locoToBegin.duration    = 0.1f;

            // Locomotion → JumpAir  (walked off an edge — no jump, already falling)
            var locoToAir = locoState.AddTransition(jumpAirState);
            locoToAir.AddCondition(AnimatorConditionMode.IfNot, 0,     "IsGrounded");
            locoToAir.AddCondition(AnimatorConditionMode.Less,  -0.5f, "VerticalVelocity");
            locoToAir.hasExitTime = false;
            locoToAir.duration    = 0.15f;

            // JumpBegin → JumpAir  (let takeoff finish)
            var beginToAir = jumpBeginState.AddTransition(jumpAirState);
            beginToAir.hasExitTime = true;
            beginToAir.exitTime    = 0.7f;
            beginToAir.duration    = 0.1f;

            // JumpAir → JumpLand
            var airToLand = jumpAirState.AddTransition(jumpLandState);
            airToLand.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            airToLand.hasExitTime = false;
            airToLand.duration    = 0.1f;

            // JumpLand → JumpBegin  (jumped again before landing clip finished)
            var landToBegin = jumpLandState.AddTransition(jumpBeginState);
            landToBegin.AddCondition(AnimatorConditionMode.IfNot,   0,    "IsGrounded");
            landToBegin.AddCondition(AnimatorConditionMode.Greater, 0.5f, "VerticalVelocity");
            landToBegin.hasExitTime = false;
            landToBegin.duration    = 0.05f;

            // JumpAir → JumpBegin  (bunny-hop: landed for one frame and jumped again)
            var airToBegin = jumpAirState.AddTransition(jumpBeginState);
            airToBegin.AddCondition(AnimatorConditionMode.IfNot,   0,    "IsGrounded");
            airToBegin.AddCondition(AnimatorConditionMode.Greater, 2.0f, "VerticalVelocity");
            airToBegin.hasExitTime = false;
            airToBegin.duration    = 0.05f;

            // JumpLand → Locomotion  (blend out over the last ~30% of the clip).
            // duration is in NORMALIZED clip time (hasFixedDuration = false) so that
            // exitTime + duration < 1.0 holds for any clip length — the blend finishes
            // before the clip can loop back to frame 0 and bleed the air pose through
            // (the "weird legs" glitch). A fixed-seconds duration can't guarantee this.
            var landToLoco = jumpLandState.AddTransition(locoState);
            landToLoco.hasExitTime      = true;
            landToLoco.exitTime         = 0.50f;
            landToLoco.hasFixedDuration = false;
            landToLoco.duration         = 0.48f;

            Debug.Log("[Setup] Jump states wired.");
        }

        // Hang state
        if (hangClip != null)
        {
            var hangState = sm.AddState("Hang", new Vector3(200, 200));
            hangState.motion = hangClip;

            var toHang = locoState.AddTransition(hangState);
            toHang.AddCondition(AnimatorConditionMode.If, 0, "IsHanging");
            toHang.duration    = 0.15f;
            toHang.hasExitTime = false;

            var toLoco = hangState.AddTransition(locoState);
            toLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHanging");
            toLoco.duration    = 0.2f;
            toLoco.hasExitTime = false;
        }

        // ── Crouch / Crawl (C key) ────────────────────────────────────────────
        BuildCrouchCrawlStates(ac, sm, locoState);

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] Built FootIK_Demo.controller.");
        return ac;
    }

    // Crouch (1D Idle↔Walk on Speed) and Crawl (2D MoveX/Speed) states driven by
    // SimpleCharacter's C-key stance machine. Skipped silently if the Kimodo clips
    // aren't present yet, so the controller still builds without them.
    private static void BuildCrouchCrawlStates(AnimatorController ac,
        AnimatorStateMachine sm, AnimatorState locoState)
    {
        var crouchIdle   = LoadFirstClip(CrouchIdleFbxPath);
        var crouchWalk   = LoadFirstClip(CrouchWalkFbxPath);
        var crawlIdle    = LoadFirstClip(CrawlIdleFbxPath);
        var crawlForward = LoadFirstClip(CrawlForwardFbxPath);
        var crawlLeft    = LoadFirstClip(CrawlLeftFbxPath);
        var crawlRight   = LoadFirstClip(CrawlRightFbxPath);

        bool hasCrouch = crouchIdle != null && crouchWalk != null;
        bool hasCrawl  = crawlIdle != null && crawlForward != null
                      && crawlLeft != null && crawlRight != null;

        if (!hasCrouch) Debug.LogWarning("[Setup] Crouch clips not found — Crouch state skipped.");
        if (!hasCrawl)  Debug.LogWarning("[Setup] Crawl clips not found — Crawl state skipped.");

        AnimatorState crouchState = null, crawlState = null;

        if (hasCrouch)
        {
            crouchState = sm.AddState("Crouch", new Vector3(200, 320));
            var ct = new BlendTree();
            AssetDatabase.AddObjectToAsset(ct, GeneratedControllerPath);
            ct.name                   = "Crouch";
            ct.blendType              = BlendTreeType.Simple1D;
            ct.blendParameter         = "Speed";
            ct.useAutomaticThresholds = false;
            ct.AddChild(crouchIdle, 0f);
            ct.AddChild(crouchWalk, 1f);
            crouchState.motion = ct;

            // Locomotion ↔ Crouch
            var toCrouch = locoState.AddTransition(crouchState);
            toCrouch.AddCondition(AnimatorConditionMode.If,    0, "IsCrouching");
            toCrouch.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrawling");
            toCrouch.hasExitTime = false;
            toCrouch.duration    = 0.2f;

            var crouchToLoco = crouchState.AddTransition(locoState);
            crouchToLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            crouchToLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrawling");
            crouchToLoco.hasExitTime = false;
            crouchToLoco.duration    = 0.2f;
        }

        if (hasCrawl)
        {
            crawlState = sm.AddState("Crawl", new Vector3(200, 420));
            var wt = new BlendTree();
            AssetDatabase.AddObjectToAsset(wt, GeneratedControllerPath);
            wt.name            = "Crawl";
            // 2D Cartesian on (MoveX, Speed): idle at origin, forward up the Speed
            // axis, left/right out the MoveX axis. Pure side input lands (±1, 1)
            // exactly on the side clip; pure forward (0, 1) on the forward clip.
            wt.blendType       = BlendTreeType.FreeformCartesian2D;
            wt.blendParameter  = "MoveX";
            wt.blendParameterY = "Speed";
            wt.AddChild(crawlIdle,    new Vector2( 0f, 0f));
            wt.AddChild(crawlForward, new Vector2( 0f, 1f));
            wt.AddChild(crawlLeft,    new Vector2(-1f, 1f));
            wt.AddChild(crawlRight,   new Vector2( 1f, 1f));
            crawlState.motion = wt;

            // Enter crawl from Locomotion (hold C from standing) or Crouch (hold C
            // while crouched). Leave to whichever stance the release resolves to.
            var locoToCrawl = locoState.AddTransition(crawlState);
            locoToCrawl.AddCondition(AnimatorConditionMode.If, 0, "IsCrawling");
            locoToCrawl.hasExitTime = false;
            locoToCrawl.duration    = 0.25f;

            var crawlToLoco = crawlState.AddTransition(locoState);
            crawlToLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrawling");
            crawlToLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            crawlToLoco.hasExitTime = false;
            crawlToLoco.duration    = 0.25f;

            if (crouchState != null)
            {
                var crouchToCrawl = crouchState.AddTransition(crawlState);
                crouchToCrawl.AddCondition(AnimatorConditionMode.If, 0, "IsCrawling");
                crouchToCrawl.hasExitTime = false;
                crouchToCrawl.duration    = 0.25f;

                var crawlToCrouch = crawlState.AddTransition(crouchState);
                crawlToCrouch.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrawling");
                crawlToCrouch.AddCondition(AnimatorConditionMode.If,    0, "IsCrouching");
                crawlToCrouch.hasExitTime = false;
                crawlToCrouch.duration    = 0.25f;
            }
        }

        if (hasCrouch || hasCrawl) Debug.Log("[Setup] Crouch/Crawl states wired.");
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
        // 160×160 flat, top face at Y=0
        Box(root, "Floor", pos: new Vector3(0, -0.5f, 0), scale: new Vector3(160, 1, 160));

        // ── Small test steps (straight ahead from spawn, along +Z) ───────────
        Box(root, "Step_Low",  pos: new Vector3(0, 0.10f,  8f), scale: new Vector3(5, 0.20f, 3));
        Box(root, "Step_Mid",  pos: new Vector3(0, 0.25f, 13f), scale: new Vector3(5, 0.50f, 3));
        Box(root, "Step_High", pos: new Vector3(0, 0.50f, 18f), scale: new Vector3(5, 1.00f, 3));
        Box(root, "Step_Tall", pos: new Vector3(0, 0.75f, 23f), scale: new Vector3(5, 1.50f, 3));

        // ── Diagonal ramp (slope for foot rotation IK) ───────────────────────
        var ramp = Box(root, "Ramp", pos: new Vector3(9, 0.55f, 7f), scale: new Vector3(4, 0.3f, 7));
        ramp.transform.localEulerAngles = new Vector3(-18, 0, 0);

        // ── Ledge islands (hang testing) ──────────────────────────────────────
        Box(root, "Ledge_A", pos: new Vector3( 14,  0.75f,  8f), scale: new Vector3(8, 1.5f, 8));
        Box(root, "Ledge_B", pos: new Vector3( 14,  1.00f, 20f), scale: new Vector3(8, 2.0f, 8));
        Box(root, "Ledge_C", pos: new Vector3(-12,  0.60f,  8f), scale: new Vector3(8, 1.2f, 8));
        Box(root, "Ledge_D", pos: new Vector3(-12,  0.90f, 20f), scale: new Vector3(6, 1.8f, 6));

        // ── Large grand staircase ─────────────────────────────────────────────
        // 12 steps × 0.45 m tall × 2.5 m deep × 18 m wide = 5.4 m total height.
        // Centered at X=−30, ascending along +Z starting at Z=−20.
        // Each box fills from Y=0 down so the faces are clean and continuous.
        const int   StairCount  = 12;
        const float StepH       = 0.45f;   // height per step
        const float StepD       = 2.5f;    // depth per step (Z extent)
        const float StepW       = 18f;     // width (X extent)
        const float StairCX     = -35f;    // centre X of the staircase
        const float StairStartZ = -20f;    // Z of the first step front face

        for (int i = 1; i <= StairCount; i++)
        {
            float h       = i * StepH;              // surface height of this step
            float centerY = h * 0.5f;               // box centre Y (extends to floor)
            float centerZ = StairStartZ + (i - 0.5f) * StepD;
            Box(root, $"GrandStair_{i:D2}",
                pos:   new Vector3(StairCX, centerY, centerZ),
                scale: new Vector3(StepW, h, StepD));
        }

        // Top landing platform (same width, 8 m deep, surface flush with step 12)
        float topY     = StairCount * StepH;
        float landingZ = StairStartZ + StairCount * StepD + 4f;  // 4 m past last step
        Box(root, "GrandStair_Landing",
            pos:   new Vector3(StairCX, topY * 0.5f, landingZ),
            scale: new Vector3(StepW, topY, 8f));

        // ── Small staircase (next to grand, same start Z, shallower steps) ───
        // 10 steps × 0.22 m tall × 1.4 m deep × 12 m wide = 2.2 m total height.
        // Placed to the right of the grand staircase, 4 m gap between them.
        const int   SmallCount  = 10;
        const float SmallH      = 0.22f;
        const float SmallD      = 1.4f;
        const float SmallW      = 12f;
        const float SmallCX     = StairCX + StepW * 0.5f + SmallW * 0.5f + 4f; // 4 m gap

        for (int i = 1; i <= SmallCount; i++)
        {
            float h       = i * SmallH;
            float centerZ = StairStartZ + (i - 0.5f) * SmallD;
            Box(root, $"SmallStair_{i:D2}",
                pos:   new Vector3(SmallCX, h * 0.5f, centerZ),
                scale: new Vector3(SmallW, h, SmallD));
        }

        // Small staircase landing
        float smallTopY     = SmallCount * SmallH;
        float smallLandingZ = StairStartZ + SmallCount * SmallD + 4f;
        Box(root, "SmallStair_Landing",
            pos:   new Vector3(SmallCX, smallTopY * 0.5f, smallLandingZ),
            scale: new Vector3(SmallW, smallTopY, 6f));

        // ── Scattered low obstacles ───────────────────────────────────────────
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
        string urpFolder = "Assets/ThirdParty/HomebrewIK/Demo/Materials/URP";
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
