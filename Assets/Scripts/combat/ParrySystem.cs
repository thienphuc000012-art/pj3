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
        if (activeUnit == null || activeUnit.IsDead || activeUnit.isPlayer) return;
        if (CombatManager.Instance.state != CombatState.Executing) return;

        // 2. Bắt nút Space với Cooldown chống spam
        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= lastParryTime + parryCooldown)
        {
            lastParryTime = Time.time;
            TriggerParryAction();
        }
    }

    private void TriggerParryAction()
    {
        CombatManager combat = CombatManager.Instance;
        if (combat == null || combat.state != CombatState.Executing ||
            combat.currentActiveUnit == null || combat.currentActiveUnit.IsDead ||
            combat.currentActiveUnit.isPlayer) return;

        // =========================================================
        // ENEMY AOE ATTACK:
        // Một lần bấm Space -> toàn bộ Party còn sống cùng Parry.
        // =========================================================
        if (combat.IsCurrentEnemyAoEAttack())
        {
            bool foundLivingPlayer = false;

            foreach (BattleUnit player in combat.playerParty)
            {
                if (player == null || player.currentHP <= 0) continue;

                foundLivingPlayer = true;

                if (player.animator != null)
                {
                    player.animator.ResetTrigger("Hit");
                    player.animator.SetTrigger("Parry");
                }
            }

            if (!foundLivingPlayer) return;

            if (isParryWindowOpen)
            {
                // CombatManager đang dùng cùng một parrySuccessful cho toàn bộ
                // targets của AoE, nên true = cả Party chặn đòn.
                parrySuccessful = true;
                isParryWindowOpen = false;

                // Hiện chữ PARRY riêng trên đầu từng thành viên còn sống.
                if (AdvancedUIManager.Instance != null)
                {
                    foreach (BattleUnit player in combat.playerParty)
                    {
                        if (player == null || player.currentHP <= 0) continue;

                        AdvancedUIManager.Instance.ShowParryText(player.transform);
                    }
                }

                Debug.Log("[PARRY AOE] Cả Party Parry thành công!");
            }
            else
            {
                // Bấm sai timing: cả Party vẫn chạy animation Parry,
                // nhưng damage AoE vẫn đi qua bình thường.
                parrySuccessful = false;
                Debug.Log("[PARRY AOE] Cả Party Parry sai timing!");
            }

            return;
        }

        // =========================================================
        // ĐÒN ĐƠN MỤC TIÊU:
        // Giữ nguyên hành vi cũ, chỉ target hiện tại Parry.
        // =========================================================
        BattleUnit targetPlayer = combat.currentTarget;
        if (targetPlayer == null || !targetPlayer.isPlayer || targetPlayer.IsDead) return;

        if (targetPlayer.animator != null)
        {
            targetPlayer.animator.ResetTrigger("Hit");
            targetPlayer.animator.SetTrigger("Parry");
        }

        if (isParryWindowOpen)
        {
            parrySuccessful = true;
            isParryWindowOpen = false;

            if (AdvancedUIManager.Instance != null)
            {
                AdvancedUIManager.Instance.ShowParryText(targetPlayer.transform);
            }
        }
        else
        {
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
