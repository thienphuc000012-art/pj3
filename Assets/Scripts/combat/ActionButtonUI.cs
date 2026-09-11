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

    private void OnClick()
    {
        if (CombatManager.Instance != null && currentAction != null)
        {
            CombatManager.Instance.OnSkillButtonClicked(currentAction);
        }
    }
}