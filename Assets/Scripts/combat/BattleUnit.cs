using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ActiveBuff
{
    public ActionData.BuffStat stat;
    public int amount;
    public int duration;
}

[System.Serializable]
public class BossPhase
{
    [Tooltip("Phần trăm máu để kích hoạt (Ví dụ: 0.5 = 50% máu)")]
    [Range(0f, 1f)] public float hpPercentThreshold = 0.5f;

    [Tooltip("Số lượt đánh trong 1 turn khi ở phase này")]
    public int actionsPerTurn = 2;

    [Tooltip("Chuỗi kỹ năng Boss sẽ dùng LẦN LƯỢT trong phase này (Không random)")]
    public List<ActionData> phaseActionPattern;
}

public class BattleUnit : MonoBehaviour
{
    public string unitName;
    public int maxHP = 100;

    [Header("Base Stats")]
    public int baseAtk = 10;
    public int baseDef = 5;
    [Range(0, 100)] public int baseCrit = 10;
    [Tooltip("Total damage percentage on a critical hit: 150 means x1.5 damage before defense.")]
    [Min(100)] public int baseCritDamage = 150;

    [Header("Player Skills")]
    public ActionData defaultAttack;
    public List<ActionData> characterSkills; // Danh sách skill hiển thị ở Menu cho Player

    [Header("Enemy AI & Boss Settings")]
    public bool isBoss = false;
    [Tooltip("Số lượt đánh mặc định (Quái thường để 1)")]
    public int actionsPerTurn = 1;

    [Tooltip("Chuỗi kỹ năng Quái sẽ dùng LẦN LƯỢT (VD: Đánh thường -> Skill -> Đánh thường -> Lặp lại)")]
    public List<ActionData> baseActionPattern;

    [Tooltip("Danh sách các Phase của Boss (Nhập theo thứ tự giảm dần: 0.7 -> 0.5 -> 0.2)")]
    public List<BossPhase> bossPhases;

    private int currentPhaseIndex = -1; // -1 = Phase 1/base, bossPhases[0] = Phase 2
    private int currentPatternIndex = 0; // Đếm số thứ tự chiêu thức trong chuỗi

    [Header("Phase 2 Intro")]
    [Tooltip("Tên Trigger trong Animator chạy đúng 1 lần khi Boss bắt đầu Phase 2.")]
    public string phase2AnimationTriggerName = "Phase2";

    [Tooltip("Thời gian giữ camera Phase 2 trước khi tiếp tục Turn Order.")]
    [Min(0f)] public float phase2IntroDuration = 2.5f;

    [HideInInspector] public bool phase2IntroPlayed = false;

    [Header("Phase 2 Idle")]
    [Tooltip("Idle clip currently used by the boss Animator. Replaced only for this boss after the Phase 2 intro.")]
    public AnimationClip baseIdleClip;
    [Tooltip("Looping idle animation used after the Phase 2 intro and between all Phase 2 skills.")]
    public AnimationClip phase2IdleClip;
    [Tooltip("Animator idle state path, for example Base Layer.Idle.")]
    public string idleStateName = "Base Layer.Idle";
    private AnimatorOverrideController phaseIdleController;
    private RuntimeAnimatorController originalIdleController;

    [Header("Hit Reactions")]
    [Tooltip("Original animation assigned to the Animator Hit state.")]
    public AnimationClip baseHitClip;
    [Tooltip("Optional stronger reaction when receiving a critical hit.")]
    public AnimationClip criticalHitClip;
    [Tooltip("Hit reaction while the boss is in Phase 2 or later.")]
    public AnimationClip phase2HitClip;
    [Tooltip("Optional critical reaction in Phase 2. Falls back to Phase 2 Hit first.")]
    public AnimationClip phase2CriticalHitClip;

    public AnimationClip ResolveHitReactionClip(bool isCritical)
    {
        if (isBoss && currentPhaseIndex >= 0)
        {
            if (isCritical && phase2CriticalHitClip != null) return phase2CriticalHitClip;
            if (phase2HitClip != null) return phase2HitClip;
        }
        return isCritical && criticalHitClip != null ? criticalHitClip : baseHitClip;
    }

