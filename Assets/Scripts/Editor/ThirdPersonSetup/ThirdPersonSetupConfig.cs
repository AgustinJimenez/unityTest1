internal static class ThirdPersonSetupConfig
{
    public static readonly UnityEngine.Vector3 PlayerSpawnPosition = new UnityEngine.Vector3(0f, 1f, 0f);
    public static readonly UnityEngine.Vector3 GroundScale = new UnityEngine.Vector3(5f, 1f, 5f);
    public static readonly UnityEngine.Color GroundColor = new UnityEngine.Color(0.5f, 0.6f, 0.5f);
    public static readonly UnityEngine.Color PlayerColor = new UnityEngine.Color(0.3f, 0.5f, 0.8f);

    public const float CharacterControllerHeight = 2f;
    public const float CharacterControllerRadius = 0.5f;
    public static readonly UnityEngine.Vector3 CharacterControllerCenter = UnityEngine.Vector3.zero;

    public static readonly UnityEngine.Color AmbientLightColor = new UnityEngine.Color(0.7f, 0.7f, 0.7f);
    public const float AmbientIntensity = 1.5f;
    public const float DirectionalLightIntensity = 3f;
    public static readonly UnityEngine.Vector3 DirectionalLightRotationEuler = new UnityEngine.Vector3(50f, -30f, 0f);

    public const string InputActionsSearch = "InputSystem_Actions t:InputActionAsset";
    public const string DefaultActionMap = "Player";
    public const string CameraTargetPropertyName = "target";

    public const string SpeedParam = "Speed";
    public const string IsGroundedParam = "IsGrounded";
    public const string JumpParam = "Jump";
    public const string VerticalVelocityParam = "VerticalVelocity";
    public const string IsSprintingParam = "IsSprinting";
    public const string HorizontalParam = "Horizontal";
    public const string VerticalParam = "Vertical";

    public const string IdleStateName = "Idle";
    public const string WalkStateName = "Walk";
    public const string JumpBeginStateName = "JumpBegin";
    public const string JumpLoopStateName = "JumpLoop";
    public const string JumpFallStateName = "JumpFall";
    public const string JumpLandStateName = "JumpLand";
    public const string SprintStateName = "Sprint";

    public const string DummyPrefabPath = "Assets/Kevin Iglesias/Human Character Dummy/Prefabs/HumanDummy_M White.prefab";
    public const string DummyPrefabsDir = "Assets/Kevin Iglesias/Human Character Dummy/Prefabs";
    public const string CharactersRootPath = "Assets/Characters";
    public const string LeonardMaterialsPath = "Assets/Characters/leonard/Materials";
    public const string CharactersMaterialsPath = "Assets/Characters/Materials";
    public const string DefaultAnimatorControllerPath = "Assets/Characters/leonard/CharacterAnimator.controller";

    public static readonly string[] MixamoSearchTerms =
    {
        "leonard",
        "leonard_character",
        "character",
        "mixamo"
    };

    public static readonly string[] KevinFallbackDirs =
    {
        "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles",
        "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk",
        "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump"
    };

    public static readonly string[] KevinIdleClipPaths =
    {
        "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx",
        "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle02.fbx",
        "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01-Idle02.fbx"
    };

    public static readonly string[] KevinWalkClipPaths =
    {
        "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx"
    };

    public static readonly string[] KevinJumpLoopClipPaths =
    {
        "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Jump01.fbx"
    };

    public const string SprintRootMotionPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/RootMotion";
    public static readonly string[] SprintAnimationNames =
    {
        "HumanM@Sprint01_Forward [RM]",
        "HumanM@Sprint01_ForwardLeft [RM]",
        "HumanM@Sprint01_ForwardRight [RM]",
        "HumanM@Sprint01_Left [RM]",
        "HumanM@Sprint01_Right [RM]"
    };

    public const string HairDiffuseSearch = "Ch31_1002_Diffuse";
}
