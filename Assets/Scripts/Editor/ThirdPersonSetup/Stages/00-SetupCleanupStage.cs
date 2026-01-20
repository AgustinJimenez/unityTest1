public sealed class SetupCleanupStage : ISetupStage
{
    public void Run(ThirdPersonSetup.SetupContext context)
    {
        ThirdPersonSetup.CleanupExistingSetup();
    }
}
