public sealed class SetupEnvironmentStage : ISetupStage
{
    public void Run(ThirdPersonSetup.SetupContext context)
    {
        ThirdPersonSetup.CreateGround();
        ThirdPersonSetup.CreateRampAndStairs();
    }
}
