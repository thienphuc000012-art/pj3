using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

public class ActionButtonUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI nameText;       // Tên kỹ năng
    public TextMeshProUGUI infoText;       // THAY ĐỔI: Sẽ dùng để hiển thị Mô tả (Description)
    [FormerlySerializedAs("stainText"), Tooltip("Shows item quantity for items, or Stain/Ultimate resource for actions.")]
    public TextMeshProUGUI resourceText;
    public Image iconImage;                // Icon kỹ năng
    public Button button;

    private ActionData currentAction;

    void Update()
    {
        if (currentAction == null) return;
        var manager = CombatManager.Instance;
        var unit = manager != null ? manager.currentActiveUnit : null;
        if (button != null && manager != null)
            button.interactable = unit != null && unit.CanUseAction(currentAction);
        UpdateResourceText(unit);
    }

    public void Setup(ActionData action)
    {
        currentAction = action;

        // 1. Hiển thị Tên
        if (nameText != null)
            GameLocalization.SetText(nameText, VietnameseGameUIText.ActionName(action));

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

        // Items show their owned quantity in this label; other actions keep Stain cost/gain.
        UpdateResourceText(CombatManager.Instance != null ? CombatManager.Instance.currentActiveUnit : null);

        // 4. Hiển thị Mô tả (Description) thay vì các thông số khô khan
        if (infoText != null)
        {
            if (!string.IsNullOrEmpty(action.description))
            {
                // Nếu có ghi description trong Inspector thì hiển thị description đó
                GameLocalization.SetText(infoText, VietnameseGameUIText.ActionDescription(action));
            }
            else
            {
                // Nếu quên ghi description, hiển thị tạm dòng chữ này để nhắc nhở
                GameLocalization.SetText(infoText, "Chưa có mô tả kỹ năng.");
            }
        }

        // 5. Gán sự kiện khi bấm nút (Chuyển lệnh về CombatManager)
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
        GameUITheme.Apply(gameObject);
    }

    private void UpdateResourceText(BattleUnit unit)
    {
        if (resourceText == null || currentAction == null) return;

        if (currentAction.type == ActionData.ActionType.Item)
        {
            int count = 1;
            if (CampaignSession.Instance != null &&
                CampaignSession.Instance.TryGetBattleItemCount(currentAction, out int ownedCount))
                count = ownedCount;
            resourceText.text = "x" + Mathf.Max(0, count);
            resourceText.color = new Color(.82f, .84f, .87f, 1f);
            return;
        }

        if (currentAction.isUltimate)
        {
            GameLocalization.SetText(resourceText, "Tuyệt kỹ " + (unit != null ? Mathf.FloorToInt(unit.UltimateEnergy) : 0) + "/100");
            resourceText.color = unit != null && unit.UltimateReady ? new Color(.92f, .93f, .95f, 1f) : new Color(.52f, .54f, .57f, 1f);
            return;
        }

        if (currentAction.stainChange > 0)
        {
            GameLocalization.SetText(resourceText, $"+{currentAction.stainChange} Tinh lực");
            resourceText.color = new Color(.82f, .84f, .87f, 1f);
        }
        else if (currentAction.stainChange < 0)
        {
            GameLocalization.SetText(resourceText, $"{currentAction.stainChange} Tinh lực");
            resourceText.color = new Color(.72f, .74f, .77f, 1f);
        }
        else
        {
            GameLocalization.SetText(resourceText, "0 Tinh lực");
            resourceText.color = new Color(.82f, .84f, .87f, 1f);
        }
    }

    public void SetupEmpty(string slotName)
    {
        currentAction = null;
        if (nameText != null) GameLocalization.SetText(nameText, slotName == "Ultimate" ? "Tuyệt kỹ" : slotName.StartsWith("Skill ") ? "Kỹ năng " + slotName.Substring(6) : slotName);
        if (infoText != null) GameLocalization.SetText(infoText, "Chưa trang bị");
        if (resourceText != null) GameLocalization.SetText(resourceText, "");
        if (iconImage != null) iconImage.gameObject.SetActive(false);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = false;
        }
        GameUITheme.Apply(gameObject);
    }

    public void FitFixedSlot()
    {
        void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }
        // Every field lives inside the row. The resource column uses a share of
        // the available width rather than reserving 170px on a narrow menu.
        if (iconImage != null)
        {
            Place(iconImage.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -56), new Vector2(56, -16));
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }
        if (nameText != null)
        {
            Place(nameText.rectTransform, new Vector2(0, 1), new Vector2(.67f, 1), new Vector2(70, -48), new Vector2(-10, -12));
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 16f;
            nameText.fontSizeMax = 24f;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            nameText.margin = Vector4.zero;
            nameText.raycastTarget = false;
        }
        if (resourceText != null)
        {
            Place(resourceText.rectTransform, new Vector2(.67f, 1), Vector2.one, new Vector2(0, -48), new Vector2(-18, -12));
            resourceText.alignment = TextAlignmentOptions.MidlineRight;
            resourceText.enableAutoSizing = true;
            resourceText.fontSizeMin = 12f;
            resourceText.fontSizeMax = 21f;
            resourceText.enableWordWrapping = false;
            resourceText.overflowMode = TextOverflowModes.Ellipsis;
            resourceText.margin = Vector4.zero;
            resourceText.raycastTarget = false;
        }
        if (infoText != null)
        {
            Place(infoText.rectTransform, Vector2.zero, Vector2.one, new Vector2(70, 12), new Vector2(-18, -52));
            infoText.alignment = TextAlignmentOptions.TopLeft;
            infoText.enableAutoSizing = true;
            infoText.fontSizeMin = 12; infoText.fontSizeMax = 20;
            infoText.enableWordWrapping = true;
            infoText.overflowMode = TextOverflowModes.Ellipsis;
            infoText.margin = Vector4.zero;
            infoText.raycastTarget = false;
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
