using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Applies the shared RPG palette and edge-only interaction highlight.</summary>
public static class GameUITheme
{
    static readonly Color Background = new Color(0.018f, 0.022f, 0.03f, 1f);
    static readonly Color Panel = new Color(0.018f, 0.026f, 0.032f, 0.91f);
    static readonly Color Row = new Color(0.012f, 0.022f, 0.03f, 0.84f);
    static readonly Color Trim = new Color(0.83f, 0.67f, 0.38f, 0.44f);
    static readonly Color Hover = new Color(1f, 0.82f, 0.48f, 0.98f);
    static readonly Color HoverGlow = new Color(1f, 0.73f, 0.34f, 0.42f);
    static GameUIThemeSettings settings;
    static bool missingButtonSpriteReported;

    static Sprite ButtonBackgroundSprite
    {
        get
        {
            if (settings == null) settings = Resources.Load<GameUIThemeSettings>("Adventure/UI/GameUIThemeSettings");
            return settings != null ? settings.buttonBackground : null;
        }
    }

    static Sprite SkillBackgroundSprite
    {
        get
        {
            if (settings == null) settings = Resources.Load<GameUIThemeSettings>("Adventure/UI/GameUIThemeSettings");
            return settings != null ? settings.skillBackground : null;
        }
    }

