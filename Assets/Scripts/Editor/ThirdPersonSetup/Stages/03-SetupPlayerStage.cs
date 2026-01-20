public sealed class SetupPlayerStage : ISetupStage
{
    public void Run(ThirdPersonSetup.SetupContext context)
    {
        context.Player = ThirdPersonSetup.CreatePlayer();
        ThirdPersonSetup.SetupCamera(context.Player);
        context.CharacterApplied = ThirdPersonSetup.TryApplyCharacterModel(context.Player);
        if (context.CharacterApplied)
        {
            ThirdPersonSetup.FixMaterialTextures();
        }
    }
}
