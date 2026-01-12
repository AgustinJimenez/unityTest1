using UnityEngine;
using UnityEditor;
using System.IO;

public partial class ThirdPersonSetup
{
    private static readonly System.Collections.Generic.Dictionary<string, string[]> AnimationClipGuidCache =
        new System.Collections.Generic.Dictionary<string, string[]>();

    private static void SetupAnimatorController(Animator animator, string characterPath, string avatarSourcePath)
    {
        Debug.Log("=== SETTING UP ANIMATOR CONTROLLER ===");

        // Get the character directory
        string characterDir = Path.GetDirectoryName(characterPath);

        // Check if an animator controller already exists
        string controllerPath = Path.Combine(characterDir, "CharacterAnimator.controller").Replace("\\", "/");
        UnityEditor.Animations.AnimatorController controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);

        if (controller == null)
        {
            // Create new animator controller
            controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            Debug.Log($"Created animator controller at {controllerPath}");
        }
        else
        {
            Debug.Log($"Using existing animator controller at {controllerPath}");
        }

        // Configure all animations in subdirectories
        ConfigureAnimation(characterDir, "Idle", avatarSourcePath);
        ConfigureAnimation(characterDir, "Animations/Walking", avatarSourcePath);
        ConfigureAnimation(characterDir, "Jump", avatarSourcePath);

        // Find and add all animation clips
        var stateMachine = controller.layers[0].stateMachine;

        // Dictionary to store found animation clips
        System.Collections.Generic.Dictionary<string, AnimationClip> animations = new System.Collections.Generic.Dictionary<string, AnimationClip>();

        // Search in multiple locations
        foreach (string[] searchSet in ThirdPersonSetupConfig.BuildAnimationSearchSets(characterDir))
        {
            AddAnimationsFromDirs(animations, searchSet);
        }

        if (!animations.ContainsKey(ThirdPersonSetupConfig.IdleStateName)
            || !animations.ContainsKey(ThirdPersonSetupConfig.WalkStateName)
            || !animations.ContainsKey(ThirdPersonSetupConfig.JumpLoopStateName))
        {
            TryAddExplicitClips(animations);
        }

        ValidateAnimatorSetup(controller, animations);
        if (animations.Count == 0)
        {
            ReportWarning("No animation clips found for Idle/Walk/Jump; animator states may be empty.");
        }

        // Add Speed parameter for movement
        if (controller.parameters.Length == 0 || System.Array.Find(controller.parameters, p => p.name == ThirdPersonSetupConfig.SpeedParam) == null)
        {
            controller.AddParameter(ThirdPersonSetupConfig.SpeedParam, UnityEngine.AnimatorControllerParameterType.Float);
            Debug.Log("Added Speed parameter");
        }

        if (System.Array.Find(controller.parameters, p => p.name == ThirdPersonSetupConfig.IsGroundedParam) == null)
        {
            controller.AddParameter(ThirdPersonSetupConfig.IsGroundedParam, UnityEngine.AnimatorControllerParameterType.Bool);
        }

        // Create or update animation states
        UnityEditor.Animations.AnimatorState idleState = null;
        UnityEditor.Animations.AnimatorState walkState = null;
        UnityEditor.Animations.AnimatorState jumpBeginState = null;
        UnityEditor.Animations.AnimatorState jumpLoopState = null;
        UnityEditor.Animations.AnimatorState jumpFallState = null;
        UnityEditor.Animations.AnimatorState jumpLandState = null;

        // Create Idle state
        if (animations.ContainsKey("Idle"))
        {
            idleState = GetOrCreateState(stateMachine, ThirdPersonSetupConfig.IdleStateName, animations["Idle"]);
            stateMachine.defaultState = idleState;
        }

        // Create Walk state
        if (animations.ContainsKey("Walk"))
        {
            walkState = GetOrCreateState(stateMachine, ThirdPersonSetupConfig.WalkStateName, animations["Walk"]);
        }

