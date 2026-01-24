using UnityEngine;
using UnityEditor;

public partial class ThirdPersonSetup
{
    internal static void SetupKevinIglesiasSprint()
    {
        string[] clipPaths = ThirdPersonSetupConfig.SprintClipPaths;

        // Configure avatar for each clip (resolves correct avatar per character)
        ConfigureSprintAnimations(clipPaths);

        // Add them to the animator controller
        var controller = GetActiveAnimatorController();
        AddSprintToAnimatorController(controller, clipPaths);
    }
    private static void ConfigureSprintAnimations(string[] clipPaths)
    {
        string avatarSourcePath = !string.IsNullOrEmpty(LastAvatarSourcePath)
            ? LastAvatarSourcePath
            : ThirdPersonSetupConfig.DummyPrefabPath;

        Debug.Log("Configuring sprint animations avatar...");

        foreach (string clipPath in clipPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;

            if (importer == null)
            {
                Debug.LogError($"Could not find ModelImporter for: {clipPath}");
                ReportWarning($"Sprint clip missing importer: {clipPath}");
                continue;
            }

            ConfigureAnimationFile(clipPath, avatarSourcePath);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("Sprint avatar configuration complete.");

        System.Threading.Thread.Sleep(1000);
    }
    private static void AddSprintToAnimatorController(UnityEditor.Animations.AnimatorController controller, string[] clipPaths)
    {
        if (controller == null)
        {
            Debug.LogError("Could not find an active AnimatorController to add sprint animations.");
            ReportError("Sprint setup skipped: no active AnimatorController.");
            return;
        }

        AnimationClip[] sprintClips = new AnimationClip[clipPaths.Length];
        int loadedCount = 0;

        for (int i = 0; i < clipPaths.Length; i++)
        {
            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(clipPaths[i]);

            foreach (var asset in allAssets)
            {
                if (asset is AnimationClip c && !c.name.Contains("__preview__") && sprintClips[i] == null)
                {
                    sprintClips[i] = c;
                    loadedCount++;
                    Debug.Log($"Sprint clip[{i}]: {c.name} from {clipPaths[i]}");
                    break;
                }
            }

            if (sprintClips[i] == null)
            {
                Debug.LogWarning($"Sprint clip[{i}]: FAILED to load from {clipPaths[i]}");
            }
        }

        if (loadedCount == 0)
        {
            Debug.LogError("Sprint Setup Failed: No animation clips found.");
            ReportWarning("Sprint setup skipped: no sprint clips were loaded.");
            return;
        }

        Debug.Log($"Successfully loaded {loadedCount} sprint animations.");

        // Create parameters if they don't exist
        bool hasSprintParam = System.Array.Exists(controller.parameters, p => p.name == ThirdPersonSetupConfig.IsSprintingParam);
        bool hasHorizontalParam = System.Array.Exists(controller.parameters, p => p.name == ThirdPersonSetupConfig.HorizontalParam);
        bool hasVerticalParam = System.Array.Exists(controller.parameters, p => p.name == ThirdPersonSetupConfig.VerticalParam);

        if (!hasSprintParam)
        {
            controller.AddParameter(ThirdPersonSetupConfig.IsSprintingParam, AnimatorControllerParameterType.Bool);
        }

        if (!hasHorizontalParam)
        {
            controller.AddParameter(ThirdPersonSetupConfig.HorizontalParam, AnimatorControllerParameterType.Float);
        }

        if (!hasVerticalParam)
        {
            controller.AddParameter(ThirdPersonSetupConfig.VerticalParam, AnimatorControllerParameterType.Float);
        }

        // Get or create Sprint state with blend tree
        var rootStateMachine = controller.layers[0].stateMachine;
        var sprintState = GetOrCreateBlendTreeState(rootStateMachine, ThirdPersonSetupConfig.SprintStateName, sprintClips, controller);

        // Find Walk state
        UnityEditor.Animations.AnimatorState walkState = null;
        foreach (var state in rootStateMachine.states)
        {
            if (state.state.name == ThirdPersonSetupConfig.WalkStateName)
            {
                walkState = state.state;
                break;
            }
        }

        if (walkState != null && sprintState != null)
        {
            // Create transitions
            // Walk → Sprint (when IsSprinting becomes true)
            bool hasWalkToSprint = false;
            foreach (var transition in walkState.transitions)
            {
                if (transition.destinationState == sprintState)
                {
                    hasWalkToSprint = true;
                    break;
                }
            }

            if (!hasWalkToSprint)
            {
                var walkToSprint = walkState.AddTransition(sprintState);
                walkToSprint.hasExitTime = false;
                walkToSprint.duration = 0.1f;
                walkToSprint.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, ThirdPersonSetupConfig.IsSprintingParam);
            }

            // Sprint → Walk (when IsSprinting becomes false)
            bool hasSprintToWalk = false;
            foreach (var transition in sprintState.transitions)
            {
                if (transition.destinationState == walkState)
                {
                    hasSprintToWalk = true;
                    break;
                }
            }

            if (!hasSprintToWalk)
            {
                var sprintToWalk = sprintState.AddTransition(walkState);
                sprintToWalk.hasExitTime = false;
                sprintToWalk.duration = 0.1f;
                sprintToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, ThirdPersonSetupConfig.IsSprintingParam);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
    private static UnityEditor.Animations.AnimatorController GetActiveAnimatorController()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            Transform characterModel = player.transform.Find("CharacterModel");
            if (characterModel != null)
            {
                Animator animator = characterModel.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    return animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                }
            }
        }

