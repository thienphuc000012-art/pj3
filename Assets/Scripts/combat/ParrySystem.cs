using UnityEngine;

public class ParrySystem : MonoBehaviour
{
    public static ParrySystem Instance;

    public bool isParryWindowOpen { get; private set; }
    public bool parrySuccessful { get; private set; }

    [Header("Parry Anti-Spam")]
    public float parryCooldown = 0.5f; // Thời gian nghỉ giữa 2 lần bấm Parry (giây)
    [Header("Successful Parry VFX")]
    [Tooltip("Prefab hiệu ứng phát khi parry đúng thời điểm. Gán prefab tại đây.")]
    public GameObject parrySuccessVfxPrefab;
    [Tooltip("Vị trí cục bộ của VFX tính từ nhân vật parry.")]
    public Vector3 parrySuccessVfxLocalOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("Góc xoay cục bộ của VFX so với nhân vật parry.")]
    public Vector3 parrySuccessVfxLocalEuler;
    [Min(0.01f), Tooltip("Tốc độ phát hạt của VFX parry.")]
    public float parrySuccessVfxPlaybackSpeed = 1f;
    [Min(0.1f), Tooltip("Thời gian tối thiểu trước khi tự xóa VFX parry.")]
    public float parrySuccessVfxLifetime = 1.5f;
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
                foreach (var member in combat.playerParty)
                {
                    if (member == null || member.IsDead) continue;
                    member.AddUltimateEnergy(member.parryUltimateGain);
                    SpawnParrySuccessVFX(member);
                }
                combat.GetComponent<BattleResultPanel>()?.RecordParry();
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
            targetPlayer.AddUltimateEnergy(targetPlayer.parryUltimateGain);
            SpawnParrySuccessVFX(targetPlayer);
            combat.GetComponent<BattleResultPanel>()?.RecordParry();
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

    private void SpawnParrySuccessVFX(BattleUnit player)
    {
        if (player == null || parrySuccessVfxPrefab == null) return;

        // Stage inactive so the playback speed is applied before any particles emit.
        GameObject staging = new GameObject("Parry Success VFX Setup");
        staging.SetActive(false);
        GameObject effect = Instantiate(
            parrySuccessVfxPrefab,
            player.transform.position,
            player.transform.rotation,
            staging.transform);
        effect.SetActive(false);

        float speed = Mathf.Max(0.01f, parrySuccessVfxPlaybackSpeed);
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.simulationSpeed = speed;
        }

        effect.transform.SetParent(player.transform, true);
        effect.transform.localPosition = parrySuccessVfxLocalOffset;
        effect.transform.localRotation = Quaternion.Euler(parrySuccessVfxLocalEuler);
        effect.SetActive(true);
        Destroy(staging);

        float lifetime = Mathf.Max(0.1f, parrySuccessVfxLifetime);
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            if (main.loop) continue;
            float duration = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
            lifetime = Mathf.Max(lifetime, duration / speed);
        }
        Destroy(effect, lifetime);
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
