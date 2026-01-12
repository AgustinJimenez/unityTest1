using UnityEngine;
using UnityEditor;

public partial class ThirdPersonSetup : EditorWindow
{
    private const string AutoRunPrefsKey = "ThirdPersonSetup.AutoRun";

    [InitializeOnLoadMethod]
    private static void AutoRunOnCompile()
    {
        if (!EditorPrefs.GetBool(AutoRunPrefsKey, true))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            PerformCompleteSetup();
        };
    }

    [MenuItem("Tools/Third Person/Auto Run Setup")]
    private static void ToggleAutoRun()
    {
        bool current = EditorPrefs.GetBool(AutoRunPrefsKey, true);
        EditorPrefs.SetBool(AutoRunPrefsKey, !current);
        Debug.Log($"ThirdPerson auto-run set to {!current}");
    }

    [MenuItem("Tools/Third Person/Complete Setup")]
    public static void CompleteSetup()
    {
        PerformCompleteSetup();
    }

    private static void PerformCompleteSetup()
    {
        // Step 1: Clean up existing setup first
        CleanupExistingSetup();

        // Step 3: Create Ground
        GameObject ground = CreateGround();

        // Step 4: Add lighting if missing
        EnsureLighting();

        // Step 5: Create Player with capsule
        GameObject player = CreatePlayer();

        // Step 5: Setup Camera
        SetupCamera(player);

        // Step 6: Try to find and apply character model
        bool characterApplied = TryApplyCharacterModel(player);

        // Step 7: Fix materials after character is applied (to connect textures)
        if (characterApplied)
        {
            FixMaterialTextures();
        }

        // Step 8: Setup sprint animations
        SetupKevinIglesiasSprint();

        // Select the player for easy inspection
        Selection.activeGameObject = player;
        SceneView.lastActiveSceneView?.FrameSelected();

        // Log completion
        Debug.Log("=== THIRD PERSON SETUP COMPLETE ===");
        Debug.Log($"Created: Player {(characterApplied ? "(with character model)" : "(capsule)")}, Camera, Ground");
        Debug.Log("Press PLAY to test! Controls: WASD=Move, Mouse=Look, Space=Jump, Shift=Sprint");
    }
}
