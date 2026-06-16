using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

/// <summary>
/// Escape-key menu with debug/config toggles and an animation preview page.
/// While open, the cursor is unlocked and camera look is suspended
/// (FollowCamera checks GameMenu.IsOpen). Attach to the Player.
/// </summary>
public class GameMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [SerializeField] private IKDebugMenu ikDebugMenu;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private CharacterSwitcher characterSwitcher;

    // Animation preview — a hand-driven playable graph that bypasses the animator
    // controller, so any clip can be looped regardless of its import loop setting.
    // While a preview plays, the state machine (and OnAnimatorIK foot IK) is offline.
    private PlayableGraph         previewGraph;
    private AnimationClipPlayable previewPlayable;
    private AnimationClip         previewClip;
    private Vector3               previewAnchorXZ; // root pos captured at preview start

    private bool            animationsPage;
    private bool            charactersPage;
    private Vector2         animScroll;
    private AnimationClip[] clips;

    private void Awake()
    {
        if (ikDebugMenu == null)       ikDebugMenu       = GetComponentInChildren<IKDebugMenu>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
        if (characterSwitcher == null) characterSwitcher = GetComponent<CharacterSwitcher>();
    }

    // Called by CharacterSwitcher after a model swap: drop any preview and re-grab
    // the now-active Animator so the menu drives the new character.
    public void RefreshAnimator()
    {
        StopPreview();
        characterAnimator = GetComponentInChildren<Animator>();
        clips = null; // rebuilt next time the Animations page opens (controller clips may differ)
    }

    private void OnDisable()
    {
        StopPreview();
        SetOpen(false);
    }

    private void Update()
    {
        // Force-loop the preview: wrap time manually so even clips imported as
        // non-looping (jump begin / land) repeat forever.
        if (previewClip != null && previewPlayable.IsValid()
            && previewPlayable.GetTime() >= previewClip.length)
        {
            previewPlayable.SetTime(previewPlayable.GetTime() % previewClip.length);
        }

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) SetOpen(!IsOpen);
    }

    private void LateUpdate()
    {
        // Keep the character horizontally in place while a clip previews, so motions
        // with baked-in travel (walk, sprint, turns, crouch-walk) play centered
        // instead of wandering off. Y is left free so vertical motion (jump) shows.
        // Runs in LateUpdate to override both root motion and the character controller.
        if (previewClip != null)
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(previewAnchorXZ.x, p.y, previewAnchorXZ.z);
        }
    }

    private void SetOpen(bool open)
    {
        IsOpen           = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = open;
        if (open) { animationsPage = false; charactersPage = false; } // always land on the main page
    }

    // ── Animation preview ────────────────────────────────────────────────────

    // Folders under any Resources/ holding imported motions (Humanoid clips).
    private const string KimodoResourcesFolder = "KimodoMotions";
    private const string GvhmrResourcesFolder  = "GVHMRMotions";

    private AnimationClip[] CollectClips()
    {
        var seen = new HashSet<AnimationClip>();
        var list = new List<AnimationClip>();

        // Clips already in the character's controller (Idle, Run, jump, ...).
        if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null)
            foreach (var c in characterAnimator.runtimeAnimatorController.animationClips)
                if (c != null && seen.Add(c)) list.Add(c);

        // Kimodo-generated motions — temporarily hidden from the menu to declutter it
        // while iterating on GVHMR clips. Files are untouched on disk (and crawl clips
        // remain wired into the controller); re-enable this loop to list them again.
        // foreach (var c in Resources.LoadAll<AnimationClip>(KimodoResourcesFolder))
        //     if (c != null && !c.name.StartsWith("__preview__") && seen.Add(c)) list.Add(c);

        // GVHMR (video→motion) clips dropped into Resources/GVHMRMotions/.
        foreach (var c in Resources.LoadAll<AnimationClip>(GvhmrResourcesFolder))
            if (c != null && !c.name.StartsWith("__preview__") && seen.Add(c)) list.Add(c);

        return list.ToArray();
    }

    // Display order of categories in the Animations menu.
    private static readonly string[] CategoryOrder =
        { "Idle", "Walk", "Run", "Strafe", "Turn", "Crouch", "Jump", "GVHMR", "Other" };

    // Derive a category from a clip name (keyword match; order matters because
    // e.g. "CrouchWalk" contains both "crouch" and "walk").
    private static string CategoryOf(string clipName)
    {
        string n = clipName.ToLowerInvariant();
        if (n.Contains("gvhmr")) return "GVHMR";
        if (n.Contains("crouch")) return "Crouch";
        if (n.Contains("jump") || n.Contains("land") || n.Contains("fall")
            || n.Contains("air") || n.Contains("begin")) return "Jump";
        if (n.Contains("strafe")) return "Strafe";
        if (n.Contains("turn"))   return "Turn";
        if (n.Contains("sprint") || n.Contains("run")) return "Run";
        if (n.Contains("walk") || n.Contains("back"))  return "Walk";
        if (n.Contains("idle"))   return "Idle";
        return "Other";
    }

    private static List<KeyValuePair<string, List<AnimationClip>>> GroupByCategory(AnimationClip[] clips)
    {
        var map = new Dictionary<string, List<AnimationClip>>();
        foreach (var c in clips)
        {
            string cat = CategoryOf(c.name);
            if (!map.TryGetValue(cat, out var list)) { list = new List<AnimationClip>(); map[cat] = list; }
            list.Add(c);
        }
        var result = new List<KeyValuePair<string, List<AnimationClip>>>();
        foreach (var cat in CategoryOrder)
            if (map.TryGetValue(cat, out var list))
                result.Add(new KeyValuePair<string, List<AnimationClip>>(cat, list));
        return result;
    }

    private void StartPreview(AnimationClip clip)
    {
        StopPreview();
        if (characterAnimator == null || clip == null) return;

        previewGraph    = PlayableGraph.Create("GameMenu.AnimPreview");
        var output      = AnimationPlayableOutput.Create(previewGraph, "AnimPreview", characterAnimator);
        previewPlayable = AnimationClipPlayable.Create(previewGraph, clip);
        output.SetSourcePlayable(previewPlayable);
        previewGraph.Play();
        previewClip = clip;
        previewAnchorXZ = transform.position; // lock horizontal travel during preview
    }

    private void StopPreview()
    {
        if (previewGraph.IsValid()) previewGraph.Destroy();
        if (previewClip != null && characterAnimator != null)
            characterAnimator.Rebind(); // hand control back to the animator controller
        previewClip = null;
    }

    // ── GUI ──────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!IsOpen) return;
        if (animationsPage)      DrawAnimationsPage();
        else if (charactersPage) DrawCharacterPage();
        else                     DrawMainPage();
    }

    private static GUIStyle TitleStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.yellow }
        };
    }

    private void DrawMainPage()
    {
        const float width  = 280f;
        const float height = 262f;
        float x = (Screen.width  - width)  * 0.5f;
        float y = (Screen.height - height) * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y + 8, width, 24), "MENU", TitleStyle());

        float rowY = y + 42;
        if (ikDebugMenu != null)
        {
            ikDebugMenu.showDiagnostics = GUI.Toggle(
                new Rect(x + 24, rowY, width - 48, 24),
                ikDebugMenu.showDiagnostics, " Diagnostics UI");
            rowY += 28;

            ikDebugMenu.showTuningPanel = GUI.Toggle(
                new Rect(x + 24, rowY, width - 48, 24),
                ikDebugMenu.showTuningPanel, " IK Debug Panel");
            rowY += 28;

            bool slow = GUI.Toggle(
                new Rect(x + 24, rowY, width - 48, 24),
                ikDebugMenu.SlowMotion, " Slow Motion (O)");
            if (slow != ikDebugMenu.SlowMotion) ikDebugMenu.SlowMotion = slow;
            rowY += 28;
        }
        else
        {
            GUI.Label(new Rect(x + 24, rowY, width - 48, 24), "No IKDebugMenu found");
            rowY += 28;
        }

        string animLabel = previewClip != null ? $"Animations ▸  (▶ {previewClip.name})" : "Animations ▸";
        if (GUI.Button(new Rect(x + 24, rowY + 4, width - 48, 26), animLabel))
        {
            clips = CollectClips();
            animationsPage = true;
        }
        rowY += 34;

        if (characterSwitcher != null && characterSwitcher.Characters.Count > 0)
        {
            int ai = characterSwitcher.ActiveIndex;
            string cur = (ai >= 0 && ai < characterSwitcher.Characters.Count)
                ? characterSwitcher.Characters[ai].name : "?";
            if (GUI.Button(new Rect(x + 24, rowY + 4, width - 48, 26), $"Character ▸  ({cur})"))
                charactersPage = true;
            rowY += 34;
        }

        if (GUI.Button(new Rect(x + 24, rowY + 8, width - 48, 26), "Close  (Esc)"))
            SetOpen(false);
    }

    private void DrawCharacterPage()
    {
        const float width = 240f;
        var list = characterSwitcher != null ? characterSwitcher.Characters : null;
        int n = list != null ? list.Count : 0;
        float height = 60f + n * 28f + 36f;
        float x = 8f, y = 8f;

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var title = TitleStyle(); title.fontSize = 12;
        GUI.Label(new Rect(x, y + 4, width, 18), "CHARACTER", title);

        float rowY = y + 30f;
        if (n == 0)
        {
            GUI.Label(new Rect(x + 10, rowY, width - 20, 20), "No characters configured");
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                bool active = i == characterSwitcher.ActiveIndex;
                var st = new GUIStyle(GUI.skin.button)
                    { alignment = TextAnchor.MiddleLeft, fontSize = 12, padding = new RectOffset(14, 2, 2, 2) };
                if (active) { st.normal.textColor = Color.cyan; st.hover.textColor = Color.cyan; }
                string label = $"{(active ? "● " : "   ")}{list[i].name}";
                if (GUI.Button(new Rect(x + 10, rowY, width - 20, 24), label, st) && !active)
                    characterSwitcher.SwitchTo(i);
                rowY += 28f;
            }
        }

        var smallBtn = new GUIStyle(GUI.skin.button) { fontSize = 11 };
        if (GUI.Button(new Rect(x + 10, rowY + 6, width - 20, 20), "◂ Back", smallBtn))
            charactersPage = false;
    }

    private void DrawAnimationsPage()
    {
        if (clips == null) clips = CollectClips();

        const float width   = 220f;
        const float pad     = 8f;
        const int   lh      = 19;
        const float headerH = 24f;
        const float footerH = 52f;

        // Group clips by category (Idle / Walk / Run / ...) for a tidy list.
        var groups = GroupByCategory(clips);
        int totalRows = 0;
        foreach (var g in groups) totalRows += 1 + g.Value.Count;  // header + clips

        float listH  = Mathf.Min(totalRows * lh, Screen.height - 40f - headerH - footerH);
        float height = headerH + listH + footerH;
        float x = 8f;   // top-left, so the character stays visible while switching
        float y = 8f;

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var animTitle = TitleStyle(); animTitle.fontSize = 12;
        GUI.Label(new Rect(x, y + 4, width, 18), "ANIMATIONS", animTitle);

        if (clips.Length == 0)
        {
            GUI.Label(new Rect(x + pad, y + headerH, width - pad * 2, lh), "No clips found");
        }
        else
        {
            Rect view    = new Rect(x + pad, y + headerH, width - pad * 2, listH);
            Rect content = new Rect(0, 0, width - pad * 2 - 16, totalRows * lh);
            animScroll = GUI.BeginScrollView(view, animScroll, content);

            var hdrStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.3f) } };

            int row = 0;
            foreach (var g in groups)
            {
                GUI.Label(new Rect(2, row * lh, content.width, lh), g.Key.ToUpperInvariant(), hdrStyle);
                row++;
                foreach (var clip in g.Value)
                {
                    bool   active = clip == previewClip;
                    string label  = $"{(active ? "▶ " : "   ")}{clip.name}  ({clip.length:F2}s)";

                    var st = new GUIStyle(GUI.skin.button)
                        { alignment = TextAnchor.MiddleLeft, fontSize = 11, padding = new RectOffset(14, 2, 1, 1) };
                    if (active) { st.normal.textColor = Color.cyan; st.hover.textColor = Color.cyan; }

                    if (GUI.Button(new Rect(0, row * lh, content.width, lh - 2), label, st))
                    {
                        if (active) StopPreview();   // click the playing clip to stop it
                        else        StartPreview(clip);
                    }
                    row++;
                }
            }

            GUI.EndScrollView();
        }

        float rowY = y + headerH + listH + 8f;

        var smallBtn = new GUIStyle(GUI.skin.button) { fontSize = 11 };
        GUI.enabled = previewClip != null;
        if (GUI.Button(new Rect(x + pad, rowY, width - pad * 2, 20), "Stop Preview", smallBtn))
            StopPreview();
        GUI.enabled = true;

        if (GUI.Button(new Rect(x + pad, rowY + 24, width - pad * 2, 20), "◂ Back", smallBtn))
            animationsPage = false;
    }
}