        ConfigureAnimationFiles(ThirdPersonSetupConfig.KevinIdleClipPaths, avatarSourcePath);
        ConfigureAnimationFiles(ThirdPersonSetupConfig.KevinWalkClipPaths, avatarSourcePath);

        EnsureStateMotion(idleState, ThirdPersonSetupConfig.KevinIdleClipPaths);
        EnsureStateMotion(walkState, ThirdPersonSetupConfig.KevinWalkClipPaths);

        // Create Jump states
        if (animations.ContainsKey("JumpBegin"))
        {
            jumpBeginState = GetOrCreateState(stateMachine, ThirdPersonSetupConfig.JumpBeginStateName, animations["JumpBegin"]);
        }
        if (animations.ContainsKey("JumpLoop"))
        {
            jumpLoopState = GetOrCreateState(stateMachine, ThirdPersonSetupConfig.JumpLoopStateName, animations["JumpLoop"]);
        }
        if (animations.ContainsKey("JumpFall"))
        {
            jumpFallState = GetOrCreateState(stateMachine, ThirdPersonSetupConfig.JumpFallStateName, animations["JumpFall"]);
        }
        if (animations.ContainsKey("JumpLand"))
        {
            jumpLandState = GetOrCreateState(stateMachine, ThirdPersonSetupConfig.JumpLandStateName, animations["JumpLand"]);
        }

        ConfigureAnimationFiles(ThirdPersonSetupConfig.KevinJumpLoopClipPaths, avatarSourcePath);
        EnsureStateMotion(jumpLoopState, ThirdPersonSetupConfig.KevinJumpLoopClipPaths);

        // Setup transitions if both states exist
        if (idleState != null && walkState != null)
        {
            // Idle to Walk transition
            bool hasIdleToWalk = false;
            foreach (var transition in idleState.transitions)
            {
                if (transition.destinationState == walkState)
                {
                    hasIdleToWalk = true;
                    break;
                }
            }

            if (!hasIdleToWalk)
            {
                var idleToWalk = idleState.AddTransition(walkState);
                idleToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 0.1f, ThirdPersonSetupConfig.SpeedParam);
                idleToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                idleToWalk.hasExitTime = false;
                idleToWalk.duration = 0.25f;
                Debug.Log("Created Idle -> Walk transition");
            }
            else
            {
                foreach (var transition in idleState.transitions)
                {
                    if (transition.destinationState == walkState)
                    {
                        bool hasGrounded = System.Array.Exists(transition.conditions, c => c.parameter == ThirdPersonSetupConfig.IsGroundedParam);
                        if (!hasGrounded)
                        {
                            transition.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                        }
                    }
                }
            }

            // Walk to Idle transition
            bool hasWalkToIdle = false;
            foreach (var transition in walkState.transitions)
            {
                if (transition.destinationState == idleState)
                {
                    hasWalkToIdle = true;
                    break;
                }
            }

