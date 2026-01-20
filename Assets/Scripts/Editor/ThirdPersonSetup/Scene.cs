using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

public partial class ThirdPersonSetup
{
    internal static void CleanupExistingSetup()
    {
        DestroyAllByName("Player");
        DestroyAllByName("Ground");
        DestroyAllByName("Ramp");
        DestroyAllByName("Stairs");

        // Clean up camera script
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            ThirdPersonCamera existingCameraScript = mainCamera.GetComponent<ThirdPersonCamera>();
            if (existingCameraScript != null)
            {
                Undo.DestroyObjectImmediate(existingCameraScript);
            }
        }
    }

    private static void DestroyAllByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        foreach (GameObject obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (obj != null && obj.name == name)
            {
                Undo.DestroyObjectImmediate(obj);
            }
        }
    }
    internal static GameObject CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = ThirdPersonSetupConfig.GroundScale;

        // Add a material for better visibility
        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = ThirdPersonSetupConfig.GroundColor;
            renderer.sharedMaterial = mat;
        }

        Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
        return ground;
    }

    internal static void CreateRampAndStairs()
    {
        GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = "Ramp";
        ramp.transform.position = ThirdPersonSetupConfig.RampPosition;
        ramp.transform.localScale = ThirdPersonSetupConfig.RampScale;
        ramp.transform.rotation = Quaternion.Euler(ThirdPersonSetupConfig.RampRotationEuler);
        ApplyMaterialColor(ramp, ThirdPersonSetupConfig.RampColor);
        Undo.RegisterCreatedObjectUndo(ramp, "Create Ramp");

        GameObject stairsRoot = new GameObject("Stairs");
        Undo.RegisterCreatedObjectUndo(stairsRoot, "Create Stairs Root");

        Vector3 stepSize = ThirdPersonSetupConfig.StairStepSize;
        Vector3 start = ThirdPersonSetupConfig.StairStartPosition;

        var stepFilters = new System.Collections.Generic.List<MeshFilter>();

        for (int i = 0; i < ThirdPersonSetupConfig.StairStepCount; i++)
        {
            GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Step_{i + 1}";
            step.transform.SetParent(stairsRoot.transform);
            step.transform.localScale = stepSize;
            step.transform.position = start + new Vector3(0f, stepSize.y * i, stepSize.z * i);
            ApplyMaterialColor(step, ThirdPersonSetupConfig.StairsColor);
            Undo.RegisterCreatedObjectUndo(step, "Create Stair Step");

            MeshFilter filter = step.GetComponent<MeshFilter>();
            if (filter != null)
            {
                stepFilters.Add(filter);
            }
        }

        BuildStairsMeshCollider(stairsRoot, stepFilters);
    }

    private static void BuildStairsMeshCollider(GameObject stairsRoot, System.Collections.Generic.List<MeshFilter> stepFilters)
    {
        if (stairsRoot == null || stepFilters == null || stepFilters.Count == 0)
        {
            return;
        }

        GameObject meshColliderObject = new GameObject("Stairs_MeshCollider");
        Undo.RegisterCreatedObjectUndo(meshColliderObject, "Create Stairs Mesh Collider");
        meshColliderObject.transform.SetParent(stairsRoot.transform);
        meshColliderObject.transform.localPosition = Vector3.zero;
        meshColliderObject.transform.localRotation = Quaternion.identity;
        meshColliderObject.transform.localScale = Vector3.one;

        var combine = new CombineInstance[stepFilters.Count];
        for (int i = 0; i < stepFilters.Count; i++)
        {
            MeshFilter filter = stepFilters[i];
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            combine[i].mesh = filter.sharedMesh;
            combine[i].transform = filter.transform.localToWorldMatrix;

            BoxCollider stepCollider = filter.GetComponent<BoxCollider>();
            if (stepCollider != null)
            {
                stepCollider.enabled = false;
            }
        }

        Mesh combined = new Mesh();
        combined.name = "Stairs_CombinedMesh";
        combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combined.CombineMeshes(combine, true, true);

        MeshCollider meshCollider = meshColliderObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = combined;
        meshCollider.convex = false;
    }

    private static void ApplyMaterialColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }
    internal static void EnsureLighting()
    {
        // Set ambient lighting for better visibility - using Skybox mode with high intensity
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ThirdPersonSetupConfig.AmbientLightColor;
        RenderSettings.ambientIntensity = ThirdPersonSetupConfig.AmbientIntensity;

        // Remove skybox to use flat color (simpler, brighter)
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;

        Debug.Log("Set ambient lighting (Flat mode with high intensity)");

        // Check if there's already a directional light
        Light[] existingLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        bool hasDirectionalLight = false;
        foreach (Light existingLight in existingLights)
        {
            if (existingLight.type == LightType.Directional)
            {
                hasDirectionalLight = true;
                // Update existing light to be much brighter
                existingLight.intensity = ThirdPersonSetupConfig.DirectionalLightIntensity;
                Debug.Log("Updated existing directional light intensity to 3");
            }
        }

        if (!hasDirectionalLight)
        {
            // Create a directional light
            GameObject lightGameObject = new GameObject("Directional Light");
            Light lightComponent = lightGameObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.color = Color.white;
            lightComponent.intensity = ThirdPersonSetupConfig.DirectionalLightIntensity;

            // Position and rotate the light (standard Unity setup)
            lightGameObject.transform.rotation = Quaternion.Euler(ThirdPersonSetupConfig.DirectionalLightRotationEuler);

            Undo.RegisterCreatedObjectUndo(lightGameObject, "Create Directional Light");
            Debug.Log("Created directional light with intensity 3");
        }
    }
    internal static GameObject CreatePlayer()
    {
        // Create capsule
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = ThirdPersonSetupConfig.PlayerSpawnPosition;

        // Remove the default collider (CharacterController will handle collision)
        CapsuleCollider capsuleCollider = player.GetComponent<CapsuleCollider>();
        if (capsuleCollider != null)
        {
            Object.DestroyImmediate(capsuleCollider);
        }

        // Add CharacterController
        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = ThirdPersonSetupConfig.CharacterControllerHeight;
        characterController.radius = ThirdPersonSetupConfig.CharacterControllerRadius;
        characterController.center = ThirdPersonSetupConfig.CharacterControllerCenter;

        // Add ThirdPersonController
        player.AddComponent<ThirdPersonController>();

        // Add PlayerInput
        PlayerInput playerInput = player.AddComponent<PlayerInput>();

        // Try to find and assign the InputSystem_Actions asset
        string[] guids = AssetDatabase.FindAssets(ThirdPersonSetupConfig.InputActionsSearch);
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);

            if (inputActions != null)
            {
                playerInput.actions = inputActions;
                playerInput.defaultActionMap = ThirdPersonSetupConfig.DefaultActionMap;
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            }
        }

        // Change capsule color for better visibility
        Renderer renderer = player.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = ThirdPersonSetupConfig.PlayerColor;
            renderer.sharedMaterial = mat;
        }

        Undo.RegisterCreatedObjectUndo(player, "Create Player");
        return player;
    }
    internal static void SetupCamera(GameObject player)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("No Main Camera found in scene!");
            return;
        }

        // Add fresh ThirdPersonCamera
        ThirdPersonCamera cameraScript = mainCamera.gameObject.AddComponent<ThirdPersonCamera>();
        Undo.RegisterCreatedObjectUndo(cameraScript, "Add Third Person Camera");

        // Assign player as target
        SerializedObject serializedCamera = new SerializedObject(cameraScript);
        SerializedProperty targetProperty = serializedCamera.FindProperty(ThirdPersonSetupConfig.CameraTargetPropertyName);
        targetProperty.objectReferenceValue = player.transform;
        serializedCamera.ApplyModifiedProperties();
    }
}
