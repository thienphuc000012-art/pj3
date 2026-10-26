using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class AdvancedUIManager : MonoBehaviour
{
    public static AdvancedUIManager Instance;
    private bool battleUIVisible;

    [Header("Action Menu (Fixed Position)")]
    public RectTransform actionMenuPanel;
    [SerializeField] private Button runAwayButton;
    private RectTransform combatNoticePanel;
    private TextMeshProUGUI combatNoticeText;
    private Coroutine combatNoticeRoutine;
    private Canvas battleCanvas;

    [Header("Sub Menu (Skills/Items List)")]
    public GameObject subMenuPanel;
    public Transform subMenuContent;
    public GameObject actionButtonPrefab;
    private RectTransform fixedSkillContent;
    private ScrollRect subMenuScroll;
    private bool subMenuScrollInitiallyEnabled;
    private bool subMenuScrollCaptured;

    private void SetFixedSkillMode(bool skills)
    {
        if (!subMenuScrollCaptured)
        {
            subMenuScroll = subMenuPanel.GetComponent<ScrollRect>();
            subMenuScrollInitiallyEnabled = subMenuScroll != null && subMenuScroll.enabled;
            subMenuScrollCaptured = true;
        }
        if (subMenuScroll != null)
        {
            subMenuScroll.StopMovement();
            subMenuScroll.enabled = skills ? false : subMenuScrollInitiallyEnabled;
        }
        subMenuContent.gameObject.SetActive(!skills);
        if (skills && fixedSkillContent == null)
        {
            var go = new GameObject("Fixed Skill Slots", typeof(RectTransform));
            go.layer = subMenuContent.gameObject.layer;
            fixedSkillContent = go.GetComponent<RectTransform>();
            fixedSkillContent.SetParent(subMenuContent.parent, false);
            fixedSkillContent.anchorMin = Vector2.zero;
            fixedSkillContent.anchorMax = Vector2.one;
            fixedSkillContent.offsetMin = fixedSkillContent.offsetMax = Vector2.zero;
        }
        if (fixedSkillContent != null) fixedSkillContent.gameObject.SetActive(skills);
    }

    public void PopulateSkillSubMenu(List<ActionData> actions)
    {
        if (subMenuPanel == null || subMenuContent == null || actionButtonPrefab == null) return;
        subMenuPanel.SetActive(true);
        SetFixedSkillMode(true);
        foreach (Transform child in fixedSkillContent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        var slots = new ActionData[5];
        int normalIndex = 0;
        if (actions != null)
            foreach (var action in actions)
            {
                if (action == null) continue;
                if (action.isUltimate)
                {
                    if (slots[4] == null) slots[4] = action;
                }
                else if (normalIndex < 4) slots[normalIndex++] = action;
            }
        for (int i = 0; i < slots.Length; i++)
        {
            var go = Instantiate(actionButtonPrefab, fixedSkillContent);
            go.name = i == 4 ? "Ultimate Slot" : "Skill Slot " + (i + 1);
            var rect = go.GetComponent<RectTransform>();
            var fitter = go.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            rect.anchorMin = new Vector2(0f, 1f - (i + 1) / 5f);
            rect.anchorMax = new Vector2(1f, 1f - i / 5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(6f, 4f);
            rect.offsetMax = new Vector2(-6f, -4f);
            rect.localScale = Vector3.one;
            var button = go.GetComponent<ActionButtonUI>();
            if (button == null) continue;
            if (slots[i] != null) button.Setup(slots[i]);
            else button.SetupEmpty(i == 4 ? "Ultimate" : "Skill " + (i + 1));
            button.FitFixedSlot();
        }
    }

    [Header("Hints")]
    public GameObject changeTargetHintText;

    [Header("Stains UI")]
    public Image[] stainIcons;

    private int currentStainsForUI = 0;
    private int currentPreviewChange = 0;

    [Header("Turn Order UI (Left Bar)")]
    public Transform turnOrderContainer;
    public GameObject portraitPrefab;
    private List<GameObject> activePortraits = new List<GameObject>();

    [Header("Boss / Enemy HP Bar (Top Screen)")]
    public GameObject enemyHudPanel;
    public Image enemyHpFill;
    [Tooltip("Image viền nằm trên HP Fill. Để trống sẽ tự tạo khi chạy.")]
    public Image enemyHpBorder;
    [Tooltip("BuffContainer Enemy độc lập với phần name. Để trống sẽ tự tạo làm object cùng cấp với name.")]
    public Transform enemyBuffContainer;
    [Tooltip("RectTransform làm cha cho BuffContainer Enemy. Để trống sẽ dùng cùng vùng cha với HP/name.")]
    public RectTransform enemyBuffContainerParent;
    [Tooltip("Vị trí neo của BuffContainer Enemy trong RectTransform cha.")]
    public Vector2 enemyBuffContainerAnchor = new Vector2(0.5f, 0f);
    public Vector2 enemyBuffContainerPivot = new Vector2(0.5f, 1f);
    [Tooltip("Vị trí tương đối theo anchor; có thể chỉnh trực tiếp trong Inspector.")]
    public Vector2 enemyBuffContainerPosition = new Vector2(0f, -6f);
    public Vector2 enemyBuffContainerSize = new Vector2(220f, 38f);
    private readonly DebuffIconUI enemyDebuffIcon = new DebuffIconUI();
    private readonly List<GameObject> enemyBuffIcons = new List<GameObject>();
    [Header("HP Damage Trail")]
    [Min(0.05f)] public float enemyHpDrainDuration = 0.8f;
    [Min(0.05f)] public float playerHpDrainDuration = 0.8f;
    private Image enemyHpDamageTrail;
    private sealed class DamageTrailState
    {
        public float actual, shown, start, elapsed;
        public bool draining;
        public System.Action<int, int, int> listener;
        public void Receive(int hp, int max, int shield)
        {
            float next = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;
            if (next < actual) draining = false; // Hold every new hit until its turn ends.
            if (next > actual && draining)
            {
                start = Mathf.Max(shown, next);
                elapsed = 0f;
            }
            actual = next;
            shown = Mathf.Max(shown, actual); // Healing never leaves white over restored HP.
        }
    }
    private readonly Dictionary<BattleUnit, DamageTrailState> damageStates = new Dictionary<BattleUnit, DamageTrailState>();
    [Tooltip("Lề trái / dưới của viền HP tự tạo, tính từ panel HP.")]
    public Vector2 enemyHpBorderOffsetMin = new Vector2(-25.45506f, -55.93742f);
    [Tooltip("Lề phải / trên của viền HP tự tạo, tính từ panel HP.")]
    public Vector2 enemyHpBorderOffsetMax = new Vector2(0.000041008f, 44.18607f);
    private BattleUnit boundEnemy;
    private Sprite defaultEnemyBorder;
    private Color defaultEnemyBorderColor;
    private bool defaultEnemyBorderEnabled;
    private bool enemyBorderInitialized;
    public TextMeshProUGUI enemyNameText;
    public TextMeshProUGUI enemyHpText;

    [Header("Party Members HP HUD (Bottom Right)")]
    public List<PartyHUDUnit> partyHUDList;

    [Header("Target HUD")]
    public TextMeshProUGUI targetNameText;
    [Header("Parry UI")]
    [Tooltip("Object mẫu chữ Parry. Hệ thống sẽ clone object này cho từng nhân vật.")]
    public GameObject parryTextObject;
    public Vector3 parryTextOffset = new Vector3(0, -1f, 0);
    public float parryHorizontalOffset = 1.5f;
    [Min(0.1f)] public float parryTextDuration = 1f;
    [Header("Damage Text UI")]
    public GameObject damageTextPrefab;
    public Transform damageTextContainer;

    // --- THÊM MỚI: HỆ THỐNG ICON BUFF ---
    [Header("Buff UI Settings")]
    public GameObject buffIconPrefab; // Prefab Image dùng làm Icon Buff
    public List<BuffSpriteMapping> buffSprites; // Map loại Buff với hình ảnh

    [Header("Status Effect Info Panel")]
    public Vector2 statusTooltipSize = new Vector2(300f, 120f);
    public Vector2 statusTooltipPointerOffset = new Vector2(18f, -18f);
    private Canvas statusTooltipCanvas;
    private RectTransform statusTooltipRect;
    private TextMeshProUGUI statusTooltipTitle;
    private TextMeshProUGUI statusTooltipBody;

    [System.Serializable]
    public struct BuffSpriteMapping
    {
        public ActionData.BuffStat stat;
        public Sprite sprite;
    }

    public Sprite GetBuffSprite(ActionData.BuffStat stat)
    {
        foreach (var mapping in buffSprites)
        {
            if (mapping.stat == stat) return mapping.sprite;
        }
        return null;
    }

    private void CreateStatusTooltip()
    {
        statusTooltipCanvas = GetComponentInParent<Canvas>();
        if (statusTooltipCanvas == null) statusTooltipCanvas = FindAnyObjectByType<Canvas>();
        if (statusTooltipCanvas == null) return;
        statusTooltipCanvas = statusTooltipCanvas.rootCanvas;

        var panel = new GameObject("Status Effect Info Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(statusTooltipCanvas.transform, false);
        statusTooltipRect = panel.GetComponent<RectTransform>();
        statusTooltipRect.anchorMin = statusTooltipRect.anchorMax = new Vector2(0f, 1f);
        statusTooltipRect.pivot = new Vector2(0f, 1f);
        statusTooltipRect.sizeDelta = statusTooltipSize;
        var background = panel.GetComponent<Image>();
        background.color = new Color(0.015f, 0.035f, 0.045f, 0.96f);
        background.raycastTarget = false;
        var group = panel.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        statusTooltipTitle = CreateTooltipText("Title", statusTooltipRect, 17f, FontStyles.Bold);
        RectTransform titleRect = statusTooltipTitle.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(12f, -37f);
        titleRect.offsetMax = new Vector2(-12f, -8f);
        statusTooltipTitle.color = new Color(0.95f, 0.78f, 0.42f);

        statusTooltipBody = CreateTooltipText("Details", statusTooltipRect, 14f, FontStyles.Normal);
        RectTransform bodyRect = statusTooltipBody.rectTransform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.pivot = new Vector2(0f, 1f);
        bodyRect.offsetMin = new Vector2(12f, 10f);
        bodyRect.offsetMax = new Vector2(-12f, -42f);
        statusTooltipBody.color = new Color(0.9f, 0.93f, 0.94f);
        panel.SetActive(false);
    }

    private static TextMeshProUGUI CreateTooltipText(string objectName, RectTransform parent, float fontSize, FontStyles style)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        return text;
    }

    public void BindBuffTooltip(GameObject icon, ActionData.BuffStat stat, int amount, int stacks, int turns)
    {
        string name = GetBuffDisplayName(stat);
        string value = stat == ActionData.BuffStat.Crit || stat == ActionData.BuffStat.CritDamage
            ? $"+{amount}%"
            : $"+{amount}";
        string detail = stat == ActionData.BuffStat.Shield
            ? $"Hấp thụ tối đa {amount} sát thương. Còn {turns} lượt."
            : $"{name}: {value}. Còn {turns} lượt.";
        if (stacks > 1) detail += $" ({stacks} cộng dồn)";
        BindStatusTooltip(icon, name, detail);
    }

    public void BindDebuffTooltip(GameObject icon, BattleUnit.ActiveDebuff activeDebuff)
    {
        if (activeDebuff == null || activeDebuff.settings == null) return;
        var settings = activeDebuff.settings;
        string name;
        string detail;
        switch (settings.type)
        {
            case ActionData.DebuffType.Burn:
                name = "Thiêu đốt";
                detail = $"Trừ {settings.burnDamage} HP cuối lượt và giảm ATK {settings.burnAttackReductionPercent:0.#}%. Còn {activeDebuff.remainingTurns} lượt.";
                break;
            case ActionData.DebuffType.Paralysis:
                name = "Tê liệt";
                detail = $"Có {settings.paralysisSkipChance:0.#}% khả năng mất lượt. Còn {activeDebuff.remainingTurns} lượt.";
                break;
            default:
                name = "Nhiễm độc";
                detail = $"Mất {settings.poisonMaxHpPercent:0.#}% HP tối đa cuối lượt. Còn {activeDebuff.remainingTurns} lượt.";
                break;
        }
        BindStatusTooltip(icon, name, detail);
    }

    private static string GetBuffDisplayName(ActionData.BuffStat stat)
    {
        switch (stat)
        {
            case ActionData.BuffStat.Atk: return "Tăng ATK";
            case ActionData.BuffStat.Def: return "Tăng DEF";
            case ActionData.BuffStat.Crit: return "Tăng tỷ lệ chí mạng";
            case ActionData.BuffStat.Shield: return "Lá chắn";
            case ActionData.BuffStat.CritDamage: return "Tăng sát thương chí mạng";
            default: return "Hiệu ứng tăng cường";
        }
    }

    private void BindStatusTooltip(GameObject icon, string title, string detail)
    {
        if (icon == null) return;
        var graphic = icon.GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = true;
        foreach (var childText in icon.GetComponentsInChildren<TMP_Text>(true))
            childText.raycastTarget = false;

        var hover = icon.GetComponent<StatusTooltipHover>();
        if (hover == null) hover = icon.AddComponent<StatusTooltipHover>();
        hover.Configure(this, title, detail);
    }

    public void ShowStatusTooltip(string title, string detail, Vector2 pointerPosition)
    {
        if (statusTooltipRect == null || statusTooltipCanvas == null) return;
        statusTooltipTitle.text = title;
        statusTooltipBody.text = detail;
        statusTooltipRect.sizeDelta = statusTooltipSize;
        statusTooltipRect.SetAsLastSibling();
        statusTooltipRect.gameObject.SetActive(true);

        MoveStatusTooltip(pointerPosition);
    }

    public void MoveStatusTooltip(Vector2 pointerPosition)
    {
        if (statusTooltipRect == null || statusTooltipCanvas == null || !statusTooltipRect.gameObject.activeSelf) return;
        float scale = Mathf.Max(0.01f, statusTooltipCanvas.scaleFactor);
        Vector2 size = statusTooltipRect.rect.size * scale;
        Vector2 screenPosition = pointerPosition + statusTooltipPointerOffset;
        if (screenPosition.x + size.x > Screen.width - 8f)
            screenPosition.x = pointerPosition.x - size.x - Mathf.Abs(statusTooltipPointerOffset.x);
        if (screenPosition.y - size.y < 8f)
            screenPosition.y = pointerPosition.y + size.y + Mathf.Abs(statusTooltipPointerOffset.y);
        screenPosition.x = Mathf.Clamp(screenPosition.x, 8f, Mathf.Max(8f, Screen.width - size.x - 8f));
        screenPosition.y = Mathf.Clamp(screenPosition.y, size.y + 8f, Screen.height - 8f);

        RectTransform canvasRect = statusTooltipCanvas.transform as RectTransform;
        Camera eventCamera = statusTooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : statusTooltipCanvas.worldCamera;
        if (canvasRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector3 worldPosition))
            statusTooltipRect.position = worldPosition;
    }

    public void HideStatusTooltip()
    {
        if (statusTooltipRect != null) statusTooltipRect.gameObject.SetActive(false);
    }
    // ------------------------------------

    void Awake()
    {
        Instance = this;
        battleCanvas = GetComponentInParent<Canvas>();
        if (battleCanvas == null) battleCanvas = GetComponentInChildren<Canvas>(true);
        if (battleCanvas != null) GameUITheme.Apply(battleCanvas.gameObject);
        BindRunAwayButton();
        CreateStatusTooltip();

        if (enemyHudPanel != null) enemyHudPanel.SetActive(false);
        if (enemyBuffContainer != null) enemyBuffContainer.gameObject.SetActive(false);
        if (turnOrderContainer != null) turnOrderContainer.gameObject.SetActive(false);
        if (changeTargetHintText != null) changeTargetHintText.SetActive(false);

        // parryTextObject giờ chỉ dùng làm object mẫu để clone.
        if (parryTextObject != null) parryTextObject.SetActive(false);

        if (stainIcons != null && stainIcons.Length > 0 && stainIcons[0] != null)
        {
            Transform stainParent = stainIcons[0].transform.parent;
            if (stainParent != null) stainParent.gameObject.SetActive(false);
        }

        foreach (var hud in partyHUDList)
        {
            if (hud == null) continue;
            if (hud.buffContainer != null) hud.buffContainer.gameObject.SetActive(false);
            if (hud.gameObject != null) hud.gameObject.SetActive(false);
        }

        ShowActionMenu(false);
    }

    void Update()
    {
        UpdateEnemyBinding();
        enemyDebuffIcon.Blink(Mathf.PingPong(Time.time * 5f, 0.7f) + 0.3f);
        UpdateDamageTrails();
        // 1. Nhấp nháy Stain
        if (stainIcons != null)
        {
            float blinkSpeed = 1f;
            float blinkAlpha = Mathf.PingPong(Time.time * blinkSpeed, 0.6f) + 0.2f;

            for (int i = 0; i < stainIcons.Length; i++)
            {
                if (stainIcons[i] == null) continue;

                if (i < currentStainsForUI)
                {
                    if (currentPreviewChange < 0 && i >= currentStainsForUI + currentPreviewChange)
                        stainIcons[i].color = new Color(1f, 0.3f, 0.3f, blinkAlpha);
                    else
                        stainIcons[i].color = Color.white;
                }
                else
                {
                    if (currentPreviewChange > 0 && i < currentStainsForUI + currentPreviewChange)
                        stainIcons[i].color = new Color(1f, 1f, 1f, blinkAlpha);
                    else
                        stainIcons[i].color = new Color(1f, 1f, 1f, 0.2f);
                }
            }
        }

        // --- THÊM MỚI: 2. Nhấp nháy Icon Buff sắp hết hạn ---
        float buffBlinkSpeed = 5f; // Tốc độ nhấp nháy buff
        float buffBlinkAlpha = Mathf.PingPong(Time.time * buffBlinkSpeed, 0.7f) + 0.3f; // Alpha dao động từ 0.3 đến 1.0

        foreach (var hud in partyHUDList)
        {
            if (hud != null && hud.gameObject != null && hud.gameObject.activeInHierarchy)
            {
                hud.UpdateBuffBlinking(buffBlinkAlpha);
                hud.AnimateUltimate(Time.unscaledDeltaTime);
            }
        }
    }

    public void PositionActionMenu(Transform target) { }

    private void BindRunAwayButton()
    {
        if (actionMenuPanel == null) return;
        if (runAwayButton == null)
        {
            Button[] buttons = actionMenuPanel.GetComponentsInChildren<Button>(true);
            runAwayButton = buttons.FirstOrDefault(IsRunAwayButton);
            if (runAwayButton == null) runAwayButton = CreateRunAwayButton(buttons);
        }

        if (runAwayButton == null)
        {
            Debug.LogWarning("Action Menu không có Button Run Away và không thể tạo nút thay thế.", this);
            return;
        }

        runAwayButton.onClick.AddListener(() =>
        {
            if (CombatManager.Instance != null) CombatManager.Instance.OnRunAwayClicked();
        });
    }

    private static bool IsRunAwayButton(Button button)
    {
        if (button == null) return false;
        string label = button.GetComponentInChildren<TMP_Text>(true)?.text ?? string.Empty;
        string searchable = (button.gameObject.name + label).ToLowerInvariant();
        string normalized = new string(searchable.Where(char.IsLetterOrDigit).ToArray());
        return normalized.Contains("runaway") || normalized.Contains("flee") || normalized.Contains("bochay");
    }

    private Button CreateRunAwayButton(Button[] existingButtons)
    {
        Button template = existingButtons.FirstOrDefault(button => button != null);
        if (template == null) return null;

        RectTransform templateRect = template.transform as RectTransform;
        RectTransform lowestRow = existingButtons
            .Where(button => button != null && button.transform is RectTransform)
            .Select(button => (RectTransform)button.transform)
            .Where(rect => rect.anchorMin == new Vector2(.5f, .5f) && rect.anchorMax == new Vector2(.5f, .5f))
            .OrderBy(rect => rect.anchoredPosition.y)
            .FirstOrDefault();

        var buttonObject = new GameObject("Run Away", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = actionMenuPanel.gameObject.layer;
        buttonObject.transform.SetParent(actionMenuPanel, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = templateRect != null ? templateRect.sizeDelta : new Vector2(160f, 30f);
        rect.anchoredPosition = lowestRow != null
            ? lowestRow.anchoredPosition + Vector2.down * Mathf.Max(54f, lowestRow.rect.height + 24f)
            : new Vector2(50f, -200f);

        Image background = buttonObject.GetComponent<Image>();
        Image templateImage = template.targetGraphic as Image;
        if (templateImage != null)
        {
            background.sprite = templateImage.sprite;
            background.type = templateImage.type;
            background.color = templateImage.color;
        }
        else background.color = new Color(.035f, .038f, .042f, .94f);
        background.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = buttonObject.layer;
        textObject.transform.SetParent(buttonObject.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);
        var label = textObject.GetComponent<TextMeshProUGUI>();
        TMP_Text templateText = template.GetComponentInChildren<TMP_Text>(true);
        if (templateText != null)
        {
            label.font = templateText.font;
            label.fontSize = templateText.fontSize;
            label.color = templateText.color;
        }
        else
        {
            label.fontSize = 24f;
            label.color = new Color(.88f, .88f, .88f, 1f);
        }
        label.text = "Run Away";
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;

        GameUITheme.Apply(buttonObject);
        return button;
    }

    public void ShowCombatNotice(string message, float duration = 1.5f)
    {
        EnsureCombatNotice();
        if (combatNoticePanel == null || combatNoticeText == null) return;

        if (combatNoticeRoutine != null) StopCoroutine(combatNoticeRoutine);
        combatNoticeText.text = message;
        combatNoticePanel.gameObject.SetActive(true);
        combatNoticeRoutine = StartCoroutine(HideCombatNoticeAfter(duration));
    }

    private void EnsureCombatNotice()
    {
        if (combatNoticePanel != null) return;
        Canvas canvas = battleCanvas != null ? battleCanvas.rootCanvas : null;
        if (canvas == null) canvas = GetComponentInParent<Canvas>() ?? GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        var panelObject = new GameObject("Combat Notice Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.layer = canvas.gameObject.layer;
        panelObject.transform.SetParent(canvas.transform, false);
        combatNoticePanel = panelObject.GetComponent<RectTransform>();
        combatNoticePanel.anchorMin = combatNoticePanel.anchorMax = new Vector2(.5f, .8f);
        combatNoticePanel.pivot = new Vector2(.5f, .5f);
        combatNoticePanel.sizeDelta = new Vector2(640f, 76f);
        combatNoticePanel.anchoredPosition = Vector2.zero;
        var panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(.012f, .014f, .017f, .9f);
        panelImage.raycastTarget = false;

        var textObject = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = panelObject.layer;
        textObject.transform.SetParent(panelObject.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 6f);
        textRect.offsetMax = new Vector2(-18f, -6f);
        combatNoticeText = textObject.GetComponent<TextMeshProUGUI>();
        TMP_Text templateText = actionMenuPanel != null ? actionMenuPanel.GetComponentInChildren<TMP_Text>(true) : null;
        if (templateText != null) combatNoticeText.font = templateText.font;
        combatNoticeText.fontSize = 30f;
        combatNoticeText.color = new Color(.94f, .89f, .77f, 1f);
        combatNoticeText.alignment = TextAlignmentOptions.Center;
        combatNoticeText.enableWordWrapping = false;
        combatNoticeText.raycastTarget = false;
        combatNoticePanel.gameObject.SetActive(false);
        combatNoticePanel.SetAsLastSibling();
    }

    private IEnumerator HideCombatNoticeAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(.1f, duration));
        if (combatNoticePanel != null) combatNoticePanel.gameObject.SetActive(false);
        combatNoticeRoutine = null;
    }

    public void ShowActionMenu(bool isShow)
    {
        if (actionMenuPanel != null)
        {
            actionMenuPanel.gameObject.SetActive(isShow);
        }
    }

    public void ToggleChangeTargetHint(bool isShow)
    {
        if (changeTargetHintText != null) changeTargetHintText.SetActive(isShow);
    }

    public void PopulateSubMenu(List<ActionData> actionList)
    {
        if (subMenuPanel == null) return;
        subMenuPanel.SetActive(true);
        SetFixedSkillMode(false);

        foreach (Transform child in subMenuContent)
        {
            Destroy(child.gameObject);
        }

        foreach (ActionData action in actionList)
        {
            GameObject newBtnObj = Instantiate(actionButtonPrefab, subMenuContent);
            ActionButtonUI btnUI = newBtnObj.GetComponent<ActionButtonUI>();
            if (btnUI != null) btnUI.Setup(action);
        }
    }

    public void UpdateStainsUI(int currentAmount, int previewChange = 0)
    {
        currentStainsForUI = currentAmount;
        currentPreviewChange = previewChange;
    }

    public void UpdateTurnOrderUI(List<BattleUnit> units)
    {
        if (turnOrderContainer == null) return;

        foreach (GameObject obj in activePortraits)
        {
            if (obj != null) Destroy(obj);
        }
        activePortraits.Clear();

        for (int i = 0; i < units.Count; i++)
        {
            BattleUnit unit = units[i];
            if (unit == null || portraitPrefab == null) continue;

            GameObject portrait = Instantiate(portraitPrefab, turnOrderContainer);
            activePortraits.Add(portrait);

            Image imgComponent = portrait.GetComponent<Image>();
            if (imgComponent != null && unit.unitPortrait != null)
            {
                imgComponent.sprite = unit.unitPortrait;
            }

            if (!unit.isPlayer && imgComponent != null) imgComponent.color = Color.red;

            if (i == 0) portrait.transform.localScale = Vector3.one * 1.2f;
            else portrait.transform.localScale = Vector3.one;
        }
    }

    public void RemoveFirstPortrait()
    {
        if (activePortraits.Count > 0)
        {
            GameObject firstPortrait = activePortraits[0];
            activePortraits.RemoveAt(0);
            if (firstPortrait != null) Destroy(firstPortrait);

            // --- CẬP NHẬT: Phóng to chân dung của nhân vật vừa được đẩy lên đầu (người đang đánh) ---
            if (activePortraits.Count > 0 && activePortraits[0] != null)
            {
                activePortraits[0].transform.localScale = Vector3.one * 1.2f;
            }
        }
    }

    public void RegisterEnemyHP(BattleUnit enemy)
    {
        if (enemy == null || enemy.IsDead || enemy.isSummonedUnit) return;
        TrackDamage(enemy);
        if (CombatManager.Instance != null)
            foreach (var unit in CombatManager.Instance.enemyParty)
                if (unit != null) TrackDamage(unit);
        if (boundEnemy != null) boundEnemy.OnStatsChanged -= UpdateEnemyHPUISafe;
        boundEnemy = enemy;
        enemy.OnStatsChanged -= UpdateEnemyHPUISafe;
        enemy.OnStatsChanged += UpdateEnemyHPUISafe;
        ApplyEnemyBorder(enemy);
        UpdateEnemyHPUISafe(enemy.currentHP, enemy.maxHP, enemy.GetTotalShield());

        if (enemyNameText != null) enemyNameText.text = enemy.unitName;

        bool isStartingGame = CombatManager.Instance != null &&
                             (CombatManager.Instance.state == CombatState.Start ||
                              CombatManager.Instance.state == CombatState.BattleStartAnim);

        if (enemyHudPanel != null)
            enemyHudPanel.SetActive(!isStartingGame);
    }

    private void UpdateEnemyBinding()
    {
        var combat = CombatManager.Instance;
        if (combat == null || combat.state == CombatState.Won || combat.state == CombatState.Lost) return;

        // Keep a dead summoner's HP panel bound until its white damage trail
        // reaches the real HP value. Otherwise rebinding to another enemy hides
        // the bar while the lethal HP drain is still being shown.
        if (boundEnemy != null && boundEnemy.IsDead &&
            damageStates.TryGetValue(boundEnemy, out DamageTrailState deadEnemyTrail) &&
            (deadEnemyTrail.draining || deadEnemyTrail.shown > deadEnemyTrail.actual + 0.0001f))
            return;

        BattleUnit enemy = combat.currentTarget;
        if (enemy == null || enemy.isPlayer || enemy.IsDead || enemy.isSummonedUnit)
            enemy = combat.currentActiveUnit != null && !combat.currentActiveUnit.isPlayer &&
                !combat.currentActiveUnit.IsDead && !combat.currentActiveUnit.isSummonedUnit
                ? combat.currentActiveUnit
                : combat.enemyParty.FirstOrDefault(unit => unit != null && !unit.IsDead && !unit.isSummonedUnit);
        if (enemy == null && boundEnemy != null && !boundEnemy.IsDead) enemy = boundEnemy;
        if (enemy != null && !enemy.isPlayer && enemy != boundEnemy) RegisterEnemyHP(enemy);
        else if (enemy == null && enemyHudPanel != null) enemyHudPanel.SetActive(false);
    }

    private void ApplyEnemyBorder(BattleUnit enemy)
    {
        if (!enemyBorderInitialized)
        {
            if (enemyHpBorder == null && enemyHpFill != null)
            {
                var go = new GameObject("Enemy HP Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = go.GetComponent<RectTransform>();
                // The existing fill's parent is the shared HP background.
                rect.SetParent(enemyHpFill.transform.parent, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = enemyHpBorderOffsetMin;
                rect.offsetMax = enemyHpBorderOffsetMax;
                rect.SetAsLastSibling();
                enemyHpBorder = go.GetComponent<Image>();
                enemyHpBorder.raycastTarget = false;
                enemyHpBorder.enabled = false;
            }
            if (enemyHpBorder == null || enemyHpBorder == enemyHpFill) return;
            defaultEnemyBorder = enemyHpBorder.sprite;
            defaultEnemyBorderColor = enemyHpBorder.color;
            defaultEnemyBorderEnabled = enemyHpBorder.enabled;
            enemyBorderInitialized = true;
        }
        bool custom = enemy.enemyHpBorderSprite != null;
        enemyHpBorder.sprite = custom ? enemy.enemyHpBorderSprite : defaultEnemyBorder;
        enemyHpBorder.color = custom ? Color.white : defaultEnemyBorderColor;
        enemyHpBorder.enabled = custom || defaultEnemyBorderEnabled;
    }

    private void OnDestroy()
    {
        if (boundEnemy != null) boundEnemy.OnStatsChanged -= UpdateEnemyHPUISafe;
        foreach (var pair in damageStates)
            if (pair.Key != null) pair.Key.OnStatsChanged -= pair.Value.listener;
        if (statusTooltipRect != null) Destroy(statusTooltipRect.gameObject);
    }

    private void TrackDamage(BattleUnit unit)
    {
        if (damageStates.ContainsKey(unit)) return;
        float hp = unit.maxHP > 0 ? Mathf.Clamp01((float)unit.currentHP / unit.maxHP) : 0f;
        var data = new DamageTrailState { actual = hp, shown = hp };
        data.listener = data.Receive;
        unit.OnStatsChanged += data.listener;
        damageStates.Add(unit, data);
    }

    // Called after outgoing turn VFX have finished, before the next actor starts.
    public void DrainDamageAtTurnEnd()
    {
        foreach (var data in damageStates.Values)
        {
            data.start = data.shown;
            data.elapsed = 0f;
            data.draining = data.shown > data.actual + 0.0001f;
        }

        foreach (var pair in damageStates)
            if (!pair.Value.draining && pair.Key != null)
                pair.Key.PlayPendingDeathAnimation();
    }

    public System.Collections.IEnumerator WaitForDamageDrain(BattleUnit unit)
    {
        if (unit == null) yield break;
        if (!damageStates.TryGetValue(unit, out DamageTrailState data))
        {
            unit.PlayPendingDeathAnimation();
            yield break;
        }

        while (data.draining) yield return null;
        unit.PlayPendingDeathAnimation();
    }

    private void UpdateDamageTrails()
    {
        foreach (var pair in damageStates)
        {
            var data = pair.Value;
            float duration = pair.Key != null && pair.Key.isPlayer ? playerHpDrainDuration : enemyHpDrainDuration;
            if (!data.draining) continue;
            data.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(data.elapsed / Mathf.Max(0.05f, duration));
            data.shown = Mathf.Lerp(data.start, data.actual, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f)
            {
                data.draining = false;
                if (pair.Key != null) pair.Key.PlayPendingDeathAnimation();
            }
        }
        if (boundEnemy != null && damageStates.TryGetValue(boundEnemy, out var enemy))
            RenderDamageTrail(enemyHpFill, ref enemyHpDamageTrail, enemy.shown);
        foreach (var hud in partyHUDList)
        {
            if (hud == null || hud.BoundUnit == null) continue;
            if (damageStates.TryGetValue(hud.BoundUnit, out var player))
                RenderDamageTrail(hud.hpFill, ref hud.damageTrail, player.shown);
        }
    }

    private static void RenderDamageTrail(Image fill, ref Image trail, float shown)
    {
        if (fill == null) return;
        if (trail == null)
        {
            var go = new GameObject("HP Damage Trail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.layer = fill.gameObject.layer;
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            trail = go.GetComponent<Image>();
            trail.rectTransform.SetParent(fill.transform.parent, false);
            trail.raycastTarget = false;
            trail.color = Color.white;
        }
        var source = fill.rectTransform;
        var rect = trail.rectTransform;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = source.sizeDelta;
        rect.anchoredPosition3D = source.anchoredPosition3D;
        rect.localRotation = source.localRotation;
        rect.localScale = source.localScale;
        // White under the red fill: only the lost HP segment remains visible.
        if (rect.GetSiblingIndex() != source.GetSiblingIndex() - 1)
        {
            rect.SetSiblingIndex(source.GetSiblingIndex());
            source.SetSiblingIndex(rect.GetSiblingIndex() + 1);
        }
        trail.sprite = fill.sprite;
        trail.type = fill.type;
        trail.fillMethod = fill.fillMethod;
        trail.fillOrigin = fill.fillOrigin;
        trail.fillClockwise = fill.fillClockwise;
        trail.preserveAspect = fill.preserveAspect;
        trail.enabled = fill.enabled;
        trail.fillAmount = shown;
    }

    void UpdateEnemyHPUISafe(int current, int max, int shield)
    {
        EnsureEnemyBuffContainer();
        enemyDebuffIcon.Refresh(boundEnemy, enemyBuffContainer);
        RefreshEnemyBuffIcons();
        if (enemyHpFill != null) enemyHpFill.fillAmount = (float)current / max;
        if (enemyHpText != null)
            enemyHpText.text = shield > 0 ? $"{current}/{max} <color=yellow>[+{shield}]</color>" : $"{current} / {max}";
    }

    private void EnsureEnemyBuffContainer()
    {
        if (enemyBuffContainer == null)
        {
            if (boundEnemy == null || enemyNameText == null) return;
            var go = new GameObject("BuffContainer Enemy", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bool useNameRelativePlacement = enemyBuffContainerParent == null;
            RectTransform parent = enemyBuffContainerParent != null
                ? enemyBuffContainerParent
                : enemyHpFill != null && enemyHpFill.transform.parent is RectTransform hpParent
                    ? hpParent
                    : enemyNameText.rectTransform.parent as RectTransform;
            if (parent == null) parent = enemyNameText.rectTransform;
            go.layer = parent.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.pivot = enemyBuffContainerPivot;
            rect.sizeDelta = enemyBuffContainerSize;
            if (useNameRelativePlacement)
            {
                RectTransform nameRect = enemyNameText.rectTransform;
                rect.anchorMin = rect.anchorMax = nameRect.anchorMin;
                rect.anchoredPosition = nameRect.anchoredPosition +
                    new Vector2(0f, -nameRect.rect.height * (1f - nameRect.pivot.y)) +
                    enemyBuffContainerPosition;
                rect.SetSiblingIndex(nameRect.GetSiblingIndex() + 1);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = enemyBuffContainerAnchor;
                rect.anchoredPosition = enemyBuffContainerPosition;
            }
            enemyBuffContainer = rect;
        }

        var layout = enemyBuffContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = enemyBuffContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        // This container may be outside the main enemy panel in the Canvas.
        // Keep it synchronized with the battle HUD visibility state.
        enemyBuffContainer.gameObject.SetActive(battleUIVisible);
        // Icons flow left-to-right, starting at the left edge of the container.
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 0f;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childScaleWidth = true;
    }

    private void RefreshEnemyBuffIcons()
    {
        if (enemyBuffIcons.Count > 0) HideStatusTooltip();
        foreach (GameObject icon in enemyBuffIcons)
            if (icon != null) Destroy(icon);
        enemyBuffIcons.Clear();
        if (boundEnemy == null || boundEnemy.IsDead || enemyBuffContainer == null || buffIconPrefab == null) return;

        var groups = new List<EnemyBuffDisplay>();
        foreach (ActiveBuff buff in boundEnemy.activeBuffs)
        {
            if (buff == null || buff.stat == ActionData.BuffStat.None) continue;
            EnemyBuffDisplay group = null;
            foreach (var existing in groups)
                if (existing.stat == buff.stat) { group = existing; break; }
            if (group == null)
            {
                group = new EnemyBuffDisplay { stat = buff.stat };
                groups.Add(group);
            }
            group.amount += buff.amount;
            group.stacks++;
            group.turns = Mathf.Max(group.turns, buff.duration);
        }

        foreach (var group in groups)
        {
            GameObject icon = Instantiate(buffIconPrefab, enemyBuffContainer);
            icon.name = "Enemy Buff " + group.stat;
            Image image = icon.GetComponent<Image>();
            if (image == null) { Destroy(icon); continue; }
            Sprite sprite = GetBuffSprite(group.stat);
            if (sprite != null) image.sprite = sprite;
            image.raycastTarget = true;
            var stackText = icon.GetComponentInChildren<TextMeshProUGUI>(true);
            if (stackText != null) stackText.text = group.stacks > 1 ? "x" + group.stacks : "";
            BindBuffTooltip(icon, group.stat, group.amount, group.stacks, group.turns);
            enemyBuffIcons.Add(icon);
        }
    }

    private sealed class EnemyBuffDisplay
    {
        public ActionData.BuffStat stat;
        public int amount;
        public int stacks;
        public int turns;
    }

    public void RegisterPartyHP(List<BattleUnit> playerList)
    {
        // Layout Groups use sibling order, not the serialized HUD list order.
        // Party slots run right to left, while the HUD layout runs left to right.
        // Keep each card bound to its party slot, but reverse the visual order.
        var orderedParents = new Dictionary<Transform, List<Transform>>();
        foreach (var hud in partyHUDList)
        {
            if (hud == null || hud.gameObject == null) continue;
            Transform card = hud.gameObject.transform;
            if (card.parent == null) continue;
            if (!orderedParents.TryGetValue(card.parent, out var cards))
                orderedParents.Add(card.parent, cards = new List<Transform>());
            if (!cards.Contains(card)) cards.Add(card);
        }
        foreach (var group in orderedParents)
        {
            // Retain unrelated children in their existing slots.
            var slots = new List<int>();
            foreach (var card in group.Value) slots.Add(card.GetSiblingIndex());
            slots.Sort();
            group.Value.Reverse();
            for (int i = 0; i < group.Value.Count; i++) group.Value[i].SetSiblingIndex(slots[i]);
            if (group.Key is RectTransform rect) LayoutRebuilder.MarkLayoutForRebuild(rect);
        }

        for (int i = 0; i < partyHUDList.Count; i++)
        {
            if (partyHUDList[i] == null) continue;

            if (i < playerList.Count && playerList[i] != null)
            {
                TrackDamage(playerList[i]);
                partyHUDList[i].BindUnit(playerList[i]);
            }
            else
            {
                if (partyHUDList[i].gameObject != null)
                    partyHUDList[i].gameObject.SetActive(false);
            }
        }
    }

    public void UpdateTargetUI(string targetName)
    {
        if (targetNameText != null) targetNameText.gameObject.SetActive(false);
    }

    public void ShowParryText(Transform targetTransform)
    {
        if (parryTextObject == null || targetTransform == null || Camera.main == null)
            return;

        // Mỗi target có một instance riêng -> có thể hiện đồng thời cho cả Party.
        Transform parent = parryTextObject.transform.parent != null
            ? parryTextObject.transform.parent
            : transform;

        GameObject parryInstance = Instantiate(parryTextObject, parent);
        parryInstance.name = parryTextObject.name + "_Instance";
        parryInstance.SetActive(true);

        float randomDirection = Random.Range(-0.5f, 0.5f);

        Vector3 dynamicOffset = new Vector3(
            parryHorizontalOffset * randomDirection,
            parryTextOffset.y,
            parryTextOffset.z);

        Vector3 worldPos = targetTransform.position + dynamicOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        parryInstance.transform.position = screenPos;

        Destroy(parryInstance, parryTextDuration);
    }

    public void ShowDamageText(Transform targetTransform, int amount, bool isCrit, bool isHeal = false)
    {
        if (damageTextPrefab == null || targetTransform == null) return;

        float randomX = Random.Range(-0.4f, 0.4f);
        float randomY = Random.Range(0f, 0.5f);
        Vector3 randomOffset = new Vector3(randomX, randomY, 0);

        Vector3 worldPos = targetTransform.position + Vector3.up * 0.8f + randomOffset;

        if (Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            GameObject damageObj = Instantiate(damageTextPrefab, screenPos, Quaternion.identity, damageTextContainer != null ? damageTextContainer : transform);
            TextMeshProUGUI txt = damageObj.GetComponent<TextMeshProUGUI>();

            if (txt != null)
            {
                if (isHeal)
                {
                    txt.text = $"+{amount}";
                    txt.color = Color.green;
                }
                else
                {
                    txt.text = amount.ToString();
                    txt.color = isCrit ? new Color(1f, 0.5f, 0f) : Color.white;
                    if (isCrit) txt.fontSize *= 1.3f;
                }
            }

            Destroy(damageObj, 1f);
        }
    }

    public void ToggleAllUI(bool isShow)
    {
        battleUIVisible = isShow;
        if (!isShow) HideStatusTooltip();
        ShowActionMenu(isShow);
        if (!isShow) ToggleChangeTargetHint(false);

        if (turnOrderContainer != null) turnOrderContainer.gameObject.SetActive(isShow);

        if (enemyHudPanel != null)
        {
            if (isShow && CombatManager.Instance != null && CombatManager.Instance.enemyParty.Count > 0)
                enemyHudPanel.SetActive(true);
            else
                enemyHudPanel.SetActive(false);
        }
        if (enemyBuffContainer != null)
            enemyBuffContainer.gameObject.SetActive(isShow && boundEnemy != null && !boundEnemy.IsDead);

        for (int i = 0; i < partyHUDList.Count; i++)
        {
            PartyHUDUnit hud = partyHUDList[i];
            if (hud == null) continue;
            bool shouldShow = isShow && hud.gameObject != null && CombatManager.Instance != null &&
                              i < CombatManager.Instance.playerParty.Count && CombatManager.Instance.playerParty[i] != null;
            if (hud.buffContainer != null) hud.buffContainer.gameObject.SetActive(shouldShow);
            if (hud.gameObject != null) hud.gameObject.SetActive(shouldShow);
        }

        if (isShow && CombatManager.Instance != null) RegisterPartyHP(CombatManager.Instance.playerParty);

        if (stainIcons != null && stainIcons.Length > 0 && stainIcons[0] != null)
        {
            Transform stainParent = stainIcons[0].transform.parent;
            if (stainParent != null) stainParent.gameObject.SetActive(isShow);
        }
    }
}

public sealed class StatusTooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private AdvancedUIManager manager;
    private string title;
    private string detail;
    private bool isHovered;
    private RectTransform iconRect;
    private Canvas iconCanvas;

    public void Configure(AdvancedUIManager tooltipManager, string tooltipTitle, string tooltipDetail)
    {
        manager = tooltipManager;
        title = tooltipTitle;
        detail = tooltipDetail;
        iconRect = transform as RectTransform;
        iconCanvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (manager == null || iconRect == null || iconCanvas == null || !iconCanvas.isActiveAndEnabled)
            return;

        Camera eventCamera = iconCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : iconCanvas.worldCamera;
        bool pointerInside = RectTransformUtility.RectangleContainsScreenPoint(iconRect, Input.mousePosition, eventCamera);
        if (pointerInside && !isHovered)
        {
            isHovered = true;
            manager.ShowStatusTooltip(title, detail, Input.mousePosition);
        }
        else if (!pointerInside && isHovered)
        {
            isHovered = false;
            manager.HideStatusTooltip();
        }
        else if (pointerInside)
        {
            manager.MoveStatusTooltip(Input.mousePosition);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (manager != null && !isHovered) manager.ShowStatusTooltip(title, detail, eventData.position);
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (manager != null) manager.HideStatusTooltip();
    }

    private void OnDisable()
    {
        if (isHovered && manager != null) manager.HideStatusTooltip();
        isHovered = false;
    }
}

[System.Serializable]
public class PartyHUDUnit
{
    private readonly DebuffIconUI debuffIcon = new DebuffIconUI();
    public GameObject gameObject;
    public Image hpFill;
    public Image shieldFill;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI nameText;
    public Image ultimateFill;

    public void EnsureUltimateBar()
    {
        if (ultimateFill != null || gameObject == null || hpFill == null) return;
        var root = gameObject.GetComponent<RectTransform>();
        if (root == null) return;
        var existing = root.Find("Ultimate Energy/Fill");
        if (existing != null) { ultimateFill = existing.GetComponent<Image>(); return; }
        Canvas.ForceUpdateCanvases();
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, hpFill.rectTransform);
        var bar = new GameObject("Ultimate Energy", typeof(RectTransform), typeof(Image));
        var rect = bar.GetComponent<RectTransform>(); rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(Mathf.Max(40, bounds.size.x), 8);
        rect.localPosition = new Vector3(bounds.center.x, bounds.min.y - 10, 0);
        var background = bar.GetComponent<Image>(); background.color = new Color(.08f, .1f, .14f); background.raycastTarget = false;
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fillRect = fill.GetComponent<RectTransform>(); fillRect.SetParent(rect, false);
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        ultimateFill = fill.GetComponent<Image>(); ultimateFill.sprite = hpFill.sprite;
        ultimateFill.type = Image.Type.Filled; ultimateFill.fillMethod = Image.FillMethod.Horizontal;
        ultimateFill.fillOrigin = 0; ultimateFill.fillAmount = 0; ultimateFill.raycastTarget = false;
        ultimateFill.color = new Color(.95f, .65f, .12f);
        if (nameText != null)
        {
            Vector3 position = root.InverseTransformPoint(nameText.transform.position);
            position.y = bounds.min.y - 22 - nameText.rectTransform.rect.height * (1 - nameText.rectTransform.pivot.y);
            nameText.transform.position = root.TransformPoint(position);
        }
    }

    [Min(.01f)] public float ultimateFillDuration = .35f;
    float ultimateTarget, ultimateStart, ultimateElapsed;
    public void AnimateUltimate(float deltaTime)
    {
        if (ultimateFill == null) return;
        ultimateElapsed += Mathf.Max(0, deltaTime);
        float t = Mathf.Clamp01(ultimateElapsed / Mathf.Max(.01f, ultimateFillDuration));
        ultimateFill.fillAmount = Mathf.Lerp(ultimateStart, ultimateTarget, Mathf.SmoothStep(0, 1, t));
        ultimateFill.color = ultimateFill.fillAmount >= .999f ? new Color(1f, .9f, .35f) : new Color(.95f, .65f, .12f);
    }
    void OnUltimateChanged(float amount)
    {
        if (ultimateFill == null) return;
        ultimateStart = ultimateFill.fillAmount;
        ultimateTarget = amount / 100f;
        ultimateElapsed = 0;
        // Spending an Ultimate clears immediately; gains animate upwards.
        if (ultimateTarget < ultimateStart) ultimateStart = ultimateFill.fillAmount = ultimateTarget;
    }

    // --- THÊM MỚI: Biến chứa Icon Buff ---
    [Header("Buff UI")]
    public Transform buffContainer; // Gắn Object chứa Layout Group để xếp Icon buff
    private List<Image> spawnedBuffIcons = new List<Image>();

    // UI chỉ giữ 1 entry cho mỗi loại BuffStat.
    // Gameplay vẫn giữ từng ActiveBuff riêng để duration và amount hoạt động như cũ.
    private List<GroupedBuffUI> groupedBuffs = new List<GroupedBuffUI>();

    private class GroupedBuffUI
    {
        public ActionData.BuffStat stat;
        public int stackCount;
        public int totalAmount;

        // Icon chỉ nhấp nháy khi stack cuối cùng của loại buff này
        // cũng sắp hết hạn.
        public int maxDuration;
    }

    [System.NonSerialized] public Image damageTrail;
    public BattleUnit BoundUnit => boundUnit;
    private BattleUnit boundUnit;

    public void BindUnit(BattleUnit unit)
    {
        if (boundUnit != null)
        {
            boundUnit.OnStatsChanged -= OnStatsChangedHandler;
            boundUnit.OnUltimateEnergyChanged -= OnUltimateChanged;
        }
        boundUnit = unit;
        EnsureUltimateBar();
        boundUnit.OnUltimateEnergyChanged += OnUltimateChanged;
        OnUltimateChanged(boundUnit.UltimateEnergy);
        AnimateUltimate(ultimateFillDuration);
        if (nameText != null) nameText.text = unit.unitName;

        boundUnit.OnStatsChanged -= OnStatsChangedHandler;
        boundUnit.OnStatsChanged += OnStatsChangedHandler;

        OnStatsChangedHandler(boundUnit.currentHP, boundUnit.maxHP, boundUnit.GetTotalShield());
    }

    void OnStatsChangedHandler(int current, int max, int shield)
    {
        debuffIcon.Refresh(boundUnit, buffContainer);
        if (hpFill != null) hpFill.fillAmount = (float)current / max;

        if (shieldFill != null)
        {
            shieldFill.gameObject.SetActive(shield > 0);
            shieldFill.fillAmount = (float)shield / max;
        }

        if (hpText != null)
        {
            hpText.text = shield > 0 ? $"{current}/{max} <color=white>[+{shield}]</color>" : $"{current} / {max}";
        }

        // --- BUFF UI: gộp các buff cùng BuffStat thành 1 icon ---
        if (boundUnit != null && buffContainer != null)
        {
            if (spawnedBuffIcons.Count > 0)
                AdvancedUIManager.Instance?.HideStatusTooltip();
            groupedBuffs.Clear();

            // =========================================================
            // GỘP BUFF CÙNG LOẠI
            // =========================================================
            foreach (ActiveBuff buff in boundUnit.activeBuffs)
            {
                if (buff == null ||
                    buff.stat == ActionData.BuffStat.None)
                {
                    continue;
                }

                GroupedBuffUI group = null;

                for (int i = 0; i < groupedBuffs.Count; i++)
                {
                    if (groupedBuffs[i].stat == buff.stat)
                    {
                        group = groupedBuffs[i];
                        break;
                    }
                }

                if (group == null)
                {
                    group = new GroupedBuffUI
                    {
                        stat = buff.stat,
                        stackCount = 0,
                        totalAmount = 0,
                        maxDuration = 0
                    };

                    groupedBuffs.Add(group);
                }

                group.stackCount++;
                group.totalAmount += buff.amount;
                group.maxDuration = Mathf.Max(
                    group.maxDuration,
                    buff.duration
                );
            }

            // =========================================================
            // XÓA ICON UI CŨ
            // =========================================================
            foreach (Image icon in spawnedBuffIcons)
            {
                if (icon != null)
                {
                    UnityEngine.Object.Destroy(
                        icon.gameObject
                    );
                }
            }

            spawnedBuffIcons.Clear();

            // =========================================================
            // MỖI LOẠI BUFF CHỈ SINH 1 ICON
            // =========================================================
            foreach (GroupedBuffUI group in groupedBuffs)
            {
                if (AdvancedUIManager.Instance == null ||
                    AdvancedUIManager.Instance.buffIconPrefab == null)
                {
                    continue;
                }

                GameObject newIcon =
                    UnityEngine.Object.Instantiate(
                        AdvancedUIManager.Instance.buffIconPrefab,
                        buffContainer
                    );

                Image img =
                    newIcon.GetComponent<Image>();

                if (img == null)
                {
                    UnityEngine.Object.Destroy(newIcon);
                    continue;
                }

                Sprite buffSprite =
                    AdvancedUIManager.Instance.GetBuffSprite(
                        group.stat
                    );

                if (buffSprite != null)
                {
                    img.sprite = buffSprite;
                }

                // Nếu prefab icon có TextMeshProUGUI con:
                // 1 stack -> trống
                // 2 stack -> x2
                // 3 stack -> x3
                TextMeshProUGUI stackText =
                    newIcon.GetComponentInChildren<
                        TextMeshProUGUI>(true);

                if (stackText != null)
                {
                    stackText.text =
                        group.stackCount > 1
                        ? "x" + group.stackCount
                        : "";
                    stackText.raycastTarget = false;
                }

                img.raycastTarget = true;
                AdvancedUIManager.Instance.BindBuffTooltip(
                    newIcon,
                    group.stat,
                    group.totalAmount,
                    group.stackCount,
                    group.maxDuration);

                spawnedBuffIcons.Add(img);
            }
        }
    }

    // --- Nhấp nháy Buff Icon sắp hết hạn ---
    public void UpdateBuffBlinking(float currentAlpha)
    {
        debuffIcon.Blink(currentAlpha);
        for (int i = 0; i < spawnedBuffIcons.Count; i++)
        {
            if (spawnedBuffIcons[i] == null)
                continue;

            bool aboutToExpire =
                i < groupedBuffs.Count &&
                groupedBuffs[i].maxDuration <= 1;

            if (aboutToExpire)
            {
                spawnedBuffIcons[i].color =
                    new Color(
                        1f,
                        1f,
                        1f,
                        currentAlpha
                    );
            }
            else
            {
                spawnedBuffIcons[i].color =
                    Color.white;
            }
        }
    }
}

// Separate from buff stack icons: debuffs have one slot and display remaining turns.
public sealed class DebuffIconUI
{
    private Image image;
    private TextMeshProUGUI label;
    private int turns;
    private Color tint;

    public void Refresh(BattleUnit unit, Transform container)
    {
        var effect = unit != null && !unit.IsDead && unit.activeDebuffs.Count > 0 ? unit.activeDebuffs[0] : null;
        if (effect == null || container == null)
        {
            if (image != null)
            {
                if (image.gameObject.activeSelf) AdvancedUIManager.Instance?.HideStatusTooltip();
                image.gameObject.SetActive(false);
            }
            return;
        }
        if (image == null)
        {
            var manager = AdvancedUIManager.Instance;
            var go = manager != null && manager.buffIconPrefab != null
                ? UnityEngine.Object.Instantiate(manager.buffIconPrefab, container)
                : new GameObject("Debuff Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(container, false);
            image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            if (image.rectTransform.sizeDelta == Vector2.zero) image.rectTransform.sizeDelta = new Vector2(36, 36);
            label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                var text = new GameObject("Turns", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                text.transform.SetParent(go.transform, false);
                label = text.GetComponent<TextMeshProUGUI>();
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            }
            image.raycastTarget = true;
            label.raycastTarget = false;
        }
        image.gameObject.name = "Debuff " + effect.settings.type;
        image.gameObject.SetActive(true);
        image.transform.SetAsLastSibling();
        image.sprite = effect.settings.icon;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        turns = effect.remainingTurns;
        bool hasIcon = image.sprite != null;
        tint = hasIcon ? Color.white : new Color(0.3f, 0.12f, 0.12f, 1f);
        image.color = tint;
        label.gameObject.SetActive(true);
        label.text = hasIcon ? turns.ToString() : effect.settings.type + "\n" + turns;
        label.fontSize = hasIcon ? 16 : 10;
        label.alignment = hasIcon ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.Center;
        label.color = Color.white;
        AdvancedUIManager.Instance?.BindDebuffTooltip(image.gameObject, effect);
    }

    public void Blink(float alpha)
    {
        if (image != null && image.gameObject.activeSelf)
            image.color = new Color(tint.r, tint.g, tint.b, turns <= 1 ? alpha : 1f);
    }
}
