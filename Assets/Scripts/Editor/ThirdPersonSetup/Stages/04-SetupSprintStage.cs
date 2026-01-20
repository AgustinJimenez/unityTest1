public sealed class SetupSprintStage : ISetupStage
{
    public void Run(ThirdPersonSetup.SetupContext context)
    {
        ThirdPersonSetup.SetupKevinIglesiasSprint();
    }
}
