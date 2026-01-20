using UnityEngine;
using UnityEditor;
using System.IO;

public partial class ThirdPersonSetup
{
    private static readonly System.Collections.Generic.Dictionary<string, string[]> AssetGuidCache =
        new System.Collections.Generic.Dictionary<string, string[]>();
    private static readonly System.Collections.Generic.Dictionary<string, GameObject> AssetObjectCache =
        new System.Collections.Generic.Dictionary<string, GameObject>();
    private static readonly System.Collections.Generic.Dictionary<string, bool> AssetExistsCache =
        new System.Collections.Generic.Dictionary<string, bool>();

    internal static bool TryApplyCharacterModel(GameObject player)
    {
        GameObject characterPrefab = null;

        // Prefer the Kevin Iglesias Human Character Dummy prefab if available.
        characterPrefab = LoadAssetCached<GameObject>(ThirdPersonSetupConfig.DummyPrefabPath);

        if (characterPrefab == null)
        {
            string[] dummyGuids = GetAssetGuidsCached("t:GameObject", new[] { ThirdPersonSetupConfig.DummyPrefabsDir });
            if (dummyGuids.Length > 0)
            {
                string dummyPath = AssetDatabase.GUIDToAssetPath(dummyGuids[0]);
                characterPrefab = LoadAssetCached<GameObject>(dummyPath);
            }
        }

        // Fallback to Mixamo-style models if the dummy isn't found.
        string[] searchTerms = ThirdPersonSetupConfig.MixamoSearchTerms;

        if (characterPrefab == null)
        {
            foreach (string term in searchTerms)
            {
                // Search for both FBX and DAE models
                string[] guids = GetAssetGuidsCached(term + " t:GameObject", null);
                if (guids.Length > 0)
                {
                    // Prefer DAE files if available (better texture support)
                    foreach (string guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (path.EndsWith(".dae") || path.EndsWith(".fbx"))
                        {
                            characterPrefab = LoadAssetCached<GameObject>(path);
                            if (characterPrefab != null)
                            {
                                break;
                            }
                        }
                    }
                    if (characterPrefab != null) break;
                }
            }
        }

        if (characterPrefab == null)
        {
            ReportWarning("No character prefab found; using capsule only.");
            return false;
        }

        // Remove capsule visuals
        MeshRenderer meshRenderer = player.GetComponent<MeshRenderer>();
        MeshFilter meshFilter = player.GetComponent<MeshFilter>();

        if (meshRenderer != null)
        {
            Undo.DestroyObjectImmediate(meshRenderer);
        }

        if (meshFilter != null)
        {
            Undo.DestroyObjectImmediate(meshFilter);
        }

        // Check if it's a model file and configure import settings
        string prefabPath = AssetDatabase.GetAssetPath(characterPrefab);
        string avatarModelPath = GetModelPathFromPrefab(prefabPath);
        string avatarSourcePath = string.IsNullOrEmpty(avatarModelPath) ? prefabPath : avatarModelPath;
        LastAvatarSourcePath = avatarSourcePath;

        if (avatarSourcePath.EndsWith(".dae") && FileExistsCached(avatarSourcePath))
        {
            ConfigureCharacterModel(avatarSourcePath);
            ExtractMaterialsFromModel(avatarSourcePath);
        }

        // Instantiate character as child
        GameObject characterInstance = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab, player.transform);
        characterInstance.name = "CharacterModel";
        characterInstance.transform.localPosition = Vector3.zero;
        characterInstance.transform.localRotation = Quaternion.identity;
        characterInstance.transform.localScale = Vector3.one; // No runtime scaling needed - scale is set in importer

        Undo.RegisterCreatedObjectUndo(characterInstance, "Add Character Model");

        // Check for materials, apply simple ones if needed
        bool hasMaterials = CheckForMaterials(characterInstance);
        if (!hasMaterials)
        {
            ApplySimpleMaterials(characterInstance);
        }

