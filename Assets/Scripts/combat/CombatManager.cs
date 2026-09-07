using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CombatState { Start, PlayerTurn, EnemyTurn, Executing, Won, Lost }
public enum OpenMenuType { None, Skills, Items }

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance;

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
    public Transform[] playerMeleeSlots; // Vị trí dành cho Quái nhảy tới áp sát Player
    public Transform[] enemyMeleeSlots;  // Vị trí dành cho Player nhảy tới áp sát Quái

    [HideInInspector] public bool isSelectingBuffTarget = false;
    private bool isTransitioningTurn = false;

    // --- BIẾN MỚI: Cờ đánh dấu Anim tấn công đã xong chưa ---
    [HideInInspector] public bool isAttackAnimationFinished = false;

    private OpenMenuType currentOpenMenu = OpenMenuType.None;

    void Awake() { Instance = this; }

    void Start()
    {
        state = CombatState.Start;
        SetupPositions();

        if (enemyParty.Count > 0 && enemyParty[0] != null)
        {
            AdvancedUIManager.Instance.RegisterEnemyHP(enemyParty[0]);
        }
        AdvancedUIManager.Instance.RegisterPartyHP(playerParty);

        DetermineTurnOrder();
    }

    void SetupPositions()
    {
        for (int i = 0; i < playerParty.Count; i++)
        {
            if (i < playerSlots.Length && playerParty[i] != null)
            {
                playerParty[i].transform.position = playerSlots[i].position;
                playerParty[i].transform.rotation = playerSlots[i].rotation;
            }
        }
        for (int i = 0; i < enemyParty.Count; i++)
        {
            if (i < enemySlots.Length && enemyParty[i] != null)
            {
                enemyParty[i].transform.position = enemySlots[i].position;
                enemyParty[i].transform.rotation = enemySlots[i].rotation;
            }
        }
    }

    public void DetermineTurnOrder()
    {
        // --- TICK GIẢM THỜI GIAN BUFF KHI BẮT ĐẦU MỘT VÒNG MỚI (NEW WAVE) ---
        // Chỉ chạy tick nếu đây không phải là lượt khởi tạo đầu tiên của trận đấu (tránh trừ ngay turn 1)
        if (state != CombatState.Start)
        {
            foreach (var unit in playerParty.Concat(enemyParty).Where(u => u != null && u.currentHP > 0))
            {
                unit.TickBuffs();
            }
        }

        allUnitsTimeline.Clear();
        allUnitsTimeline.AddRange(playerParty.Where(u => u.currentHP > 0));
        allUnitsTimeline.AddRange(enemyParty.Where(u => u.currentHP > 0));

        allUnitsTimeline = allUnitsTimeline.OrderByDescending(u => u.speed).ToList();

        AdvancedUIManager.Instance.UpdateTurnOrderUI(allUnitsTimeline);

        currentTimelineIndex = 0;
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

    void StartNextUnitTurn()
    {
        isTransitioningTurn = false;
        selectedAction = null;
        isSelectingBuffTarget = false;
        currentTarget = null;
        currentOpenMenu = OpenMenuType.None;
        AdvancedUIManager.Instance.UpdateTargetUI("");

        if (enemyParty.All(e => e.currentHP <= 0)) { state = CombatState.Won; Debug.Log("WIN!"); return; }
        if (playerParty.All(p => p.currentHP <= 0)) { state = CombatState.Lost; Debug.Log("LOSE!"); return; }

        currentActiveUnit = allUnitsTimeline[currentTimelineIndex];

        // Reset các hiệu ứng menu cũ
        ResetMenuAnimations();

        if (currentActiveUnit.isPlayer)
        {
            state = CombatState.PlayerTurn;
            int playerIndex = playerParty.IndexOf(currentActiveUnit);
            CameraManager.Instance.SwitchToPlayerTurnCam(playerIndex);
            AdvancedUIManager.Instance.PositionActionMenu(currentActiveUnit.transform);
            AdvancedUIManager.Instance.ShowActionMenu(true);
            currentTarget = enemyParty.FirstOrDefault(e => e.currentHP > 0);
        }
        else
        {
            state = CombatState.EnemyTurn;
            AdvancedUIManager.Instance.ShowActionMenu(false);
            StartCoroutine(EnemyAICore());
        }
    }
    public void OnAttackClicked()
    {
        if (state != CombatState.PlayerTurn) return;

        ResetMenuAnimations();
        currentOpenMenu = OpenMenuType.None;

        isSelectingBuffTarget = false;
        selectedAction = currentActiveUnit.defaultAttack;

        if (currentTarget == null || currentTarget.currentHP <= 0 || currentTarget.isPlayer)
        {
            currentTarget = enemyParty.FirstOrDefault(e => e.currentHP > 0);
        }

        if (currentTarget != null)
        {
            AdvancedUIManager.Instance.UpdateTargetUI(currentTarget.unitName);
            Debug.Log($"[Hành động] Người chơi {currentActiveUnit.unitName} tấn công mục tiêu: {currentTarget.unitName}");

            AdvancedUIManager.Instance.ShowActionMenu(false);
            OnActionSelected(selectedAction);
        }
        else
        {
            Debug.LogWarning("[Lỗi] Không còn kẻ địch nào sống sót!");
        }
    }

    public void OnSkillsClicked()
    {
        if (state != CombatState.PlayerTurn) return;

        if (currentOpenMenu == OpenMenuType.Skills)
        {
            currentOpenMenu = OpenMenuType.None;
            ResetMenuAnimations();
            AdvancedUIManager.Instance.subMenuPanel.SetActive(false);
            Debug.Log($"[Hành động] Đóng Menu Kỹ năng");
            return;
        }

        currentOpenMenu = OpenMenuType.Skills;
        selectedAction = null;
        isSelectingBuffTarget = false;

        ResetMenuAnimations();
        if (currentActiveUnit != null && currentActiveUnit.animator != null)
        {
            currentActiveUnit.animator.SetBool("IsSkillIdle", true);
        }

        Debug.Log($"[Hành động] Người chơi {currentActiveUnit.unitName} mở Menu Kỹ năng (Skills)");
        AdvancedUIManager.Instance.PopulateSubMenu(currentActiveUnit.characterSkills);
    }

    public void OnItemsClicked()
    {
        if (state != CombatState.PlayerTurn) return;

        if (currentOpenMenu == OpenMenuType.Items)
        {
            currentOpenMenu = OpenMenuType.None;
            ResetMenuAnimations();
            AdvancedUIManager.Instance.subMenuPanel.SetActive(false);
            Debug.Log($"[Hành động] Đóng Túi đồ");
            return;
        }

        currentOpenMenu = OpenMenuType.Items;
        selectedAction = null;
        isSelectingBuffTarget = false;

        ResetMenuAnimations();
        if (currentActiveUnit != null && currentActiveUnit.animator != null)
        {
            currentActiveUnit.animator.SetBool("IsItemIdle", true);
        }

        Debug.Log($"[Hành động] Người chơi {currentActiveUnit.unitName} mở Túi đồ (Items)");
        AdvancedUIManager.Instance.PopulateSubMenu(inventoryItems);
    }

    public void OnSkillButtonClicked(ActionData skillAction)
    {
        if (state != CombatState.PlayerTurn) return;

        selectedAction = skillAction;
        isSelectingBuffTarget = skillAction.isFriendlyAction;

        if (isSelectingBuffTarget)
        {
            currentTarget = currentActiveUnit;
            Debug.Log($"[Chọn Skill Buff] {skillAction.actionName} - Đang nhắm vào bản thân.");
        }
        else
        {
            currentTarget = enemyParty.FirstOrDefault(e => e.currentHP > 0);
            Debug.Log($"[Chọn Skill Tấn Công] {skillAction.actionName} - Hãy click vào kẻ địch trên màn hình.");
        }

        if (currentTarget != null)
        {
            AdvancedUIManager.Instance.UpdateTargetUI(currentTarget.unitName);
        }

        AdvancedUIManager.Instance.ShowActionMenu(false);
    }

    public void OnActionSelected(ActionData action)
    {
        selectedAction = action;
        currentOpenMenu = OpenMenuType.None;

        ResetMenuAnimations();

        if (action != null)
        {
            Debug.Log($"[Hành động] {currentActiveUnit.unitName} THỰC THI {action.type}: '{action.actionName}' lên {currentTarget?.unitName}");
        }

        AdvancedUIManager.Instance.ShowActionMenu(false);
        AdvancedUIManager.Instance.UpdateTargetUI("");

        StartCoroutine(ExecuteActionRoutine(currentActiveUnit, currentTarget, selectedAction));
    }

    private void ExecuteHealAndEndTurn()
    {
        if (currentTarget != null && selectedAction != null)
        {
            currentTarget.Heal(selectedAction.power);
            Debug.Log($"[Buff Thành Công] {currentActiveUnit.unitName} hồi {selectedAction.power} HP cho {currentTarget.unitName}");
        }

        EndCurrentTurn();
    }

    System.Collections.IEnumerator EnemyAICore()
    {
        Debug.Log($"[AI] Quái {currentActiveUnit.unitName} đang suy nghĩ...");
        yield return new WaitForSeconds(0.5f);

        List<BattleUnit> livePlayers = playerParty.Where(p => p.currentHP > 0).ToList();
        if (livePlayers.Count == 0) yield break;

        currentTarget = livePlayers[Random.Range(0, livePlayers.Count)];
        selectedAction = currentActiveUnit.defaultAttack != null ? currentActiveUnit.defaultAttack : currentActiveUnit.characterSkills.FirstOrDefault();

        Debug.Log($"[AI] Quái {currentActiveUnit.unitName} quyết định TẤN CÔNG {currentTarget.unitName}!");
        CameraManager.Instance.SwitchToTargetHitCam(currentTarget);

        yield return new WaitForSeconds(0.5f);

        StartCoroutine(ExecuteActionRoutine(currentActiveUnit, currentTarget, selectedAction));
    }
    public Vector3 GetMeleeAttackPosition(BattleUnit attacker, BattleUnit target)
    {
        if (target == null) return attacker.transform.position;

        // 1. Tính hướng từ Attacker nhìn về Target
        Vector3 directionToTarget = (target.transform.position - attacker.transform.position).normalized;

        // 2. Vị trí dừng lại = Vị trí Target trừ đi một khoảng offset (1.6m trước mặt Target)
        float stopDistance = 1.6f;
        Vector3 attackPosition = target.transform.position - (directionToTarget * stopDistance);

        return attackPosition;
    }

    private IEnumerator ExecuteActionRoutine(BattleUnit attacker, BattleUnit target, ActionData action)
    {
        state = CombatState.Executing;

        if (action != null && action.type == ActionData.ActionType.Skill)
            AddStain(1);

        // Lưu vị trí ban đầu chuẩn xác của nhân vật
        Vector3 originalPosition = attacker.transform.position;
        isAttackAnimationFinished = false;

        // Tắt Root Motion để Animator không tự làm xê dịch Transform
        if (attacker.animator != null)
        {
            attacker.animator.applyRootMotion = false;
        }

        // --- TRƯỜNG HỢP 1: CẬN CHIẾN (MELEE) ---
        if (action != null && action.isMelee && target != null && !action.isFriendlyAction && !action.isHeal)
        {
            // 1. Lấy vị trí áp sát chính xác dựa trên Target hiện tại
            Vector3 attackPosition = GetMeleeAttackPosition(attacker, target);

            // 2. Nhảy tới vị trí mục tiêu
            attacker.animator.Play("JumpForward");
            yield return StartCoroutine(MoveToPosition(attacker.transform, attackPosition, 0.35f));

            // 3. Thực hiện Animation Tấn công
            string animTrigger = !string.IsNullOrEmpty(action.animationTriggerName) ? action.animationTriggerName : "Attack";
            attacker.animator.SetTrigger(animTrigger);

            // Chờ Animation đánh xong (Animation Event gửi cờ isAttackAnimationFinished = true)
            yield return new WaitUntil(() => isAttackAnimationFinished);

            // 4. Nhảy về đúng vị trí ban đầu (Thời gian 0.35s khớp với lượt đi)
            attacker.animator.Play("JumpBack");
            yield return StartCoroutine(MoveToPosition(attacker.transform, originalPosition, 0.35f));
        }
        // --- TRƯỜNG HỢP 2: ĐÁNH XA / BUFF ---
        else
        {
            string animTrigger = action != null ? action.animationTriggerName : "Attack";
            attacker.animator.SetTrigger(animTrigger);

            yield return new WaitUntil(() => isAttackAnimationFinished);
        }

        // 5. Khôi phục vị trí tuyệt đối và đưa Animator về Idle
        attacker.transform.position = originalPosition;

        if (attacker.animator != null)
        {
            attacker.animator.CrossFade("Idle", 0.1f);
        }

        EndCurrentTurn();
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
        // 1. Lấy action đang thực thi
        ActionData actionToUse = selectedAction != null ? selectedAction : attacker.defaultAttack;
        if (actionToUse == null) return;

        // 2. Tính toán tổng chỉ số tấn công (Base ATK + Buff ATK)
        int totalAtk = attacker.baseAtk + attacker.GetBuffValue(ActionData.BuffStat.Atk);

        // 3. Công thức tính sát thương riêng biệt scale theo kỹ năng:
        // Công thức: (ATK tổng * Hệ số scale của skill) + Power cố định của skill
        float calculatedDamage = (totalAtk * actionToUse.damageMultiplier) + actionToUse.power;
        int rawDamage = Mathf.RoundToInt(calculatedDamage);

        // 4. Tính toán chí mạng (Crit)
        int critChance = attacker.baseCrit + attacker.GetBuffValue(ActionData.BuffStat.Crit);
        bool isCrit = UnityEngine.Random.Range(0, 100) < critChance;

        if (isCrit && !actionToUse.isFriendlyAction)
        {
            rawDamage = Mathf.RoundToInt(rawDamage * 1.5f); // Chí mạng nhân 1.5 lần sát thương
            Debug.Log($"<color=orange>CHÍ MẠNG!</color>");
        }

        // 5. Thực thi gây sát thương hoặc hiệu ứng
        if (attacker.isPlayer)
        {
            if (currentTarget == null) return;

            if (actionToUse.isFriendlyAction || actionToUse.isHeal)
            {
                List<BattleUnit> targets = actionToUse.isAoE ? playerParty.Where(u => u.currentHP > 0).ToList() : new List<BattleUnit> { currentTarget };

                foreach (var ally in targets)
                {
                    if (actionToUse.isHeal) ally.Heal(rawDamage > 0 ? rawDamage : actionToUse.power); // Hồi máu có thể tận dụng power hoặc scale nếu muốn
                    if (actionToUse.buffStat != ActionData.BuffStat.None)
                    {
                        ally.AddBuff(actionToUse.buffStat, actionToUse.buffAmount, actionToUse.buffDuration);
                        Debug.Log($"[Buff] {attacker.unitName} tăng {actionToUse.buffStat} cho {ally.unitName} ({actionToUse.buffDuration} wave).");
                    }
                }
            }
            else // Tấn công kẻ địch
            {
                List<BattleUnit> targets = actionToUse.isAoE ? enemyParty.Where(u => u.currentHP > 0).ToList() : new List<BattleUnit> { currentTarget };

                foreach (var enemy in targets)
                {
                    enemy.TakeDamage(rawDamage, false);
                }
                Debug.Log($"[Tấn Công] {attacker.unitName} dùng {actionToUse.actionName} gây {rawDamage} DMG.");
            }
        }
        else // Enemy tấn công Player
        {
            bool parried = ParrySystem.Instance.parrySuccessful;
            List<BattleUnit> targets = actionToUse.isAoE ? playerParty.Where(u => u.currentHP > 0).ToList() : new List<BattleUnit> { currentTarget };

            foreach (var ally in targets)
            {
                ally.TakeDamage(rawDamage, parried);
            }
            Debug.Log($"[Enemy Đánh] {attacker.unitName} gây {rawDamage} DMG. (Parry: {parried})");
            ParrySystem.Instance.ResetParryState();
        }
    }

    private void PlayVFX(BattleUnit attacker, BattleUnit target, ActionData action)
    {
        if (action.vfxPrefab == null || target == null) return;

        // --- GOM DANH SÁCH MỤC TIÊU ---
        List<BattleUnit> targetList = new List<BattleUnit>();
        if (action.isAoE)
        {
            // Nếu là AoE, lấy list toàn bộ phe tương ứng đang còn sống
            targetList = (action.isFriendlyAction || action.isHeal) ? playerParty : enemyParty;
        }
        else
        {
            // Nếu đơn mục tiêu, chỉ ép target vào list
            targetList.Add(target);
        }

        // --- SPAWN VFX CHO TỪNG NGƯỜI TRONG LIST ---
        if (action.vfxType == ActionData.VfxType.SpawnAtTarget)
        {
            foreach (var u in targetList.Where(u => u.currentHP > 0))
            {
                GameObject vfx = Instantiate(action.vfxPrefab, u.transform.position, u.transform.rotation);
                Destroy(vfx, 2f);
            }
        }
        else if (action.vfxType == ActionData.VfxType.Shoot)
        {
            Vector3 startPos = attacker.handTransform != null ? attacker.handTransform.position : attacker.transform.position + Vector3.up * 1f;

            foreach (var u in targetList.Where(u => u.currentHP > 0))
            {
                Vector3 targetPos = u.transform.position + Vector3.up * 1f;
                Vector3 directionToTarget = targetPos - startPos;
                Quaternion rotation = directionToTarget != Vector3.zero ? Quaternion.LookRotation(directionToTarget) : Quaternion.identity;

                GameObject vfx = Instantiate(action.vfxPrefab, startPos, rotation);

                RFX4_EffectSettings rfxSettings = vfx.GetComponent<RFX4_EffectSettings>();
                if (rfxSettings != null) rfxSettings.UseGravity = false;

                MonoBehaviour[] allScripts = vfx.GetComponentsInChildren<MonoBehaviour>();
                foreach (MonoBehaviour script in allScripts)
                {
                    if (script.GetType().Name == "RFX4_PhysicsMotion" || script.GetType().Name == "RFX4_RaycastCollision")
                    {
                        Destroy(script);
                    }
                }

                // Chạy Coroutine bắn ra nhiều tia/cầu lửa cùng lúc tới các mục tiêu
                StartCoroutine(MoveVFXRoutine(vfx, targetPos, action.vfxSpeed, action.hitVfxPrefab));
            }
        }
    }
    // --- HÀM MỚI: TẠO VFX TRÊN TAY ---
    public void PlayCastVFXFromAnimation(BattleUnit attacker)
    {
        ActionData currentAction = attacker.isPlayer ? selectedAction : (selectedAction != null ? selectedAction : attacker.defaultAttack);

        if (currentAction != null && currentAction.castVfxPrefab != null)
        {
            // Tìm vị trí tay
            Transform spawnPoint = attacker.handTransform != null ? attacker.handTransform : attacker.transform;

            // Instantiate làm con của tay (spawnPoint) để di chuyển theo animation giống ActivateCharacterEffect của RFX4
            GameObject castVfx = Instantiate(currentAction.castVfxPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);

            // RFX4 thường tự hủy dựa trên EffectSettings, nhưng dự phòng hủy sau 1.5s
            Destroy(castVfx, 1.5f);
        }
    }
    public void PlayVFXFromAnimation(BattleUnit attacker)
    {
        ActionData currentAction = attacker.isPlayer ? selectedAction : (selectedAction != null ? selectedAction : attacker.defaultAttack);
        if (currentAction != null && currentAction.vfxPrefab != null)
        {
            PlayVFX(attacker, currentTarget, currentAction);
        }
    }
  
    // --- CẬP NHẬT: Nhận thêm tham số hitPrefab ---
    private IEnumerator MoveVFXRoutine(GameObject vfx, Vector3 targetPos, float speed, GameObject hitPrefab)
    {
        while (vfx != null && Vector3.Distance(vfx.transform.position, targetPos) > 0.1f)
        {
            vfx.transform.position = Vector3.MoveTowards(vfx.transform.position, targetPos, speed * Time.deltaTime);
            vfx.transform.LookAt(targetPos);
            yield return null;
        }

        if (vfx != null)
        {
            Destroy(vfx);
        }

        // --- SPAWN HIỆU ỨNG NỔ TẠI ĐÂY KHI ĐÃ ĐẾN NƠI ---
        if (hitPrefab != null)
        {
            // Tạo hiệu ứng nổ tại vị trí kẻ địch (targetPos)
            GameObject hitVfx = Instantiate(hitPrefab, targetPos, Quaternion.identity);

            // Xóa hiệu ứng nổ sau 2 giây (có thể tùy chỉnh lại nếu VFX nổ dài hơn)
            Destroy(hitVfx, 2f);
        }
    }
    public void EndCurrentTurn()
    {
        if (isTransitioningTurn) return;
        isTransitioningTurn = true;

        SetupPositions();
        AdvancedUIManager.Instance.RemoveFirstPortrait();
        currentTimelineIndex++;

        if (currentTimelineIndex >= allUnitsTimeline.Count)
        {
            DetermineTurnOrder(); // Khi chạy hết hàng đợi, tự động bắt đầu vòng mới và trừ duration ở đây!
        }
        else
        {
            StartNextUnitTurn();
        }
    }

    public void SetTargetUnit(BattleUnit newTarget)
    {
        if (state != CombatState.PlayerTurn) return;

        if (newTarget != null && newTarget.currentHP > 0)
        {
            if (isSelectingBuffTarget && !newTarget.isPlayer) return;
            if (!isSelectingBuffTarget && newTarget.isPlayer) return;

            currentTarget = newTarget;
            AdvancedUIManager.Instance.UpdateTargetUI(currentTarget.unitName);

            if (selectedAction != null)
            {
                OnActionSelected(selectedAction);
            }
        }
    }

    public void AddStain(int amount)
    {
        currentStains = Mathf.Clamp(currentStains + amount, 0, maxStains);
        AdvancedUIManager.Instance.UpdateStainsUI(currentStains);
    }
}