    // One instance-local override controller holds BOTH idle and hit overrides.
    // Preparing Phase 2 after a critical hit must not skip the idle replacement.
    private bool OverrideAnimationClip(AnimationClip original, AnimationClip replacement)
    {
        if (animator == null || animator.runtimeAnimatorController == null || original == null || replacement == null) return false;
        bool creating = phaseIdleController == null;
        var controller = creating ? new AnimatorOverrideController(animator.runtimeAnimatorController) : phaseIdleController;
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        bool found = false;
        for (int i = 0; i < overrides.Count; i++)
        {
            if (overrides[i].Key != original && overrides[i].Value != original) continue;
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, replacement);
            found = true;
        }
        if (!found)
        {
            if (creating)
            {
                if (Application.isPlaying) Destroy(controller); else DestroyImmediate(controller);
            }
            Debug.LogWarning("Animation clip is not in the unit Animator: " + original.name, this);
            return false;
        }
        controller.ApplyOverrides(overrides);
        if (creating)
        {
            originalIdleController = animator.runtimeAnimatorController;
            controller.name = name + " Combat Animations";
            phaseIdleController = controller;
            animator.runtimeAnimatorController = controller;
        }
        return true;
    }

    public void PreparePhase2Idle()
    {
        if (IsDead || animator == null) return;
        if (phase2IdleClip != null) OverrideAnimationClip(baseIdleClip, phase2IdleClip);
        if (phase2HitClip != null) OverrideAnimationClip(baseHitClip, phase2HitClip);
    }

    public void EnterPhase2Idle()
    {
        PreparePhase2Idle();
        ReturnToCombatIdle();
    }

    public void ReturnToCombatIdle()
    {
        if (IsDead || animator == null || string.IsNullOrEmpty(idleStateName)) return;
        int stateHash = Animator.StringToHash(idleStateName);
        if (animator.HasState(0, stateHash)) animator.CrossFadeInFixedTime(stateHash, .1f, 0);
    }

    private void OnDestroy()
    {
        if (phaseIdleController == null) return;
        if (animator != null && animator.runtimeAnimatorController == phaseIdleController)
            animator.runtimeAnimatorController = originalIdleController;
        if (Application.isPlaying) Destroy(phaseIdleController);
        else DestroyImmediate(phaseIdleController);
    }

    [HideInInspector] public BattleUnit lastTarget = null; // Ghi nhớ mục tiêu vừa đánh

    [Header("Turn System")]
    public int speed;
    public Sprite unitPortrait;
    public int currentHP { get; private set; }
    public bool IsDead => currentHP <= 0;
    public void SetPersistentHP(int hp)
    {
        currentHP = Mathf.Clamp(hp, 0, maxHP);
        activeBuffs.Clear();
        if (IsDead && animator != null) animator.SetTrigger("Die");
        UpdateUI();
    }
    public bool isPlayer;

    [Header("VFX References")]
    public Transform handTransform;
    [Tooltip("VFX spawn point on the left hand. Falls back to the Humanoid LeftHand bone when unassigned.")]
    public Transform leftHandTransform;

    public Transform VfxOrigin => handTransform != null ? handTransform : transform;

    public Vector3 GetVfxTargetPosition(Vector3 offset) => transform.position + offset;

    public Animator animator;

    public event Action<int, int, int> OnStatsChanged;

    public List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    void Awake()
    {
        currentHP = maxHP;
        // CombatManager owns placement and movement. Set this before slot
        // placement, rather than reinitializing the Animator on the first attack.
        if (animator != null) animator.applyRootMotion = false;
    }

    void Start()
    {
        if (animator != null && animator.isHuman)
        {
            if (handTransform == null)
                handTransform = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (leftHandTransform == null)
                leftHandTransform = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }
        UpdateUI();
    }

    // Trả về số Phase vừa chuyển tới.
    // 0 = không đổi phase, 2 = bossPhases[0], 3 = bossPhases[1], ...
    public int CheckPhase()
    {
        if (!isBoss || bossPhases == null || bossPhases.Count == 0) return 0;

        float currentHpPercent = (float)currentHP / maxHP;

        for (int i = bossPhases.Count - 1; i >= 0; i--)
        {
            if (currentHpPercent <= bossPhases[i].hpPercentThreshold && currentPhaseIndex < i)
            {
                currentPhaseIndex = i;
                actionsPerTurn = bossPhases[i].actionsPerTurn;
                currentPatternIndex = 0;

                int phaseNumber = i + 2; // Base = Phase 1, bossPhases[0] = Phase 2
                Debug.Log($"[BOSS HỆ THỐNG] {unitName} chuyển sang Phase {phaseNumber}!");
                return phaseNumber;
            }
        }

        return 0;
    }

    // --- CẬP NHẬT: Lấy kỹ năng tiếp theo trong chuỗi thay vì Random ---
    public ActionData GetNextAction()
    {
        List<ActionData> currentPattern = baseActionPattern;

        if (isBoss && currentPhaseIndex >= 0 && currentPhaseIndex < bossPhases.Count)
        {
            if (bossPhases[currentPhaseIndex].phaseActionPattern != null && bossPhases[currentPhaseIndex].phaseActionPattern.Count > 0)
            {
                currentPattern = bossPhases[currentPhaseIndex].phaseActionPattern;
            }
        }

        // Nếu có setup chuỗi kỹ năng thì đánh theo thứ tự
        if (currentPattern != null && currentPattern.Count > 0)
        {
            ActionData action = currentPattern[currentPatternIndex];

            currentPatternIndex++;
            if (currentPatternIndex >= currentPattern.Count) currentPatternIndex = 0; // Xoay vòng lại từ đầu

            return action;
        }

        return defaultAttack; // Fallback nếu quên không set kỹ năng
    }

    public void AddBuff(ActionData.BuffStat stat, int amount, int duration)
    {
        if (IsDead || stat == ActionData.BuffStat.None || duration <= 0) return;
        activeBuffs.Add(new ActiveBuff { stat = stat, amount = amount, duration = duration });
        UpdateUI();
    }

    public int GetBuffValue(ActionData.BuffStat stat) => activeBuffs.Where(b => b.stat == stat).Sum(b => b.amount);
    public int GetTotalShield() => GetBuffValue(ActionData.BuffStat.Shield);
    public int GetCritChance() => Mathf.Clamp(baseCrit + GetBuffValue(ActionData.BuffStat.Crit), 0, 100);
    public float GetCritDamageMultiplier() => Mathf.Max(100, baseCritDamage + GetBuffValue(ActionData.BuffStat.CritDamage)) / 100f;

    public void TickBuffs()
    {
        foreach (var buff in activeBuffs) buff.duration--;
        activeBuffs.RemoveAll(b => b.duration <= 0);
        UpdateUI();
    }

    public void TakeDamage(int rawDamage, bool isParried, bool isCritical = false)
    {
        if (IsDead) return;
        // --- CẬP NHẬT: Nếu Parry thành công, chặn Damage và KHÔNG gọi lại SetTrigger("Parry") ---
        if (isParried)
        {
            // (Đã xóa animator.SetTrigger("Parry") ở đây vì nó đã được gọi ngay lúc bấm phím Space ở ParrySystem)
            return;
        }

        int totalDef = baseDef + GetBuffValue(ActionData.BuffStat.Def);
        int finalDamage = Mathf.Max(1, rawDamage - totalDef);

        foreach (var buff in activeBuffs.Where(b => b.stat == ActionData.BuffStat.Shield).ToList())
        {
            if (finalDamage <= 0) break;

            if (buff.amount >= finalDamage)
            {
                buff.amount -= finalDamage;
                finalDamage = 0;
            }
            else
            {
                finalDamage -= buff.amount;
                buff.amount = 0;
            }
        }
        activeBuffs.RemoveAll(b => b.amount <= 0 && b.stat == ActionData.BuffStat.Shield);

        currentHP = Mathf.Max(0, currentHP - finalDamage);

        UpdateUI();
        if (IsDead)
        {
            foreach (SwordBladeTrail trail in GetComponentsInChildren<SwordBladeTrail>(true))
                trail.StopTrail();
            if (animator != null)
            {
                // Discard queued attack/Hit/Parry triggers before entering Die.
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger)
                        animator.ResetTrigger(parameter.nameHash);
                animator.SetTrigger("Die");
            }
        }
        else if (animator != null && finalDamage > 0)
        {
            OverrideAnimationClip(baseHitClip, ResolveHitReactionClip(isCritical));
            if (animator.parameters.Any(p => p.type == AnimatorControllerParameterType.Trigger && p.name == "Parry"))
                animator.ResetTrigger("Parry");
            animator.SetTrigger("Hit");
        }
    }
    public void Heal(int amount)
    {
        if (IsDead) return; // Healing is not a revive action.
        if (amount > 0)
        {
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            UpdateUI();
        }
    }

    public void UpdateUI() => OnStatsChanged?.Invoke(currentHP, maxHP, GetTotalShield());
}
