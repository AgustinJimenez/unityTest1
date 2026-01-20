using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public partial class ThirdPersonSetup
{
    public sealed class SetupContext
    {
        public GameObject Player;
        public bool CharacterApplied;
    }

    internal static string LastAvatarSourcePath;

    private static List<ISetupStage> BuildStages()
    {
        return new List<ISetupStage>
        {
            new SetupCleanupStage(),
            new SetupEnvironmentStage(),
            new SetupLightingStage(),
            new SetupPlayerStage(),
            new SetupSprintStage(),
            new SetupFinalizeStage()
        };
    }
}
