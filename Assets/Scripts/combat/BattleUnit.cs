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

[Serializable]
public class BattleStartEffect
{
    public enum EffectKind { Buff, Debuff }
    public EffectKind kind;
    public ActionData.BuffStat buffStat = ActionData.BuffStat.Atk;
    public int buffAmount = 10;
    [Min(1)] public int buffDuration = 3;
    public ActionData.DebuffSettings debuff = new ActionData.DebuffSettings();
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

[Serializable]
public class IdleVfxAttachment
{
    public GameObject prefab;
    [Tooltip("VFX follows this point/bone. Leave empty to use the unit root.")]
    public Transform spawnPoint;
    public Vector3 localPosition;
    public Vector3 localRotation;
    public Vector3 localScale = Vector3.one;
}

public class BattleUnit : MonoBehaviour
{
    public string unitName;
    public int maxHP = 100;
    [HideInInspector] public bool isSummonedUnit;
    [HideInInspector] public BattleUnit summonOwner;
    [HideInInspector] public float summonDeathOwnerMaxHpCostPercent;
    private bool summonDeathCostApplied;
    [Header("Enemy HP Appearance")]
    [Tooltip("Ảnh viền HP riêng của enemy. Để trống dùng viền mặc định; HP Fill vẫn dùng chung.")]
    public Sprite enemyHpBorderSprite;

    [Header("Ultimate Energy")]
    [Range(0, 100)] public float parryUltimateGain = 15f;
    [Range(0, 100), Tooltip("Energy gained per 100% of maximum HP actually lost. Shield damage does not charge.")]
    public float damageUltimateGain = 50f;
    public float UltimateEnergy { get; private set; }
    public bool UltimateReady => !IsDead && UltimateEnergy >= 100f;
    public event Action<float> OnUltimateEnergyChanged;
    public void AddUltimateEnergy(float amount)
    {
        if (!isPlayer || IsDead || float.IsNaN(amount) || amount <= 0) return;
        UltimateEnergy = Mathf.Clamp(UltimateEnergy + amount, 0, 100);
        OnUltimateEnergyChanged?.Invoke(UltimateEnergy);
    }
    public bool CanUseAction(ActionData action) => action != null && !IsDead && (!isPlayer || !action.isUltimate || UltimateReady);
    public bool CommitActionEnergy(ActionData action)
    {
        if (!CanUseAction(action)) return false;
        if (!isPlayer) return true;
        if (action.isUltimate)
        {
            UltimateEnergy = 0;
            OnUltimateEnergyChanged?.Invoke(0);
        }
        return true;
    }
    public void GainUltimateFromHit(ActionData action)
    {
        if (action != null && !action.isUltimate && action.type != ActionData.ActionType.Item)
            AddUltimateEnergy(action.ultimateEnergyGain);
    }

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

    [Header("Starting Battle Effects")]
    [Tooltip("Các buff/debuff được áp dụng lên chính nhân vật này khi trận bắt đầu. Cấu hình trực tiếp trên BattleUnit của player hoặc enemy.")]
    public List<BattleStartEffect> startingBattleEffects = new List<BattleStartEffect>();
    private bool startingBattleEffectsApplied;

    [Tooltip("Danh sách các Phase của Boss (Nhập theo thứ tự giảm dần: 0.7 -> 0.5 -> 0.2)")]
    public List<BossPhase> bossPhases;

    private int currentPhaseIndex = -1; // -1 = Phase 1/base, bossPhases[0] = Phase 2
    private int currentPatternIndex = 0; // Đếm số thứ tự chiêu thức trong chuỗi
    private int enemyTurnNumber;
    private readonly Dictionary<ActionData, int> enemyActionReadyTurns = new Dictionary<ActionData, int>();

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

    [Header("Idle VFX")]
    [Tooltip("Effects shown only while in Idle State Name. Use looping VFX prefabs for continuous effects.")]
    public List<IdleVfxAttachment> idleVfx = new List<IdleVfxAttachment>();
    [Tooltip("Effects for Phase 2 and later. An empty list means no idle effects in Phase 2.")]
    public List<IdleVfxAttachment> phase2IdleVfx = new List<IdleVfxAttachment>();
    private readonly List<GameObject> activeIdleVfx = new List<GameObject>();
    private int activeIdleVfxPhase = -1;

