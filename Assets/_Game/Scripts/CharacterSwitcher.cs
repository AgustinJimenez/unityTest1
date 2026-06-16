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
        active = originalModel;
        activeIndex = 0;
        instances[0] = originalModel;
    }

    public void SwitchTo(int index)
    {
        if (index < 0 || index >= characters.Count) return;
        if (index == activeIndex && active != null && active.activeSelf) return;

        if (active != null) active.SetActive(false);

        GameObject model;
        if (instances.TryGetValue(index, out var cached) && cached != null)
        {
            model = cached;
            model.SetActive(true);
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
            }
            instances[index] = model;
        }

        if (model != null) model.SetActive(true);
        active = model;
        activeIndex = index;

        // Re-point the gameplay scripts at the newly-active Animator.
        var sc = GetComponent<SimpleCharacter>();
        if (sc != null) sc.RefreshAnimator();
        var menu = GetComponent<GameMenu>();
        if (menu != null) menu.RefreshAnimator();
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
