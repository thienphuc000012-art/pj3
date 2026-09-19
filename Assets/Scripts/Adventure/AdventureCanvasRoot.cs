using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public enum AdventureCanvasKind { Adventure, BattleResult, Loading }

// The prefab owns layout and appearance; controllers only bind content and actions.
public class AdventureCanvasRoot : MonoBehaviour
{
    public AdventureCanvasKind kind;
    [Tooltip("A font file included in builds for dynamic Vietnamese glyphs.")]
    public Font languageFont;
    public TMP_FontAsset defaultFont;
    public Color defeatAccent = new Color(.64f, .22f, .19f);
    readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
    readonly Dictionary<string, List<GameObject>> rows = new Dictionary<string, List<GameObject>>();
    TMP_FontAsset runtimeFont;
    void Awake()
    {
        foreach (var preview in GetComponentsInChildren<RawImage>(true))
            if (preview.texture == null) preview.enabled = false;
        if (languageFont != null)
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset(languageFont);
            runtimeFont.name = "Adventure Vietnamese UI";
            runtimeFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            runtimeFont.isMultiAtlasTexturesEnabled = true;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                if (text.font == defaultFont || text.font == null) text.font = runtimeFont;
        }
        EnsureEventSystem();
    }
    public static AdventureCanvasRoot Acquire(AdventureCanvasKind kind)
    {
        foreach (var root in FindObjectsByType<AdventureCanvasRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (root.kind == kind && root.gameObject.scene == SceneManager.GetActiveScene())
            { root.gameObject.SetActive(true); EnsureEventSystem(); return root; }
        var prefab = Resources.Load<AdventureCanvasRoot>("Adventure/UI/" + kind + "Canvas");
        if (prefab == null) throw new InvalidOperationException("Missing Canvas prefab: " + kind);
        var instance = Instantiate(prefab); instance.gameObject.SetActive(true); return instance;
    }
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("Adventure UI EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
    public Transform Node(string path)
    {
        if (nodes.TryGetValue(path, out var found) && found != null) return found;
        found = transform.Find(path);
        if (found == null) throw new InvalidOperationException(name + ": missing UI node " + path);
        nodes[path] = found; return found;
    }
    public T Component<T>(string path) where T : Component => Node(path).GetComponent<T>();
    public void Active(string path, bool active)
    {
        var go = Node(path).gameObject; if (go.activeSelf != active) go.SetActive(active);
    }
    public void Text(string path, string value) { var text = Component<TMP_Text>(path); if (text.text != value) text.text = value; }
    public void Fill(string path, float value) { Component<Image>(path).fillAmount = Mathf.Clamp01(value); }
    public void Click(string path, UnityAction action)
    {
        // Add runtime listeners without removing designer-authored persistent events.
        Component<Button>(path).onClick.AddListener(action);
    }
    public void Enabled(string path, bool enabled) { Component<Button>(path).interactable = enabled; }
    public List<GameObject> Rows(string path, int count, Action<GameObject, int> onCreate = null)
    {
        var content = Node(path);
        var template = content.Find("Template").gameObject;
        if (template.activeSelf) template.SetActive(false);
        if (!rows.TryGetValue(path, out var list)) { list = new List<GameObject>(); rows.Add(path, list); }
        while (list.Count < count)
        {
            int index = list.Count;
            var row = Instantiate(template, content); row.name = "Row " + (index + 1); list.Add(row);
            onCreate?.Invoke(row, index);
        }
        for (int i = 0; i < list.Count; i++) if (list[i].activeSelf != (i < count)) list[i].SetActive(i < count);
        return list;
    }
    public static void RowText(GameObject row, string path, string value)
    {
        var text = row.transform.Find(path).GetComponent<TMP_Text>(); if (text.text != value) text.text = value;
    }
    public static void RowFill(GameObject row, string path, float value) => row.transform.Find(path).GetComponent<Image>().fillAmount = Mathf.Clamp01(value);
    public static void RowPortrait(GameObject row, string path, Sprite sprite)
    {
        var image = row.transform.Find(path).GetComponent<Image>(); image.sprite = sprite; image.enabled = sprite != null; image.preserveAspect = true;
    }
    public static void RowClick(GameObject row, string path, UnityAction action)
    {
        row.transform.Find(path).GetComponent<Button>().onClick.AddListener(action);
        if (path != "Select") return;
        // Selecting a member/item should work over the entire card, including its portrait.
        var graphic = row.GetComponent<Image>();
        if (graphic == null) return;
        graphic.raycastTarget = true;
        var button = row.GetComponent<Button>();
        if (button == null) button = row.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
    }
    public static void RowSelected(GameObject row, bool selected)
    {
        var highlight = row.transform.Find("SelectionHighlight");
        if (highlight == null)
        {
            var go = new GameObject("SelectionHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(SelectionFadeGraphic));
            go.transform.SetParent(row.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var fill = go.GetComponent<SelectionFadeGraphic>(); fill.color = new Color(.25f, .8f, 1f, .24f); fill.raycastTarget = false;
            highlight = rect;
        }
        if (highlight.gameObject.activeSelf != selected) highlight.gameObject.SetActive(selected);
    }
    void OnDestroy()
    {
        if (runtimeFont == null) return;
        foreach (var atlas in runtimeFont.atlasTextures) if (atlas != null) Destroy(atlas);
        if (runtimeFont.material != null) Destroy(runtimeFont.material);
        Destroy(runtimeFont);
    }
}
