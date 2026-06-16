using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Renders Kimodo clips on the actual character MESH (not just the skeleton) so
/// generated poses can be inspected as a real figure. Samples each clip onto the
/// Humanoid character via AnimationMode (retargets like the in-game preview),
/// then renders side + front to PNG. Output goes outside Assets so it doesn't get
/// imported. Run via Tools ▸ Capture Kimodo Poses.
/// </summary>
public static class KimodoPoseCapture
{
    const string OutDir      = "E:/repo/kimodo/_renders/mesh";
    const string CharPrefab  = "Assets/ThirdParty/HomebrewIK/Demo/Models/Armature_Idle.prefab";
    const string ClipFolder  = "KimodoMotions";     // under Resources
    // Which clips to capture — substring match on the clip name.
    static string Filter => EditorPrefs.GetString("KimodoCapture.Filter", "CrouchIdle");
    static float  Frac   => 0.5f;                   // sample at mid-clip

    [MenuItem("Tools/Capture Kimodo Poses")]
    public static void Capture()
    {
        Directory.CreateDirectory(OutDir);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharPrefab);
        if (prefab == null) { Debug.LogError("[Capture] Character prefab not found: " + CharPrefab); return; }
        var character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        character.transform.position = Vector3.zero;
        character.transform.rotation = Quaternion.identity;

        // Put the whole character on a spare layer and cull everything else, so the
        // scene's level geometry doesn't clutter the shot.
        const int CapLayer = 31;
        foreach (var tr in character.GetComponentsInChildren<Transform>(true))
            tr.gameObject.layer = CapLayer;

        // Neutral URP material so Built-in shaders don't render magenta in URP.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.82f, 0.82f, 0.85f) };
        foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        var camGO = new GameObject("CapCam");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
        cam.fieldOfView     = 35f;
        cam.cullingMask     = 1 << CapLayer;   // character only — hide level geometry

        var lightGO = new GameObject("CapLight");
        var light   = lightGO.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.3f;
        lightGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        var clips = new List<AnimationClip>();
        foreach (var c in Resources.LoadAll<AnimationClip>(ClipFolder))
            if (c != null && c.name.Contains(Filter) && !c.name.StartsWith("__preview__")) clips.Add(c);
        clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (clips.Count == 0) { Debug.LogWarning("[Capture] No clips match '" + Filter + "'."); }

        var rt = new RenderTexture(520, 640, 24);
        cam.targetTexture = rt;

        AnimationMode.StartAnimationMode();
        foreach (var clip in clips)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(character, clip, clip.length * Frac);
            AnimationMode.EndSampling();

