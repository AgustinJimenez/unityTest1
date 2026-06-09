using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;

public static class IKTestSetup
{
    private const string ScenePath              = "Assets/FootIK_Test.unity";
    private const string CharacterLayerName     = "Character";
    private const string IdlePrefabPath         = "Assets/HomebrewIK/Demo/Models/Armature_Idle.prefab";
    private const string IdleFbxPath            = "Assets/HomebrewIK/Demo/Animations/Idle.fbx";
    private const string WalkFbxPath            = "Assets/HomebrewIK/Demo/Animations/Run.fbx";
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
        player.transform.position = new Vector3(0f, 2f, 0f);
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
        soChar.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
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
    }

    private static AnimatorController BuildDemoController()
    {
        AnimationClip idleClip = null;
        AnimationClip runClip  = null;

        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(IdleFbxPath))
            if (obj is AnimationClip c && !c.name.Contains("__preview__")) { idleClip = c; break; }

        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(WalkFbxPath))
            if (obj is AnimationClip c && !c.name.Contains("__preview__")) { runClip = c; break; }

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
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // Rebuild the base layer state machine
        var layer = ac.layers[0];
        layer.iKPass = true;
        ac.layers    = new[] { layer };   // write back — layers is a copy

        var sm = ac.layers[0].stateMachine;
        foreach (var s in sm.states) sm.RemoveState(s.state);

        var blendTreeState = sm.AddState("Locomotion");
        BlendTree bt = new BlendTree();
        AssetDatabase.AddObjectToAsset(bt, GeneratedControllerPath);
        bt.name                   = "Locomotion";
        bt.blendType              = BlendTreeType.Simple1D;
        bt.blendParameter         = "Speed";
        bt.useAutomaticThresholds = false;
        bt.AddChild(idleClip, 0f);
        bt.AddChild(runClip,  1f);
        blendTreeState.motion = bt;
        sm.defaultState       = blendTreeState;

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] Built FootIK_Demo.controller with Idle→Run blend tree.");
        return ac;
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
