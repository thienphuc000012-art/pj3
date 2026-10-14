using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionButtonUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI nameText;       // Tên kỹ năng
    public TextMeshProUGUI infoText;       // THAY ĐỔI: Sẽ dùng để hiển thị Mô tả (Description)
    public TextMeshProUGUI stainText;      // Hiển thị Stain (+ hoặc -)
    public Image iconImage;                // Icon kỹ năng
    public Button button;

    private ActionData currentAction;

    void Update()
    {
        if (button == null || currentAction == null || CombatManager.Instance == null) return;
        var unit = CombatManager.Instance.currentActiveUnit;
        button.interactable = unit != null && unit.CanUseAction(currentAction);
        if (currentAction.isUltimate && stainText != null)
        {
            stainText.text = "Ultimate " + (unit != null ? Mathf.FloorToInt(unit.UltimateEnergy) : 0) + "/100";
            stainText.color = unit != null && unit.UltimateReady ? Color.yellow : Color.gray;
        }
    }

    public void Setup(ActionData action)
    {
        currentAction = action;

        // 1. Hiển thị Tên
        if (nameText != null)
            nameText.text = action.actionName;

        // 2. Hiển thị Icon
        if (iconImage != null && action.icon != null)
        {
            iconImage.sprite = action.icon;
            iconImage.gameObject.SetActive(true);
        }
        else if (iconImage != null)
        {
            iconImage.gameObject.SetActive(false);
        }

        // 3. Hiển thị số lượng Stain
        if (stainText != null)
        {
            if (action.stainChange > 0)
            {
                stainText.text = $"+{action.stainChange} Stain";
                stainText.color = Color.cyan; // Màu xanh dương cho hồi Stain
            }
            else if (action.stainChange < 0)
            {
                stainText.text = $"{action.stainChange} Stain"; // Đã có sẵn dấu trừ
                stainText.color = new Color(1f, 0.4f, 0.4f); // Màu đỏ cho tiêu hao Stain
            }
            else
            {
                stainText.text = "0 Stain";
                stainText.color = Color.white;
            }
        }

        // 4. Hiển thị Mô tả (Description) thay vì các thông số khô khan
        if (infoText != null)
        {
            if (!string.IsNullOrEmpty(action.description))
            {
                // Nếu có ghi description trong Inspector thì hiển thị description đó
                infoText.text = action.description;
            }
            else
            {
                // Nếu quên ghi description, hiển thị tạm dòng chữ này để nhắc nhở
                infoText.text = "<color=#AAAAAA><i>Chưa có mô tả kỹ năng.</i></color>";
            }
        }

        // 5. Gán sự kiện khi bấm nút (Chuyển lệnh về CombatManager)
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }

    public void SetupEmpty(string slotName)
    {
        currentAction = null;
        if (nameText != null) nameText.text = slotName;
        if (infoText != null) infoText.text = "Chưa trang bị";
        if (stainText != null) stainText.text = "";
        if (iconImage != null) iconImage.gameObject.SetActive(false);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = false;
        }
    }

    public void FitFixedSlot()
    {
        void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }
        if (iconImage != null)
            Place(iconImage.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -48), new Vector2(48, -8));
        if (nameText != null)
            Place(nameText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(56, -46), new Vector2(-170, -6));
        if (stainText != null)
            Place(stainText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-162, -46), new Vector2(-8, -6));
        if (infoText != null)
        {
            Place(infoText.rectTransform, Vector2.zero, Vector2.one, new Vector2(56, 6), new Vector2(-8, -48));
            infoText.enableAutoSizing = true;
            infoText.fontSizeMin = 12; infoText.fontSizeMax = 20;
            infoText.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void OnClick()
    {
        if (CombatManager.Instance != null && currentAction != null)
        {
            CombatManager.Instance.OnSkillButtonClicked(currentAction);
        }
    }
}
