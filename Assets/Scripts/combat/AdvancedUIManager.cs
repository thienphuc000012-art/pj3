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
    public GameObject changeTargetHintText; // --- THÊM MỚI: UI Text gợi ý "A/D để đổi mục tiêu" ---

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
    public TextMeshProUGUI enemyNameText;
    public TextMeshProUGUI enemyHpText;

    [Header("Party Members HP HUD (Bottom Right)")]
    public List<PartyHUDUnit> partyHUDList;

    [Header("Target HUD")]
    public TextMeshProUGUI targetNameText;
    [Header("Parry UI")]
    public GameObject parryTextObject;
    public Vector3 parryTextOffset = new Vector3(0, -1f, 0);
    public float parryHorizontalOffset = 1.5f;
    [Header("Damage Text UI")]
    public GameObject damageTextPrefab;
    public Transform damageTextContainer;

    void Awake()
    {
        Instance = this;

        if (enemyHudPanel != null) enemyHudPanel.SetActive(false);
        if (turnOrderContainer != null) turnOrderContainer.gameObject.SetActive(false);
        if (changeTargetHintText != null) changeTargetHintText.SetActive(false); // --- Tắt Text gợi ý ban đầu ---

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
        if (stainIcons == null) return;

        float blinkSpeed = 1f;
        float blinkAlpha = Mathf.PingPong(Time.time * blinkSpeed, 0.6f) + 0.2f;

        for (int i = 0; i < stainIcons.Length; i++)
        {
            if (stainIcons[i] == null) continue;

            if (i < currentStainsForUI)
            {
                if (currentPreviewChange < 0 && i >= currentStainsForUI + currentPreviewChange)
                {
                    stainIcons[i].color = new Color(1f, 0.3f, 0.3f, blinkAlpha);
                }
                else
                {
                    stainIcons[i].color = Color.white;
                }
            }
            else
            {
                if (currentPreviewChange > 0 && i < currentStainsForUI + currentPreviewChange)
                {
                    stainIcons[i].color = new Color(1f, 1f, 1f, blinkAlpha);
                }
                else
                {
                    stainIcons[i].color = new Color(1f, 1f, 1f, 0.2f);
                }
            }
        }
    }

    public void PositionActionMenu(Transform target) { }

    public void ShowActionMenu(bool isShow)
    {
        if (actionMenuPanel != null)
        {
            actionMenuPanel.gameObject.SetActive(isShow);
            // Đã xóa dòng ép ẩn subMenuPanel ở đây để ta có thể ẩn riêng 3 nút (actionMenuPanel) mà subMenuPanel vẫn hiện
        }
    }

    // --- THÊM MỚI: Bật/Tắt Text gợi ý ---
    public void ToggleChangeTargetHint(bool isShow)
    {
        if (changeTargetHintText != null)
        {
            changeTargetHintText.SetActive(isShow);
        }
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

            if (!unit.isPlayer && imgComponent != null)
            {
                imgComponent.color = Color.red;
            }

            if (i == 0)
            {
                portrait.transform.localScale = Vector3.one * 1.2f;
            }
            else
            {
                portrait.transform.localScale = Vector3.one;
            }
        }
    }

    public void RemoveFirstPortrait()
    {
        if (activePortraits.Count > 0)
        {
            GameObject firstPortrait = activePortraits[0];
            activePortraits.RemoveAt(0);
            if (firstPortrait != null)
            {
                Destroy(firstPortrait);
            }
        }
    }

    public void RegisterEnemyHP(BattleUnit enemy)
    {
        if (enemy == null) return;

        enemy.OnStatsChanged -= UpdateEnemyHPUISafe;
        enemy.OnStatsChanged += UpdateEnemyHPUISafe;
        UpdateEnemyHPUISafe(enemy.currentHP, enemy.maxHP, enemy.GetTotalShield());

        if (enemyNameText != null) enemyNameText.text = enemy.unitName;

        bool isStartingGame = CombatManager.Instance != null &&
                             (CombatManager.Instance.state == CombatState.Start ||
                              CombatManager.Instance.state == CombatState.BattleStartAnim);

        if (enemyHudPanel != null)
            enemyHudPanel.SetActive(!isStartingGame);
    }

    void UpdateEnemyHPUISafe(int current, int max, int shield)
    {
        if (enemyHpFill != null) enemyHpFill.fillAmount = (float)current / max;
        if (enemyHpText != null)
            enemyHpText.text = shield > 0 ? $"{current}/{max} <color=yellow>[+{shield}]</color>" : $"{current} / {max}";
    }

    public void RegisterPartyHP(List<BattleUnit> playerList)
    {
        for (int i = 0; i < partyHUDList.Count; i++)
        {
            if (partyHUDList[i] == null) continue;

            if (i < playerList.Count && playerList[i] != null)
            {
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
        if (targetNameText != null)
        {
            targetNameText.gameObject.SetActive(false);
        }
    }

    public void ShowParryText(Transform targetTransform)
    {
        if (parryTextObject != null)
        {
            parryTextObject.SetActive(true);

            if (targetTransform != null && Camera.main != null)
            {
                float randomDirection = Random.Range(-0.5f, 0.5f);
                Vector3 dynamicOffset = new Vector3(parryHorizontalOffset * randomDirection, parryTextOffset.y, parryTextOffset.z);
                Vector3 worldPos = targetTransform.position + dynamicOffset;
                Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

                parryTextObject.transform.position = screenPos;
            }

            CancelInvoke(nameof(HideParryText));
            Invoke(nameof(HideParryText), 1f);
        }
    }

    private void HideParryText()
    {
        if (parryTextObject != null)
        {
            parryTextObject.SetActive(false);
        }
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
        if (!isShow) ToggleChangeTargetHint(false); // --- Đảm bảo tắt Text gợi ý khi tắt toàn bộ UI ---

        if (turnOrderContainer != null)
            turnOrderContainer.gameObject.SetActive(isShow);

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

        if (isShow && CombatManager.Instance != null)
        {
            RegisterPartyHP(CombatManager.Instance.playerParty);
        }

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

    private BattleUnit boundUnit;

    public void BindUnit(BattleUnit unit)
    {
        boundUnit = unit;
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
    }
}