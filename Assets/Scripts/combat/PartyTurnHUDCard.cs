using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PartyTurnHUDCard : MonoBehaviour
{
    public PartyHUDStyle style;
    [Header("Edit these RectTransforms directly on the Canvas")]
    public RectTransform visualRoot;
    public Image portrait;
    public Image barFrame;
    public TextMeshProUGUI ultimateText;
    public CanvasGroup visualGroup;
    [SerializeField] Image ultimateFill;
    BattleUnit unit;
    bool acting;
    float blend;
    public bool IsActing => acting;

    public void Bind(BattleUnit value)
    {
        unit = value;
        RefreshPortrait();
        SetTurn(false, true);
    }

    public void SetTurn(bool value, bool immediate = false)
    {
        value &= unit != null && !unit.IsDead;
        if (acting != value) { acting = value; RefreshPortrait(); }
        if (immediate) { blend = acting ? 1f : 0f; ApplyPose(); }
    }

    public void Tick(float deltaTime)
    {
        blend = Mathf.MoveTowards(blend, acting ? 1f : 0f,
            Mathf.Max(0, deltaTime) / Mathf.Max(.01f, style != null ? style.transitionDuration : .18f));
        ApplyPose();
        if (ultimateText != null && unit != null)
            ultimateText.SetText("{0:0}", ultimateFill != null ? ultimateFill.fillAmount * 100f : unit.UltimateEnergy);
        if (ultimateFill != null && style != null)
            ultimateFill.color = unit != null && unit.UltimateReady ? Color.white : style.ultimateColor;
        if (visualGroup != null) visualGroup.alpha = unit != null && unit.IsDead ? .45f : 1f;
    }

    void ApplyPose()
    {
        if (visualRoot == null) return;
        float t = Mathf.SmoothStep(0f, 1f, blend);
        visualRoot.localScale = Vector3.one * Mathf.Lerp(1f, style != null ? style.activeScale : 1.22f, t);
        // Only the visual child moves: slot order and layout never change during a turn.
        visualRoot.anchoredPosition = new Vector2(0, t * (style != null ? style.activeLift : 8f));
    }

    void RefreshPortrait()
    {
        if (portrait == null) return;
        portrait.sprite = style != null ? style.PortraitFor(unit, acting) : unit != null ? unit.unitPortrait : null;
        portrait.enabled = portrait.sprite != null;
    }

    void OnDisable()
    {
        acting = false; blend = 0;
        ApplyPose(); RefreshPortrait();
    }

    public static PartyHUDUnit Create(Transform parent, PartyHUDStyle style, string name)
    {
        RectTransform slot = Rect(name, parent, Vector2.zero, style.slotSize);
        slot.pivot = new Vector2(.5f, 0f);
        var layout = slot.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = style.slotSize.x; layout.preferredHeight = style.slotSize.y;
        var card = slot.gameObject.AddComponent<PartyTurnHUDCard>();
        card.style = style;
        card.visualRoot = Rect("Turn Visual", slot, Vector2.zero, style.slotSize);
        card.visualRoot.anchorMin = card.visualRoot.anchorMax = new Vector2(.5f, 0f);
        card.visualRoot.pivot = new Vector2(.5f, 0f);
        card.visualGroup = card.visualRoot.gameObject.AddComponent<CanvasGroup>();
        card.visualGroup.blocksRaycasts = true;

        card.portrait = Picture("Portrait", card.visualRoot, new Vector2(-43, 135), new Vector2(160, 160), null);
        card.portrait.preserveAspect = true;
        var artRect = Rect("HP Stain Frame", card.visualRoot, new Vector2(5, 78), new Vector2(250, 250 * 1084f / 1448f));
        card.barFrame = artRect.gameObject.AddComponent<Image>();
        card.barFrame.sprite = style.barFrame; card.barFrame.raycastTarget = false;

        var hud = new PartyHUDUnit { gameObject = slot.gameObject, turnCard = card };
        // Coordinates follow the two inner parallelograms in the supplied hp stain artwork.
        hud.hpFill = Rail("HP Fill", artRect, new Vector2(216, 635), new Vector2(1031, 77), false, style.hpColor);
        hud.shieldFill = Rail("Shield Fill", artRect, new Vector2(216, 635), new Vector2(1031, 77), false, new Color(.55f, .8f, 1f, .8f));
        hud.shieldFill.gameObject.SetActive(false);
        hud.hpText = Label("HP Value", card.visualRoot, Vector2.zero, Vector2.one, 32, style.hpColor);
        card.ConfigureResources(hud);
        hud.elementIcon = Picture("Element Icon", card.visualRoot, new Vector2(-98, 54), new Vector2(20, 20), null);
        hud.elementLabel = Label("Element Label", hud.elementIcon.transform, Vector2.zero, new Vector2(20, 20), 12, Color.white);
        hud.elementIconSize = new Vector2(20, 20);
        hud.buffContainer = Rect("BuffContainer", card.visualRoot, new Vector2(3, 20), new Vector2(220, 40));
        var buffs = hud.buffContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        buffs.childAlignment = TextAnchor.MiddleLeft; buffs.spacing = 8;
        buffs.childControlWidth = buffs.childControlHeight = true;
        buffs.childForceExpandHeight = buffs.childForceExpandWidth = false;
        var size = hud.buffContainer.gameObject.AddComponent<PartyHUDBuffLayout>();
        size.iconSize = 36;
        card.SetTurn(false, true);
        return hud;
    }

    // Also used by the Editor's targeted migration; existing portrait/slot transforms stay authored.
    public void ConfigureResources(PartyHUDUnit hud)
    {
        if (visualRoot == null || barFrame == null || hud == null) return;
        var lower = barFrame.transform.Find("Stain Fill") ?? barFrame.transform.Find("Ultimate Energy");
        hud.ultimateFill = lower != null ? lower.GetComponent<Image>() :
            Rail("Ultimate Energy", barFrame.rectTransform, new Vector2(203, 744), new Vector2(1034, 67), true, style.ultimateColor);
        hud.ultimateFill.gameObject.name = "Ultimate Energy";
        hud.ultimateFill.color = style.ultimateColor;
        hud.ultimateFill.fillAmount = 0;
        ultimateFill = hud.ultimateFill;
        Remove("Stain Value"); Remove("Stain Label"); Remove("Name");
        Remove("Ultimate Energy"); // Previous extra, straight rail directly under Turn Visual.
        hud.nameText = null;

        ConfigureLabel(hud.hpText, new Vector2(-23, 94), new Vector2(100, 38), 32, style.hpColor, TextAlignmentOptions.Right);
        var hpLabel = FindLabel("HP Label");
        ConfigureLabel(hpLabel, new Vector2(-70, 116), new Vector2(28, 17), 12, style.hpColor, TextAlignmentOptions.Center);
        hpLabel.text = "HP";
        var divider = FindLabel("Resource Divider");
        ConfigureLabel(divider, new Vector2(35, 94), new Vector2(16, 35), 26, Color.white, TextAlignmentOptions.Center);
        divider.text = "/";
        ultimateText = ultimateText != null ? ultimateText : FindLabel("Ultimate Value");
        ConfigureLabel(ultimateText, new Vector2(80, 96), new Vector2(74, 38), 32, style.ultimateColor, TextAlignmentOptions.Left);
        ultimateText.text = "0";
        var ultimateLabel = FindLabel("Ultimate Label");
        ConfigureLabel(ultimateLabel, new Vector2(81, 120), new Vector2(55, 17), 11, style.ultimateColor, TextAlignmentOptions.Center);
        ultimateLabel.text = "ULT";
        if (hud.buffContainer != null && hud.buffContainer is RectTransform buffRect &&
            (buffRect.anchoredPosition - new Vector2(3, -16)).sqrMagnitude < .01f)
            buffRect.anchoredPosition = new Vector2(3, 20);
    }

    TextMeshProUGUI FindLabel(string name)
    {
        var existing = visualRoot.Find(name);
        return existing != null ? existing.GetComponent<TextMeshProUGUI>() : Label(name, visualRoot, Vector2.zero, Vector2.one, 12, Color.white);
    }

    static void ConfigureLabel(TextMeshProUGUI text, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        if (text == null) return;
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
        text.color = color; text.fontSize = text.fontSizeMax = fontSize;
        text.fontSizeMin = 10; text.enableAutoSizing = true; text.alignment = alignment;
        text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        text.raycastTarget = false;
    }

    void Remove(string name)
    {
        Transform old = visualRoot.Find(name);
        if (old == null) return;
        old.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
    }

    static PartyHUDSlantedFill Rail(string name, RectTransform art, Vector2 lowerLeftPixel, Vector2 pixels, bool stain, Color color)
    {
        Vector2 scale = new Vector2(art.sizeDelta.x / 1448f, art.sizeDelta.y / 1084f);
        Vector2 pos = new Vector2(lowerLeftPixel.x + pixels.x * .5f - 724f, 1086f - lowerLeftPixel.y + pixels.y * .5f - 542f);
        RectTransform rect = Rect(name, art, Vector2.Scale(pos, scale), Vector2.Scale(pixels, scale));
        var fill = rect.gameObject.AddComponent<PartyHUDSlantedFill>();
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
        fill.color = color; fill.raycastTarget = false;
        if (stain)
        {
            fill.topLeft = new Vector2(34f / 1034, 43f / 67);
            fill.bottomRight = new Vector2(999f / 1034, 21f / 67);
        }
        return fill;
    }

    static Image Picture(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI Label(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset; text.fontSize = fontSize;
        text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center; text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        return text;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
}
