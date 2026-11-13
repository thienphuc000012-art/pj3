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
    [Header("Interaction Label")]
    public Color interactLabelBackgroundColor = new Color(0f, 0f, 0f, .82f);
    public Vector2 interactLabelBackgroundPadding = new Vector2(8f, 3f);
    readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
    readonly Dictionary<string, List<GameObject>> rows = new Dictionary<string, List<GameObject>>();
    TMP_FontAsset runtimeFont;
    int runtimeFontKey;

    sealed class SharedLanguageFont
    {
        public TMP_FontAsset asset;
        public int users;
    }
    static readonly Dictionary<int, SharedLanguageFont> languageFonts = new Dictionary<int, SharedLanguageFont>();

    void Awake()
    {
        // Menus are not visible when a map first appears. Avoid enabling all
        // their TMP/layout/button behaviours just to hide them in UI.Update.
        // Find/GetComponentsInChildren(true) below still binds authored objects.
        if (kind == AdventureCanvasKind.Adventure)
        {
            var menu = transform.Find("Menu");
            if (menu != null) menu.gameObject.SetActive(false);
        }
        // Every full-screen UI reads the same background authored in AdventureCanvas.
        var common = Resources.Load<GameObject>("Adventure/UI/AdventureCanvas");
        var source = common != null ? common.transform.Find("Menu/Background")?.GetComponent<Image>() : null;
        var target = transform.Find(kind == AdventureCanvasKind.Adventure ? "Menu/Background" : "Background")?.GetComponent<Image>();
        if (source != null && target != null) ApplyBackground(source, target);
        foreach (var path in new[] { "Menu/Party/Overview/Backdrop", "Menu/Party/Member/Backdrop", "Menu/Inventory/Backdrop", "Menu/Rest/Backdrop", "Menu/Attributes/Backdrop", "Menu/Skills/Backdrop", "Menu/Craft/Backdrop" })
        {
            var backdrop = transform.Find(path)?.GetComponent<Image>();
            if (source != null && backdrop != null) ApplyBackground(source, backdrop);
        }
        var dialog = transform.Find("Menu/Party/Member/SkillDialog")?.GetComponent<Image>();
        if (source != null && dialog != null) { dialog.color = source.color; dialog.sprite = source.sprite; dialog.type = source.type; }
        foreach (var preview in GetComponentsInChildren<RawImage>(true))
            if (preview.texture == null) preview.enabled = false;
        if (languageFont != null)
        {
            runtimeFont = AcquireLanguageFont(languageFont, out runtimeFontKey);
            if (runtimeFont != null)
                foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                    if (text.font == defaultFont || text.font == null) text.font = runtimeFont;
        }
        GameLocalization.BindHierarchy(gameObject);
        GameUITheme.Apply(gameObject);
        EnsureInteractLabelBackground();
        EnsureEventSystem(gameObject.scene);
        if (kind == AdventureCanvasKind.Adventure && GetComponent<AdventureCompassHUD>() == null)
            gameObject.AddComponent<AdventureCompassHUD>();
    }

    public void EnsureInteractLabelBackground()
    {
        if (kind != AdventureCanvasKind.Adventure) return;
        Transform prompt = transform.Find("HUD/Interact");
        var label = prompt != null ? prompt.Find("Label") as RectTransform : null;
        if (label == null) return;
        Transform existing = prompt.Find("Label Background");
        var background = existing != null ? existing.GetComponent<Image>() : null;
        if (background == null)
        {
            var go = existing != null ? existing.gameObject : new GameObject("Label Background", typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = prompt.gameObject.layer;
            go.transform.SetParent(prompt, false);
            background = go.AddComponent<Image>();
        }
        RectTransform rect = background.rectTransform;
        rect.anchorMin = label.anchorMin; rect.anchorMax = label.anchorMax; rect.pivot = label.pivot;
        rect.offsetMin = label.offsetMin - interactLabelBackgroundPadding;
        rect.offsetMax = label.offsetMax + interactLabelBackgroundPadding;
        rect.localRotation = label.localRotation; rect.localScale = label.localScale;
        int labelIndex = label.GetSiblingIndex();
        rect.SetSiblingIndex(Mathf.Max(0, labelIndex - (rect.GetSiblingIndex() < labelIndex ? 1 : 0)));
        background.sprite = null; background.type = Image.Type.Simple;
        background.color = interactLabelBackgroundColor; background.raycastTarget = false;
        background.enabled = true; background.gameObject.SetActive(true);
    }

    public static bool IsInteractLabelBackground(Transform node)
    {
        if (node == null || node.name != "Label Background" || node.parent == null || node.parent.name != "Interact") return false;
        return node.parent.GetComponentInParent<AdventureCanvasRoot>(true) != null;
    }

    static TMP_FontAsset AcquireLanguageFont(Font source, out int key)
    {
        key = source.GetInstanceID();
        if (languageFonts.TryGetValue(key, out var shared) && shared.asset != null)
        {
            shared.users++;
            return shared.asset;
        }
        var asset = TMP_FontAsset.CreateFontAsset(source);
        if (asset == null) return null;
        asset.name = "Adventure Vietnamese UI";
        asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        asset.isMultiAtlasTexturesEnabled = true;
        languageFonts[key] = new SharedLanguageFont { asset = asset, users = 1 };
        return asset;
    }
    static void ApplyBackground(Image source, Image target)
    {
        target.sprite = source.sprite; target.color = source.color; target.type = source.type;
        target.material = source.material; target.enabled = true; target.gameObject.SetActive(true);
    }
    public static AdventureCanvasRoot Acquire(AdventureCanvasKind kind)
    {
        foreach (var root in FindObjectsByType<AdventureCanvasRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (root.kind == kind && root.gameObject.scene == SceneManager.GetActiveScene())
            { root.gameObject.SetActive(true); root.EnsureInteractLabelBackground(); EnsureEventSystem(root.gameObject.scene); return root; }
        var prefab = Resources.Load<AdventureCanvasRoot>("Adventure/UI/" + kind + "Canvas");
        if (prefab == null) throw new InvalidOperationException("Missing Canvas prefab: " + kind);
        var instance = Instantiate(prefab); instance.gameObject.SetActive(true); return instance;
    }
    public static void EnsureEventSystem() => EnsureEventSystem(SceneManager.GetActiveScene());

    public static void EnsureEventSystem(Scene owner)
    {
        // A disabled Loading EventSystem must not prevent the destination from
        // getting input. During additive activation the active scene can still
        // be Loading, so explicitly assign ownership before adding components.
        if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled) return;
        foreach (var system in FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (system.isActiveAndEnabled) return;
        var go = new GameObject("Adventure UI EventSystem");
        if (owner.IsValid() && owner.isLoaded) SceneManager.MoveGameObjectToScene(go, owner);
        go.AddComponent<EventSystem>();
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
    public void Text(string path, string value) => GameLocalization.SetText(Component<TMP_Text>(path), value);
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
            GameUITheme.Apply(row);
        }
        for (int i = 0; i < list.Count; i++) if (list[i].activeSelf != (i < count)) list[i].SetActive(i < count);
        return list;
    }
    public static void RowText(GameObject row, string path, string value)
    {
        var text = row.transform.Find(path).GetComponent<TMP_Text>(); GameLocalization.SetText(text, value);
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
        button.transition = Selectable.Transition.None;
        var edge = graphic.GetComponent<UIHoverOutline>();
        if (edge == null) edge = graphic.gameObject.AddComponent<UIHoverOutline>();
        edge.Configure(new Color(1f, .82f, .48f, .98f), new Vector2(2.5f, 2.5f));
        button.onClick.AddListener(action);
    }
    public static void RowSelected(GameObject row, bool selected)
    {
        // Older prefab instances may still contain the former full-row fill overlay.
        var oldFill = row.transform.Find("SelectionHighlight");
        if (oldFill != null && oldFill.gameObject.activeSelf) oldFill.gameObject.SetActive(false);
        var graphic = row.GetComponent<Image>();
        if (graphic == null) return;
        var edge = graphic.GetComponent<UIHoverOutline>();
        if (edge == null) edge = graphic.gameObject.AddComponent<UIHoverOutline>();
        edge.SetSelected(selected);
    }
    void OnDestroy()
    {
        if (runtimeFont == null) return;
        if (languageFonts.TryGetValue(runtimeFontKey, out var shared) && shared.asset == runtimeFont)
        {
            shared.users--;
            if (shared.users > 0) { runtimeFont = null; return; }
            languageFonts.Remove(runtimeFontKey);
        }
        foreach (var atlas in runtimeFont.atlasTextures) if (atlas != null) Destroy(atlas);
        if (runtimeFont.material != null) Destroy(runtimeFont.material);
        Destroy(runtimeFont);
    }
}
