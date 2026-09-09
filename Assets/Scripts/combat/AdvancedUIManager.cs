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

    [Header("Stains UI")]
    public Image[] stainIcons;

    // --- LƯU TRỮ TRẠNG THÁI STAIN ĐỂ UPDATE ANIMATION ---
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
    }

    void Update()
    {
        // --- TẠO HIỆU ỨNG CHỚP NHÁY (BLINK) CHO STAIN UI MỖI FRAME ---
        if (stainIcons == null) return;

        // Giảm blinkSpeed từ 4f xuống 1.5f để nhịp chớp chậm và mượt hơn
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

    public void PositionActionMenu(Transform target)
    {
        // Không còn gán vị trí nữa, UI sẽ đứng yên
    }

    public void ShowActionMenu(bool isShow)
    {
        if (actionMenuPanel != null)
        {
            actionMenuPanel.gameObject.SetActive(isShow);
            if (!isShow && subMenuPanel != null) subMenuPanel.SetActive(false);
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

    // --- HÀM CẬP NHẬT TRẠNG THÁI STAIN (Không set màu trực tiếp nữa mà lưu biến để Update xử lý blink) ---
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

    // --- QUẢN LÝ HP BAR CHO ENEMY VÀ PARTY ---
    public void RegisterEnemyHP(BattleUnit enemy)
    {
        if (enemy == null) return;
        if (enemyHudPanel != null) enemyHudPanel.SetActive(true);
        if (enemyNameText != null) enemyNameText.text = enemy.unitName;

        enemy.OnStatsChanged -= UpdateEnemyHPUISafe;
        enemy.OnStatsChanged += UpdateEnemyHPUISafe;
        UpdateEnemyHPUISafe(enemy.currentHP, enemy.maxHP, enemy.GetTotalShield());
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
            if (i < playerList.Count && playerList[i] != null)
            {
                partyHUDList[i].gameObject.SetActive(true);
                partyHUDList[i].BindUnit(playerList[i]);
            }
            else
            {
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

    // --- CHỨC NĂNG PARRY UI ---
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