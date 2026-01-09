using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

public class ThirdPersonSetup : EditorWindow
{
    [MenuItem("Tools/Third Person/Auto Setup Scene")]
    public static void SetupThirdPersonScene()
    {
        if (EditorUtility.DisplayDialog("Third Person Setup",
            "This will create a Player capsule, configure the camera, and add a ground plane to your scene. Continue?",
            "Yes, Set It Up!",
            "Cancel"))
        {
            CreateThirdPersonScene();
        }
    }

    private static void CreateThirdPersonScene()
    {
        // Create Ground
        GameObject ground = CreateGround();

        // Create Player
        GameObject player = CreatePlayer();

        // Setup Camera
        SetupCamera(player);

        // Select the player for easy inspection
        Selection.activeGameObject = player;

        // Focus scene view on player
        SceneView.lastActiveSceneView?.FrameSelected();

        EditorUtility.DisplayDialog("Setup Complete!",
            "Third-person scene setup complete!\n\n" +
            "Created:\n" +
            "- Player (Capsule with CharacterController)\n" +
            "- Main Camera (with ThirdPersonCamera)\n" +
            "- Ground Plane\n\n" +
            "Press PLAY to test!\n\n" +
            "Controls:\n" +
            "WASD - Move\n" +
            "Mouse - Look\n" +
            "Space - Jump\n" +
            "Shift - Sprint",
            "Awesome!");

        Debug.Log("Third Person Setup Complete! Press Play to test.");
    }

    private static GameObject CreateGround()
    {
        // Check if ground already exists and delete it
        GameObject existingGround = GameObject.Find("Ground");
        if (existingGround != null)
        {
            Debug.Log("Removing existing Ground...");
            Undo.DestroyObjectImmediate(existingGround);
        }

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(5, 1, 5);

        // Add a material for better visibility (optional)
        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.5f, 0.6f, 0.5f); // Greenish gray
            renderer.sharedMaterial = mat;
        }

        Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
        Debug.Log("Created Ground plane");
        return ground;
    }

    private static GameObject CreatePlayer()
    {
        // Check if player already exists and delete it
        GameObject existingPlayer = GameObject.Find("Player");
        if (existingPlayer != null)
        {
            Debug.Log("Removing existing Player...");
            Undo.DestroyObjectImmediate(existingPlayer);
        }

        // Create capsule
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0, 1, 0);

        // Remove the default collider (CharacterController will handle collision)
        CapsuleCollider capsuleCollider = player.GetComponent<CapsuleCollider>();
        if (capsuleCollider != null)
        {
            Object.DestroyImmediate(capsuleCollider);
        }

        // Add CharacterController
        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 2f;
        characterController.radius = 0.5f;
        characterController.center = Vector3.zero;

        // Add ThirdPersonController
        ThirdPersonController controller = player.AddComponent<ThirdPersonController>();

        // Add PlayerInput
        PlayerInput playerInput = player.AddComponent<PlayerInput>();

        // Try to find and assign the InputSystem_Actions asset
        string[] guids = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);

            if (inputActions != null)
            {
                playerInput.actions = inputActions;
                playerInput.defaultActionMap = "Player";
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
                Debug.Log("Assigned InputSystem_Actions to Player Input");
            }
        }
        else
        {
            Debug.LogWarning("Could not find InputSystem_Actions asset. Please assign it manually to the Player Input component.");
        }

        // Change capsule color for better visibility
        Renderer renderer = player.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.3f, 0.5f, 0.8f); // Blue
            renderer.sharedMaterial = mat;
        }

        Undo.RegisterCreatedObjectUndo(player, "Create Player");
        Debug.Log("Created Player with CharacterController and ThirdPersonController");
        return player;
    }

    private static void SetupCamera(GameObject player)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("No Main Camera found in scene!");
            return;
        }

        // Remove existing ThirdPersonCamera if present
        ThirdPersonCamera existingCameraScript = mainCamera.GetComponent<ThirdPersonCamera>();
        if (existingCameraScript != null)
        {
            Debug.Log("Removing existing ThirdPersonCamera from Main Camera...");
            Undo.DestroyObjectImmediate(existingCameraScript);
        }

        // Add fresh ThirdPersonCamera
        ThirdPersonCamera cameraScript = mainCamera.gameObject.AddComponent<ThirdPersonCamera>();
        Undo.RegisterCreatedObjectUndo(cameraScript, "Add Third Person Camera");

        // Assign player as target using SerializedObject for proper Undo support
        SerializedObject serializedCamera = new SerializedObject(cameraScript);
        SerializedProperty targetProperty = serializedCamera.FindProperty("target");
        targetProperty.objectReferenceValue = player.transform;
        serializedCamera.ApplyModifiedProperties();

        Debug.Log("Configured Main Camera with ThirdPersonCamera targeting Player");
    }

    [MenuItem("Tools/Third Person/Remove Setup")]
    public static void RemoveSetup()
    {
        if (EditorUtility.DisplayDialog("Remove Third Person Setup",
            "This will remove the Player and Ground objects from the scene. Continue?",
            "Yes, Remove",
            "Cancel"))
        {
            GameObject player = GameObject.Find("Player");
            GameObject ground = GameObject.Find("Ground");

            if (player != null)
            {
                Undo.DestroyObjectImmediate(player);
                Debug.Log("Removed Player");
            }

            if (ground != null)
            {
                Undo.DestroyObjectImmediate(ground);
                Debug.Log("Removed Ground");
            }

            // Optionally remove camera component
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                ThirdPersonCamera cameraScript = mainCamera.GetComponent<ThirdPersonCamera>();
                if (cameraScript != null)
                {
                    Undo.DestroyObjectImmediate(cameraScript);
                    Debug.Log("Removed ThirdPersonCamera from Main Camera");
                }
            }

            Debug.Log("Third Person Setup removed");
        }
    }

    [MenuItem("Tools/Third Person/Open Setup Guide")]
    public static void OpenSetupGuide()
    {
        string guidePath = "Assets/../THIRD_PERSON_SETUP.md";
        System.Diagnostics.Process.Start(guidePath);
    }
}
