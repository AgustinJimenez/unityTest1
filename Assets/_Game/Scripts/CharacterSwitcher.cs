using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime swap of the player's visible Humanoid model, driven from the Character
/// page of <see cref="GameMenu"/>. The original model (built by IKTestSetup, with
/// homebrew foot IK) is kept and just hidden/shown when selected; other characters
/// are instantiated on demand and animate through the shared animator controller.
/// (Foot IK is editor-time configured, so swapped-in characters animate but don't
/// get the homebrew foot IK — a known limitation; the default mannequin keeps it.)
/// Attach to the Player; configure via <see cref="Configure"/> from IKTestSetup.
/// </summary>
public class CharacterSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string name = "Character";
        public GameObject prefab;            // null = the original built-in model (index 0)
        public float scale = 1f;
        public Vector3 localOffset;
    }

    [SerializeField] private List<Entry> characters = new List<Entry>();
    [SerializeField] private RuntimeAnimatorController controller;
    [SerializeField] private GameObject originalModel;   // the IKTestSetup mannequin

    private readonly Dictionary<int, GameObject> instances = new Dictionary<int, GameObject>();
    private GameObject active;
    private int activeIndex;
    // Renderers that belong to the original character — cached before any other characters
    // are instantiated so we don't accidentally grab renderers from swapped-in characters.
    private Renderer[] originalRenderers;

    public IReadOnlyList<Entry> Characters => characters;
    public int ActiveIndex => activeIndex;

    // Called by IKTestSetup after the player is built.
    public void Configure(RuntimeAnimatorController ctrl, GameObject original, List<Entry> entries)
    {
        controller    = ctrl;
        originalModel = original;
        characters    = entries;
    }

    private void Awake()
    {
        if (originalModel == null)
        {
            var a = GetComponentInChildren<Animator>();
            if (a != null) originalModel = a.gameObject;
        }
        // Cache renderers NOW, before any swapped-in characters are parented here.
        // If originalModel is the player root (Animator on root in Armature_Idle prefab),
        // we can't call SetActive(false) on it — that would kill GameMenu / SimpleCharacter.
        // We hide the root character by toggling these renderers instead.
        if (originalModel != null)
            originalRenderers = originalModel.GetComponentsInChildren<Renderer>(true);
        active = originalModel;
        activeIndex = 0;
        instances[0] = originalModel;
    }

    public void SwitchTo(int index)
    {
        if (index < 0 || index >= characters.Count) return;
        if (index == activeIndex && active != null && IsModelVisible(active)) return;

        if (active != null) SetModelVisible(active, false);

        GameObject model;
        if (instances.TryGetValue(index, out var cached) && cached != null)
        {
            model = cached;
            SetModelVisible(model, true);
        }
        else
        {
            var e = characters[index];
            if (e.prefab == null) { model = originalModel; }   // safety: index 0 w/o prefab
            else
            {
                model = Instantiate(e.prefab, transform);
                model.name = $"Character_{e.name}";
                model.transform.localPosition = e.localOffset;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale    = Vector3.one * (e.scale <= 0f ? 1f : e.scale);
                SetLayerRecursive(model, gameObject.layer);
                var anim = model.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    if (controller != null) anim.runtimeAnimatorController = controller;
                    anim.applyRootMotion = false;
                }
                // Force bounds update so the renderer isn't frustum-culled on the first frame.
                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.updateWhenOffscreen = true;
            }
            instances[index] = model;
        }

        if (model != null) SetModelVisible(model, true);
        active = model;
        activeIndex = index;

        // Re-point the gameplay scripts at the newly-active Animator.
        var sc = GetComponent<SimpleCharacter>();
        if (sc != null) sc.RefreshAnimator();
        var menu = GetComponent<GameMenu>();
        if (menu != null) menu.RefreshAnimator();
    }

    // Whether a model is currently shown.  The original character may be the root
    // (Animator on root in the Armature_Idle prefab), so use renderer state, not activeSelf.
    private bool IsModelVisible(GameObject model)
    {
        if (model == gameObject)
            return originalRenderers != null && originalRenderers.Length > 0
                   && originalRenderers[0] != null && originalRenderers[0].enabled;
        return model != null && model.activeSelf;
    }

    // Hide/show a model.  For the root character, toggle its cached renderers so we never
    // call SetActive(false) on the root — that would deactivate GameMenu / SimpleCharacter.
    private void SetModelVisible(GameObject model, bool visible)
    {
        if (model == gameObject)
        {
            if (originalRenderers == null) return;
            foreach (var r in originalRenderers)
                if (r != null) r.enabled = visible;
        }
        else
        {
            model.SetActive(visible);
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