        return AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ThirdPersonSetupConfig.DefaultAnimatorControllerPath);
    }
    private static UnityEditor.Animations.AnimatorState GetOrCreateBlendTreeState(
        UnityEditor.Animations.AnimatorStateMachine stateMachine,
        string stateName,
        AnimationClip[] clips,
        UnityEditor.Animations.AnimatorController controller)
    {
        // Find existing state
        foreach (var childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                // Destroy old blend tree sub-asset
                if (childState.state.motion is UnityEditor.Animations.BlendTree oldTree)
                {
                    Object.DestroyImmediate(oldTree, true);
                }

                var blendTree = CreateSprintBlendTree(clips);
                blendTree.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(blendTree, controller);
                childState.state.motion = blendTree;
                childState.state.iKOnFeet = ThirdPersonSetupConfig.UseAnimatorFootIk;
                return childState.state;
            }
        }

        // Create new state
        var state = stateMachine.AddState(stateName);
        var newBlendTree = CreateSprintBlendTree(clips);
        newBlendTree.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(newBlendTree, controller);
        state.motion = newBlendTree;
        state.iKOnFeet = ThirdPersonSetupConfig.UseAnimatorFootIk;
        return state;
    }
    private static UnityEditor.Animations.BlendTree CreateSprintBlendTree(AnimationClip[] clips)
    {
        // Create a new blend tree asset
        var blendTree = new UnityEditor.Animations.BlendTree();
        blendTree.name = "Sprint Blend Tree";
        blendTree.blendType = UnityEditor.Animations.BlendTreeType.SimpleDirectional2D;
        blendTree.blendParameter = ThirdPersonSetupConfig.HorizontalParam;
        blendTree.blendParameterY = ThirdPersonSetupConfig.VerticalParam;
        blendTree.useAutomaticThresholds = false;

        // Add animations in 5 directions (no backward sprint - more realistic)
        // Order: Forward, ForwardLeft, ForwardRight, Left, Right
        Vector2[] positions = new Vector2[]
        {
            new Vector2(0f, 1f),      // Forward
            new Vector2(-0.7f, 0.7f), // ForwardLeft
            new Vector2(0.7f, 0.7f),  // ForwardRight
            new Vector2(-1f, 0f),     // Left
            new Vector2(1f, 0f)       // Right
        };

        for (int i = 0; i < clips.Length && i < positions.Length; i++)
        {
            if (clips[i] != null)
            {
                blendTree.AddChild(clips[i], positions[i]);
            }
        }

        return blendTree;
    }
}