            if (!hasWalkToIdle)
            {
                var walkToIdle = walkState.AddTransition(idleState);
                walkToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 0.1f, ThirdPersonSetupConfig.SpeedParam);
                walkToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                walkToIdle.hasExitTime = false;
                walkToIdle.duration = 0.25f;
                Debug.Log("Created Walk -> Idle transition");
            }
            else
            {
                foreach (var transition in walkState.transitions)
                {
                    if (transition.destinationState == idleState)
                    {
                        bool hasGrounded = System.Array.Exists(transition.conditions, c => c.parameter == ThirdPersonSetupConfig.IsGroundedParam);
                        if (!hasGrounded)
                        {
                            transition.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                        }
                    }
                }
            }
        }

        EnforceGroundedOnLocomotion(stateMachine, idleState, walkState);

        if (jumpBeginState != null || jumpLoopState != null || jumpFallState != null || jumpLandState != null)
        {
            if (System.Array.Find(controller.parameters, p => p.name == ThirdPersonSetupConfig.JumpParam) == null)
            {
                controller.AddParameter(ThirdPersonSetupConfig.JumpParam, UnityEngine.AnimatorControllerParameterType.Trigger);
            }

            if (System.Array.Find(controller.parameters, p => p.name == ThirdPersonSetupConfig.VerticalVelocityParam) == null)
            {
                controller.AddParameter(ThirdPersonSetupConfig.VerticalVelocityParam, UnityEngine.AnimatorControllerParameterType.Float);
            }

            var jumpEntryState = jumpBeginState ?? jumpLoopState ?? jumpFallState ?? jumpLandState;

            bool hasAnyStateJump = false;
            foreach (var transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == jumpEntryState)
                {
                    hasAnyStateJump = true;
                    break;
                }
            }

            if (!hasAnyStateJump)
            {
                var anyToJump = stateMachine.AddAnyStateTransition(jumpEntryState);
                anyToJump.hasExitTime = false;
                anyToJump.duration = 0.05f;
                anyToJump.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.JumpParam);
                Debug.Log("Created Any State -> Jump transition");
            }

            if (jumpBeginState != null && jumpLoopState != null)
            {
                bool hasBeginToLoop = false;
                foreach (var transition in jumpBeginState.transitions)
                {
                    if (transition.destinationState == jumpLoopState)
                    {
                        hasBeginToLoop = true;
                        break;
                    }
                }

                if (!hasBeginToLoop)
                {
                    var beginToLoop = jumpBeginState.AddTransition(jumpLoopState);
                    beginToLoop.hasExitTime = true;
                    beginToLoop.exitTime = 0.9f;
                    beginToLoop.duration = 0.05f;
                }
            }

            if (jumpBeginState != null && jumpFallState != null)
            {
                bool hasBeginToFall = false;
                foreach (var transition in jumpBeginState.transitions)
                {
                    if (transition.destinationState == jumpFallState)
                    {
                        hasBeginToFall = true;
                        break;
                    }
                }

                if (!hasBeginToFall)
                {
                    var beginToFall = jumpBeginState.AddTransition(jumpFallState);
                    beginToFall.hasExitTime = false;
                    beginToFall.duration = 0.05f;
                    beginToFall.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, -0.1f, ThirdPersonSetupConfig.VerticalVelocityParam);
                }
            }

            if (jumpBeginState != null && jumpLandState != null)
            {
                bool hasBeginToLand = false;
                foreach (var transition in jumpBeginState.transitions)
                {
                    if (transition.destinationState == jumpLandState)
                    {
                        hasBeginToLand = true;
                        break;
                    }
                }

                if (!hasBeginToLand)
                {
                    var beginToLand = jumpBeginState.AddTransition(jumpLandState);
                    beginToLand.hasExitTime = false;
                    beginToLand.duration = 0.05f;
                    beginToLand.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                }
            }

            if (jumpLoopState != null && jumpFallState != null)
            {
                bool hasLoopToFall = false;
                foreach (var transition in jumpLoopState.transitions)
                {
                    if (transition.destinationState == jumpFallState)
                    {
                        hasLoopToFall = true;
                        break;
                    }
                }

                if (!hasLoopToFall)
                {
                    var loopToFall = jumpLoopState.AddTransition(jumpFallState);
                    loopToFall.hasExitTime = false;
                    loopToFall.duration = 0.05f;
                    loopToFall.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, -0.1f, ThirdPersonSetupConfig.VerticalVelocityParam);
                }
            }

            if (jumpLoopState != null && jumpLandState != null)
            {
                bool hasLoopToLand = false;
                foreach (var transition in jumpLoopState.transitions)
                {
                    if (transition.destinationState == jumpLandState)
                    {
                        hasLoopToLand = true;
                        break;
                    }
                }

                if (!hasLoopToLand)
                {
                    var loopToLand = jumpLoopState.AddTransition(jumpLandState);
                    loopToLand.hasExitTime = false;
                    loopToLand.duration = 0.05f;
                    loopToLand.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                }
            }

            if (jumpFallState != null && jumpLandState != null)
            {
                bool hasFallToLand = false;
                foreach (var transition in jumpFallState.transitions)
                {
                    if (transition.destinationState == jumpLandState)
                    {
                        hasFallToLand = true;
                        break;
                    }
                }

                if (!hasFallToLand)
                {
                    var fallToLand = jumpFallState.AddTransition(jumpLandState);
                    fallToLand.hasExitTime = false;
                    fallToLand.duration = 0.05f;
                    fallToLand.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                }
            }

            if (jumpLandState != null)
            {
                if (idleState != null)
                {
                    bool hasLandToIdle = false;
                    foreach (var transition in jumpLandState.transitions)
                    {
                        if (transition.destinationState == idleState)
                        {
                            hasLandToIdle = true;
                            break;
                        }
                    }

                    if (!hasLandToIdle)
                    {
                        var landToIdle = jumpLandState.AddTransition(idleState);
                        landToIdle.hasExitTime = true;
                        landToIdle.exitTime = 0.9f;
                        landToIdle.duration = 0.1f;
                        landToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 0.1f, ThirdPersonSetupConfig.SpeedParam);
                    }
                }

                if (walkState != null)
                {
                    bool hasLandToWalk = false;
                    foreach (var transition in jumpLandState.transitions)
                    {
                        if (transition.destinationState == walkState)
                        {
                            hasLandToWalk = true;
                            break;
                        }
                    }

                    if (!hasLandToWalk)
                    {
                        var landToWalk = jumpLandState.AddTransition(walkState);
                        landToWalk.hasExitTime = true;
                        landToWalk.exitTime = 0.9f;
                        landToWalk.duration = 0.1f;
                        landToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 0.1f, ThirdPersonSetupConfig.SpeedParam);
                    }
                }
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // Assign controller to animator
        animator.runtimeAnimatorController = controller;
        Debug.Log("Assigned animator controller to character");
    }
    private static void EnforceGroundedOnLocomotion(UnityEditor.Animations.AnimatorStateMachine stateMachine,
        UnityEditor.Animations.AnimatorState idleState,
        UnityEditor.Animations.AnimatorState walkState)
    {
        if (idleState == null && walkState == null)
        {
            return;
        }

        if (stateMachine != null)
        {
            foreach (var transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == idleState || transition.destinationState == walkState)
                {
                    bool hasGrounded = System.Array.Exists(transition.conditions, c => c.parameter == ThirdPersonSetupConfig.IsGroundedParam);
                    if (!hasGrounded)
                    {
                        transition.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                    }
                }
            }
        }

        if (idleState != null)
        {
            foreach (var transition in idleState.transitions)
            {
                if (transition.destinationState == walkState)
                {
                    bool hasGrounded = System.Array.Exists(transition.conditions, c => c.parameter == ThirdPersonSetupConfig.IsGroundedParam);
                    if (!hasGrounded)
                    {
                        transition.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                    }
                }
            }
        }

        if (walkState != null)
        {
            foreach (var transition in walkState.transitions)
            {
                if (transition.destinationState == idleState)
                {
                    bool hasGrounded = System.Array.Exists(transition.conditions, c => c.parameter == ThirdPersonSetupConfig.IsGroundedParam);
                    if (!hasGrounded)
                    {
                        transition.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, ThirdPersonSetupConfig.IsGroundedParam);
                    }
                }
            }
        }
    }
    private static UnityEditor.Animations.AnimatorState GetOrCreateState(UnityEditor.Animations.AnimatorStateMachine stateMachine, string stateName, AnimationClip clip)
    {
        // Check if state already exists
        foreach (var childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                childState.state.motion = clip;
                Debug.Log($"Updated existing {stateName} state");
                return childState.state;
            }
        }

        // Create new state
        var newState = stateMachine.AddState(stateName);
        newState.motion = clip;
        Debug.Log($"Created {stateName} state");
        return newState;
    }
    private static void EnsureStateMotion(UnityEditor.Animations.AnimatorState state, string[] clipPaths)
    {
        if (state == null || state.motion != null)
        {
            return;
        }

        foreach (string clipPath in clipPaths)
        {
            AnimationClip clip = LoadFirstClip(clipPath);
            if (clip != null)
            {
                state.motion = clip;
                Debug.Log($"Assigned clip {clip.name} to state {state.name}");
                return;
            }
        }
    }
    private static void ConfigureAnimationFiles(string[] clipPaths, string avatarSourcePath)
    {
        if (clipPaths == null)
        {
            return;
        }

        foreach (string clipPath in clipPaths)
        {
            if (!string.IsNullOrEmpty(clipPath))
            {
                ConfigureAnimationFile(clipPath, avatarSourcePath);
            }
        }
    }
    private static void ConfigureAnimation(string characterDir, string animPath, string avatarSourcePath)
    {
        string fullPath = Path.Combine(characterDir, animPath).Replace("\\", "/");

        // Check if it's a directory or file path
        if (AssetDatabase.IsValidFolder(fullPath))
        {
            // It's a directory, find animation files inside
            string[] files = AssetDatabase.FindAssets("t:Model", new[] { fullPath });
            foreach (string guid in files)
            {
                string filePath = AssetDatabase.GUIDToAssetPath(guid);
                if (filePath.EndsWith(".dae") || filePath.EndsWith(".fbx"))
                {
                    ConfigureAnimationFile(filePath, avatarSourcePath);
                }
            }
        }
        else
        {
            // Try as a file pattern
            string dir = Path.GetDirectoryName(fullPath).Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(dir))
            {
                string[] files = AssetDatabase.FindAssets("t:Model", new[] { dir });
                foreach (string guid in files)
                {
                    string filePath = AssetDatabase.GUIDToAssetPath(guid);
                    if (filePath.EndsWith(".dae") || filePath.EndsWith(".fbx"))
                    {
                        ConfigureAnimationFile(filePath, avatarSourcePath);
                    }
                }
            }
        }
    }
    private static void ConfigureAnimationPath(string fullPath, string avatarSourcePath)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return;
        }

        if (AssetDatabase.IsValidFolder(fullPath))
        {
            string[] files = AssetDatabase.FindAssets("t:Model", new[] { fullPath });
            foreach (string guid in files)
            {
                string filePath = AssetDatabase.GUIDToAssetPath(guid);
                if (filePath.EndsWith(".dae") || filePath.EndsWith(".fbx"))
                {
                    ConfigureAnimationFile(filePath, avatarSourcePath);
                }
            }
        }
        else
        {
            string dir = Path.GetDirectoryName(fullPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir))
            {
                string[] files = AssetDatabase.FindAssets("t:Model", new[] { dir });
                foreach (string guid in files)
                {
                    string filePath = AssetDatabase.GUIDToAssetPath(guid);
                    if (filePath.EndsWith(".dae") || filePath.EndsWith(".fbx"))
                    {
                        ConfigureAnimationFile(filePath, avatarSourcePath);
                    }
                }
            }
        }
    }
    private static void ConfigureAnimationFile(string animFilePath, string avatarSourcePath)
    {
        Debug.Log($"Configuring animation file: {animFilePath}");

        ModelImporter animImporter = AssetImporter.GetAtPath(animFilePath) as ModelImporter;
        if (animImporter == null) return;

        bool needsReimport = false;

        string characterModelPath = avatarSourcePath;

        // Configure animation type
        if (animImporter.animationType != ModelImporterAnimationType.Human)
        {
            animImporter.animationType = ModelImporterAnimationType.Human;
            needsReimport = true;
        }

        // Copy avatar from character model
        if (characterModelPath != null)
        {
            AssetDatabase.ImportAsset(characterModelPath, ImportAssetOptions.ForceUpdate);
            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(characterModelPath);
            Avatar characterAvatar = null;

            foreach (var asset in allAssets)
            {
                if (asset is Avatar)
                {
                    characterAvatar = asset as Avatar;
                    break;
                }
            }

            if (characterAvatar != null && animImporter.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
            {
                animImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                animImporter.sourceAvatar = characterAvatar;
                needsReimport = true;
            }
        }

        // Set scale for DAE files
        if (animFilePath.EndsWith(".dae") && animImporter.globalScale != 0.01f)
        {
            animImporter.globalScale = 0.01f;
            needsReimport = true;
        }

        // Don't import materials from animation files
        if (animImporter.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            animImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            needsReimport = true;
        }

        // Configure animation clips
        ModelImporterClipAnimation[] clipAnimations = animImporter.defaultClipAnimations;
        if (clipAnimations.Length > 0)
        {
            for (int i = 0; i < clipAnimations.Length; i++)
            {
                string clipNameLower = clipAnimations[i].name.ToLower();
                string pathLower = animFilePath.ToLower();
                bool isJumpBegin = clipNameLower.Contains("begin") || pathLower.Contains("begin");
                bool isJumpLand = clipNameLower.Contains("land") || pathLower.Contains("land");
                bool isJumpFall = clipNameLower.Contains("fall") || pathLower.Contains("fall");

                clipAnimations[i].loopTime = !isJumpBegin && !isJumpLand;
                if (isJumpFall)
                {
                    clipAnimations[i].loopTime = true;
                }
                clipAnimations[i].lockRootRotation = true;
                clipAnimations[i].lockRootHeightY = true;
                clipAnimations[i].lockRootPositionXZ = false;
                clipAnimations[i].keepOriginalPositionY = false;
                clipAnimations[i].keepOriginalPositionXZ = false;
                clipAnimations[i].heightFromFeet = true;
            }
            animImporter.clipAnimations = clipAnimations;
            needsReimport = true;
        }

        if (needsReimport)
        {
            animImporter.SaveAndReimport();
            Debug.Log($"Configured animation: {Path.GetFileName(animFilePath)}");
        }
    }
    private static void AddAnimationsFromDirs(System.Collections.Generic.Dictionary<string, AnimationClip> animations, string[] searchDirs)
    {
        foreach (string searchDir in searchDirs)
        {
            if (!AssetDatabase.IsValidFolder(searchDir))
            {
                continue;
            }

            string[] animGuids = GetAnimationClipGuids(searchDir);

            AnimationClip bestWalkClip = null;
            int bestWalkScore = int.MinValue;
            AnimationClip bestIdleClip = null;
            int bestIdleScore = int.MinValue;
            AnimationClip bestJumpBeginClip = null;
            int bestJumpBeginScore = int.MinValue;
            AnimationClip bestJumpLoopClip = null;
            int bestJumpLoopScore = int.MinValue;
            AnimationClip bestJumpFallClip = null;
            int bestJumpFallScore = int.MinValue;
            AnimationClip bestJumpLandClip = null;
            int bestJumpLandScore = int.MinValue;

            foreach (string guid in animGuids)
            {
                string animPath = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);

                if (clip == null)
                {
                    continue;
                }

                string animPathLower = animPath.ToLower();
                string animName = null;

                if (animPathLower.Contains("idle"))
                {
                    animName = "Idle";
                }
                else if (animPathLower.Contains("walk"))
                {
                    animName = "Walk";
                }
                else if (animPathLower.Contains("jump") || animPathLower.Contains("fall"))
                {
                    animName = "Jump";
                }

                if (animName == "Walk")
                {
                    int score = 0;
                    if (animPathLower.Contains("walk01_forward"))
                    {
                        score += 10;
                    }
                    if (animPathLower.Contains("forward"))
                    {
                        score += 5;
                    }
                    if (animPathLower.Contains("rootmotion") || animPathLower.Contains("[rm]"))
                    {
                        score -= 10;
                    }

                    if (score > bestWalkScore)
                    {
                        bestWalkScore = score;
                        bestWalkClip = clip;
                    }
                }
                else if (animName == "Jump")
                {
                    bool isRootMotion = animPathLower.Contains("rootmotion") || animPathLower.Contains("[rm]");
                    int baseScore = isRootMotion ? -10 : 0;

                    if (animPathLower.Contains("fall"))
                    {
                        int score = baseScore + 5;
                        if (score > bestJumpFallScore)
                        {
                            bestJumpFallScore = score;
                            bestJumpFallClip = clip;
                        }
                    }
                    else if (animPathLower.Contains("land"))
                    {
                        int score = baseScore + 5;
                        if (score > bestJumpLandScore)
                        {
                            bestJumpLandScore = score;
                            bestJumpLandClip = clip;
                        }
                    }
                    else if (animPathLower.Contains("begin"))
                    {
                        int score = baseScore + 5;
                        if (score > bestJumpBeginScore)
                        {
                            bestJumpBeginScore = score;
                            bestJumpBeginClip = clip;
                        }
                    }
                    else
                    {
                        int score = baseScore + 10;
                        if (animPathLower.Contains("jump01"))
                        {
                            score += 5;
                        }

                        if (score > bestJumpLoopScore)
                        {
                            bestJumpLoopScore = score;
                            bestJumpLoopClip = clip;
                        }
                    }
                }
                else if (animName == "Idle")
                {
                    int score = 0;
                    bool isBlend = animPathLower.Contains("idle01-idle02") || animPathLower.Contains("idle02-idle01");

                    if (isBlend)
                    {
                        score -= 10;
                    }
                    if (animPathLower.Contains("idle01"))
                    {
                        score += 5;
                    }
                    if (animPathLower.Contains("idle02"))
                    {
                        score += 3;
                    }
                    if (animPathLower.Contains("rootmotion") || animPathLower.Contains("[rm]"))
                    {
                        score -= 5;
                    }

                    if (score > bestIdleScore)
                    {
                        bestIdleScore = score;
                        bestIdleClip = clip;
                    }
                }
            }

            if (bestIdleClip != null)
            {
                ApplyIdleCandidate(animations, bestIdleClip, bestIdleScore);
            }

            if (bestWalkClip != null && !animations.ContainsKey("Walk"))
            {
                animations["Walk"] = bestWalkClip;
                Debug.Log($"Found Walk animation (preferred): {bestWalkClip.name}");
            }

            if (bestJumpBeginClip != null && !animations.ContainsKey("JumpBegin"))
            {
                animations["JumpBegin"] = bestJumpBeginClip;
                Debug.Log($"Found JumpBegin animation: {bestJumpBeginClip.name}");
            }
            if (bestJumpLoopClip != null && !animations.ContainsKey("JumpLoop"))
            {
                animations["JumpLoop"] = bestJumpLoopClip;
                Debug.Log($"Found JumpLoop animation: {bestJumpLoopClip.name}");
            }
            if (bestJumpFallClip != null && !animations.ContainsKey("JumpFall"))
            {
                animations["JumpFall"] = bestJumpFallClip;
                Debug.Log($"Found JumpFall animation: {bestJumpFallClip.name}");
            }
            if (bestJumpLandClip != null && !animations.ContainsKey("JumpLand"))
            {
                animations["JumpLand"] = bestJumpLandClip;
                Debug.Log($"Found JumpLand animation: {bestJumpLandClip.name}");
            }

        }
    }
    private static AnimationClip LoadFirstClip(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                return clip;
            }
        }

        return null;
    }

    private static void TryAddExplicitClips(System.Collections.Generic.Dictionary<string, AnimationClip> animations)
    {
        AnimationClip idleClip = LoadFirstClip(ThirdPersonSetupConfig.KevinIdleClipPaths[0]);
        AnimationClip walkClip = LoadFirstClip(ThirdPersonSetupConfig.KevinWalkClipPaths[0]);
        AnimationClip jumpLoopClip = LoadFirstClip(ThirdPersonSetupConfig.KevinJumpLoopClipPaths[0]);

        if (idleClip != null)
        {
            ApplyIdleCandidate(animations, idleClip, ScoreIdleClip(idleClip.name.ToLower()));
        }

        if (walkClip != null && !animations.ContainsKey(ThirdPersonSetupConfig.WalkStateName))
        {
            animations[ThirdPersonSetupConfig.WalkStateName] = walkClip;
        }

        if (jumpLoopClip != null && !animations.ContainsKey(ThirdPersonSetupConfig.JumpLoopStateName))
        {
            animations[ThirdPersonSetupConfig.JumpLoopStateName] = jumpLoopClip;
        }
    }

    private static void ValidateAnimatorSetup(UnityEditor.Animations.AnimatorController controller,
        System.Collections.Generic.Dictionary<string, AnimationClip> animations)
    {
        if (controller == null)
        {
            ReportError("Animator controller not found; animation setup may be incomplete.");
            return;
        }

        if (!animations.ContainsKey(ThirdPersonSetupConfig.IdleStateName))
        {
            ReportWarning("Idle animation clip not found.");
        }

        if (!animations.ContainsKey(ThirdPersonSetupConfig.WalkStateName))
        {
            ReportWarning("Walk animation clip not found.");
        }

        if (!animations.ContainsKey(ThirdPersonSetupConfig.JumpLoopStateName)
            && !animations.ContainsKey(ThirdPersonSetupConfig.JumpBeginStateName)
            && !animations.ContainsKey(ThirdPersonSetupConfig.JumpFallStateName)
            && !animations.ContainsKey(ThirdPersonSetupConfig.JumpLandStateName))
        {
            ReportWarning("Jump animation clips not found (begin/loop/fall/land).");
        }
    }

    private static string[] GetAnimationClipGuids(string searchDir)
    {
        if (AnimationClipGuidCache.TryGetValue(searchDir, out string[] cached))
        {
            return cached;
        }

        string[] animGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { searchDir });
        AnimationClipGuidCache[searchDir] = animGuids;
        return animGuids;
    }

    private static void ApplyIdleCandidate(System.Collections.Generic.Dictionary<string, AnimationClip> animations,
        AnimationClip candidate,
        int candidateScore)
    {
        if (!animations.TryGetValue(ThirdPersonSetupConfig.IdleStateName, out AnimationClip existing))
        {
            animations[ThirdPersonSetupConfig.IdleStateName] = candidate;
            Debug.Log($"Found Idle animation (preferred): {candidate.name}");
            return;
        }

        int existingScore = ScoreIdleClip(existing.name.ToLower());
        if (candidateScore > existingScore)
        {
            animations[ThirdPersonSetupConfig.IdleStateName] = candidate;
            Debug.Log($"Updated Idle animation (preferred): {candidate.name}");
        }
    }

    private static int ScoreIdleClip(string nameOrPathLower)
    {
        int score = 0;
        bool isBlend = nameOrPathLower.Contains("idle01-idle02") || nameOrPathLower.Contains("idle02-idle01");

        if (isBlend)
        {
            score -= 10;
        }
        if (nameOrPathLower.Contains("idle01"))
        {
            score += 5;
        }
        if (nameOrPathLower.Contains("idle02"))
        {
            score += 3;
        }
        if (nameOrPathLower.Contains("rootmotion") || nameOrPathLower.Contains("[rm]"))
        {
            score -= 5;
        }

        return score;
    }
}