    private void LateUpdate()
    {
        ObserveDeathAnimation();
        int phase = -1;
        if (!IsDead && animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && !string.IsNullOrEmpty(idleStateName))
        {
            // Follow the destination during a blend so attacks/Hit/Die hide idle effects immediately.
            var state = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(idleStateName)) phase = isBoss && currentPhaseIndex >= 0 ? 1 : 0;
        }
        if (phase == activeIdleVfxPhase) return;
        ClearIdleVfx();
        activeIdleVfxPhase = phase;
        if (phase < 0) return;
        var attachments = phase == 1 ? phase2IdleVfx : idleVfx;
        if (attachments == null) return;
        foreach (var entry in attachments)
        {
            if (entry == null || entry.prefab == null) continue;
            var instance = Instantiate(entry.prefab, entry.spawnPoint != null ? entry.spawnPoint : transform, false);
            instance.transform.localPosition = entry.localPosition;
            instance.transform.localRotation = Quaternion.Euler(entry.localRotation);
            instance.transform.localScale = Vector3.Scale(entry.prefab.transform.localScale, entry.localScale);
            activeIdleVfx.Add(instance);
            instance.SetActive(true);
        }
    }

    private void ClearIdleVfx()
    {
        foreach (var instance in activeIdleVfx)
        {
            if (instance == null) continue;
            instance.SetActive(false);
            if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
        }
        activeIdleVfx.Clear();
        activeIdleVfxPhase = -1;
    }

    private void OnDisable() => ClearIdleVfx();

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
        RestoreDeathPresentation();
        ClearIdleVfx();
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
    [Header("Death Presentation")]
    [Tooltip("Tên state chết trong Animator. Controller hiện tại dùng death.")]
    public string deathStateName = "death";
    [Min(1f), Tooltip("Giới hạn chờ nếu Animator thiếu state chết hoặc bị dừng.")]
    public float deathAnimationTimeout = 30f;
    [Header("Enemy Death Dissolve")]
    [Tooltip("SliceDissolve_URP material used only on this enemy's runtime material instances while its Die animation plays.")]
    public Material enemyDeathDissolveMaterial;
    [Range(0f, .95f), Tooltip("Normalized Die animation time when dissolve begins. The default starts late so the enemy stays visible through most of Die.")]
    public float deathDissolveStartTime = .6f;
    [Range(.8f, 1f), Tooltip("Normalized Die animation time when dissolve completes. Keep this at 1 so the enemy only disappears at the end of Die.")]
    public float deathDissolveFinishTime = 1f;
    [Range(.5f, .99f), Tooltip("Keep this much dissolve while the Animator blends out of Die; the final dissolve completes after the blend.")]
    public float deathDissolveTransitionHoldAmount = .9f;
    [Tooltip("Progress curve. Holds most of the model visible until the final part of Die, then finishes the dissolve near the last frame.")]
    public AnimationCurve deathDissolveCurve = CreateSlowDeathDissolveCurve();
    private bool deathStateSeen;
    private bool deathAnimationFinished;
    private bool deathAnimationPending;
    private bool deathAnimationTriggered;
    private static readonly int DeathDissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    private Renderer[] deathDissolveRenderers;
    private Material[][] originalDeathMaterials;
    private Material[][] runtimeDeathMaterials;
    private MaterialPropertyBlock[][] originalDeathMaterialPropertyBlocks;
    private MaterialPropertyBlock deathDissolvePropertyBlock;
    private bool deathDissolveStarted;
    private bool deathProgressInitialized;
    private bool deathStateExitPending;
    private float lastDeathNormalizedTime;
    private float deathSecondsPerNormalizedTime = 1f;
    private float deathExitElapsed;
    private float deathExitDuration;

