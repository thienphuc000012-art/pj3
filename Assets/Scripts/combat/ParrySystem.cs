using UnityEngine;

public class ParrySystem : MonoBehaviour
{
    public static ParrySystem Instance;

    public bool isParryWindowOpen { get; private set; }
    public bool parrySuccessful { get; private set; }

    [Header("Parry Anti-Spam")]
    public float parryCooldown = 0.5f; // Thời gian nghỉ giữa 2 lần bấm Parry (giây)
    private float lastParryTime = -99f;

    void Awake() { Instance = this; }

    void Update()
    {
        if (CombatManager.Instance == null) return;

        // 1. Chỉ cho phép bấm Parry khi Địch đang hành động
        BattleUnit activeUnit = CombatManager.Instance.currentActiveUnit;
        if (activeUnit == null || activeUnit.isPlayer) return;

        // 2. Bắt nút Space với Cooldown chống spam
        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= lastParryTime + parryCooldown)
        {
            lastParryTime = Time.time;
            TriggerParryAction();
        }
    }

    private void TriggerParryAction()
    {
        // TÌM NHÂN VẬT ĐANG BỊ ĐÁNH (Target hiện tại của CombatManager)
        BattleUnit targetPlayer = CombatManager.Instance.currentTarget;
        if (targetPlayer == null || !targetPlayer.isPlayer) return;

        // Bật Animation Parry NGAY LẬP TỨC
        if (targetPlayer.animator != null)
        {
            targetPlayer.animator.SetTrigger("Parry");
        }

        // KIỂM TRA THÀNH CÔNG (Nếu bấm đúng lúc cửa sổ Parry đang mở)
        if (isParryWindowOpen)
        {
            parrySuccessful = true;
            isParryWindowOpen = false; // Đóng cửa sổ để không ăn 2 lần

            if (AdvancedUIManager.Instance != null)
            {
                AdvancedUIManager.Instance.ShowParryText(targetPlayer.transform);
            }
        }
        else
        {
            // Bấm sai lúc -> Coi như thất bại (Dù vẫn có Animation)
            parrySuccessful = false;
        }
    }

    public void OpenWindow()
    {
        isParryWindowOpen = true;
    }

    public void CloseWindow()
    {
        isParryWindowOpen = false;
    }

    public void ResetParryState()
    {
        isParryWindowOpen = false;
        parrySuccessful = false;
    }
}