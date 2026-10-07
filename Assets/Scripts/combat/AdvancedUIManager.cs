using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AdvancedUIManager : MonoBehaviour
{
    public static AdvancedUIManager Instance;

    [Header("Action Menu (Fixed Position)")]
    public RectTransform actionMenuPanel;

    [Header("Sub Menu (Skills/Items List)")]
    public GameObject subMenuPanel;
    public Transform subMenuContent;
    public GameObject actionButtonPrefab;

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
    // ------------------------------------

    void Awake()
    {
        Instance = this;

        if (enemyHudPanel != null) enemyHudPanel.SetActive(false);
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
            if (hud != null && hud.gameObject != null)
                hud.gameObject.SetActive(false);
        }

        ShowActionMenu(false);
    }

    void Update()
    {
        UpdateEnemyBinding();
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
        if (enemy == null) return;
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
        BattleUnit enemy = combat.currentTarget;
        if (enemy == null || enemy.isPlayer)
            enemy = combat.currentActiveUnit != null && !combat.currentActiveUnit.isPlayer ? combat.currentActiveUnit : boundEnemy;
        if (enemy != null && !enemy.isPlayer && enemy != boundEnemy) RegisterEnemyHP(enemy);
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
            data.draining = true;
        }
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
            if (t >= 1f) data.draining = false;
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
        if (enemyHpFill != null) enemyHpFill.fillAmount = (float)current / max;
        if (enemyHpText != null)
            enemyHpText.text = shield > 0 ? $"{current}/{max} <color=yellow>[+{shield}]</color>" : $"{current} / {max}";
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

        for (int i = 0; i < partyHUDList.Count; i++)
        {
            if (partyHUDList[i] != null && partyHUDList[i].gameObject != null)
            {
                bool shouldShow = isShow && CombatManager.Instance != null && i < CombatManager.Instance.playerParty.Count && CombatManager.Instance.playerParty[i] != null;
                partyHUDList[i].gameObject.SetActive(shouldShow);
            }
        }

        if (isShow && CombatManager.Instance != null) RegisterPartyHP(CombatManager.Instance.playerParty);

        if (stainIcons != null && stainIcons.Length > 0 && stainIcons[0] != null)
        {
            Transform stainParent = stainIcons[0].transform.parent;
            if (stainParent != null) stainParent.gameObject.SetActive(isShow);
        }
    }
}

[System.Serializable]
public class PartyHUDUnit
{
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
                }

                spawnedBuffIcons.Add(img);
            }
        }
    }

    // --- Nhấp nháy Buff Icon sắp hết hạn ---
    public void UpdateBuffBlinking(float currentAlpha)
    {
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
