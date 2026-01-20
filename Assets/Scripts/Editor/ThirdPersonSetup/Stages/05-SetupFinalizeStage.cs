using UnityEngine;
using UnityEditor;

public sealed class SetupFinalizeStage : ISetupStage
{
    public void Run(ThirdPersonSetup.SetupContext context)
    {
        if (context.Player != null)
        {
            Selection.activeGameObject = context.Player;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        Debug.Log("=== THIRD PERSON SETUP COMPLETE ===");
        Debug.Log($"Created: Player {(context.CharacterApplied ? "(with character model)" : "(capsule)")}, Camera, Ground");
        Debug.Log("Press PLAY to test! Controls: WASD=Move, Mouse=Look, Space=Jump, Shift=Sprint");
        ThirdPersonSetup.EnsureTmpSettingsAsset();
        ThirdPersonSetup.PrintReportSummary();
    }
}
