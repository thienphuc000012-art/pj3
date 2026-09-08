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
    public Vector3 parryTextOffset = new Vector3(0, -1f, 0); // Giữ nguyên độ cao (Y)
    public float parryHorizontalOffset = 1.5f;
    [Header("Damage Text UI")]
    public GameObject damageTextPrefab; // Prefab chứa TextMeshProUGUI (có thể thêm component animation bay lên)
    public Transform damageTextContainer; // Panel/Canvas để chứa các text sát thương
    void Awake()
    {
        Instance = this;
    }

    // Đã xóa nội dung của Update() vì chúng ta không cần UI bay theo nhân vật 3D nữa.

    // Vẫn giữ tên hàm này để CombatManager gọi không bị lỗi báo đỏ, nhưng không làm gì cả
    public void PositionActionMenu(Transform target)
    {
        // Không còn gán vị trí nữa, UI sẽ đứng yên ở nơi bạn xếp trong Canvas
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

    public void UpdateStainsUI(int currentAmount)
    {
        if (stainIcons == null) return;
        for (int i = 0; i < stainIcons.Length; i++)
        {
            if (stainIcons[i] != null)
            {
                stainIcons[i].color = i < currentAmount ? Color.white : new Color(1, 1, 1, 0.2f);
            }
        }
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
            if (string.IsNullOrEmpty(targetName))
            {
                targetNameText.gameObject.SetActive(false);
            }
            else
            {
                targetNameText.gameObject.SetActive(true);
                targetNameText.text = $"Target: {targetName}";
            }
        }
    }
    // --- CHỨC NĂNG PARRY UI ---
    public void ShowParryText(Transform targetTransform)
    {
        if (parryTextObject != null)
        {
            parryTextObject.SetActive(true);

            // Cập nhật vị trí UI bay ngẫu nhiên trái/phải nhân vật
            if (targetTransform != null && Camera.main != null)
            {
                // Cách 1: Random bất kỳ điểm nào TRONG KHOẢNG từ -0.5 đến 0.5 (có thể rơi vào giữa là 0)
                float randomDirection = Random.Range(-0.5f, 0.5f);

                // Cách 2: Nếu bạn CHỈ muốn ra đúng -0.5 (trái) hoặc 0.5 (phải) mà KHÔNG BAO GIỜ rơi vào chính giữa
                // thì comment dòng trên lại và bỏ comment dòng dưới đây:
                // float randomDirection = Random.Range(0, 2) == 0 ? -0.5f : 0.5f;

                // Tạo offset mới kết hợp độ cao (Y) cũ và độ lệch ngang (X) mới
                Vector3 dynamicOffset = new Vector3(
                    parryHorizontalOffset * randomDirection,
                    parryTextOffset.y,
                    parryTextOffset.z
                );

                // Lấy vị trí 3D của nhân vật cộng thêm offset
                Vector3 worldPos = targetTransform.position + dynamicOffset;

                // Chuyển từ tọa độ 3D sang tọa độ 2D của màn hình UI
                Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

                // Gán vị trí cho text
                parryTextObject.transform.position = screenPos;
            }

            // Hủy các lệnh tắt trước đó (nếu người chơi parry liên tục)
            CancelInvoke(nameof(HideParryText));

            // Tự động tắt chữ sau 1 giây
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

        // Giảm biên độ random để sát thương hiện gom lại gần nhau hơn
        float randomX = Random.Range(-0.4f, 0.4f);
        float randomY = Random.Range(0f, 0.5f);
        Vector3 randomOffset = new Vector3(randomX, randomY, 0);

        // Giảm độ cao cơ sở (từ 1.5f xuống 0.8f) để gần người mục tiêu hơn
        Vector3 worldPos = targetTransform.position + Vector3.up * 0.8f + randomOffset;

        if (Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Tạo text tại vị trí container
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
                    txt.color = isCrit ? new Color(1f, 0.5f, 0f) : Color.white; // Màu cam nếu chí mạng, trắng nếu đánh thường
                    if (isCrit) txt.fontSize *= 1.3f; // Phóng to chữ nếu chí mạng
                }
            }

            // Tự động hủy UI sau 1 giây
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

        // Gọi thẳng hàm cập nhật để ăn giá trị hiện tại ngay lập tức
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