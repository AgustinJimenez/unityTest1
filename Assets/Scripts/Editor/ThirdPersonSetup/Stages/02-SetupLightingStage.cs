public sealed class SetupLightingStage : ISetupStage
{
    public void Run(ThirdPersonSetup.SetupContext context)
    {
        ThirdPersonSetup.EnsureLighting();
    }
}
