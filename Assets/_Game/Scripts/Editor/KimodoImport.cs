using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto-configures Kimodo-generated FBXes (anything under a
/// Resources/KimodoMotions/ folder) as Humanoid rigs and names their clip after
/// the file, so each clip retargets onto any Humanoid avatar and shows up with a
/// distinct name in the in-game animation menu (loaded via Resources at runtime).
/// </summary>
public class KimodoImport : AssetPostprocessor
{
    private static bool IsKimodo(string path)
    {
        var p = path.Replace('\\', '/');
        // GVHMR (video→motion) FBXes get the same Humanoid + rename + loop treatment.
        return p.Contains("/Resources/KimodoMotions/")
            || p.Contains("/Resources/GVHMRMotions/");
    }

    private void OnPreprocessModel()
    {
        if (!IsKimodo(assetPath)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType   = ModelImporterAnimationType.Human;
        mi.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = true;
    }

    // Runs after the FBX's takes are parsed (unlike OnPreprocessModel), so the
    // clip list is populated and the rename actually sticks. Renames the single
    // take to the file name so a library of clips is distinguishable in the menu
    // (otherwise every Kimodo FBX's take is "Scene").
    private void OnPreprocessAnimation()
    {
        if (!IsKimodo(assetPath)) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.clipAnimations;
        if (clips.Length == 0) clips = mi.defaultClipAnimations;
        if (clips.Length > 0)
        {
            clips[0].name     = Path.GetFileNameWithoutExtension(assetPath);
            // Kimodo motions are cyclic locomotion (idle/walk/crawl) meant to loop —
            // without loopTime the clip plays once and freezes in an animator state.
            clips[0].loopTime = true;
            mi.clipAnimations = clips;
        }
    }

    private void OnPostprocessModel(GameObject go)
    {
        if (!IsKimodo(assetPath)) return;
        var anim = go.GetComponent<Animator>();
        var avatar = anim != null ? anim.avatar : null;
        Debug.Log($"[Kimodo] Imported {Path.GetFileNameWithoutExtension(assetPath)} as Humanoid. " +
                  $"avatar valid={(avatar != null && avatar.isValid)} human={(avatar != null && avatar.isHuman)}");
    }
}