        // Adjust CharacterController to fit the character
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            Renderer[] renderers = characterInstance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                }

                Vector3 localMin = player.transform.InverseTransformPoint(bounds.min);
                Vector3 localMax = player.transform.InverseTransformPoint(bounds.max);

                float height = Mathf.Abs(localMax.y - localMin.y);
                // Don't use full bounding box width (includes arms) - use a reasonable torso radius
                // Typical human torso is ~0.3m radius, cap at 0.4m max
                float boundingRadius = Mathf.Max(Mathf.Abs(localMax.x - localMin.x), Mathf.Abs(localMax.z - localMin.z)) * 0.5f;
                float radius = Mathf.Min(boundingRadius, 0.3f);
                Vector3 center = new Vector3(0, height * 0.5f, 0);

                Undo.RecordObject(controller, "Adjust Character Controller");
                controller.height = height;
                controller.radius = radius;
                controller.center = center;
                controller.skinWidth = Mathf.Clamp(ThirdPersonSetupConfig.CharacterControllerSkinWidth, 0.005f, 0.08f);
                controller.stepOffset = Mathf.Clamp(ThirdPersonSetupConfig.CharacterControllerStepOffset, 0.05f, height * 0.5f);
            }
        }

        // Add Animator and setup controller (prefer existing animator on the model)
        Animator animator = characterInstance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = characterInstance.GetComponentInChildren<Animator>(true);
        }
        if (animator == null)
        {
            animator = characterInstance.AddComponent<Animator>();
            Undo.RegisterCreatedObjectUndo(animator, "Add Animator");
        }

        // Setup avatar (required for humanoid animations)
        SetupAvatar(animator, avatarSourcePath);

        // Setup animation controller
        SetupAnimatorController(animator, prefabPath, avatarSourcePath);

        // CRITICAL: Disable root motion AFTER controller is assigned (prevents character from moving with animation)
        // Must be done after controller assignment or it gets reset
        animator.applyRootMotion = false;

        Debug.Log("Disabled Apply Root Motion on Animator");

        return true;
    }
    private static bool CheckForMaterials(GameObject characterInstance)
    {
        SkinnedMeshRenderer[] skinnedRenderers = characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
        MeshRenderer[] meshRenderers = characterInstance.GetComponentsInChildren<MeshRenderer>();

        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && mat.shader != null)
                    {
                        return true;
                    }
                }
            }
        }

        foreach (MeshRenderer renderer in meshRenderers)
        {
            if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && mat.shader != null)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
    private static void ApplySimpleMaterials(GameObject characterInstance)
    {
        Material skinMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        skinMaterial.color = new Color(0.9f, 0.7f, 0.6f);

        Material clothesMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        clothesMaterial.color = new Color(0.2f, 0.3f, 0.5f);

        Material hairMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        hairMaterial.color = new Color(0.3f, 0.2f, 0.1f);

        SkinnedMeshRenderer[] skinnedRenderers = characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
        MeshRenderer[] meshRenderers = characterInstance.GetComponentsInChildren<MeshRenderer>();

        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            string meshName = renderer.name.ToLower();
            Material[] materials = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                if (meshName.Contains("hair") || meshName.Contains("head"))
                {
                    materials[i] = hairMaterial;
                }
                else if (meshName.Contains("body") || meshName.Contains("torso"))
                {
                    materials[i] = clothesMaterial;
                }
                else
                {
                    materials[i] = skinMaterial;
                }
            }

            renderer.sharedMaterials = materials;
        }

        foreach (MeshRenderer renderer in meshRenderers)
        {
            string meshName = renderer.name.ToLower();
            Material[] materials = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                if (meshName.Contains("hair") || meshName.Contains("head"))
                {
                    materials[i] = hairMaterial;
                }
                else if (meshName.Contains("body") || meshName.Contains("torso"))
                {
                    materials[i] = clothesMaterial;
                }
                else
                {
                    materials[i] = skinMaterial;
                }
            }

            renderer.sharedMaterials = materials;
        }
    }
    internal static void FixMaterialTextures()
    {
        Debug.Log("=== FIXING MATERIAL TEXTURES ===");

        // Find all materials in Characters folder and subfolders
        string[] materialGuids = GetAssetGuidsCached("t:Material", new[] { ThirdPersonSetupConfig.CharactersRootPath });

        if (materialGuids.Length == 0)
        {
            Debug.LogWarning($"⚠️ No materials found in {ThirdPersonSetupConfig.CharactersRootPath} - materials may not have been extracted!");
            return; // No materials to fix
        }

        Debug.Log($"✓ Found {materialGuids.Length} materials to process");

        foreach (string guid in materialGuids)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null) continue;

            Debug.Log($"========================================");
            Debug.Log($"Processing: {material.name}");
            Debug.Log($"  Path: {materialPath}");
            Debug.Log($"  Current Shader: {material.shader.name}");

            // Log BEFORE state
            Texture beforeBaseMap = material.GetTexture("_BaseMap");
            float beforeWorkflow = material.HasProperty("_WorkflowMode") ? material.GetFloat("_WorkflowMode") : -1f;
            Color beforeColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.black;

            Debug.Log($"  BEFORE - BaseMap: {(beforeBaseMap != null ? beforeBaseMap.name : "NULL")}");
            Debug.Log($"  BEFORE - WorkflowMode: {beforeWorkflow} (0=Metallic, 1=Specular)");
            Debug.Log($"  BEFORE - BaseColor: {beforeColor}");

            // Ensure material uses URP Lit shader
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (material.shader != urpShader)
            {
                material.shader = urpShader;
                Debug.Log($"  ✓ Changed shader to URP/Lit");
            }

            // Get the material name for later use
            string materialName = material.name;
            string prefix = materialName.Split('_')[0];

            // IMPORTANT: Set to Specular workflow
            if (material.HasProperty("_WorkflowMode"))
            {
                material.SetFloat("_WorkflowMode", 1f); // 1 = Specular
                material.EnableKeyword("_SPECULAR_SETUP");
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                Debug.Log($"  ✓ Set to Specular workflow");
            }

            // Set material colors and properties
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_SpecColor", Color.white);

            // Adjust smoothness based on material type
            if (materialName.ToLower().Contains("hair"))
            {
                material.SetFloat("_Smoothness", 0.3f); // Less shiny for hair
                Debug.Log("  Set hair smoothness to 0.3");
            }
            else
            {
                material.SetFloat("_Smoothness", 0.5f); // Default for body
            }

            EditorUtility.SetDirty(material);

            // Find textures with matching prefix
            string[] textureGuids = GetAssetGuidsCached($"{prefix} t:Texture2D",
                new[] { ThirdPersonSetupConfig.CharactersRootPath });

            if (textureGuids.Length == 0)
            {
                Debug.LogWarning($"  ⚠️ No textures found for prefix '{prefix}'");
            }
            else
            {
                Debug.Log($"  Found {textureGuids.Length} textures for prefix '{prefix}'");
            }

            int texturesAssigned = 0;
            bool diffuseAssigned = false; // Track if we already assigned a diffuse texture

            // Determine which texture set to use based on material name
            // For Mixamo characters: 1001 = body/skin, 1002 = hair
            string preferredTextureNumber = "";
            if (materialName.ToLower().Contains("hair"))
            {
                preferredTextureNumber = "1002"; // Hair textures
                Debug.Log($"  Material is HAIR, preferring 1002 textures");
            }
            else if (materialName.ToLower().Contains("body"))
            {
                preferredTextureNumber = "1001"; // Body textures
                Debug.Log($"  Material is BODY, preferring 1001 textures");
            }

            foreach (string texGuid in textureGuids)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(texGuid);
                string textureName = Path.GetFileNameWithoutExtension(texturePath);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

                if (texture == null) continue;

                // Assign textures directly (SerializedProperty approach was failing)
                if (textureName.Contains("Diffuse") || textureName.Contains("BaseColor"))
                {
                    // Only assign diffuse if it matches the preferred texture number
                    bool isPreferredTexture = string.IsNullOrEmpty(preferredTextureNumber) || textureName.Contains(preferredTextureNumber);

                    if (!diffuseAssigned && isPreferredTexture)
                    {
                        material.SetTexture("_BaseMap", texture);
                        Debug.Log($"    ✓ Assigned {textureName} to _BaseMap");
                        texturesAssigned++;
                        diffuseAssigned = true;
                    }
                    else if (!isPreferredTexture)
                    {
                        Debug.Log($"    Skipped {textureName} (not preferred texture set)");
                    }
                    else
                    {
                        Debug.Log($"    Skipped {textureName} (diffuse already assigned)");
                    }
                }
                else if (textureName.Contains("Normal"))
                {
                    bool isPreferredTexture = string.IsNullOrEmpty(preferredTextureNumber) || textureName.Contains(preferredTextureNumber);
                    if (isPreferredTexture)
                    {
                        EnsureNormalMapSettings(texturePath);
                        material.SetTexture("_BumpMap", texture);
                        material.EnableKeyword("_NORMALMAP");
                        Debug.Log($"    ✓ Assigned {textureName} to _BumpMap");
                        texturesAssigned++;
                    }
                    else
                    {
                        Debug.Log($"    Skipped {textureName} (not preferred texture set)");
                    }
                }
                else if (textureName.Contains("Specular"))
                {
                    bool isPreferredTexture = string.IsNullOrEmpty(preferredTextureNumber) || textureName.Contains(preferredTextureNumber);
                    if (isPreferredTexture)
                    {
                        material.SetTexture("_SpecGlossMap", texture);
                        Debug.Log($"    ✓ Assigned {textureName} to _SpecGlossMap");
                        texturesAssigned++;
                    }
                    else
                    {
                        Debug.Log($"    Skipped {textureName} (not preferred texture set)");
                    }
                }
                else if (textureName.Contains("Glossiness"))
                {
                    // For Specular workflow, Glossiness goes in the alpha of SpecGlossMap
                    Debug.Log($"    Found Glossiness texture: {textureName} (embedded in Specular)");
                }
            }

            if (texturesAssigned == 0)
            {
                Debug.LogWarning($"  ⚠️ No textures assigned to {material.name}");
            }
            else
            {
                Debug.Log($"  ✓ Total: {texturesAssigned} textures assigned");
            }

            EditorUtility.SetDirty(material);

            // Log AFTER state to verify changes were saved
            Texture afterBaseMap = material.GetTexture("_BaseMap");
            float afterWorkflow = material.HasProperty("_WorkflowMode") ? material.GetFloat("_WorkflowMode") : -1f;
            Color afterColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.black;

            Debug.Log($"  AFTER - BaseMap: {(afterBaseMap != null ? afterBaseMap.name : "NULL")}");
            Debug.Log($"  AFTER - WorkflowMode: {afterWorkflow} (0=Metallic, 1=Specular)");
            Debug.Log($"  AFTER - BaseColor: {afterColor}");

            if (afterBaseMap == null)
            {
                Debug.LogError($"  ❌ TEXTURE ASSIGNMENT FAILED! BaseMap is still NULL after assignment!");
            }
            if (afterWorkflow == 0f)
            {
                Debug.LogError($"  ❌ WORKFLOW CHANGE FAILED! Still in Metallic mode!");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Materials saved and refreshed");

        // Convert materials to URP using Unity's built-in converter
        ConvertMaterialsToURP();

        // Force reload materials on character instance in scene
        ReloadCharacterMaterials();

        // Create and apply special eyelash material
        CreateEyelashMaterial();
    }
    private static void ConvertMaterialsToURP()
    {
        Debug.Log("Converting materials to URP...");

        // Find all materials in Characters folder
        string[] materialGuids = GetAssetGuidsCached("t:Material", new[] { ThirdPersonSetupConfig.CharactersRootPath });

        foreach (string guid in materialGuids)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null) continue;

            // Check if material is using a built-in shader
            string shaderName = material.shader.name;
            if (shaderName.Contains("Standard") || shaderName.Contains("Legacy") || shaderName.Contains("Diffuse"))
            {
                Debug.Log($"  Converting {material.name} from {shaderName} to URP/Lit");

                // Save current textures before conversion
                Texture baseMap = material.GetTexture("_MainTex");
                Texture normalMap = material.GetTexture("_BumpMap");
                Texture specMap = material.GetTexture("_SpecGlossMap");
                Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

                // Switch to URP Lit shader
                material.shader = Shader.Find("Universal Render Pipeline/Lit");

                // Restore textures with URP property names
                if (baseMap != null)
                {
                    material.SetTexture("_BaseMap", baseMap);
                    material.SetTexture("_MainTex", baseMap);
                }
                if (normalMap != null)
                {
                    material.SetTexture("_BumpMap", normalMap);
                    material.EnableKeyword("_NORMALMAP");
                }
                if (specMap != null)
                {
                    material.SetFloat("_WorkflowMode", 1f); // Specular workflow
                    material.SetTexture("_SpecGlossMap", specMap);
                }

                material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("URP conversion complete");
    }
    private static void ReloadCharacterMaterials()
    {
        // Find the Player and its CharacterModel child
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.Log("No Player found to reload materials");
            return;
        }

        Transform characterModel = player.transform.Find("CharacterModel");
        if (characterModel == null)
        {
            Debug.Log("No CharacterModel found to reload materials");
            return;
        }

        Debug.Log("Reloading materials on character renderers...");

        // Get all renderers in the character
        SkinnedMeshRenderer[] skinnedRenderers = characterModel.GetComponentsInChildren<SkinnedMeshRenderer>();
        MeshRenderer[] meshRenderers = characterModel.GetComponentsInChildren<MeshRenderer>();

        // Reload materials from Characters materials folder
        string[] materialGuids = GetAssetGuidsCached("t:Material", new[] { ThirdPersonSetupConfig.CharactersRootPath });

        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            Material[] newMaterials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material oldMat = renderer.sharedMaterials[i];
                if (oldMat != null)
                {
                    // Find the extracted material with the same name
                    foreach (string guid in materialGuids)
                    {
                        string matPath = AssetDatabase.GUIDToAssetPath(guid);
                        Material extractedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (extractedMat != null && extractedMat.name == oldMat.name)
                        {
                            newMaterials[i] = extractedMat;
                            Debug.Log($"  ✓ Reloaded material: {extractedMat.name}");
                            break;
                        }
                    }
                }
                if (newMaterials[i] == null)
                {
                    newMaterials[i] = oldMat; // Keep old if not found
                }
            }
            renderer.sharedMaterials = newMaterials;
        }

        foreach (MeshRenderer renderer in meshRenderers)
        {
            Material[] newMaterials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material oldMat = renderer.sharedMaterials[i];
                if (oldMat != null)
                {
                    foreach (string guid in materialGuids)
                    {
                        string matPath = AssetDatabase.GUIDToAssetPath(guid);
                        Material extractedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (extractedMat != null && extractedMat.name == oldMat.name)
                        {
                            newMaterials[i] = extractedMat;
                            Debug.Log($"  ✓ Reloaded material: {extractedMat.name}");
                            break;
                        }
                    }
                }
                if (newMaterials[i] == null)
                {
                    newMaterials[i] = oldMat;
                }
            }
            renderer.sharedMaterials = newMaterials;
        }

        Debug.Log("Character materials reloaded from extracted assets");
    }
    private static void CreateEyelashMaterial()
    {
        Debug.Log("=== CREATING EYELASH MATERIAL ===");

        // Find the Player and eyelash mesh
        GameObject player = GameObject.Find("Player");
        if (player == null) return;

        Transform characterModel = player.transform.Find("CharacterModel");
        if (characterModel == null) return;

        // Look for eyelash mesh
        SkinnedMeshRenderer[] renderers = characterModel.GetComponentsInChildren<SkinnedMeshRenderer>();
        SkinnedMeshRenderer eyelashRenderer = null;

        foreach (var renderer in renderers)
        {
            if (renderer.name.ToLower().Contains("eyelash"))
            {
                eyelashRenderer = renderer;
                Debug.Log($"Found eyelash mesh: {renderer.name}");
                break;
            }
        }

        if (eyelashRenderer == null)
        {
            Debug.Log("No eyelash mesh found");
            return;
        }

        // Create eyelash material
        Material eyelashMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        eyelashMaterial.name = "Ch31_eyelashes";

        // Set to Alpha Cutout for transparency
        eyelashMaterial.SetFloat("_Surface", 0); // 0 = Opaque, 1 = Transparent
        eyelashMaterial.SetFloat("_AlphaClip", 1); // Enable alpha clipping
        eyelashMaterial.SetFloat("_Cutoff", 0.5f); // Alpha cutoff threshold

        // Use hair texture as base
        string[] textureGuids = GetAssetGuidsCached($"{ThirdPersonSetupConfig.HairDiffuseSearch} t:Texture2D",
            new[] { ThirdPersonSetupConfig.CharactersRootPath });
        if (textureGuids.Length > 0)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[0]);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null)
            {
                eyelashMaterial.SetTexture("_BaseMap", texture);
                Debug.Log("Assigned hair texture to eyelashes");
            }
        }

        // Make it lighter/more subtle (dark brown instead of black)
        eyelashMaterial.SetColor("_BaseColor", new Color(0.3f, 0.2f, 0.15f, 1f));

        // Two-sided rendering
        eyelashMaterial.SetFloat("_Cull", 0); // 0 = Off (both sides), 2 = Back (front only)

        // Specular workflow
        eyelashMaterial.SetFloat("_WorkflowMode", 1f);
        eyelashMaterial.EnableKeyword("_SPECULAR_SETUP");

        // Less shiny
        eyelashMaterial.SetFloat("_Smoothness", 0.1f);

        // Save material to disk
        string materialsFolder = ThirdPersonSetupConfig.LeonardMaterialsPath;
        if (!AssetDatabase.IsValidFolder(materialsFolder))
        {
            materialsFolder = ThirdPersonSetupConfig.CharactersMaterialsPath;
        }

        string materialPath = $"{materialsFolder}/Ch31_eyelashes.mat";
        AssetDatabase.CreateAsset(eyelashMaterial, materialPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Created eyelash material at {materialPath}");

        // Apply to eyelash renderer
        eyelashRenderer.sharedMaterial = eyelashMaterial;
        Debug.Log("Applied eyelash material to mesh");
    }


    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
        child.transform.SetParent(parent);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }

    private static string[] GetAssetGuidsCached(string filter, string[] searchDirs)
    {
        string key = searchDirs == null ? filter : $"{filter}::{string.Join("|", searchDirs)}";
        if (AssetGuidCache.TryGetValue(key, out string[] cached))
        {
            return cached;
        }

        string[] guids = searchDirs == null
            ? AssetDatabase.FindAssets(filter)
            : AssetDatabase.FindAssets(filter, searchDirs);
        AssetGuidCache[key] = guids;
        return guids;
    }

    private static T LoadAssetCached<T>(string assetPath) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        string key = $"{typeof(T).FullName}::{assetPath}";
        if (AssetObjectCache.TryGetValue(key, out GameObject cached))
        {
            return cached as T;
        }

        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset is GameObject go)
        {
            AssetObjectCache[key] = go;
        }

        return asset;
    }

    private static bool FileExistsCached(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (AssetExistsCache.TryGetValue(path, out bool exists))
        {
            return exists;
        }

        exists = System.IO.File.Exists(path);
        AssetExistsCache[path] = exists;
        return exists;
    }
    private static void EnsureNormalMapSettings(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"  Set {Path.GetFileName(texturePath)} as Normal Map");
        }
    }
    private static void ConfigureCharacterModel(string modelPath)
    {
        if (!modelPath.EndsWith(".dae") && !modelPath.EndsWith(".fbx")) return;

        Debug.Log($"Configuring character model at {modelPath}");

        ModelImporter modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;

        if (modelImporter != null)
        {
            bool needsReimport = false;

            // Set scale to 0.01 for Mixamo DAE files (they're 100x too large)
            if (modelPath.EndsWith(".dae") && modelImporter.globalScale != 0.01f)
            {
                modelImporter.globalScale = 0.01f;
                needsReimport = true;
                Debug.Log("Set character model scale to 0.01");
            }

            // Set to humanoid animation type with "Create From This Model"
            if (modelImporter.animationType != ModelImporterAnimationType.Human)
            {
                modelImporter.animationType = ModelImporterAnimationType.Human;
                needsReimport = true;
                Debug.Log("Set character model to Humanoid animation type");
            }

            // CRITICAL: Create avatar from this model (not copy from other)
            if (modelImporter.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
                Debug.Log("Set avatar to 'Create From This Model'");
            }

            // CRITICAL: Enforce T-Pose to fix "hanging crouch" issue
            HumanDescription humanDesc = modelImporter.humanDescription;
            humanDesc.hasTranslationDoF = false;
            modelImporter.humanDescription = humanDesc;
            Debug.Log("Configured human description to prevent hanging crouch");
            needsReimport = true;

            if (needsReimport)
            {
                modelImporter.SaveAndReimport();
                Debug.Log("Character model configured: scale=0.01, humanoid avatar with T-pose enforcement");
            }
        }
    }
    private static void SetupAvatar(Animator animator, string characterPath)
    {
        Debug.Log("=== SETTING UP AVATAR ===");

        // Configure the character model to generate a humanoid avatar
        ModelImporter modelImporter = AssetImporter.GetAtPath(characterPath) as ModelImporter;

        if (modelImporter != null)
        {
            bool needsReimport = false;

            // Set to humanoid animation type
            if (modelImporter.animationType != ModelImporterAnimationType.Human)
            {
                modelImporter.animationType = ModelImporterAnimationType.Human;
                needsReimport = true;
                Debug.Log("Set model to Humanoid animation type");
            }

            // Save and reimport if needed
            if (needsReimport)
            {
                modelImporter.SaveAndReimport();
                Debug.Log("Reimported model with Humanoid settings");
            }

            // Load the avatar from the model
            Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(characterPath);

            if (avatar != null)
            {
                animator.avatar = avatar;
                Debug.Log($"Assigned avatar: {avatar.name}");
            }
            else
            {
                Debug.LogWarning("Could not load avatar from model");
            }
        }
        else
        {
            Debug.LogWarning("Could not access model importer for avatar setup");
        }
    }
    private static string GetModelPathFromPrefab(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab"))
        {
            return null;
        }

        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
        string fallback = null;

        foreach (string dependency in dependencies)
        {
            if (dependency.EndsWith(".fbx") || dependency.EndsWith(".dae"))
            {
                if (dependency.Contains("/Models/"))
                {
                    return dependency;
                }

                if (fallback == null)
                {
                    fallback = dependency;
                }
            }
        }

        return fallback;
    }
    private static void ExtractMaterialsFromModel(string modelPath)
    {
        // Import the model asset
        ModelImporter modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;

        if (modelImporter == null) return;

        // Get the directory of the model
        string modelDirectory = Path.GetDirectoryName(modelPath);
        string materialsFolder = Path.Combine(modelDirectory, "Materials");

        // Check if materials folder already exists
        if (AssetDatabase.IsValidFolder(materialsFolder))
        {
            Debug.Log("Materials already extracted for " + modelPath);
            return; // Already extracted
        }

        Debug.Log("Extracting materials from " + modelPath);

        // Create materials folder
        if (!AssetDatabase.IsValidFolder(materialsFolder))
        {
            AssetDatabase.CreateFolder(modelDirectory, "Materials");
        }

        // Extract materials
        try
        {
            // Load all sub-assets (materials)
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            int materialCount = 0;

            foreach (Object asset in assets)
            {
                if (asset is Material)
                {
                    string materialPath = Path.Combine(materialsFolder, asset.name + ".mat");
                    materialPath = materialPath.Replace("\\", "/");

                    // Extract the material
                    string error = AssetDatabase.ExtractAsset(asset, materialPath);
                    if (string.IsNullOrEmpty(error))
                    {
                        AssetDatabase.WriteImportSettingsIfDirty(modelPath);
                        AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate);
                        materialCount++;
                        Debug.Log($"Extracted material: {asset.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to extract material {asset.name}: {error}");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Extracted {materialCount} materials from {modelPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not extract materials from {modelPath}: {e.Message}");
        }
    }
}