    public static Sprite DialogueBackgroundSprite
    {
        get
        {
            if (settings == null) settings = Resources.Load<GameUIThemeSettings>("Adventure/UI/GameUIThemeSettings");
            return settings != null ? settings.dialogueBackground : null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallSceneTheme()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.gameObject.scene != scene) continue;
            var parent = canvas.transform.parent != null ? canvas.transform.parent.GetComponentInParent<Canvas>() : null;
            if (parent == null) Apply(canvas.gameObject);
        }
    }

    public static void Apply(GameObject root)
    {
        if (root == null) return;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image == null) continue;
            string n = image.gameObject.name.ToLowerInvariant();
            if (Contains(n, "hp", "health", "fill", "icon", "portrait", "preview", "buff", "debuff", "ultimate", "energy")) continue;
            if (IsInsidePickerRow(image.transform))
            {
                if (image.gameObject.name != "Portrait")
                {
                    image.color = Color.clear;
                    RemoveFrame(image);
                }
                continue;
            }
            if (Contains(n, "background", "backdrop"))
            {
                image.color = WithAlpha(Background, image.color.a);
                continue;
            }
            if (IsAttributeStatRow(image))
            {
                SetSkillBackground(image);
                continue;
            }
            if (IsHorizontalGoldRule(image, n))
            {
                image.color = WithAlpha(Color.clear, 0f);
                continue;
            }
            if (Contains(n, "row", "cell"))
            {
                image.color = WithAlpha(Row, image.color.a);
                AddFrame(image, false);
                continue;
            }
            if (Contains(n, "panel", "card", "dialog", "container", "header", "footer", "statsbackground", "skillcontext", "skilldialog", "slots", "summary"))
            {
                image.color = WithAlpha(Panel, image.color.a);
                AddFrame(image, true);
            }
        }

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null) continue;
            // Keep authored gold, cyan, red, and status colors. Warm neutral white
            // slightly so labels fit the dark fantasy frame without flattening accents.
            Color c = text.color;
            if (Mathf.Abs(c.r - c.g) < 0.035f && Mathf.Abs(c.g - c.b) < 0.035f && c.r > 0.72f)
                text.color = new Color(c.r, c.g * 0.97f, c.b * 0.87f, c.a);
        }

        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            if (button == null) continue;
            if (IsInsidePickerRow(button.transform))
            {
                MakeButtonBackgroundTransparent(button);
                RemoveButtonDecorations(button, true);
                continue;
            }
            if (IsAttributePointButton(button.transform))
            {
                MakeButtonBackgroundTransparent(button);
                RemoveButtonDecorations(button, true);
                continue;
            }

            bool useSkillBackground = UsesSkillBackground(button.transform);
            if (useSkillBackground)
            {
                Image skillPlate = PreparePersistentSkillBackground(button, SkillBackgroundSprite);
                if (skillPlate == null) continue;
                button.transition = Selectable.Transition.None;
                RemoveButtonDecorations(button, false, skillPlate);
                var hover = skillPlate.GetComponent<UIHoverOutline>();
                if (hover == null) hover = skillPlate.gameObject.AddComponent<UIHoverOutline>();
                hover.Configure(new Color(.88f, .9f, .93f, .95f), new Vector2(3f, 3f));

                if (IsMainMenuOrRestButton(button.transform) || IsAttributeConfirmButton(button.transform))
                {
                    RemoveDynamicButtonUI(button);
                    AddHoverScale(button);
                    FitButtonLabelsInsideBackground(button);
                }
                else
                {
                    var dynamic = button.GetComponent<DynamicButtonUI>();
                    if (dynamic == null) dynamic = button.gameObject.AddComponent<DynamicButtonUI>();
                    dynamic.enabled = true;
                    dynamic.RefreshHoverGraphic();
                }
                continue;
            }

            Sprite buttonSprite = useSkillBackground ? SkillBackgroundSprite : ButtonBackgroundSprite;
            if (buttonSprite == null) buttonSprite = ButtonBackgroundSprite;
            Image background = PrepareButtonBackground(button, buttonSprite);
            if (background == null) continue;
            button.transition = Selectable.Transition.None;
            bool emphasizeHover = IsMainMenuOrRestButton(button.transform);
            var edge = background.GetComponent<UIHoverOutline>();
            if (edge == null) edge = background.gameObject.AddComponent<UIHoverOutline>();
            edge.Configure(emphasizeHover ? HoverGlow : Hover,
                emphasizeHover ? new Vector2(4f, 4f) : new Vector2(2.5f, 2.5f));
            var wipe = button.GetComponent<UIButtonWipeReveal>();
            if (wipe == null) wipe = button.gameObject.AddComponent<UIButtonWipeReveal>();
            wipe.Configure(background, buttonSprite, useSkillBackground ? Color.white : new Color(.70f, .58f, .34f, .96f));
            if (emphasizeHover)
                AddHoverScale(button);
        }

        RefreshMenuButtonHover(root);
    }

    static bool IsAttributeStatRow(Image image)
    {
        if (image == null || !image.gameObject.name.StartsWith("Row ", System.StringComparison.Ordinal)) return false;
        var canvas = image.GetComponentInParent<AdventureCanvasRoot>();
        if (canvas == null) return false;
        Transform content = canvas.transform.Find("Menu/Attributes/Stats/Viewport/Content");
        return content != null && image.transform.parent == content;
    }

    static bool IsInsidePickerRow(Transform target)
    {
        if (target == null) return false;
        var canvas = target.GetComponentInParent<AdventureCanvasRoot>();
        if (canvas == null) return false;
        foreach (string path in new[]
        {
            "Menu/Attributes/Picker/Viewport/Content",
            "Menu/Skills/Picker/Viewport/Content",
            "Menu/Party/Member/Picker/Viewport/Content"
        })
        {
            Transform content = canvas.transform.Find(path);
            if (content == null) continue;
            for (Transform current = target; current != null && current != content; current = current.parent)
                if (current.parent == content) return true;
        }
        return false;
    }

    static void RemoveFrame(Image image)
    {
        if (image == null) return;
        var frame = image.GetComponentInChildren<GameUIFrameGraphic>(true);
        if (frame != null) Object.Destroy(frame.gameObject);
    }

    static void SetSkillBackground(Image image)
    {
        Sprite sprite = SkillBackgroundSprite;
        if (image == null || sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        image.enabled = true;
    }

    static bool UsesSkillBackground(Transform button)
    {
        var canvas = button != null ? button.GetComponentInParent<AdventureCanvasRoot>() : null;
        if (canvas == null) return false;
        foreach (string path in new[]
        {
            "Menu/Party/Overview", "Menu/Rest", "Menu/FastTravelList", "Menu/FastTravelConfirmation",
            "Menu/Attributes/Confirm", "Menu/Back"
        })
        {
            Transform scope = canvas.transform.Find(path);
            if (scope != null && (button == scope || button.IsChildOf(scope))) return true;
        }
        return false;
    }

    static bool IsAttributePointButton(Transform button)
    {
        if (button == null || (button.name != "Upgrade" && button.name != "Reduce")) return false;
        var canvas = button.GetComponentInParent<AdventureCanvasRoot>();
        if (canvas == null) return false;
        Transform content = canvas.transform.Find("Menu/Attributes/Stats/Viewport/Content");
        return content != null && button.parent != null && button.parent.parent == content;
    }

    static bool IsAttributeConfirmButton(Transform button)
    {
        if (button == null) return false;
        var canvas = button.GetComponentInParent<AdventureCanvasRoot>();
        Transform confirm = canvas != null ? canvas.transform.Find("Menu/Attributes/Confirm") : null;
        return confirm != null && button == confirm;
    }

    static Image PreparePersistentSkillBackground(Button button, Sprite sprite)
    {
        if (button == null || sprite == null) return null;
        Transform existing = button.transform.Find("Skill Background Plate");
        Image plate = existing != null ? existing.GetComponent<Image>() : null;
        if (plate == null)
        {
            var go = new GameObject("Skill Background Plate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(button.transform, false);
            go.transform.SetAsFirstSibling();
            plate = go.GetComponent<Image>();
        }

        var rect = plate.rectTransform;
        rect.anchorMin = new Vector2(.05f, 0f);
        rect.anchorMax = new Vector2(.95f, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        plate.sprite = sprite;
        plate.type = Image.Type.Simple;
        plate.preserveAspect = false;
        plate.color = Color.white;
        plate.enabled = true;
        plate.gameObject.SetActive(true);
        plate.raycastTarget = true;
        plate.maskable = true;

        var rootImage = button.GetComponent<Image>();
        if (rootImage != null && rootImage != plate)
        {
            if (IsIconGraphic(rootImage)) PreserveRootIcon(button, rootImage);
            rootImage.color = Color.clear;
            rootImage.raycastTarget = false;
        }
        plate.transform.SetAsFirstSibling();
        button.targetGraphic = plate;
        return plate;
    }

    static void MakeButtonBackgroundTransparent(Button button)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image == null)
        {
            image = button.gameObject.AddComponent<Image>();
            button.targetGraphic = image;
        }
        image.color = Color.clear;
        image.raycastTarget = true;
        button.transition = Selectable.Transition.None;
    }

    static void RemoveButtonDecorations(Button button, bool removeOutlines, Image keepOutlineOn = null)
    {
        if (button == null) return;
        var wipe = button.GetComponent<UIButtonWipeReveal>();
        if (wipe != null)
        {
            wipe.enabled = false;
            Object.Destroy(wipe);
        }

        var scale = button.GetComponent<SmoothButtonHoverScale>();
        if (scale != null)
        {
            scale.enabled = false;
            Object.Destroy(scale);
        }

        foreach (var child in button.GetComponentsInChildren<Transform>(true))
        {
            if (child == null) continue;
            if (child.name == "Wipe Reveal" || child.name == "Wipe Reveal Edge")
            {
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }
        }

        foreach (var outline in button.GetComponentsInChildren<UIHoverOutline>(true))
        {
            if (outline == null || (!removeOutlines && keepOutlineOn != null && outline.gameObject == keepOutlineOn.gameObject)) continue;
            Object.Destroy(outline);
        }

        var dynamic = button.GetComponent<DynamicButtonUI>();
        if (removeOutlines && dynamic != null)
        {
            dynamic.enabled = false;
            Object.Destroy(dynamic);
        }
    }

    static void RemoveDynamicButtonUI(Button button)
    {
        if (button == null) return;
        foreach (var dynamic in button.GetComponentsInChildren<DynamicButtonUI>(true))
        {
            if (dynamic == null) continue;
            dynamic.enabled = false;
            var group = dynamic.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            Object.Destroy(dynamic);
        }
        var buttonGroup = button.GetComponent<CanvasGroup>();
        if (buttonGroup != null)
        {
            buttonGroup.alpha = 1f;
            buttonGroup.interactable = true;
            buttonGroup.blocksRaycasts = true;
        }
    }

    static void AddHoverScale(Button button)
    {
        if (button == null) return;
        var scale = button.GetComponent<SmoothButtonHoverScale>();
        if (scale == null) scale = button.gameObject.AddComponent<SmoothButtonHoverScale>();
        scale.enabled = true;
        scale.Configure(1.045f, 0.12f);
    }

    static void FitButtonLabelsInsideBackground(Button button)
    {
        if (button == null) return;
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null || text.transform.parent != button.transform) continue;
            RectTransform rect = text.rectTransform;
            Vector2 min = rect.anchorMin;
            Vector2 max = rect.anchorMax;
            min.x = .08f;
            max.x = .92f;
            rect.anchorMin = min;
            rect.anchorMax = max;
            Vector2 offsetMin = rect.offsetMin;
            Vector2 offsetMax = rect.offsetMax;
            offsetMin.x = 0f;
            offsetMax.x = 0f;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            text.alignment = TextAlignmentOptions.Center;
        }
    }

    public static void RefreshMenuButtonHover(GameObject root)
    {
        if (root == null) return;
        var canvas = root.GetComponent<AdventureCanvasRoot>();
        if (canvas == null) return;

        // Resolve the actual menu scopes directly. This keeps Party/Inventory and
        // every Rest action covered even when buttons are nested or rebuilt later.
        foreach (string path in new[] { "Menu/Party/Overview", "Menu/Rest", "Menu/Attributes/Confirm" })
        {
            Transform scope = canvas.transform.Find(path);
            if (scope == null) continue;
            foreach (var button in scope.GetComponentsInChildren<Button>(true))
            {
                if (button == null) continue;
                RemoveDynamicButtonUI(button);
                AddHoverScale(button);
                FitButtonLabelsInsideBackground(button);
            }
        }
    }

    static bool IsHorizontalGoldRule(Image image, string objectName)
    {
        if (image == null || !Contains(objectName, "goldline", "titleline", "topline")) return false;
        RectTransform rect = image.rectTransform;
        return rect != null && rect.rect.width > rect.rect.height;
    }

    static void AddFrame(Image image, bool ornate)
    {
        if (image == null || image.name.ToLowerInvariant().Contains("background")) return;
        var frame = image.GetComponentInChildren<GameUIFrameGraphic>(true);
        if (frame == null)
        {
            var go = new GameObject("GameUIFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(GameUIFrameGraphic));
            go.transform.SetParent(image.transform, false);
            go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-2f, -2f);
            rect.offsetMax = new Vector2(2f, 2f);
            frame = go.GetComponent<GameUIFrameGraphic>();
        }
        frame.gameObject.SetActive(true);
        frame.enabled = true;
        frame.Configure(Trim, ornate);
    }

    static Image PrepareButtonBackground(Button button, Sprite sprite)
    {
        if (button == null) return null;
        Transform root = button.transform;
        Image background = null;
        var rootImage = button.GetComponent<Image>();
        if (rootImage != null && !IsIconGraphic(rootImage)) background = rootImage;

        if (background == null && button.targetGraphic is Image targetImage && !IsIconGraphic(targetImage))
            background = targetImage;

        if (background == null)
        {
            var images = button.GetComponentsInChildren<Image>(true);
            foreach (var candidate in images)
            {
                if (candidate == null || IsIconGraphic(candidate)) continue;
                string candidateName = candidate.name.ToLowerInvariant();
                if (candidateName.Contains("background") || candidateName.Contains("buttonbg") || candidateName.Contains("plate"))
                { background = candidate; break; }
            }
        }

        if (background == null)
        {
            if (rootImage != null && IsIconGraphic(rootImage))
            {
                PreserveRootIcon(button, rootImage);
                background = rootImage;
            }
        }

        if (background == null)
        {
            if (sprite == null)
            {
                if (!missingButtonSpriteReported)
                {
                    Debug.LogWarning("Game UI theme: missing Resources/Adventure/UI/GameUIThemeSettings or button background sprite.");
                    missingButtonSpriteReported = true;
                }
                return null;
            }
            var go = new GameObject("Button Grunge Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);
            go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            background = go.GetComponent<Image>();
            background.raycastTarget = true;
            if (button.targetGraphic == null) button.targetGraphic = background;
        }

        if (sprite != null)
        {
            background.sprite = sprite;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
        }
        background.color = new Color(1f, 1f, 1f, 0f);
        return background;
    }

    static void PreserveRootIcon(Button button, Image source)
    {
        if (button == null || source == null || source.sprite == null) return;
        Transform existing = button.transform.Find("Button Icon Visual");
        if (existing != null) return;

        var go = new GameObject("Button Icon Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(button.transform, false);
        go.transform.SetAsFirstSibling();
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var icon = go.GetComponent<Image>();
        icon.sprite = source.sprite;
        icon.color = source.color;
        icon.material = source.material;
        icon.type = source.type;
        icon.preserveAspect = source.preserveAspect;
        icon.fillMethod = source.fillMethod;
        icon.fillOrigin = source.fillOrigin;
        icon.fillAmount = source.fillAmount;
        icon.fillClockwise = source.fillClockwise;
        icon.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        icon.useSpriteMesh = source.useSpriteMesh;
        icon.raycastTarget = false;
        icon.maskable = source.maskable;
    }

    static bool IsIconGraphic(Graphic graphic)
    {
        if (graphic == null) return true;
        string n = graphic.name.ToLowerInvariant();
        return Contains(n, "icon", "portrait", "hp", "health", "fill", "buff", "debuff", "energy", "ultimate");
    }

    static bool IsMainMenuOrRestButton(Transform button)
    {
        if (button == null) return false;
        var canvas = button.GetComponentInParent<AdventureCanvasRoot>();
        if (canvas != null)
        {
            foreach (string path in new[] { "Menu/Party/Overview", "Menu/Rest" })
            {
                var scope = canvas.transform.Find(path);
                if (scope != null && (button == scope || button.IsChildOf(scope))) return true;
            }
        }

        for (Transform current = button; current != null; current = current.parent)
        {
            if (current.name == "Rest") return true;
            if (current.name == "Overview" && current.parent != null && current.parent.name == "Party" &&
                current.parent.parent != null && current.parent.parent.name == "Menu") return true;
        }
        return false;
    }

    static bool Contains(string value, params string[] terms)
    {
        foreach (var term in terms) if (value.Contains(term)) return true;
        return false;
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}

[DisallowMultipleComponent]
public sealed class UIHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    Outline outline;
    bool hovered;
    bool selected;

    void Awake() => EnsureOutline();

    public void Configure(Color color) => Configure(color, new Vector2(2f, 2f));

    public void Configure(Color color, Vector2 effectDistance)
    {
        EnsureOutline();
        outline.effectColor = color;
        outline.effectDistance = effectDistance;
        outline.useGraphicAlpha = false;
        outline.enabled = hovered || selected;
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (outline != null) outline.enabled = hovered || selected;
    }

    void EnsureOutline()
    {
        if (outline == null) outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.88f, 0.9f, 0.93f, 0.95f);
        outline.effectDistance = new Vector2(2f, 2f);
        outline.useGraphicAlpha = false;
        outline.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; if (outline != null) outline.enabled = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; if (outline != null) outline.enabled = selected; }
    public void OnSelect(BaseEventData eventData) { selected = true; if (outline != null) outline.enabled = true; }
    public void OnDeselect(BaseEventData eventData) { selected = false; if (outline != null) outline.enabled = hovered; }
}
