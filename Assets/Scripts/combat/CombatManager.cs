using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CombatState { Start, PlayerTurn, EnemyTurn, Executing, Won, Lost, BattleStartAnim }
public enum OpenMenuType { None, Skills, Items }

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance;
    [Header("Turn VFX Cleanup")]
    [Min(0f)] public float turnVfxCleanupDelay = 1.5f;
    private Coroutine turnTransitionRoutine;
    private readonly List<CombatBeamVfx> activeBeams = new List<CombatBeamVfx>();
    private readonly List<GameObject> pendingVfxImpacts = new List<GameObject>();
    private readonly Dictionary<BattleUnit, List<GameObject>> castInstances = new Dictionary<BattleUnit, List<GameObject>>();
    private readonly HashSet<BattleUnit> castStarted = new HashSet<BattleUnit>();
    private readonly HashSet<BattleUnit> vfxLaunched = new HashSet<BattleUnit>();
    private sealed class VfxActionState
    {
        public bool parried;
        public bool impactDamagePrepared;
        public int impactRawDamage;
        public bool impactIsCrit;
    }
    private VfxActionState vfxActionState = new VfxActionState();
    private readonly HashSet<GameObject> combatVfxInstances = new HashSet<GameObject>();

    private void TrackCombatVFX(GameObject vfx)
    {
        combatVfxInstances.RemoveWhere(item => item == null);
        if (vfx == null || !combatVfxInstances.Add(vfx)) return;
        // Impacts created by RFX4 can live outside the original effect hierarchy.
        foreach (var motion in vfx.GetComponentsInChildren<RFX4_PhysicsMotion>(true))
            motion.EffectCreated += TrackCombatVFX;
        foreach (var ray in vfx.GetComponentsInChildren<RFX4_RaycastCollision>(true))
        {
            ray.EffectCreated += TrackCombatVFX;
            foreach (GameObject impact in ray.CollidedInstances)
                TrackCombatVFX(impact);
        }
        foreach (var collision in vfx.GetComponentsInChildren<RFX4_ParticleCollisionGameObject>(true))
            collision.EffectCreated += TrackCombatVFX;
    }

    private void ClearCombatVFX()
    {
        GameObject[] instances = combatVfxInstances.ToArray();
        combatVfxInstances.Clear();
        foreach (GameObject vfx in instances)
        {
            if (vfx == null) continue;
            // Hide particles, lights and audio immediately; Destroy completes
            // at frame end, before the enemy's new effects are created.
            vfx.SetActive(false);
            Destroy(vfx);
        }
        activeBeams.Clear();
        pendingVfxImpacts.Clear();
        castInstances.Clear();
        castStarted.Clear();
        vfxLaunched.Clear();
    }

    public void StopBeamVFX(BattleUnit caster)
    {
        for (int i = activeBeams.Count - 1; i >= 0; i--)
        {
            CombatBeamVfx beam = activeBeams[i];
            if (beam == null || beam.Caster == caster)
            {
                if (beam != null) beam.FinishAttack();
                if (beam == null || beam.HasEmitted) activeBeams.RemoveAt(i);
            }
        }
    }

    private void OnDisable()
    {
        if (turnTransitionRoutine != null)
        {
            StopCoroutine(turnTransitionRoutine);
            turnTransitionRoutine = null;
            isTransitioningTurn = false;
        }
        ClearCombatVFX();
    }

    [Header("Game State")]
    public CombatState state;

    [Header("Party & Enemy Lists")]
    public List<BattleUnit> playerParty = new List<BattleUnit>();
    public List<BattleUnit> enemyParty = new List<BattleUnit>();
    public List<BattleUnit> allUnitsTimeline = new List<BattleUnit>();

    [Header("Battle Positions (Slots)")]
    public Transform[] playerSlots;
    public Transform[] enemySlots;

    [Header("Inventory & Resources")]
    public List<ActionData> inventoryItems = new List<ActionData>();
    public int currentStains = 0;
    public int maxStains = 4;

    [Header("Current Turn Info")]
    public BattleUnit currentActiveUnit;
    public BattleUnit currentTarget;
    private int currentTimelineIndex = 0;
    private ActionData selectedAction;

    [Header("Melee Attack Positions (Scene Slots)")]
    public Transform[] playerMeleeSlots;
    public Transform[] enemyMeleeSlots;

    [HideInInspector] public BattleEncounterSetup activeEncounterSetup;

    // true ở index nào thì enemyMeleeSlots[index] là slot thật đã được setup
    // trong scene hoặc được BattleEncounterSetup override.
    // Slot tự sinh chỉ để đủ kích thước mảng sẽ không vô tình thay đổi combat.
    [HideInInspector] public bool[] enemyMeleeSlotConfigured;

    // true ở index nào thì playerMeleeSlots[index] là slot thật đã được setup
    // trong scene trước khi CampaignSession tự tạo slot còn thiếu.
    [HideInInspector] public bool[] playerMeleeSlotConfigured;

    [HideInInspector] public bool isSelectingBuffTarget = false;
    [HideInInspector] public bool isSelectingSelfOnly = false;

    private bool isTransitioningTurn = false;
    [HideInInspector] public bool isAttackAnimationFinished = false;

    private OpenMenuType currentOpenMenu = OpenMenuType.None;

    // Dùng cho ParrySystem:
    // true khi Enemy hiện tại đang thực hiện một đòn AoE gây damage.
    public bool IsCurrentEnemyAoEAttack()
    {
        return currentActiveUnit != null &&
               !currentActiveUnit.isPlayer &&
               selectedAction != null &&
               selectedAction.isAoE &&
               !selectedAction.isFriendlyAction &&
               !selectedAction.isHeal;
    }

    void Awake() { Instance = this; }

    void Start()
    {
        if (GetComponent<BattleResultPanel>() == null) gameObject.AddComponent<BattleResultPanel>();
        if (CampaignSession.Instance != null && CampaignSession.Instance.InEncounter)
            CampaignSession.Instance.PrepareBattle(this);
        state = CombatState.Start;
        SetupPositions();

        currentStains = maxStains;
        AdvancedUIManager.Instance.UpdateStainsUI(currentStains, 0);

        if (enemyParty.Count > 0 && enemyParty[0] != null)
        {
            AdvancedUIManager.Instance.RegisterEnemyHP(enemyParty[0]);
        }
        AdvancedUIManager.Instance.RegisterPartyHP(playerParty);

        StartCoroutine(BattleStartRoutine());
    }

    private IEnumerator BattleStartRoutine()
    {
        state = CombatState.BattleStartAnim;

        // Bật Camera tổng thể đầu trận
        CameraManager.Instance.SwitchToBattleStartCam();

        AdvancedUIManager.Instance.ToggleAllUI(false);

        foreach (var p in playerParty.Where(u => u != null && !u.IsDead && u.animator != null))
        {
            p.animator.SetTrigger("BattleStart");
        }
        foreach (var e in enemyParty.Where(u => u != null && u.animator != null))
        {
            e.animator.SetTrigger("BattleStart");
        }

        // Chờ thời gian Animation dạo đầu (Bạn có thể tăng số này lên 2-3s để xem rõ camera tổng)
        yield return new WaitForSeconds(2.5f);

        AdvancedUIManager.Instance.ToggleAllUI(true);

        DetermineTurnOrder();
    }

    void Update()
    {
        if (state != CombatState.PlayerTurn || currentActiveUnit == null || currentActiveUnit.IsDead) return;

        if (Input.GetMouseButtonDown(1))
        {
            CancelCurrentAction();
        }

        if (selectedAction != null)
        {
            if (!isSelectingSelfOnly)
            {
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    CycleTarget(-1);
                }
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    CycleTarget(1);
                }
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                ConfirmSelectedTarget();
            }
        }
    }

    private void CycleTarget(int direction)
    {
        List<BattleUnit> validTargets = isSelectingBuffTarget ?
            playerParty.Where(u => u.currentHP > 0).ToList() :
            enemyParty.Where(u => u.currentHP > 0).ToList();

        if (validTargets.Count == 0) return;

        int currentIndex = validTargets.IndexOf(currentTarget);
        if (currentIndex == -1) currentIndex = 0;

        currentIndex += direction;
        if (currentIndex < 0) currentIndex = validTargets.Count - 1;
        else if (currentIndex >= validTargets.Count) currentIndex = 0;

        currentTarget = validTargets[currentIndex];

        AdvancedUIManager.Instance.UpdateTargetUI(currentTarget.unitName);
        CameraManager.Instance.SwitchToTargetCam(currentTarget);
    }

    public void ConfirmSelectedTarget()
    {
        if (currentTarget == null || selectedAction == null) return;
        CameraManager.Instance.ResetTargetCam();

        AdvancedUIManager.Instance.ToggleChangeTargetHint(false); // Tắt UI hint

        OnActionSelected(selectedAction);
    }

    private void CancelCurrentAction()
    {
        if (state != CombatState.PlayerTurn || currentActiveUnit == null || currentActiveUnit.IsDead) return;

        // 1. Trường hợp đang ở bước Chọn Mục Tiêu (Sau khi bấm Attack hoặc chọn Skill/Item)
        if (selectedAction != null)
        {
            CameraManager.Instance.ResetTargetCam();

            selectedAction = null;
            isSelectingBuffTarget = false;
            isSelectingSelfOnly = false;

            AdvancedUIManager.Instance.UpdateStainsUI(currentStains, 0);

            currentTarget = enemyParty.FirstOrDefault(e => e.currentHP > 0);
            if (currentTarget != null)
            {
                AdvancedUIManager.Instance.UpdateTargetUI(currentTarget.unitName);
            }
            else
            {
                AdvancedUIManager.Instance.UpdateTargetUI("");
            }

            AdvancedUIManager.Instance.ToggleChangeTargetHint(false); // Tắt UI hint
            AdvancedUIManager.Instance.ShowActionMenu(true); // Bật lại 3 nút

            if (AdvancedUIManager.Instance.subMenuPanel != null)
            {
                AdvancedUIManager.Instance.subMenuPanel.SetActive(false);
            }

            currentOpenMenu = OpenMenuType.None;
            ResetMenuAnimations();

            // --- THÊM MỚI Ở ĐÂY: Trả camera về lại nhân vật đang đánh ---
            int activePlayerIndex = playerParty.IndexOf(currentActiveUnit);
            if (activePlayerIndex >= 0)
            {
                CameraManager.Instance.SwitchToPlayerTurnCam(activePlayerIndex, true);
            }

            return;
        }

        // 2. Trường hợp đang mở Menu con (Skills / Items) mà chưa chọn kỹ năng nào
        if (currentOpenMenu != OpenMenuType.None)
        {
            currentOpenMenu = OpenMenuType.None;
            ResetMenuAnimations();

            if (AdvancedUIManager.Instance.subMenuPanel != null)
                AdvancedUIManager.Instance.subMenuPanel.SetActive(false);

            AdvancedUIManager.Instance.ShowActionMenu(true); // Bật lại 3 nút

            int playerIndex = playerParty.IndexOf(currentActiveUnit);
            if (playerIndex >= 0)
            {
                CameraManager.Instance.SwitchToPlayerTurnCam(playerIndex, true);
            }
        }
    }
    void SetupPositions()
    {
        for (int i = 0; i < playerParty.Count; i++)
        {
            if (i < playerSlots.Length && playerSlots[i] != null && playerParty[i] != null)
            {
                playerParty[i].transform.position = playerSlots[i].position;
                playerParty[i].transform.rotation = playerSlots[i].rotation;
            }
        }
        for (int i = 0; i < enemyParty.Count; i++)
        {
            if (i < enemySlots.Length && enemySlots[i] != null && enemyParty[i] != null)
            {
                enemyParty[i].transform.position = enemySlots[i].position;
                enemyParty[i].transform.rotation = enemySlots[i].rotation;
            }
        }
    }

    public void DetermineTurnOrder()
    {
        if (state != CombatState.Start && state != CombatState.BattleStartAnim)
        {
            foreach (var unit in playerParty.Concat(enemyParty).Where(u => u != null && u.currentHP > 0))
            {
                unit.TickBuffs();
            }
        }

        allUnitsTimeline.Clear();

        // Gom tất cả unit còn sống
        var aliveUnits = playerParty.Where(u => u.currentHP > 0)
            .Concat(enemyParty.Where(u => u.currentHP > 0))
            .ToList();

        BattleUnit phase2IntroBoss = null;

        // Phase chỉ được check ở đầu Wave mới.
        // Nếu Boss vừa bước vào Phase 2 thì ghi lại để chạy cinematic đúng 1 lần.
        foreach (var unit in aliveUnits)
        {
            if (unit.isPlayer) continue;

            int enteredPhase = unit.CheckPhase();

            if (enteredPhase == 2 && !unit.phase2IntroPlayed)
            {
                unit.phase2IntroPlayed = true;
                phase2IntroBoss = unit;
            }
        }

        // Sắp xếp theo tốc độ
        aliveUnits = aliveUnits.OrderByDescending(u => u.speed).ToList();

        // Đẩy Unit vào Timeline NHIỀU LẦN dựa theo Actions Per Turn
        foreach (var unit in aliveUnits)
        {
            int actionsCount = unit.isPlayer ? 1 : (unit.actionsPerTurn > 0 ? unit.actionsPerTurn : 1);
            for (int i = 0; i < actionsCount; i++)
            {
                allUnitsTimeline.Add(unit);
            }
        }

        AdvancedUIManager.Instance.UpdateTurnOrderUI(allUnitsTimeline);

        currentTimelineIndex = 0;

        if (phase2IntroBoss != null)
        {
            StartCoroutine(BossPhase2IntroRoutine(phase2IntroBoss));
        }
        else
        {
            StartNextUnitTurn();
        }
    }

    private IEnumerator BossPhase2IntroRoutine(BattleUnit boss)
    {
        if (boss == null)
        {
            StartNextUnitTurn();
            yield break;
        }

        state = CombatState.Executing;
        isTransitioningTurn = true;

        // Cinematic Phase 2: ẩn UI để không che animation/camera.
        if (AdvancedUIManager.Instance != null)
        {
            AdvancedUIManager.Instance.ToggleAllUI(false);
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToEnemyPhase2Cam();
        }

        // Trigger animation chỉ chạy đúng 1 lần vì phase2IntroPlayed đã được đánh dấu.
        if (boss.animator != null && !string.IsNullOrEmpty(boss.phase2AnimationTriggerName))
        {
            boss.animator.SetTrigger(boss.phase2AnimationTriggerName);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, boss.phase2IntroDuration));

        if (AdvancedUIManager.Instance != null)
        {
            AdvancedUIManager.Instance.ToggleAllUI(true);
        }

        isTransitioningTurn = false;
        StartNextUnitTurn();
    }
    private void ResetMenuAnimations()
    {
        if (currentActiveUnit != null && currentActiveUnit.animator != null)
        {
            currentActiveUnit.animator.SetBool("IsSkillIdle", false);
            currentActiveUnit.animator.SetBool("IsItemIdle", false);
        }
    }

    private void CompleteBattle(bool victory)
    {
        state = victory ? CombatState.Won : CombatState.Lost;
        AdvancedUIManager.Instance.ToggleAllUI(false);
        ClearCombatVFX();
        if (CampaignSession.Instance != null && CampaignSession.Instance.InEncounter)
            CampaignSession.Instance.FinishBattle(this, victory);
        else Debug.Log(victory ? "WIN!" : "LOSE!");
        GetComponent<BattleResultPanel>().Show(victory);
    }

    void StartNextUnitTurn()
    {
        isTransitioningTurn = false;
        selectedAction = null;
        isSelectingBuffTarget = false;
        isSelectingSelfOnly = false;
        currentTarget = null;
        currentOpenMenu = OpenMenuType.None;

        AdvancedUIManager.Instance.UpdateTargetUI("");
        AdvancedUIManager.Instance.ToggleChangeTargetHint(false);

        AdvancedUIManager.Instance.UpdateStainsUI(currentStains, 0);

        if (enemyParty.All(e => e.currentHP <= 0)) { CompleteBattle(true); return; }
        if (playerParty.All(p => p.currentHP <= 0)) { CompleteBattle(false); return; }

        currentActiveUnit = allUnitsTimeline[currentTimelineIndex];

        if (currentActiveUnit == null || currentActiveUnit.currentHP <= 0)
        {
            EndCurrentTurn();
            return;
        }

        ResetMenuAnimations();

        if (currentActiveUnit.isPlayer)
        {
            state = CombatState.PlayerTurn;
            int playerIndex = playerParty.IndexOf(currentActiveUnit);

            // 1. Ép ẩn Action Menu trước
            AdvancedUIManager.Instance.ShowActionMenu(false);

            // 2. Chuyển Camera
            CameraManager.Instance.SwitchToPlayerTurnCam(playerIndex);
            AdvancedUIManager.Instance.PositionActionMenu(currentActiveUnit.transform);

            // 3. Gọi Coroutine chờ Camera lia xong rồi mới bật UI lên
            StartCoroutine(ShowActionMenuAfterCamera(CameraManager.Instance.transitionSpeed));

            currentTarget = enemyParty.FirstOrDefault(e => e.currentHP > 0);
        }
        else
        {
            state = CombatState.EnemyTurn;
            AdvancedUIManager.Instance.ShowActionMenu(false);
            StartCoroutine(EnemyAICore());
        }
    }

    // --- THÊM MỚI: Coroutine đợi camera lia xong ---
    private IEnumerator ShowActionMenuAfterCamera(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Kiểm tra an toàn: Chỉ bật UI nếu vẫn đang ở lượt Player và chưa mở Menu/chọn mục tiêu
        if (state == CombatState.PlayerTurn && selectedAction == null && currentOpenMenu == OpenMenuType.None)
        {
            AdvancedUIManager.Instance.ShowActionMenu(true);
        }
    }

    public void OnAttackClicked()
    {
        if (state != CombatState.PlayerTurn || currentActiveUnit == null || currentActiveUnit.IsDead) return;

        ActionData attackAction = currentActiveUnit.defaultAttack;

        if (attackAction != null && currentStains + attackAction.stainChange < 0)
        {
            Debug.LogWarning("[Hệ thống] Không đủ Stain để thực hiện Attack!");
            return;
        }

        ResetMenuAnimations();
        currentOpenMenu = OpenMenuType.None;
        isSelectingBuffTarget = false;
        isSelectingSelfOnly = false;
        selectedAction = attackAction;

        if (currentTarget == null || currentTarget.currentHP <= 0 || currentTarget.isPlayer)
        {
            currentTarget = enemyParty.FirstOrDefault(e => e.currentHP > 0);
        }

        if (currentTarget != null)
        {
            AdvancedUIManager.Instance.UpdateTargetUI(currentTarget.unitName);
            AdvancedUIManager.Instance.ShowActionMenu(false);

            AdvancedUIManager.Instance.ToggleChangeTargetHint(true); // Bật UI Hint

            if (AdvancedUIManager.Instance.subMenuPanel != null)
            {
                AdvancedUIManager.Instance.subMenuPanel.SetActive(false);
            }

            CameraManager.Instance.SwitchToTargetCam(currentTarget);

            if (selectedAction != null)
            {
                AdvancedUIManager.Instance.UpdateStainsUI(currentStains, selectedAction.stainChange);
            }
        }
    }

    public void OnSkillsClicked()
    {
        if (state != CombatState.PlayerTurn || currentActiveUnit == null || currentActiveUnit.IsDead) return;

        if (currentOpenMenu == OpenMenuType.Skills)
        {
            currentOpenMenu = OpenMenuType.None;
            ResetMenuAnimations();
            AdvancedUIManager.Instance.subMenuPanel.SetActive(false);
            AdvancedUIManager.Instance.ShowActionMenu(true); // Hiện lại 3 nút

            int playerIndex = playerParty.IndexOf(currentActiveUnit);
            CameraManager.Instance.SwitchToPlayerTurnCam(playerIndex, true);
            return;
        }

        currentOpenMenu = OpenMenuType.Skills;
        selectedAction = null;
        isSelectingBuffTarget = false;
        isSelectingSelfOnly = false;

        ResetMenuAnimations();
        if (currentActiveUnit != null && currentActiveUnit.animator != null)
        {
            currentActiveUnit.animator.SetBool("IsSkillIdle", true);
        }

        int activePlayerIdx = playerParty.IndexOf(currentActiveUnit);
        if (activePlayerIdx >= 0)
        {
            CameraManager.Instance.SwitchToPlayerMenuCam(activePlayerIdx);
        }

        AdvancedUIManager.Instance.ShowActionMenu(false); // Ẩn 3 nút đi
        AdvancedUIManager.Instance.PopulateSubMenu(currentActiveUnit.characterSkills);
    }

    public void OnItemsClicked()
    {
        if (state != CombatState.PlayerTurn || currentActiveUnit == null || currentActiveUnit.IsDead) return;

        if (currentOpenMenu == OpenMenuType.Items)
        {
            currentOpenMenu = OpenMenuType.None;
            ResetMenuAnimations();
            AdvancedUIManager.Instance.subMenuPanel.SetActive(false);
            AdvancedUIManager.Instance.ShowActionMenu(true); // Hiện lại 3 nút

            int playerIndex = playerParty.IndexOf(currentActiveUnit);
            CameraManager.Instance.SwitchToPlayerTurnCam(playerIndex, true);
            return;
        }

        currentOpenMenu = OpenMenuType.Items;
        selectedAction = null;
        isSelectingBuffTarget = false;
        isSelectingSelfOnly = false;

        ResetMenuAnimations();
        if (currentActiveUnit != null && currentActiveUnit.animator != null)
        {
            currentActiveUnit.animator.SetBool("IsItemIdle", true);
        }

        int activePlayerIdx = playerParty.IndexOf(currentActiveUnit);
        if (activePlayerIdx >= 0)
        {
            CameraManager.Instance.SwitchToPlayerMenuCam(activePlayerIdx);
        }

        AdvancedUIManager.Instance.ShowActionMenu(false); // Ẩn 3 nút đi
        AdvancedUIManager.Instance.PopulateSubMenu(inventoryItems);
    }

    public void OnSkillButtonClicked(ActionData skillAction)
    {
        if (state != CombatState.PlayerTurn || currentActiveUnit == null || currentActiveUnit.IsDead) return;

        if (currentStains + skillAction.stainChange < 0)
        {
            Debug.LogWarning($"[Hệ thống] Không đủ Stain để sử dụng {skillAction.actionName}!");
            return;
        }

        selectedAction = skillAction;
        isSelectingSelfOnly = skillAction.isSelfOnly;
        isSelectingBuffTarget = skillAction.isFriendlyAction || isSelectingSelfOnly;
        currentOpenMenu = OpenMenuType.None;

        if (AdvancedUIManager.Instance.subMenuPanel != null)
        {
            AdvancedUIManager.Instance.subMenuPanel.SetActive(false);
        }

        if (isSelectingSelfOnly)
        {
            currentTarget = currentActiveUnit;
        }
        else if (isSelectingBuffTarget)
        {
            currentTarget = currentActiveUnit;
        }
        else
        {
            currentTarget = enemyParty.FirstOrDefault(e => e.currentHP > 0);
        }

        if (currentTarget != null)
        {
            AdvancedUIManager.Instance.UpdateTargetUI(currentTarget.unitName);
            AdvancedUIManager.Instance.ToggleChangeTargetHint(true); // Bật UI Hint

            CameraManager.Instance.SwitchToTargetCam(currentTarget);
        }

        AdvancedUIManager.Instance.UpdateStainsUI(currentStains, skillAction.stainChange);
        AdvancedUIManager.Instance.ShowActionMenu(false);
    }

    public void OnActionSelected(ActionData action)
    {
        if (state != CombatState.PlayerTurn || currentActiveUnit == null || currentActiveUnit.IsDead || action == null) return;
        if (action.type == ActionData.ActionType.Item && CampaignSession.Instance != null && CampaignSession.Instance.InEncounter)
        {
            if (currentTarget == null || currentTarget.IsDead || !CampaignSession.Instance.ConsumeBattleItem(action)) return;
            CampaignSession.Instance.RefreshBattleItems(this);
        }
        selectedAction = action;
        currentOpenMenu = OpenMenuType.None;

        ResetMenuAnimations();
        AdvancedUIManager.Instance.ShowActionMenu(false);
        AdvancedUIManager.Instance.UpdateTargetUI("");

        // Truyền thêm cờ true để Player tự động kết thúc lượt sau khi đánh xong 1 hit
        StartCoroutine(ExecuteActionRoutine(currentActiveUnit, currentTarget, selectedAction, true));
    }

    System.Collections.IEnumerator EnemyAICore()
    {
        yield return new WaitForSeconds(0.5f);

        // ĐÃ XÓA: currentActiveUnit.CheckPhase(); 
        // -> Boss sẽ không đổi phase ngay giữa Turn Order nữa, 
        // mà sẽ đợi hàm DetermineTurnOrder() chạy ở đầu Wave tiếp theo.

        List<BattleUnit> livePlayers = playerParty.Where(p => p.currentHP > 0).ToList();
        if (livePlayers.Count == 0) yield break;

        // KHÔNG ĐÁNH 1 NGƯỜI 2 LẦN LIÊN TỤC (Chỉ áp dụng nếu party còn >1 người sống)
        if (livePlayers.Count > 1 && currentActiveUnit.lastTarget != null && livePlayers.Contains(currentActiveUnit.lastTarget))
        {
            livePlayers.Remove(currentActiveUnit.lastTarget);
        }

        currentTarget = livePlayers[Random.Range(0, livePlayers.Count)];
        currentActiveUnit.lastTarget = currentTarget; // Lưu lại mục tiêu để né vào đòn sau

        // Lấy chiêu theo Action Pattern (Không random)
        selectedAction = currentActiveUnit.GetNextAction();

        // Enemy dùng skill AoE -> dùng camera AoE riêng.
        // Skill thường -> ưu tiên Action Camera riêng của chính enemy này.
        // Nếu encounter chưa setup Action Camera thì fallback về camera cũ nhìn Player bị nhắm.
        if (selectedAction != null && selectedAction.isAoE)
        {
            CameraManager.Instance.SwitchToEnemyAoECam();
        }
        else
        {
            bool usedEnemyActionCamera =
                CameraManager.Instance.SwitchToEnemyActionCam(currentActiveUnit);

            if (!usedEnemyActionCamera)
                CameraManager.Instance.SwitchToTargetHitCam(currentTarget);
        }

        yield return new WaitForSeconds(0.5f);

        // Do Timeline đã nhân bản sẵn các lượt đánh, ta chỉ việc đánh 1 đòn rồi kết thúc
        // EndCurrentTurn() sẽ lo việc chuyển sang đòn thứ 2 tự động nếu còn lượt
        StartCoroutine(ExecuteActionRoutine(currentActiveUnit, currentTarget, selectedAction, true));
    }
    private IEnumerator ExecuteActionRoutine(BattleUnit attacker, BattleUnit target, ActionData action, bool endTurnAfter = true)
    {
        if (attacker == null || attacker.IsDead)
        {
            if (endTurnAfter) EndCurrentTurn();
            yield break;
        }
        state = CombatState.Executing;
        castStarted.Clear();
        vfxLaunched.Clear();
        vfxActionState = new VfxActionState();

        if (attacker.isPlayer)
        {
            int playerIndex = playerParty.IndexOf(attacker);
            if (playerIndex >= 0)
            {
                CameraManager.Instance.SetInstantCutBlendForAction();
                CameraManager.Instance.SwitchToPlayerActionCam(playerIndex);
            }
        }

        if (action != null && attacker.isPlayer)
        {
            AddStain(action.stainChange);
        }

        Vector3 originalPosition = attacker.transform.position;
        Quaternion originalRotation = attacker.transform.rotation;
        isAttackAnimationFinished = false;

        if (action != null && action.isMelee && target != null && !action.isFriendlyAction && !action.isHeal)
        {
            Transform meleeSlot = GetEncounterMeleeSlot(attacker, target);
            Vector3 attackPosition = meleeSlot != null
                ? meleeSlot.position
                : GetMeleeAttackPosition(attacker, target);

            attacker.animator.Play("JumpForward");
            yield return StartCoroutine(MoveToPosition(attacker.transform, attackPosition, 0.35f));

            // Luôn xoay mặt attacker về phía target khi đã tới vị trí melee.
            // Không dùng rotation của Melee Slot vì slot chỉ quyết định VỊ TRÍ.
            FaceMeleeTarget(attacker, target);

            string animTrigger = !string.IsNullOrEmpty(action.animationTriggerName) ? action.animationTriggerName : "Attack";
            attacker.animator.SetTrigger(animTrigger);

            yield return new WaitUntil(() => isAttackAnimationFinished || attacker == null || attacker.IsDead);
            if (attacker == null || attacker.IsDead)
            {
                if (endTurnAfter) EndCurrentTurn();
                yield break;
            }

            attacker.animator.Play("JumpBack");
            yield return StartCoroutine(MoveToPosition(attacker.transform, originalPosition, 0.35f));
        }
        else
        {
            string animTrigger = action != null ? action.animationTriggerName : "Attack";
            attacker.animator.SetTrigger(animTrigger);

            yield return new WaitUntil(() => isAttackAnimationFinished || attacker == null || attacker.IsDead);
            if (attacker == null || attacker.IsDead)
            {
                if (endTurnAfter) EndCurrentTurn();
                yield break;
            }
        }

        // A long flight/charge must resolve before the next action changes the
        // selected target. Expired or destroyed effects cannot stall the turn.
        yield return new WaitUntil(() =>
        {
            pendingVfxImpacts.RemoveAll(vfx => vfx == null);
            return pendingVfxImpacts.Count == 0;
        });

        // Shoot/Beam của Enemy giữ Parry state cho tới khi toàn bộ impact
        // của action (kể cả AoE) đã resolve xong rồi mới reset.
        if (UsesVfxImpactDamage(action) &&
            attacker != null &&
            !attacker.isPlayer &&
            ParrySystem.Instance != null)
        {
            ParrySystem.Instance.ResetParryState();
        }

        StopBeamVFX(attacker);
        FinishCastVFX(attacker);
        attacker.transform.position = originalPosition;
        attacker.transform.rotation = originalRotation;

        if (!attacker.IsDead && attacker.animator != null)
        {
            attacker.animator.CrossFade("Idle", 0.1f);
        }

        // --- CẬP NHẬT: Chỉ chuyển lượt nếu được cho phép (Boss đánh multi-hit sẽ cấm cờ này lại) ---
        if (endTurnAfter)
        {
            EndCurrentTurn();
        }
    }

    private Transform GetEncounterMeleeSlot(BattleUnit attacker, BattleUnit target)
    {
        if (attacker == null || target == null)
            return null;

        // ============================================================
        // PLAYER -> ENEMY
        // ============================================================
        // Ưu tiên:
        // 1. BattleEncounterSetup.Enemies[enemyIndex].playerMeleeSlot
        // 2. CombatManager.enemyMeleeSlots[enemyIndex] đã setup sẵn
        // ============================================================
        if (attacker.isPlayer)
        {
            int enemyIndex = enemyParty.IndexOf(target);
            if (enemyIndex < 0)
                return null;

            if (activeEncounterSetup != null)
            {
                EnemyCombatSetup enemySetup =
                    activeEncounterSetup.GetEnemySetup(enemyIndex);

                if (enemySetup != null &&
                    enemySetup.playerMeleeSlot != null)
                {
                    return enemySetup.playerMeleeSlot;
                }
            }

            if (TryGetConfiguredEnemyTargetMeleeSlot(
                target,
                out Transform enemyTargetSlot))
            {
                return enemyTargetSlot;
            }

            return null;
        }

        // ============================================================
        // ENEMY -> PLAYER
        // ============================================================
        // Ưu tiên:
        // 1. BattleEncounterSetup.Enemies[attackerEnemyIndex]
        //      .enemyMeleeSlots[targetPlayerIndex]
        // 2. CombatManager.playerMeleeSlots[targetPlayerIndex] đã setup sẵn
        // ============================================================
        int attackerEnemyIndex = enemyParty.IndexOf(attacker);
        int targetPlayerIndex = playerParty.IndexOf(target);

        if (attackerEnemyIndex < 0 || targetPlayerIndex < 0)
            return null;

        if (activeEncounterSetup != null)
        {
            EnemyCombatSetup attackerSetup =
                activeEncounterSetup.GetEnemySetup(attackerEnemyIndex);

            if (attackerSetup != null)
            {
                Transform encounterSlot =
                    attackerSetup.GetEnemyMeleeSlot(targetPlayerIndex);

                if (encounterSlot != null)
                    return encounterSlot;
            }
        }

        if (TryGetConfiguredPlayerTargetMeleeSlot(
            target,
            out Transform playerTargetSlot))
        {
            return playerTargetSlot;
        }

        return null;
    }

    /// <summary>
    /// Slot đứng gần Enemy target khi Player lao tới đánh.
    /// enemyMeleeSlots được index theo enemyParty.
    /// </summary>
    private bool TryGetConfiguredEnemyTargetMeleeSlot(
        BattleUnit targetEnemy,
        out Transform meleeSlot)
    {
        meleeSlot = null;

        if (targetEnemy == null || targetEnemy.isPlayer)
            return false;

        int enemyIndex = enemyParty.IndexOf(targetEnemy);

        if (enemyIndex < 0 ||
            enemyMeleeSlots == null ||
            enemyIndex >= enemyMeleeSlots.Length ||
            enemyMeleeSlots[enemyIndex] == null)
        {
            return false;
        }

        if (enemyMeleeSlotConfigured == null ||
            enemyIndex >= enemyMeleeSlotConfigured.Length ||
            !enemyMeleeSlotConfigured[enemyIndex])
        {
            return false;
        }

        meleeSlot = enemyMeleeSlots[enemyIndex];
        return true;
    }

    /// <summary>
    /// Slot đứng gần Player target khi Enemy lao tới đánh.
    /// playerMeleeSlots được index theo playerParty.
    /// </summary>
    private bool TryGetConfiguredPlayerTargetMeleeSlot(
        BattleUnit targetPlayer,
        out Transform meleeSlot)
    {
        meleeSlot = null;

        if (targetPlayer == null || !targetPlayer.isPlayer)
            return false;

        int playerIndex = playerParty.IndexOf(targetPlayer);

        if (playerIndex < 0 ||
            playerMeleeSlots == null ||
            playerIndex >= playerMeleeSlots.Length ||
            playerMeleeSlots[playerIndex] == null)
        {
            return false;
        }

        if (playerMeleeSlotConfigured == null ||
            playerIndex >= playerMeleeSlotConfigured.Length ||
            !playerMeleeSlotConfigured[playerIndex])
        {
            return false;
        }

        meleeSlot = playerMeleeSlots[playerIndex];
        return true;
    }

    public Vector3 GetMeleeAttackPosition(BattleUnit attacker, BattleUnit target)
    {
        if (attacker == null)
            return Vector3.zero;

        Transform encounterSlot = GetEncounterMeleeSlot(attacker, target);
        if (encounterSlot != null)
            return encounterSlot.position;

        // Không có bất kỳ Melee Slot nào -> fallback cách tính tự động cũ.
        if (target == null)
            return attacker.transform.position;

        Vector3 directionToTarget =
            (target.transform.position - attacker.transform.position).normalized;

        float stopDistance = 1.6f;

        Vector3 attackPosition =
            target.transform.position - (directionToTarget * stopDistance);

        return attackPosition;
    }

    private void FaceMeleeTarget(BattleUnit attacker, BattleUnit target)
    {
        if (attacker == null || target == null)
            return;

        Vector3 direction = target.transform.position - attacker.transform.position;

        // Chỉ xoay trên mặt phẳng ngang để nhân vật không bị cúi/ngửa
        // khi pivot của target cao hoặc thấp hơn.
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        attacker.transform.rotation =
            Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private IEnumerator MoveToPosition(Transform unitTransform, Vector3 targetPos, float duration)
    {
        Vector3 startPos = unitTransform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            unitTransform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        unitTransform.position = targetPos;
    }

    public void ApplyDamageFromAnimation(BattleUnit attacker)
    {
        if (state == CombatState.Won || state == CombatState.Lost) return;
        if (attacker == null || attacker.IsDead) return;
        ActionData actionToUse = selectedAction != null ? selectedAction : attacker.defaultAttack;
        if (actionToUse == null) return;

        // ============================================================
        // SHOOT / BEAM:
        // Damage KHÔNG còn chạy từ Animation Event.
        // Projectile/Beam phải thật sự impact target rồi callback mới gây damage.
        // ============================================================
        if (UsesVfxImpactDamage(actionToUse))
        {
            // Nếu đây là skill của Enemy, giữ lại kết quả Parry tại timing
            // của animation event (nếu clip vẫn còn AnimEvent_DealDamage).
            // Nếu clip không có event này thì callback impact vẫn đọc trực tiếp
            // trạng thái Parry hiện tại.
            if (!attacker.isPlayer && ParrySystem.Instance != null)
            {
                vfxActionState.parried =
                    vfxActionState.parried ||
                    ParrySystem.Instance.parrySuccessful;
            }

            return;
        }

        int totalAtk = attacker.baseAtk + attacker.GetBuffValue(ActionData.BuffStat.Atk);
        float calculatedDamage = (totalAtk * actionToUse.damageMultiplier) + actionToUse.power;
        int rawDamage = Mathf.RoundToInt(calculatedDamage);

        bool isCrit = !actionToUse.isFriendlyAction && !actionToUse.isHeal &&
            UnityEngine.Random.Range(0, 100) < attacker.GetCritChance();

        if (isCrit && !actionToUse.isFriendlyAction)
        {
            rawDamage = Mathf.RoundToInt(rawDamage * attacker.GetCritDamageMultiplier());
        }

        if (attacker.isPlayer)
        {
            if (currentTarget == null) return;

            if (actionToUse.isFriendlyAction || actionToUse.isHeal)
            {
                List<BattleUnit> targets = actionToUse.isAoE ? playerParty.Where(u => u.currentHP > 0).ToList() : new List<BattleUnit> { currentTarget };

                foreach (var ally in targets)
                {
                    if (actionToUse.isHeal)
                    {
                        int hpBefore = ally.currentHP;
                        int healAmount = rawDamage > 0 ? rawDamage : actionToUse.power;
                        ally.Heal(healAmount);

                        int actualHeal = ally.currentHP - hpBefore;
                        AdvancedUIManager.Instance.ShowDamageText(ally.transform, actualHeal, false, true);
                    }
                    if (actionToUse.buffStat != ActionData.BuffStat.None)
                    {
                        ally.AddBuff(actionToUse.buffStat, actionToUse.buffAmount, actionToUse.buffDuration);
                    }
                }
            }
            else
            {
                List<BattleUnit> targets = actionToUse.isAoE ? enemyParty.Where(u => u.currentHP > 0).ToList() : new List<BattleUnit> { currentTarget };

                foreach (var enemy in targets)
                {
                    // Melee/explicit animation timing uses this event. Ranged
                    // impact-timed VFX is emitted by the visual arrival callback.
                    if (!UsesVfxImpact(actionToUse)) SpawnActionHitVFX(actionToUse, enemy);

                    int hpBefore = enemy.currentHP;
                    enemy.TakeDamage(rawDamage, false);
                    int actualDamageTaken = hpBefore - enemy.currentHP;
                    GetComponent<BattleResultPanel>()?.RecordDamage(true, actualDamageTaken);

                    AdvancedUIManager.Instance.ShowDamageText(enemy.transform, actualDamageTaken, isCrit, false);
                }
            }
        }
        else
        {
            bool parried = ParrySystem.Instance.parrySuccessful;
            vfxActionState.parried = parried;
            List<BattleUnit> targets = actionToUse.isAoE ? playerParty.Where(u => u.currentHP > 0).ToList() : new List<BattleUnit> { currentTarget };

            foreach (var ally in targets)
            {
                int hpBefore = ally.currentHP;
                ally.TakeDamage(rawDamage, parried);
                int actualDamageTaken = hpBefore - ally.currentHP;
                GetComponent<BattleResultPanel>()?.RecordDamage(false, actualDamageTaken);

                if (!parried)
                {
                    // Parry thành công thì không hiện hiệu ứng trúng đòn.
                    if (!UsesVfxImpact(actionToUse)) SpawnActionHitVFX(actionToUse, ally);
                    AdvancedUIManager.Instance.ShowDamageText(ally.transform, actualDamageTaken, isCrit, false);
                }
            }
            ParrySystem.Instance.ResetParryState();
        }
    }

    private void PlayVFX(BattleUnit attacker, BattleUnit target, ActionData action)
    {
        if (attacker == null || attacker.IsDead || action == null || action.vfxPrefab == null || target == null) return;

        List<BattleUnit> targetList = new List<BattleUnit>();

        if (action.isAoE)
        {
            // Xác định AoE đánh phe nào dựa trên NGƯỜI CAST.
            // Player tấn công -> Enemy
            // Enemy tấn công -> Player
            // Heal/Friendly -> phe của chính người cast
            bool targetsOwnTeam = action.isFriendlyAction || action.isHeal;

            if (attacker.isPlayer)
            {
                targetList = targetsOwnTeam
                    ? playerParty.Where(u => u != null && u.currentHP > 0).ToList()
                    : enemyParty.Where(u => u != null && u.currentHP > 0).ToList();
            }
            else
            {
                targetList = targetsOwnTeam
                    ? enemyParty.Where(u => u != null && u.currentHP > 0).ToList()
                    : playerParty.Where(u => u != null && u.currentHP > 0).ToList();
            }
        }
        else
        {
            targetList.Add(target);
        }

        // =========================================================
        // VFX SINH TRỰC TIẾP TẠI TARGET
        // =========================================================
        if (action.vfxType == ActionData.VfxType.SpawnAtTarget)
        {
            foreach (BattleUnit u in targetList.Where(u => u != null && u.currentHP > 0))
            {
                // Giữ đúng hành vi cũ: SpawnAtTarget sinh ngay tại transform.position của target.
                GameObject vfx = Instantiate(action.vfxPrefab, u.transform.position, u.transform.rotation);
                TrackCombatVFX(vfx);
                Destroy(vfx, 2f);
            }

            return;
        }

        // =========================================================
        // PROJECTILE
        // =========================================================
        if (action.vfxType != ActionData.VfxType.Shoot &&
            action.vfxType != ActionData.VfxType.Beam) return;

        Transform spawnTransform = attacker.VfxOrigin;

        Vector3 startPos = spawnTransform.TransformPoint(action.projectileStartOffset);

        foreach (BattleUnit u in targetList.Where(u => u != null && u.currentHP > 0))
        {
            Vector3 targetPos = u.GetVfxTargetPosition(action.projectileTargetOffset);
            Vector3 directionToTarget = targetPos - startPos;

            Quaternion rotation = directionToTarget.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(directionToTarget.normalized, Vector3.up)
                : spawnTransform.rotation;

            // Configure while inactive: RFX4 raycasts run in OnEnable and can
            // otherwise hit the caster or create an unwanted impact immediately.
            GameObject staging = new GameObject("Combat VFX Setup");
            staging.SetActive(false);
            GameObject vfx = Instantiate(action.vfxPrefab, startPos, rotation, staging.transform);
            vfx.SetActive(false);

            if (action.vfxType == ActionData.VfxType.Beam)
            {
                DisableRFX4Movement(vfx);
                CombatBeamVfx beam = vfx.AddComponent<CombatBeamVfx>();
                beam.Initialize(attacker, u, action, CreateImpactCallback(vfx, attacker, u, action));
                activeBeams.RemoveAll(item => item == null);
                activeBeams.Add(beam);
                ActivateCombatVfx(vfx, staging, action.vfxLifeTime);
                continue;
            }

            if (action.projectileMoveMode != ActionData.ProjectileMoveMode.RFX4Prefab)
                DisableRFX4Movement(vfx);
            else
                SetupRFX4Projectile(vfx, action);

            System.Action impact = CreateImpactCallback(vfx, attacker, u, action);
            if (action.projectileMoveMode == ActionData.ProjectileMoveMode.RFX4Prefab)
                BindRFX4Impact(vfx, u, action, impact);

            ActivateCombatVfx(vfx, staging, action.vfxLifeTime);

            switch (action.projectileMoveMode)
            {
                // -------------------------------------------------
                // 1. BAY THẲNG - game tự điều khiển transform
                // -------------------------------------------------
                case ActionData.ProjectileMoveMode.Straight:
                    {
                        StartCoroutine(MoveVFXRoutine(
                            vfx,
                            targetPos,
                            action.vfxSpeed,
                            impact, u, action.projectileTargetOffset));
                        break;
                    }

                // -------------------------------------------------
                // 2. GIỮ NGUYÊN CHUYỂN ĐỘNG CỦA PREFAB RFX4
                // -------------------------------------------------
                case ActionData.ProjectileMoveMode.RFX4Prefab:
                    {
                        break;
                    }

                // -------------------------------------------------
                // 3. BAY THEO ĐƯỜNG CONG BEZIER
                // -------------------------------------------------
                case ActionData.ProjectileMoveMode.BezierCurve:
                    {
                        StartCoroutine(MoveVFXBezierRoutine(
                            vfx,
                            targetPos,
                            action.vfxSpeed,
                            action.curveHeight,
                            action.curveSideOffset,
                            impact, u, action.projectileTargetOffset));
                        break;
                    }
            }
        }
    }

    private void ActivateCombatVfx(GameObject vfx, GameObject staging, float lifetime)
    {
        TrackCombatVFX(vfx);
        vfx.transform.SetParent(null, true);
        vfx.SetActive(true);
        Destroy(staging);
        Destroy(vfx, Mathf.Max(0.1f, lifetime));
    }

    private static bool UsesVfxImpactDamage(ActionData action)
    {
        return action != null &&
               action.vfxPrefab != null &&
               !action.isFriendlyAction &&
               !action.isHeal &&
               (action.vfxType == ActionData.VfxType.Shoot ||
                action.vfxType == ActionData.VfxType.Beam);
    }

    // Giữ tên hàm cũ cho các đoạn code kiểm tra Hit VFX.
    // Với Shoot/Beam tấn công, Hit VFX luôn đi theo impact thật của VFX.
    private static bool UsesVfxImpact(ActionData action)
    {
        return UsesVfxImpactDamage(action);
    }

    private System.Action CreateImpactCallback(
        GameObject vfx,
        BattleUnit attacker,
        BattleUnit target,
        ActionData action)
    {
        pendingVfxImpacts.Add(vfx);

        // Mỗi action/routine có một VfxActionState riêng.
        // AoE có nhiều projectile vẫn dùng chung raw damage / crit / parry.
        VfxActionState actionState = vfxActionState;

        bool completed = false;

        return () =>
        {
            // Một projectile có thể báo collision nhiều lần.
            // Chỉ được resolve impact đúng 1 lần.
            if (completed)
                return;

            completed = true;
            pendingVfxImpacts.Remove(vfx);

            if (attacker == null ||
                target == null ||
                action == null ||
                target.IsDead)
            {
                return;
            }

            // Chỉ Shoot/Beam offensive mới gây damage bằng impact callback.
            if (!UsesVfxImpactDamage(action))
                return;

            ResolveVfxImpactDamage(
                attacker,
                target,
                action,
                actionState
            );
        };
    }

    private void PrepareVfxImpactDamage(
        BattleUnit attacker,
        ActionData action,
        VfxActionState actionState)
    {
        if (attacker == null ||
            action == null ||
            actionState == null ||
            actionState.impactDamagePrepared)
        {
            return;
        }

        int totalAtk =
            attacker.baseAtk +
            attacker.GetBuffValue(ActionData.BuffStat.Atk);

        float calculatedDamage =
            (totalAtk * action.damageMultiplier) +
            action.power;

        int rawDamage =
            Mathf.RoundToInt(calculatedDamage);

        bool isCrit =
            !action.isFriendlyAction &&
            !action.isHeal &&
            UnityEngine.Random.Range(0, 100) <
            attacker.GetCritChance();

        if (isCrit)
        {
            rawDamage = Mathf.RoundToInt(
                rawDamage *
                attacker.GetCritDamageMultiplier()
            );
        }

        actionState.impactRawDamage = rawDamage;
        actionState.impactIsCrit = isCrit;
        actionState.impactDamagePrepared = true;
    }

    private void ResolveVfxImpactDamage(
        BattleUnit attacker,
        BattleUnit target,
        ActionData action,
        VfxActionState actionState)
    {
        if (attacker == null ||
            target == null ||
            action == null ||
            actionState == null ||
            target.IsDead)
        {
            return;
        }

        PrepareVfxImpactDamage(
            attacker,
            action,
            actionState
        );

        int rawDamage =
            actionState.impactRawDamage;

        bool isCrit =
            actionState.impactIsCrit;

        // ============================================================
        // PLAYER PROJECTILE / BEAM -> ENEMY
        // ============================================================
        if (attacker.isPlayer)
        {
            int hpBefore =
                target.currentHP;

            // DAMAGE xảy ra đúng lúc VFX impact.
            target.TakeDamage(
                rawDamage,
                false
            );

            int actualDamageTaken =
                hpBefore -
                target.currentHP;

            GetComponent<BattleResultPanel>()?
                .RecordDamage(
                    true,
                    actualDamageTaken
                );

            if (AdvancedUIManager.Instance != null)
            {
                AdvancedUIManager.Instance.ShowDamageText(
                    target.transform,
                    actualDamageTaken,
                    isCrit,
                    false
                );
            }

            // Thứ tự yêu cầu:
            // 1. Damage
            // 2. Hit VFX
            SpawnActionHitVFX(
                action,
                target
            );

            return;
        }

        // ============================================================
        // ENEMY PROJECTILE / BEAM -> PLAYER
        // ============================================================
        bool parried =
            actionState.parried ||
            (ParrySystem.Instance != null &&
             ParrySystem.Instance.parrySuccessful);

        // Giữ kết quả parry cho toàn bộ projectile của cùng một action/AoE.
        actionState.parried = parried;

        int playerHpBefore =
            target.currentHP;

        target.TakeDamage(
            rawDamage,
            parried
        );

        int playerDamageTaken =
            playerHpBefore -
            target.currentHP;

        GetComponent<BattleResultPanel>()?
            .RecordDamage(
                false,
                playerDamageTaken
            );

        if (!parried)
        {
            if (AdvancedUIManager.Instance != null)
            {
                AdvancedUIManager.Instance.ShowDamageText(
                    target.transform,
                    playerDamageTaken,
                    isCrit,
                    false
                );
            }

            // Chỉ chạy Hit VFX khi đòn thật sự trúng và không bị Parry.
            SpawnActionHitVFX(
                action,
                target
            );
        }
    }

    private void BindRFX4Impact(GameObject vfx, BattleUnit target, ActionData action, System.Action impact)
    {
        System.EventHandler<RFX4_PhysicsMotion.RFX4_CollisionInfo> onCollision = (sender, info) =>
        {
            BattleUnit hitUnit = info.HitCollider != null
                ? info.HitCollider.GetComponentInParent<BattleUnit>() : null;
            if (hitUnit == target) impact();
        };
        foreach (RFX4_PhysicsMotion motion in vfx.GetComponentsInChildren<RFX4_PhysicsMotion>(true))
        {
            motion.CollisionEnter += onCollision;
            // Combat owns the configured hit effect: do not spawn a second
            // copy from the vendor's collision handler.
            if (action.hitVfxPrefab != null) motion.EffectOnCollision = null;
        }
        foreach (RFX4_RaycastCollision ray in vfx.GetComponentsInChildren<RFX4_RaycastCollision>(true))
        {
            ray.CollisionEnter += onCollision;
            if (action.hitVfxPrefab != null) ray.Effects = new GameObject[0];
        }
    }

    /// <summary>
    /// Tắt CHỈ phần movement/collision tự động của RFX4 khi CombatManager
    /// cần tự điều khiển projectile. ParticleTrail, LightCurves, shader... vẫn chạy.
    /// </summary>
    private void DisableRFX4Movement(GameObject vfx)
    {
        if (vfx == null) return;

        RFX4_PhysicsMotion[] motions = vfx.GetComponentsInChildren<RFX4_PhysicsMotion>(true);

        foreach (RFX4_PhysicsMotion motion in motions)
        {
            if (motion == null) continue;

            // RFX4_PhysicsMotion.OnDisable() reset local transform,
            // nên lưu lại và phục hồi ngay sau khi disable.
            Transform motionTransform = motion.transform;
            Vector3 savedLocalPosition = motionTransform.localPosition;
            Quaternion savedLocalRotation = motionTransform.localRotation;
            Vector3 savedLocalScale = motionTransform.localScale;

            motion.enabled = false;

            motionTransform.localPosition = savedLocalPosition;
            motionTransform.localRotation = savedLocalRotation;
            motionTransform.localScale = savedLocalScale;
        }

        // Không tham chiếu trực tiếp type để đoạn này vẫn dùng được với
        // các phiên bản RFX4 khác nhau. Chỉ disable script đúng tên.
        MonoBehaviour[] scripts = vfx.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null) continue;

            if (script.GetType().Name == "RFX4_RaycastCollision")
            {
                script.enabled = false;
            }
        }
    }

    /// <summary>
    /// Cho prefab RFX4 tự bay bằng RFX4_PhysicsMotion.
    /// CombatManager chỉ truyền Speed/Gravity và lifetime.
    /// </summary>
    private void SetupRFX4Projectile(GameObject vfx, ActionData action)
    {
        if (vfx == null || action == null) return;

        RFX4_EffectSettings settings = vfx.GetComponentInChildren<RFX4_EffectSettings>(true);

        if (settings != null)
        {
            settings.Speed = action.vfxSpeed;
            settings.UseGravity = action.rfxUseGravity;
        }

        // BindRFX4Impact owns collision callbacks and prevents duplicate hits.

        // RFX4_PhysicsMotion không tự Destroy root projectile sau collision.
        Destroy(vfx, action.vfxLifeTime);
    }

    public void PlayCastVFXFromAnimation(BattleUnit attacker)
    {
        if (attacker == null || attacker.IsDead || castStarted.Contains(attacker) || vfxLaunched.Contains(attacker)) return;
        ActionData currentAction = attacker.isPlayer
            ? selectedAction
            : (selectedAction != null ? selectedAction : attacker.defaultAttack);

        if (currentAction != null && currentAction.castVfxPrefab != null)
        {
            castStarted.Add(attacker);
            var instances = new List<GameObject>(2);
            castInstances[attacker] = instances;
            if (attacker.handTransform != null)
                instances.Add(SpawnCastVFXAtPoint(attacker.handTransform, currentAction));
            if (attacker.leftHandTransform != null && attacker.leftHandTransform != attacker.handTransform)
                instances.Add(SpawnCastVFXAtPoint(attacker.leftHandTransform, currentAction));
            if (instances.Count == 0)
                instances.Add(SpawnCastVFXAtPoint(attacker.transform, currentAction));
        }
    }

    private GameObject SpawnCastVFXAtPoint(Transform spawnPoint, ActionData currentAction)
    {
        GameObject staging = new GameObject("Combat Cast Setup");
        staging.SetActive(false);

        GameObject castVfx = Instantiate(
            currentAction.castVfxPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            staging.transform);

        castVfx.SetActive(false);
        if (currentAction.vfxType == ActionData.VfxType.Beam)
        {
            RFX4_PlaybackSpeed playback = castVfx.GetComponent<RFX4_PlaybackSpeed>();
            if (playback == null) playback = castVfx.AddComponent<RFX4_PlaybackSpeed>();
            playback.playbackSpeed = Mathf.Max(0.01f, currentAction.beamPlaybackSpeed);
        }
        castVfx.transform.SetParent(spawnPoint, true);
        TrackCombatVFX(castVfx);
        castVfx.SetActive(true);
        Destroy(staging);

        Destroy(castVfx, GetVfxLifeTime(castVfx, currentAction.castVfxLifeTime));
        return castVfx;
    }

    private void FinishCastVFX(BattleUnit attacker)
    {
        if (!castInstances.TryGetValue(attacker, out List<GameObject> instances)) return;
        // Stop ongoing charging, allowing one-shot flashes and existing
        // particles to finish instead of cutting them at an arbitrary 1.5 s.
        foreach (GameObject cast in instances)
        {
            if (cast == null) continue;
            foreach (ParticleSystem particles in cast.GetComponentsInChildren<ParticleSystem>(true))
                if (particles.main.loop) particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        castInstances.Remove(attacker);
    }

    public void PlayVFXFromAnimation(BattleUnit attacker)
    {
        if (attacker == null || attacker.IsDead) return;

        ActionData currentAction = attacker.isPlayer
            ? selectedAction
            : (selectedAction != null ? selectedAction : attacker.defaultAttack);

        if (currentAction != null && currentAction.vfxPrefab != null)
        {
            // Handle clips with a missing or later cast event without playing
            // the cast again after the projectile has already launched.
            PlayCastVFXFromAnimation(attacker);
            vfxLaunched.Add(attacker);
            FinishCastVFX(attacker);
            PlayVFX(attacker, currentTarget, currentAction);
        }
    }

    /// <summary>
    /// Projectile bay thẳng. Dùng cho prefab không cần RFX4_PhysicsMotion.
    /// </summary>
    private IEnumerator MoveVFXRoutine(
        GameObject vfx,
        Vector3 targetPos,
        float speed,
        System.Action onImpact, BattleUnit target, Vector3 targetOffset)
    {
        if (vfx == null) yield break;

        speed = Mathf.Max(0.01f, speed);

        while (vfx != null)
        {
            if (target != null) targetPos = target.GetVfxTargetPosition(targetOffset);
            if (Vector3.Distance(vfx.transform.position, targetPos) <= 0.1f) break;
            Vector3 oldPosition = vfx.transform.position;
            Vector3 newPosition = Vector3.MoveTowards(
                oldPosition,
                targetPos,
                speed * Time.deltaTime);

            vfx.transform.position = newPosition;

            Vector3 moveDirection = newPosition - oldPosition;
            if (moveDirection.sqrMagnitude > 0.000001f)
            {
                vfx.transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);
            }

            if (Vector3.Distance(newPosition, targetPos) <= 0.1f) break;
            yield return null;
        }

        if (vfx != null)
        {
            vfx.transform.position = targetPos;
            onImpact?.Invoke();
            Destroy(vfx);
        }

    }

    /// <summary>
    /// Projectile bay theo Quadratic Bezier:
    /// start -> control point -> target.
    /// </summary>
    private IEnumerator MoveVFXBezierRoutine(
        GameObject vfx,
        Vector3 targetPos,
        float speed,
        float curveHeight,
        float curveSideOffset,
        System.Action onImpact, BattleUnit target, Vector3 targetOffset)
    {
        if (vfx == null) yield break;

        Vector3 startPos = vfx.transform.position;
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / Mathf.Max(speed, 0.01f);
        duration = Mathf.Max(duration, 0.05f);

        Vector3 forward = targetPos - startPos;
        Vector3 side = Vector3.Cross(Vector3.up, forward.normalized);

        if (side.sqrMagnitude < 0.0001f)
        {
            side = vfx.transform.right;
        }

        side.Normalize();

        Vector3 controlPoint =
            (startPos + targetPos) * 0.5f
            + Vector3.up * curveHeight
            + side * curveSideOffset;

        float elapsed = 0f;

        while (vfx != null && elapsed < duration)
        {
            if (target != null) targetPos = target.GetVfxTargetPosition(targetOffset);
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 position = EvaluateQuadraticBezier(
                startPos,
                controlPoint,
                targetPos,
                t);

            // Đạo hàm Bezier để projectile luôn nhìn đúng hướng đường cong.
            Vector3 tangent =
                2f * (1f - t) * (controlPoint - startPos)
                + 2f * t * (targetPos - controlPoint);

            vfx.transform.position = position;

            if (tangent.sqrMagnitude > 0.000001f)
            {
                vfx.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            }

            if (t >= 1f) break;
            yield return null;
        }

        if (vfx != null)
        {
            vfx.transform.position = targetPos;
            onImpact?.Invoke();
            Destroy(vfx);
        }

    }

    private Vector3 EvaluateQuadraticBezier(
        Vector3 start,
        Vector3 control,
        Vector3 end,
        float t)
    {
        float oneMinusT = 1f - t;

        return
            oneMinusT * oneMinusT * start
            + 2f * oneMinusT * t * control
            + t * t * end;
    }

    private void SpawnActionHitVFX(ActionData action, BattleUnit target)
    {
        if (action == null || target == null || action.hitVfxPrefab == null) return;

        // Dùng cùng offset với điểm đích của projectile để VFX nằm đúng vị trí va chạm.
        Vector3 hitPosition = target.transform.position + action.projectileTargetOffset;
        SpawnHitVFX(action.hitVfxPrefab, hitPosition, action.hitVfxLifeTime);
    }

    private void SpawnHitVFX(GameObject hitPrefab, Vector3 position, float lifetime)
    {
        if (hitPrefab == null) return;

        GameObject hitVfx = Instantiate(hitPrefab, position, Quaternion.identity);
        TrackCombatVFX(hitVfx);
        Destroy(hitVfx, GetVfxLifeTime(hitVfx, lifetime));
    }

    private static float GetVfxLifeTime(GameObject vfx, float minimumLifetime)
    {
        float lifetime = Mathf.Max(0.1f, minimumLifetime);
        foreach (ParticleSystem particles in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            if (main.loop) continue; // Looping effects use the configured timeout.
            float duration = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
            lifetime = Mathf.Max(lifetime, duration / Mathf.Max(0.01f, main.simulationSpeed));
        }
        return lifetime;
    }

    public void EndCurrentTurn()
    {
        if (isTransitioningTurn) return;
        isTransitioningTurn = true;
        state = CombatState.Executing;
        turnTransitionRoutine = StartCoroutine(FinishTurnAfterVfx());
    }

    private IEnumerator FinishTurnAfterVfx()
    {
        // Keep the outgoing turn's effects visible before clearing them.
        // The next unit cannot create new effects until this cleanup finishes.
        yield return new WaitForSeconds(Mathf.Max(0f, turnVfxCleanupDelay));
        ClearCombatVFX();
        turnTransitionRoutine = null;

        SetupPositions();
        AdvancedUIManager.Instance.RemoveFirstPortrait();
        currentTimelineIndex++;

        if (currentTimelineIndex >= allUnitsTimeline.Count)
        {
            DetermineTurnOrder();
        }
        else
        {
            StartNextUnitTurn();
        }
    }

    public void SetTargetUnit(BattleUnit newTarget)
    {
        if (state != CombatState.PlayerTurn || currentActiveUnit == null || currentActiveUnit.IsDead) return;

        if (newTarget != null && newTarget.currentHP > 0)
        {
            if (isSelectingSelfOnly && newTarget != currentActiveUnit)
            {
                Debug.LogWarning("[Chọn mục tiêu] Skill này chỉ có thể buff cho bản thân!");
                return;
            }

            if (isSelectingBuffTarget && !newTarget.isPlayer) return;
            if (!isSelectingBuffTarget && newTarget.isPlayer) return;

            currentTarget = newTarget;
            AdvancedUIManager.Instance.UpdateTargetUI(currentTarget.unitName);

            if (selectedAction != null)
            {
                ConfirmSelectedTarget();
            }
        }
    }

    public void AddStain(int amount)
    {
        currentStains = Mathf.Clamp(currentStains + amount, 0, maxStains);
        AdvancedUIManager.Instance.UpdateStainsUI(currentStains, 0);
    }
}
