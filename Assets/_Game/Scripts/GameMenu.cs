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

    // Animation preview — a hand-driven playable graph that bypasses the animator
    // controller, so any clip can be looped regardless of its import loop setting.
    // While a preview plays, the state machine (and OnAnimatorIK foot IK) is offline.
    private PlayableGraph         previewGraph;
    private AnimationClipPlayable previewPlayable;
    private AnimationClip         previewClip;

    private bool            animationsPage;
    private Vector2         animScroll;
    private AnimationClip[] clips;

    private void Awake()
    {
        if (ikDebugMenu == null)       ikDebugMenu       = GetComponentInChildren<IKDebugMenu>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
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

    private void SetOpen(bool open)
    {
        IsOpen           = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = open;
        if (open) animationsPage = false; // always land on the main page
    }

    // ── Animation preview ────────────────────────────────────────────────────

    private AnimationClip[] CollectClips()
    {
        if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null)
            return new AnimationClip[0];

        // animationClips can contain duplicates (clip reused by several states)
        var seen = new HashSet<AnimationClip>();
        var list = new List<AnimationClip>();
        foreach (var c in characterAnimator.runtimeAnimatorController.animationClips)
            if (c != null && seen.Add(c)) list.Add(c);
        return list.ToArray();
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
        if (animationsPage) DrawAnimationsPage();
        else                DrawMainPage();
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
        const float height = 224f;
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

        if (GUI.Button(new Rect(x + 24, rowY + 8, width - 48, 26), "Close  (Esc)"))
            SetOpen(false);
    }

    private void DrawAnimationsPage()
    {
        if (clips == null) clips = CollectClips();

        const float width   = 340f;
        const float pad     = 12f;
        const int   lh      = 26;
        const float headerH = 34f;
        const float footerH = 72f;

        float listH  = Mathf.Min(clips.Length * lh, Screen.height - 80f - headerH - footerH);
        float height = headerH + listH + footerH;
        float x = (Screen.width  - width)  * 0.5f;
        float y = (Screen.height - height) * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y + 6, width, 24), "ANIMATIONS", TitleStyle());

        if (clips.Length == 0)
        {
            GUI.Label(new Rect(x + pad, y + headerH, width - pad * 2, lh), "No clips found");
        }
        else
        {
            Rect view    = new Rect(x + pad, y + headerH, width - pad * 2, listH);
            Rect content = new Rect(0, 0, width - pad * 2 - 16, clips.Length * lh);
            animScroll = GUI.BeginScrollView(view, animScroll, content);

            for (int i = 0; i < clips.Length; i++)
            {
                bool   active = clips[i] == previewClip;
                string label  = $"{(active ? "▶ " : "    ")}{clips[i].name}   ({clips[i].length:F2}s)";

                var st = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
                if (active)
                {
                    st.normal.textColor = Color.cyan;
                    st.hover.textColor  = Color.cyan;
                }

                if (GUI.Button(new Rect(0, i * lh, content.width, lh - 2), label, st))
                {
                    if (active) StopPreview();          // click the playing clip to stop it
                    else        StartPreview(clips[i]); // otherwise switch the loop to it
                }
            }

            GUI.EndScrollView();
        }

        float rowY = y + headerH + listH + 8f;

        GUI.enabled = previewClip != null;
        if (GUI.Button(new Rect(x + pad, rowY, width - pad * 2, 26), "Stop Preview"))
            StopPreview();
        GUI.enabled = true;

        if (GUI.Button(new Rect(x + pad, rowY + 32, width - pad * 2, 26), "◂ Back"))
            animationsPage = false;
    }
}