    private static AnimationCurve CreateSlowDeathDissolveCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(.65f, .04f, .12f, .12f),
            new Keyframe(.9f, .28f, 1.2f, 1.2f),
            new Keyframe(1f, 1f, 0f, 0f)
        );
    }

    private void ObserveDeathAnimation()
    {
        if (!IsDead || deathAnimationFinished || animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
        var current = animator.GetCurrentAnimatorStateInfo(0);
        bool dying = (!string.IsNullOrEmpty(deathStateName) && current.IsName(deathStateName)) ||
            current.IsName("Die") || current.IsName("Death") || current.IsTag("Death");
        if (dying)
        {
            deathStateSeen = true;
            deathStateExitPending = false;
            UpdateDeathAnimationTiming(current);
            bool dissolvingEnemy = !isPlayer && enemyDeathDissolveMaterial != null;
            float start = Mathf.Clamp01(deathDissolveStartTime);
            float finish = dissolvingEnemy
                ? Mathf.Clamp(deathDissolveFinishTime, Mathf.Max(.8f, start + .01f), 1f)
                : 1f;
            if (dissolvingEnemy)
            {
                if (!deathDissolveStarted) BeginEnemyDeathDissolve();
                float progress = Mathf.InverseLerp(start, finish, current.normalizedTime);
                float amount = deathDissolveCurve != null ? deathDissolveCurve.Evaluate(progress) : progress;
                ApplyEnemyDeathDissolve(amount);
            }
            if (current.normalizedTime >= finish)
            {
                if (dissolvingEnemy && animator.IsInTransition(0))
                {
                    ApplyEnemyDeathDissolve(Mathf.Min(1f, deathDissolveTransitionHoldAmount));
                }
                else
                {
                    if (dissolvingEnemy) ApplyEnemyDeathDissolve(1f);
                    deathAnimationFinished = true;
                }
            }
        }
        else if (deathStateSeen)
        {
            if (isPlayer || !deathDissolveStarted)
            {
                deathAnimationFinished = true;
                return;
            }

            // Some Animator controllers leave the death state during its final blend.
            // Don't snap the shader to 1 here; finish over the estimated remainder of
            // the Die clip so the dissolve cannot outrun the death animation.
            float start = Mathf.Clamp01(deathDissolveStartTime);
            float finish = Mathf.Clamp(deathDissolveFinishTime, Mathf.Max(.8f, start + .01f), 1f);
            if (!deathStateExitPending)
            {
                deathStateExitPending = true;
                deathExitElapsed = 0f;
                float remainingNormalizedTime = Mathf.Max(0f, finish - lastDeathNormalizedTime);
                deathExitDuration = Mathf.Max(.05f, remainingNormalizedTime * deathSecondsPerNormalizedTime);
            }

            deathExitElapsed += Time.deltaTime;
            float exitProgress = Mathf.Clamp01(deathExitElapsed / deathExitDuration);
            float estimatedNormalizedTime = lastDeathNormalizedTime + exitProgress * Mathf.Max(0f, finish - lastDeathNormalizedTime);
            float dissolveProgress = Mathf.InverseLerp(start, finish, estimatedNormalizedTime);
            float amount = deathDissolveCurve != null ? deathDissolveCurve.Evaluate(dissolveProgress) : dissolveProgress;
            ApplyEnemyDeathDissolve(amount);
            if (exitProgress >= 1f)
            {
                ApplyEnemyDeathDissolve(1f);
                deathAnimationFinished = true;
            }
        }
    }

    private void UpdateDeathAnimationTiming(AnimatorStateInfo state)
    {
        float normalizedTime = Mathf.Max(0f, state.normalizedTime);
        if (!deathProgressInitialized)
        {
            float playbackSpeed = Mathf.Abs(animator.speed * state.speed * state.speedMultiplier);
            deathSecondsPerNormalizedTime = state.length / Mathf.Max(.01f, playbackSpeed);
            deathProgressInitialized = true;
        }
        else
        {
            float normalizedDelta = normalizedTime - lastDeathNormalizedTime;
            if (normalizedDelta > .0001f && Time.deltaTime > 0f)
            {
                float observedSecondsPerNormalizedTime = Time.deltaTime / normalizedDelta;
                deathSecondsPerNormalizedTime = Mathf.Lerp(
                    deathSecondsPerNormalizedTime,
                    observedSecondsPerNormalizedTime,
                    .35f
                );
            }
        }

        lastDeathNormalizedTime = normalizedTime;
    }

    private void BeginEnemyDeathDissolve()
    {
        deathDissolveStarted = true;
        deathDissolveRenderers = GetComponentsInChildren<Renderer>(true)
            .Where(x => x != null && !(x is ParticleSystemRenderer) && !(x is TrailRenderer) && !(x is LineRenderer))
            .ToArray();
        originalDeathMaterials = new Material[deathDissolveRenderers.Length][];
        runtimeDeathMaterials = new Material[deathDissolveRenderers.Length][];
        originalDeathMaterialPropertyBlocks = new MaterialPropertyBlock[deathDissolveRenderers.Length][];

        for (int rendererIndex = 0; rendererIndex < deathDissolveRenderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = deathDissolveRenderers[rendererIndex];
            Material[] sourceMaterials = targetRenderer.sharedMaterials;
            Material[] dissolveMaterials = new Material[sourceMaterials.Length];
            MaterialPropertyBlock[] sourcePropertyBlocks = new MaterialPropertyBlock[sourceMaterials.Length];
            originalDeathMaterials[rendererIndex] = sourceMaterials;
            runtimeDeathMaterials[rendererIndex] = dissolveMaterials;
            originalDeathMaterialPropertyBlocks[rendererIndex] = sourcePropertyBlocks;

            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                MaterialPropertyBlock savedBlock = new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(savedBlock, materialIndex);
                sourcePropertyBlocks[materialIndex] = savedBlock;

                Material dissolveMaterial = new Material(enemyDeathDissolveMaterial)
                {
                    name = $"{enemyDeathDissolveMaterial.name} ({targetRenderer.name})"
                };
                CopySurfaceProperties(sourceMaterials[materialIndex], dissolveMaterial);
                dissolveMaterial.SetFloat(DeathDissolveAmountId, 0f);
                dissolveMaterials[materialIndex] = dissolveMaterial;
            }

            targetRenderer.sharedMaterials = dissolveMaterials;
        }

        ApplyEnemyDeathDissolve(0f);
    }

    private static void CopySurfaceProperties(Material source, Material destination)
    {
        string baseMap = FindMaterialProperty(source, "_BaseMap", "_MainTex");
        CopyTexture(source, destination, baseMap, "_BaseMap");
        CopyColor(source, destination, FindMaterialProperty(source, "_BaseColor", "_Color"), "_BaseColor", Color.white);
        CopyTexture(source, destination, FindMaterialProperty(source, "_BumpMap", "_NormalMap"), "_BumpMap");
        CopyFloat(source, destination, FindMaterialProperty(source, "_BumpScale"), "_BumpScale", 1f);
        CopyTexture(source, destination, FindMaterialProperty(source, "_MetallicGlossMap", "_MetallicSpecGlossMap"), "_MetallicGlossMap");
        CopyFloat(source, destination, FindMaterialProperty(source, "_Metallic"), "_Metallic", 0f);
        CopyFloat(source, destination, FindMaterialProperty(source, "_Smoothness", "_Glossiness"), "_Smoothness", .5f);
        CopyTexture(source, destination, FindMaterialProperty(source, "_OcclusionMap"), "_OcclusionMap");
        CopyFloat(source, destination, FindMaterialProperty(source, "_OcclusionStrength"), "_OcclusionStrength", 1f);
        CopyTexture(source, destination, FindMaterialProperty(source, "_EmissionMap"), "_EmissionMap");
        CopyColor(source, destination, FindMaterialProperty(source, "_EmissionColor"), "_EmissionColor", Color.black);
        CopyFloat(source, destination, FindMaterialProperty(source, "_AlphaClip", "_AlphaClipToggle"), "_AlphaClip", 0f);
        CopyFloat(source, destination, FindMaterialProperty(source, "_Cutoff", "_AlphaCutoff"), "_Cutoff", .5f);

        if (source != null && source.IsKeywordEnabled("_EMISSION")) destination.EnableKeyword("_EMISSION");
        else destination.DisableKeyword("_EMISSION");
    }

    private static string FindMaterialProperty(Material material, params string[] propertyNames)
    {
        if (material == null) return null;
        foreach (string propertyName in propertyNames)
            if (!string.IsNullOrEmpty(propertyName) && material.HasProperty(propertyName)) return propertyName;
        return null;
    }

    private static void CopyTexture(Material source, Material destination, string sourceProperty, string destinationProperty)
    {
        if (!destination.HasProperty(destinationProperty)) return;
        Texture texture = source != null && !string.IsNullOrEmpty(sourceProperty) ? source.GetTexture(sourceProperty) : null;
        destination.SetTexture(destinationProperty, texture);
        if (source != null && !string.IsNullOrEmpty(sourceProperty))
        {
            destination.SetTextureScale(destinationProperty, source.GetTextureScale(sourceProperty));
            destination.SetTextureOffset(destinationProperty, source.GetTextureOffset(sourceProperty));
        }
        else
        {
            destination.SetTextureScale(destinationProperty, Vector2.one);
            destination.SetTextureOffset(destinationProperty, Vector2.zero);
        }
    }

    private static void CopyColor(Material source, Material destination, string sourceProperty, string destinationProperty, Color fallback)
    {
        if (!destination.HasProperty(destinationProperty)) return;
        Color color = source != null && !string.IsNullOrEmpty(sourceProperty) ? source.GetColor(sourceProperty) : fallback;
        destination.SetColor(destinationProperty, color);
    }

    private static void CopyFloat(Material source, Material destination, string sourceProperty, string destinationProperty, float fallback)
    {
        if (!destination.HasProperty(destinationProperty)) return;
        float value = source != null && !string.IsNullOrEmpty(sourceProperty) ? source.GetFloat(sourceProperty) : fallback;
        destination.SetFloat(destinationProperty, value);
    }

    private void ApplyEnemyDeathDissolve(float amount)
    {
        if (deathDissolveRenderers == null) return;
        if (deathDissolvePropertyBlock == null) deathDissolvePropertyBlock = new MaterialPropertyBlock();
        float clampedAmount = Mathf.Clamp01(amount);
        for (int rendererIndex = 0; rendererIndex < deathDissolveRenderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = deathDissolveRenderers[rendererIndex];
            Material[][] runtimeMaterials = runtimeDeathMaterials;
            if (targetRenderer == null || runtimeMaterials == null || runtimeMaterials[rendererIndex] == null) continue;
            for (int materialIndex = 0; materialIndex < runtimeMaterials[rendererIndex].Length; materialIndex++)
            {
                targetRenderer.GetPropertyBlock(deathDissolvePropertyBlock, materialIndex);
                deathDissolvePropertyBlock.SetFloat(DeathDissolveAmountId, clampedAmount);
                targetRenderer.SetPropertyBlock(deathDissolvePropertyBlock, materialIndex);
            }
        }
    }

    private void RestoreDeathPresentation()
    {
        if (deathDissolveRenderers != null)
        {
            for (int rendererIndex = 0; rendererIndex < deathDissolveRenderers.Length; rendererIndex++)
            {
                Renderer targetRenderer = deathDissolveRenderers[rendererIndex];
                if (targetRenderer != null)
                {
                    Material[][] originals = originalDeathMaterials;
                    if (originals != null && rendererIndex < originals.Length && originals[rendererIndex] != null)
                        targetRenderer.sharedMaterials = originals[rendererIndex];

                    MaterialPropertyBlock[][] savedBlocks = originalDeathMaterialPropertyBlocks;
                    if (savedBlocks != null && rendererIndex < savedBlocks.Length && savedBlocks[rendererIndex] != null)
                    {
                        for (int materialIndex = 0; materialIndex < savedBlocks[rendererIndex].Length; materialIndex++)
                            targetRenderer.SetPropertyBlock(savedBlocks[rendererIndex][materialIndex], materialIndex);
                    }
                }

                Material[][] created = runtimeDeathMaterials;
                if (created == null || rendererIndex >= created.Length || created[rendererIndex] == null) continue;
                foreach (Material material in created[rendererIndex])
                {
                    if (material == null) continue;
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                }
            }
        }

        deathDissolveRenderers = null;
        originalDeathMaterials = null;
        runtimeDeathMaterials = null;
        originalDeathMaterialPropertyBlocks = null;
        deathDissolveStarted = false;
        deathProgressInitialized = false;
        deathStateExitPending = false;
        lastDeathNormalizedTime = 0f;
        deathSecondsPerNormalizedTime = 1f;
        deathExitElapsed = 0f;
        deathExitDuration = 0f;
    }

    public System.Collections.IEnumerator WaitForDeathAnimation()
    {
        if (!IsDead || animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) yield break;
        var deathAnimator = animator;
        var originalCulling = deathAnimator.cullingMode;
        deathAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        float deadline = Time.unscaledTime + Mathf.Max(1f, deathAnimationTimeout);
        try
        {
            // Give the queued Die trigger a chance to enter the death state.
            yield return null;
            while (this != null && IsDead && deathAnimator != null && deathAnimator.isActiveAndEnabled)
            {
                ObserveDeathAnimation();
                if (deathAnimationFinished) yield break;
                if (Time.unscaledTime >= deadline)
                {
                    Debug.LogWarning($"[{unitName}] Không thấy animation chết kết thúc. Kiểm tra Death State Name / Animator speed.", this);
                    yield break;
                }
                yield return null;
            }
        }
        finally
        {
            if (deathAnimator != null) deathAnimator.cullingMode = originalCulling;
        }
    }
    public void SetPersistentHP(int hp)
    {
        RestoreDeathPresentation();
        currentHP = Mathf.Clamp(hp, 0, maxHP);
        deathStateSeen = deathAnimationFinished = false;
        deathAnimationPending = false;
        deathAnimationTriggered = false;
        activeBuffs.Clear();
        activeDebuffs.Clear();
        if (IsDead) StartDeathAnimation();
        UpdateUI();
    }

    public void PlayPendingDeathAnimation()
    {
        if (!IsDead || !deathAnimationPending) return;
        deathAnimationPending = false;
        StartDeathAnimation();
    }

    private void StartDeathAnimation()
    {
        if (deathAnimationTriggered) return;
        RestoreDeathPresentation();
        deathAnimationTriggered = true;
        deathStateSeen = false;
        deathAnimationFinished = false;
        if (animator == null) return;

        // Discard queued attack/Hit/Parry triggers before entering the death state.
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Trigger)
                animator.ResetTrigger(parameter.nameHash);
        animator.SetTrigger("Die");
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

        // Follow the configured sequence, skipping actions that are still cooling down.
        if (currentPattern != null && currentPattern.Count > 0)
        {
            int startIndex = Mathf.Clamp(currentPatternIndex, 0, currentPattern.Count - 1);
            for (int offset = 0; offset < currentPattern.Count; offset++)
            {
                int index = (startIndex + offset) % currentPattern.Count;
                ActionData candidate = currentPattern[index];
                if (!IsEnemyActionReady(candidate)) continue;
                currentPatternIndex = (index + 1) % currentPattern.Count;
                return candidate;
            }
        }

        return IsEnemyActionReady(defaultAttack) ? defaultAttack : null;
    }

    public void BeginEnemyTurn() => enemyTurnNumber++;

    public void ResetEnemyActionCooldowns()
    {
        enemyTurnNumber = 0;
        enemyActionReadyTurns.Clear();
    }

    public bool IsEnemyActionReady(ActionData action)
    {
        return action != null &&
               (!enemyActionReadyTurns.TryGetValue(action, out int readyTurn) || enemyTurnNumber >= readyTurn);
    }

    public void RecordEnemyActionUse(ActionData action)
    {
        if (action == null || action.enemyCooldownTurns <= 0) return;
        enemyActionReadyTurns[action] = enemyTurnNumber + action.enemyCooldownTurns;
    }

    public void ApplyStartingBattleEffects()
    {
        if (startingBattleEffectsApplied) return;
        startingBattleEffectsApplied = true;
        if (IsDead || startingBattleEffects == null) return;

        bool hasDebuff = activeDebuffs.Count > 0;
        foreach (var effect in startingBattleEffects)
        {
            if (effect == null) continue;
            if (effect.kind == BattleStartEffect.EffectKind.Buff)
            {
                AddBuff(effect.buffStat, effect.buffAmount, effect.buffDuration);
                continue;
            }

            // Keep the one-debuff-per-unit rule: the first successful initial debuff owns the slot.
            if (hasDebuff || effect.debuff == null || effect.debuff.turns <= 0) continue;
            float applyChance = Mathf.Clamp(effect.debuff.applyChance, 0f, 100f);
            if (applyChance <= 0f || (applyChance < 100f && UnityEngine.Random.value * 100f >= applyChance)) continue;
            activeDebuffs.Add(new ActiveDebuff
            {
                settings = effect.debuff,
                remainingTurns = effect.debuff.turns
            });
            hasDebuff = true;
        }
        UpdateUI();
    }

    public void AddBuff(ActionData.BuffStat stat, int amount, int duration)
    {
        if (IsDead || stat == ActionData.BuffStat.None || duration <= 0) return;
        activeBuffs.Add(new ActiveBuff { stat = stat, amount = amount, duration = duration });
        UpdateUI();
    }

    [Serializable]
    public class ActiveDebuff
    {
        public ActionData.DebuffSettings settings;
        public int remainingTurns;
    }
    [NonSerialized] public List<ActiveDebuff> activeDebuffs = new List<ActiveDebuff>();
    private readonly List<GameObject> debuffVfxInstances = new List<GameObject>();
    public bool HasDebuff(ActionData.DebuffType type) =>
        activeDebuffs != null && activeDebuffs.Any(d => d != null && d.settings != null && d.settings.type == type);
    public bool HasParalysis => HasDebuff(ActionData.DebuffType.Paralysis);
    public bool HasDamagingDebuff => activeDebuffs.Any(d =>
        (d.settings.type == ActionData.DebuffType.Burn && d.settings.burnDamage > 0) ||
        (d.settings.type == ActionData.DebuffType.Poison && d.settings.poisonMaxHpPercent > 0));

    public System.Collections.IEnumerator WaitForDebuffPresentation(bool includeHit)
    {
        // Damage ticks are held by the configured VFX lifetime. Do not extend
        // the camera hold to the full Hit clip; the animation can finish while
        // the configured presentation window remains in control.
        float remaining = includeHit ? 0f : 0.3f;
        do
        {
            yield return null;
            remaining -= Time.deltaTime;
            debuffVfxInstances.RemoveAll(vfx => vfx == null || !vfx.activeInHierarchy);
        } while (remaining > 0f || debuffVfxInstances.Count > 0);
    }

    public void ApplyStatusAction(ActionData action)
    {
        if (IsDead || action == null) return;
        if (action.cleanse == ActionData.CleanseType.RemoveDebuffs) activeDebuffs.Clear();
        if (action.cleanse == ActionData.CleanseType.RemoveBuffs) activeBuffs.Clear();
        // An occupied debuff slot blocks both replacement and duration refresh.
        // Explicit cleansing above can free the slot before applying a new effect.
        if (activeDebuffs.Count == 0 && action.debuffs != null)
            foreach (var effect in action.debuffs)
            {
                if (effect == null || effect.turns <= 0 || UnityEngine.Random.value * 100f >= effect.applyChance) continue;
                activeDebuffs.Add(new ActiveDebuff { settings = effect, remainingTurns = effect.turns });
                break; // First successful entry wins when an action lists multiple debuffs.
            }
        UpdateUI();
    }

    public int GetTotalAttack(bool ignoreBurnAttackReduction = false)
    {
        float atk = Mathf.Max(0, baseAtk + GetBuffValue(ActionData.BuffStat.Atk));
        if (!ignoreBurnAttackReduction)
        {
            foreach (var effect in activeDebuffs)
            {
                if (effect != null && effect.settings != null && effect.settings.type == ActionData.DebuffType.Burn)
                    atk *= 1f - Mathf.Clamp01(effect.settings.burnAttackReductionPercent / 100f);
            }
        }
        return Mathf.Max(0, Mathf.RoundToInt(atk));
    }

    public bool RollParalysisAtTurnStart()
    {
        if (IsDead) return false;
        foreach (var effect in activeDebuffs)
            if (effect.settings.type == ActionData.DebuffType.Paralysis)
            {
                PlayDebuffVfx(effect.settings);
                return UnityEngine.Random.value * 100f < effect.settings.paralysisSkipChance;
            }
        return false;
    }

    public void TickDebuffsAtTurnEnd()
    {
        if (IsDead) return;
        foreach (var effect in activeDebuffs.ToArray())
        {
            var settings = effect.settings;
            int damage = settings.type == ActionData.DebuffType.Burn ? Mathf.Max(0, settings.burnDamage) :
                settings.type == ActionData.DebuffType.Poison ? Mathf.CeilToInt(maxHP * Mathf.Clamp01(settings.poisonMaxHpPercent / 100f)) : 0;
            if (damage > 0 && !IsDead)
            {
                PlayDebuffVfx(settings);
                int before = currentHP;
                TakeDamage(damage, false, false, true);
                AdvancedUIManager.Instance?.ShowDamageText(transform, before - currentHP, false, false);
            }
            effect.remainingTurns--;
        }
        activeDebuffs.RemoveAll(d => d.remainingTurns <= 0);
        UpdateUI();
    }

    private void PlayDebuffVfx(ActionData.DebuffSettings settings)
    {
        if (settings.turnVfxPrefab == null) return;
        var effect = Instantiate(settings.turnVfxPrefab, transform);
        effect.transform.localPosition = settings.turnVfxLocalOffset;
        effect.SetActive(true);
        float lifetime = Mathf.Max(0.1f, settings.turnVfxLifetime);
        foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>())
        {
            particles.Play();
        }
        debuffVfxInstances.Add(effect);
        // Treat Turn Vfx Lifetime as the authoritative presentation/camera hold,
        // even when child particles have a longer simulated lifetime.
        Destroy(effect, lifetime);
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

    public void TakeDamage(int rawDamage, bool isParried, bool isCritical = false, bool isDamageOverTime = false)
    {
        if (IsDead) return;
        // --- CẬP NHẬT: Nếu Parry thành công, chặn Damage và KHÔNG gọi lại SetTrigger("Parry") ---
        if (isParried)
        {
            // (Đã xóa animator.SetTrigger("Parry") ở đây vì nó đã được gọi ngay lúc bấm phím Space ở ParrySystem)
            return;
        }

        int totalDef = baseDef + GetBuffValue(ActionData.BuffStat.Def);
        int finalDamage = isDamageOverTime ? Mathf.Max(0, rawDamage) : Mathf.Max(1, rawDamage - totalDef);

        foreach (var buff in activeBuffs.Where(b => !isDamageOverTime && b.stat == ActionData.BuffStat.Shield).ToList())
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

        int hpLost = Mathf.Min(currentHP, finalDamage);
        currentHP = Mathf.Max(0, currentHP - finalDamage);
        if (hpLost > 0) AddUltimateEnergy(damageUltimateGain * hpLost / Mathf.Max(1f, maxHP));

        UpdateUI();
        if (IsDead)
        {
            ApplySummonDeathCost();
            CombatManager.Instance?.KillSummonsOwnedBy(this);
            foreach (SwordBladeTrail trail in GetComponentsInChildren<SwordBladeTrail>(true))
                trail.StopTrail();
            deathStateSeen = false;
            deathAnimationFinished = false;
            deathAnimationTriggered = false;
            deathAnimationPending = true;
            // Outside a managed combat HUD there is no HP trail to wait for.
            if (AdvancedUIManager.Instance == null) PlayPendingDeathAnimation();
        }
        else if (animator != null && finalDamage > 0)
        {
            OverrideAnimationClip(baseHitClip, ResolveHitReactionClip(isCritical));
            if (animator.parameters.Any(p => p.type == AnimatorControllerParameterType.Trigger && p.name == "Parry"))
                animator.ResetTrigger("Parry");
            animator.SetTrigger("Hit");
        }
    }

    private void ApplySummonDeathCost()
    {
        if (!isSummonedUnit || summonDeathCostApplied) return;
        summonDeathCostApplied = true;
        if (summonOwner == null || summonOwner.IsDead || summonDeathOwnerMaxHpCostPercent <= 0f) return;

        int hpCost = Mathf.CeilToInt(summonOwner.maxHP * Mathf.Clamp(summonDeathOwnerMaxHpCostPercent, 0f, 100f) / 100f);
        if (hpCost > 0)
            summonOwner.TakeDamage(hpCost, false, false, true);
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