            // Bounds of the posed mesh, for camera framing.
            Bounds b = default; bool first = true;
            foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }

            foreach (var view in new[] { (az: 0f, tag: "side"), (az: 90f, tag: "front") })
            {
                RenderView(cam, b, view.az);
                SavePNG(rt, $"{OutDir}/{clip.name}_{view.tag}.png");
            }
            Debug.Log("[Capture] " + clip.name);
        }
        AnimationMode.StopAnimationMode();

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(character);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(lightGO);
        Debug.Log("[Capture] Done -> " + OutDir);
    }

    [MenuItem("Tools/Capture Kimodo Videos")]
    public static void CaptureVideos()
    {
        string vidRoot = OutDir + "/video";
        Directory.CreateDirectory(vidRoot);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharPrefab);
        if (prefab == null) { Debug.LogError("[VideoCapture] Character prefab not found: " + CharPrefab); return; }
        var character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        character.transform.position = Vector3.zero;
        character.transform.rotation = Quaternion.identity;

        const int CapLayer = 31;
        foreach (var tr in character.GetComponentsInChildren<Transform>(true))
            tr.gameObject.layer = CapLayer;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.82f, 0.82f, 0.85f) };
        foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        var camGO = new GameObject("CapCam");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
        cam.fieldOfView = 35f;
        cam.cullingMask = 1 << CapLayer;

        var lightGO = new GameObject("CapLight");
        var light   = lightGO.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.3f;
        lightGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        var clips = new List<AnimationClip>();
        foreach (var c in Resources.LoadAll<AnimationClip>(ClipFolder))
            if (c != null && c.name.Contains(Filter) && !c.name.StartsWith("__preview__")) clips.Add(c);
        clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        var rt = new RenderTexture(480, 600, 24);
        cam.targetTexture = rt;

        AnimationMode.StartAnimationMode();
        foreach (var clip in clips)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(clip.length * clip.frameRate));

            // Fix the camera once from union bounds over the clip, so the character
            // animates within a steady frame (3/4 view).
            Bounds b = default; bool first = true;
            for (int s = 0; s < 6; s++)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(character, clip, clip.length * s / 6f);
                AnimationMode.EndSampling();
                foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
            }
            float a = 45f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0.08f, Mathf.Sin(a)).normalized;
            float size = Mathf.Max(b.size.magnitude, 0.5f);
            cam.transform.position = b.center + dir * size * 2.4f;
            cam.transform.rotation = Quaternion.LookRotation(b.center - cam.transform.position, Vector3.up);

            string dir2 = vidRoot + "/" + clip.name;
            if (Directory.Exists(dir2)) Directory.Delete(dir2, true);
            Directory.CreateDirectory(dir2);

            for (int i = 0; i < n; i++)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(character, clip, clip.length * i / n);
                AnimationMode.EndSampling();
                cam.Render();
                SavePNG(rt, $"{dir2}/frame_{i:D4}.png");
            }
            Debug.Log($"[VideoCapture] {clip.name}: {n} frames @ {clip.frameRate}fps");
        }
        AnimationMode.StopAnimationMode();

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(character);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(lightGO);
        Debug.Log("[VideoCapture] Done -> " + vidRoot);
    }

    // Render the project's authored DoubleL crouch clips on our character mesh.
    // First forces them Humanoid (they ship as Legacy) so they retarget like the
    // Kimodo clips, then captures side+front to E:/repo/kimodo/_renders/doublel.
    [MenuItem("Tools/Capture DoubleL Crouch")]
    public static void CaptureDoubleLCrouch()
    {
        string[] paths =
        {
            "Assets/ThirdParty/DoubleL/One Hand Up/Movement/Crouch/Idle/Idle/OneHand_Up_Crouch_Idle_1.fbx",
            "Assets/ThirdParty/DoubleL/One Hand Up/Movement/Crouch/Base/OneHand_Up_Crouch_F.fbx",
        };
        foreach (var p in paths)
        {
            if (AssetImporter.GetAtPath(p) is ModelImporter mi && mi.animationType != ModelImporterAnimationType.Human)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.SaveAndReimport();
            }
        }

        string outDir = OutDir + "/doublel";
        Directory.CreateDirectory(outDir);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharPrefab);
        var character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        character.transform.position = Vector3.zero;
        const int CapLayer = 31;
        foreach (var tr in character.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = CapLayer;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.82f, 0.82f, 0.85f) };
        foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        var camGO = new GameObject("CapCam");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
        cam.fieldOfView = 35f; cam.cullingMask = 1 << CapLayer;

        var lightGO = new GameObject("CapLight");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.3f;
        lightGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        var clips = new List<AnimationClip>();
        foreach (var p in paths)
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is AnimationClip c && !c.name.Contains("__preview__")) clips.Add(c);

        var rt = new RenderTexture(520, 640, 24); cam.targetTexture = rt;

        AnimationMode.StartAnimationMode();
        foreach (var clip in clips)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(character, clip, clip.length * 0.5f);
            AnimationMode.EndSampling();

            Bounds b = default; bool first = true;
            foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }

            foreach (var view in new[] { (az: 0f, tag: "side"), (az: 90f, tag: "front") })
            {
                RenderView(cam, b, view.az);
                SavePNG(rt, $"{outDir}/{clip.name}_{view.tag}.png");
            }
            Debug.Log("[DoubleL] " + clip.name);
        }
        AnimationMode.StopAnimationMode();

        cam.targetTexture = null; Object.DestroyImmediate(rt);
        Object.DestroyImmediate(character); Object.DestroyImmediate(camGO); Object.DestroyImmediate(lightGO);
        Debug.Log("[DoubleL] Done -> " + outDir);
    }

    static void RenderView(Camera cam, Bounds b, float azDeg)
    {
        Vector3 c = b.center;
        float size = Mathf.Max(b.size.magnitude, 0.5f);
        float a = azDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(a), 0.10f, Mathf.Sin(a)).normalized;
        cam.transform.position = c + dir * size * 2.4f;
        cam.transform.rotation = Quaternion.LookRotation(c - cam.transform.position, Vector3.up);
        cam.Render();
    }

    static void SavePNG(RenderTexture rt, string path)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = prev;
        Object.DestroyImmediate(tex);
    }
}
