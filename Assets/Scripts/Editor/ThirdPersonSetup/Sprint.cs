using UnityEngine;
using UnityEditor;

public partial class ThirdPersonSetup
{
    internal static void SetupKevinIglesiasSprint()
    {
        // Use Sprint animations instead of Run - they might have better retargeting
        string animBasePath = ThirdPersonSetupConfig.SprintRootMotionPath;
        string[] animationNames = ThirdPersonSetupConfig.SprintAnimationNames;

        // First, fix the avatar configuration for these animations
        ConfigureKevinIglesiasAnimations(animBasePath, animationNames);

        // Then add them to the animator controller
        var controller = GetActiveAnimatorController();
        AddSprintToAnimatorController(controller, animBasePath, animationNames);
    }
    private static void ConfigureKevinIglesiasAnimations(string animBasePath, string[] animationNames)
    {
        string avatarSourcePath = !string.IsNullOrEmpty(LastAvatarSourcePath)
            ? LastAvatarSourcePath
            : ThirdPersonSetupConfig.DummyPrefabPath;

        Debug.Log("Configuring Kevin Iglesias animations to copy the character avatar...");

        foreach (string animName in animationNames)
        {
            string animPath = $"{animBasePath}/{animName}.fbx";
            ModelImporter importer = AssetImporter.GetAtPath(animPath) as ModelImporter;

            if (importer == null)
            {
                Debug.LogError($"Could not find ModelImporter for: {animPath}");
                ReportWarning($"Sprint clip missing importer: {animPath}");
                continue;
            }

            // Use the shared configuration to copy avatar + clip settings
            ConfigureAnimationFile(animPath, avatarSourcePath);
        }

        // CRITICAL: Wait for Unity to finish importing
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("Avatar configuration complete.");

        // Give Unity a moment to process
        System.Threading.Thread.Sleep(1000);
    }
    private static void AddSprintToAnimatorController(UnityEditor.Animations.AnimatorController controller, string animBasePath, string[] animationNames)
    {
        if (controller == null)
        {
            Debug.LogError("Could not find an active AnimatorController to add sprint animations.");
            ReportError("Sprint setup skipped: no active AnimatorController.");
            return;
        }

        AnimationClip[] sprintClips = new AnimationClip[animationNames.Length];
        int loadedCount = 0;

        for (int i = 0; i < animationNames.Length; i++)
        {
            string animPath = $"{animBasePath}/{animationNames[i]}.fbx";
            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(animPath);

            foreach (var asset in allAssets)
            {
                if (asset is AnimationClip c && !c.name.Contains("__preview__") && sprintClips[i] == null)
                {
                    sprintClips[i] = c;
                    loadedCount++;
                    break;
                }
            }
        }

        if (loadedCount == 0)
        {
            Debug.LogError("Sprint Setup Failed: No animation clips found in Kevin Iglesias Sprint animations.");
            Debug.LogError($"Please select one of the .fbx files in {ThirdPersonSetupConfig.SprintRootMotionPath} and check if animations are imported.");
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
        var sprintState = GetOrCreateBlendTreeState(rootStateMachine, ThirdPersonSetupConfig.SprintStateName, sprintClips);

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
        AnimationClip[] clips)
    {
        // Find existing state
        foreach (var childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                // Update the motion even if state exists
                var blendTree = CreateSprintBlendTree(clips);
                childState.state.motion = blendTree;
                return childState.state;
            }
        }

        // Create new state
        var state = stateMachine.AddState(stateName);
        var newBlendTree = CreateSprintBlendTree(clips);
        state.motion = newBlendTree;
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
