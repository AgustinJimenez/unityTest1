using UnityEngine;
using UnityEditor;

public partial class ThirdPersonSetup : EditorWindow
{
    [InitializeOnLoadMethod]
    private static void AutoRunOnCompile()
    {
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

    [MenuItem("Tools/Third Person/Setup All")]
    public static void SetupAll()
    {
        PerformCompleteSetup();
    }

    private static void PerformCompleteSetup()
    {
        ResetReport();

        SetupContext context = new SetupContext();
        foreach (ISetupStage stage in BuildStages())
        {
            stage.Run(context);
        }
    }
}
