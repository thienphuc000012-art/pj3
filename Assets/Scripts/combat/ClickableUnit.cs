using UnityEngine;

public class ClickableUnit : MonoBehaviour
{
    private BattleUnit battleUnit;

    void Awake()
    {
        battleUnit = GetComponent<BattleUnit>();
    }

    void OnMouseDown()
    {
        // Chỉ cho phép chọn mục tiêu nếu đang trong lượt của Player
        if (CombatManager.Instance != null && CombatManager.Instance.state == CombatState.PlayerTurn)
        {
            // --- THÊM MỚI: Ưu tiên kiểm tra nếu là skill chỉ buff cho bản thân ---
            if (CombatManager.Instance.isSelectingSelfOnly)
            {
                if (battleUnit == CombatManager.Instance.currentActiveUnit)
                {
                    CombatManager.Instance.SetTargetUnit(battleUnit);
                }
                else
                {
                    Debug.LogWarning("[Chọn mục tiêu] Kỹ năng này chỉ có thể sử dụng lên bản thân!");
                }
                return; // Kết thúc sớm
            }

            // Kiểm tra xem CombatManager có đang ở chế độ chọn mục tiêu cho Skill Buff không
            if (CombatManager.Instance.isSelectingBuffTarget)
            {
                // Nếu đang chọn buff (cho cả team): CHỈ CHO PHÉP click vào Player (đồng đội)
                if (battleUnit.isPlayer)
                {
                    CombatManager.Instance.SetTargetUnit(battleUnit);
                }
                else
                {
                    Debug.LogWarning("[Chọn mục tiêu] Đang dùng Skill Buff, bạn không thể chọn kẻ địch!");
                }
            }
            else
            {
                // Mặc định (Đánh thường hoặc skill tấn công): CHỈ CHO PHÉP click vào Enemy
                if (!battleUnit.isPlayer)
                {
                    CombatManager.Instance.SetTargetUnit(battleUnit);
                }
                else
                {
                    Debug.LogWarning("[Chọn mục tiêu] Đang tấn công, bạn không thể chọn đồng đội!");
                }
            }
        }
    }
}