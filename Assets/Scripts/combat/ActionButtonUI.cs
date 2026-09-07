using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionButtonUI : MonoBehaviour
{
    public TextMeshProUGUI actionNameText;

    // --- THÊM BIẾN NÀY ĐỂ HIỂN THỊ ICON LÊN UI ---
    public Image actionIcon;
    // ---------------------------------------------

    private ActionData currentAction;

    public void Setup(ActionData action)
    {
        currentAction = action;

        // Cập nhật Tên
        if (actionNameText != null)
        {
            actionNameText.text = action.actionName;
        }

        // --- CẬP NHẬT ICON ---
        if (actionIcon != null)
        {
            if (action.icon != null)
            {
                actionIcon.sprite = action.icon;
                actionIcon.gameObject.SetActive(true); // Hiện ảnh nếu có
            }
            else
            {
                actionIcon.gameObject.SetActive(false); // Ẩn đi nếu skill này chưa gắn ảnh
            }
        }
        // ---------------------

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();

            btn.onClick.AddListener(() => {
                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.OnSkillButtonClicked(currentAction);
                }
            });
        }
    }
}