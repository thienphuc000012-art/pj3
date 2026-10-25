using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ParrySystem : MonoBehaviour
{
    public static ParrySystem Instance;

    public bool isParryWindowOpen { get; private set; }
    public bool parrySuccessful { get; private set; }

    [Header("Parry Anti-Spam")]
    public float parryCooldown = 0.5f; // Thời gian nghỉ giữa 2 lần bấm Parry (giây)
    [Min(0.01f), Tooltip("Thời gian vùng đỡ của player hoạt động sau khi nhấn parry.")]
    public float meleeGuardDuration = 0.35f;
    [Header("Successful Parry VFX")]
    [Tooltip("Prefab hiệu ứng phát khi parry đúng thời điểm. Gán prefab tại đây.")]
    public GameObject parrySuccessVfxPrefab;
    [Tooltip("Ưu tiên prefab kim loại trắng PF_Parry_Impact trong Resources/Combat; bỏ chọn để dùng prefab được gán phía trên.")]
    public bool useGeneratedRealisticParryVfx = true;
    [Tooltip("Offset cục bộ từ nhân vật: X sang ngang, Y lên cao, Z hướng về phía enemy đang đánh.")]
    public Vector3 parrySuccessVfxLocalOffset = new Vector3(0f, 1f, 0.25f);
    [Tooltip("Góc xoay bổ sung sau khi prefab đã được căn theo pháp tuyến va chạm.")]
    public Vector3 parrySuccessVfxLocalEuler;
    [Min(0.01f), Tooltip("Tốc độ phát hạt của VFX parry.")]
    public float parrySuccessVfxPlaybackSpeed = 1f;
    [Min(0.1f), Tooltip("Thời gian tối thiểu trước khi tự xóa VFX parry.")]
    public float parrySuccessVfxLifetime = 1.5f;

    [Header("Parry Impact Feedback")]
    [Tooltip("Thời gian khựng gần như đứng hình ngay tại va chạm, tính theo thời gian thực.")]
    [Range(0.01f, 0.12f)] public float parryHitStopDuration = 0.045f;
    [Tooltip("Time Scale trong đoạn hit-stop.")]
    [Range(0.01f, 0.15f)] public float parryHitStopScale = 0.025f;
    [Tooltip("Thời lượng slow motion sau hit-stop, tính theo thời gian thực.")]
    [Range(0.05f, 0.5f)] public float parrySlowMotionDuration = 0.18f;
    [Tooltip("Time Scale trong đoạn slow motion sau hit-stop.")]
    [Range(0.1f, 0.8f)] public float parrySlowMotionScale = 0.3f;
    [Tooltip("Clip va chạm parry. Để trống sẽ dùng tiếng kim loại ngắn được tạo sẵn.")]
    public AudioClip parryImpactAudioClip;
    [Range(0f, 1f)] public float parryImpactVolume = 0.32f;

    private float lastParryTime = -99f;
    private Coroutine parryFeedbackRoutine;
    private float timeScaleBeforeParryFeedback = 1f;
    private readonly HashSet<BattleUnit> activeMeleeGuards = new HashSet<BattleUnit>();

    void Awake()
    {
        Instance = this;
        if (useGeneratedRealisticParryVfx)
        {
            GameObject generatedPrefab = Resources.Load<GameObject>("Combat/PF_Parry_Impact");
            if (generatedPrefab != null) parrySuccessVfxPrefab = generatedPrefab;
        }
        if (parryImpactAudioClip == null) parryImpactAudioClip = CreateMetallicParryClip();
    }

    private void OnDisable()
    {
        if (parryFeedbackRoutine == null) return;
        StopCoroutine(parryFeedbackRoutine);
        parryFeedbackRoutine = null;
        Time.timeScale = timeScaleBeforeParryFeedback;
    }

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
            if (CombatManager.Instance.ShouldUseMeleeHitboxParry(activeUnit))
                BeginMeleeGuard(CombatManager.Instance);
            else
                TriggerParryAction();
        }
    }

    private void BeginMeleeGuard(CombatManager combat)
    {
        if (combat == null) return;
        parrySuccessful = false;

        IEnumerable<BattleUnit> targets;
        if (combat.IsCurrentEnemyAoEAttack())
            targets = combat.playerParty;
        else
            targets = new BattleUnit[] { combat.currentTarget };

        foreach (BattleUnit player in targets)
        {
            if (player == null || player.IsDead || !player.isPlayer) continue;
            if (player.animator != null)
            {
                player.animator.ResetTrigger("Hit");
                player.animator.SetTrigger("Parry");
            }
            activeMeleeGuards.Add(player);
            StartCoroutine(ExpireMeleeGuard(player));
        }
    }

    private IEnumerator ExpireMeleeGuard(BattleUnit player)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, meleeGuardDuration));
        activeMeleeGuards.Remove(player);
    }

    /// <summary>Called by CombatManager on each DealDamage event to test actual weapon/guard overlap.</summary>
    public bool TryResolveMeleeHitboxParry(BattleUnit attacker, BattleUnit player, out Vector3 contactPoint)
    {
        contactPoint = Vector3.zero;
        if (attacker == null || player == null || !activeMeleeGuards.Contains(player) ||
            CombatManager.Instance == null || !CombatManager.Instance.ShouldUseMeleeHitboxParry(attacker))
            return false;

        EnemyMeleeParryHitbox[] hitboxes = attacker.GetComponentsInChildren<EnemyMeleeParryHitbox>(true);
        PlayerMeleeParryZone[] zones = player.GetComponentsInChildren<PlayerMeleeParryZone>(true);
        foreach (EnemyMeleeParryHitbox hitbox in hitboxes)
        {
            if (hitbox == null) continue;
            foreach (PlayerMeleeParryZone zone in zones)
            {
                if (hitbox.TryGetContact(zone, out contactPoint))
                {
                    activeMeleeGuards.Remove(player);
                    parrySuccessful = true;
                    isParryWindowOpen = false;
                    player.AddUltimateEnergy(player.parryUltimateGain);
                    SpawnParrySuccessVFX(player, attacker, true, contactPoint);
                    PlayParryImpactFeedback();
                    CombatManager.Instance.GetComponent<BattleResultPanel>()?.RecordParry();
                    AdvancedUIManager.Instance?.ShowParryText(player.transform);
                    Debug.Log($"[PARRY HITBOX] {player.unitName} parried {attacker.unitName} at {contactPoint}.");
                    return true;
                }
            }
        }
        return false;
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
                bool impactSoundPlayed = false;
                foreach (var member in combat.playerParty)
                {
                    if (member == null || member.IsDead) continue;
                    member.AddUltimateEnergy(member.parryUltimateGain);
                    SpawnParrySuccessVFX(member, combat.currentActiveUnit, !impactSoundPlayed);
                    impactSoundPlayed = true;
                }
                PlayParryImpactFeedback();
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
            SpawnParrySuccessVFX(targetPlayer, combat.currentActiveUnit);
            PlayParryImpactFeedback();
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

    private void SpawnParrySuccessVFX(BattleUnit player, BattleUnit attacker, bool playImpactSound = true, Vector3? contactPosition = null)
    {
        if (player == null || parrySuccessVfxPrefab == null) return;

        Vector3 towardAttacker = attacker != null
            ? attacker.transform.position - player.transform.position
            : player.transform.forward;
        if (towardAttacker.sqrMagnitude < 0.0001f) towardAttacker = player.transform.forward;
        towardAttacker.Normalize();
        Vector3 normal = -towardAttacker;
        Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > .98f ? Vector3.forward : Vector3.up;
        Quaternion impactRotation = Quaternion.LookRotation(normal, up) * Quaternion.Euler(parrySuccessVfxLocalEuler);
        Vector3 impactPosition = contactPosition ?? (player.transform.position +
                                 player.transform.right * parrySuccessVfxLocalOffset.x +
                                 Vector3.up * parrySuccessVfxLocalOffset.y +
                                 towardAttacker * parrySuccessVfxLocalOffset.z);

        // Stage inactive so playback speed is applied before any particle emits.
        GameObject staging = new GameObject("Parry Success VFX Setup");
        staging.SetActive(false);
        GameObject effect = Instantiate(
            parrySuccessVfxPrefab,
            impactPosition,
            impactRotation,
            staging.transform);
        effect.SetActive(false);

        float speed = Mathf.Max(0.01f, parrySuccessVfxPlaybackSpeed);
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.simulationSpeed = speed;
        }

        // Keep the impact at the contact point instead of making it follow the player.
        effect.transform.SetParent(null, true);
        effect.SetActive(true);
        Destroy(staging);

        if (playImpactSound && parryImpactAudioClip != null)
            AudioSource.PlayClipAtPoint(parryImpactAudioClip, impactPosition, parryImpactVolume);

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

    private void PlayParryImpactFeedback()
    {
        if (parryFeedbackRoutine != null)
        {
            StopCoroutine(parryFeedbackRoutine);
            Time.timeScale = timeScaleBeforeParryFeedback;
        }
        timeScaleBeforeParryFeedback = Time.timeScale;
        parryFeedbackRoutine = StartCoroutine(ParryImpactFeedbackRoutine());
    }

    private IEnumerator ParryImpactFeedbackRoutine()
    {
        Time.timeScale = Mathf.Min(timeScaleBeforeParryFeedback, parryHitStopScale);
        yield return new WaitForSecondsRealtime(parryHitStopDuration);

        Time.timeScale = Mathf.Min(timeScaleBeforeParryFeedback, parrySlowMotionScale);
        yield return new WaitForSecondsRealtime(parrySlowMotionDuration);

        Time.timeScale = timeScaleBeforeParryFeedback;
        parryFeedbackRoutine = null;
    }

    private static AudioClip CreateMetallicParryClip()
    {
        const int sampleRate = 44100;
        const float duration = .18f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        var samples = new float[sampleCount];
        uint noiseState = 0xA341316Cu;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            noiseState = noiseState * 1664525u + 1013904223u;
            float noise = ((noiseState >> 8) / 8388607.5f) - 1f;
            float attack = Mathf.Clamp01(time / .0025f);
            float ring = Mathf.Sin(2f * Mathf.PI * 730f * time) * .46f +
                         Mathf.Sin(2f * Mathf.PI * 1190f * time) * .30f +
                         Mathf.Sin(2f * Mathf.PI * 1870f * time) * .16f;
            float transient = noise * .18f * Mathf.Exp(-time * 190f);
            float decay = Mathf.Exp(-time * 24f);
            samples[i] = (ring * decay + transient) * attack * .45f;
        }

        AudioClip clip = AudioClip.Create("Parry Metal Impact", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
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
        activeMeleeGuards.Clear();
    }
}
