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
    public Transform[] playerMeleeSlots;
    public Transform[] enemyMeleeSlots;

    [HideInInspector] public bool isSelectingBuffTarget = false;
    [HideInInspector] public bool isSelectingSelfOnly = false;

    private bool isTransitioningTurn = false;
    [HideInInspector] public bool isAttackAnimationFinished = false;

    private OpenMenuType currentOpenMenu = OpenMenuType.None;

    void Awake() { Instance = this; }

    void Start()
    {
        state = CombatState.Start;
        SetupPositions();

        // --- MỚI: Khởi đầu Game với Full Stain ---
        currentStains = maxStains;
        AdvancedUIManager.Instance.UpdateStainsUI(currentStains, 0);

        if (enemyParty.Count > 0 && enemyParty[0] != null)
        {
            AdvancedUIManager.Instance.RegisterEnemyHP(enemyParty[0]);
        }
        AdvancedUIManager.Instance.RegisterPartyHP(playerParty);

        DetermineTurnOrder();
    }

    void Update()
    {
        if (state != CombatState.PlayerTurn) return;

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
        OnActionSelected(selectedAction);
    }

    private void CancelCurrentAction()
    {
        if (state != CombatState.PlayerTurn) return;

        if (selectedAction != null)
        {
            CameraManager.Instance.ResetTargetCam();

            selectedAction = null;
            isSelectingBuffTarget = false;
            isSelectingSelfOnly = false;

            // Xóa hiệu ứng xem trước (Preview) mờ mờ trên thanh Stain
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

            AdvancedUIManager.Instance.ShowActionMenu(true);
            if (AdvancedUIManager.Instance.subMenuPanel != null)
            {
                AdvancedUIManager.Instance.subMenuPanel.SetActive(false);
            }

            currentOpenMenu = OpenMenuType.None;
            ResetMenuAnimations();

            return;
        }

        if (currentOpenMenu != OpenMenuType.None)
        {
            currentOpenMenu = OpenMenuType.None;
            ResetMenuAnimations();
            if (AdvancedUIManager.Instance.subMenuPanel != null)
                AdvancedUIManager.Instance.subMenuPanel.SetActive(false);
        }
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
        isSelectingSelfOnly = false;
        currentTarget = null;
        currentOpenMenu = OpenMenuType.None;
        AdvancedUIManager.Instance.UpdateTargetUI("");

        // Đảm bảo clear UI preview của turn trước
        AdvancedUIManager.Instance.UpdateStainsUI(currentStains, 0);

        if (enemyParty.All(e => e.currentHP <= 0)) { state = CombatState.Won; Debug.Log("WIN!"); return; }
        if (playerParty.All(p => p.currentHP <= 0)) { state = CombatState.Lost; Debug.Log("LOSE!"); return; }

        currentActiveUnit = allUnitsTimeline[currentTimelineIndex];

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

        ActionData attackAction = currentActiveUnit.defaultAttack;

        // KIỂM TRA: Đề phòng trường hợp Action đánh thường bị set là tốn năng lượng
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
            CameraManager.Instance.SwitchToTargetCam(currentTarget);

            // --- BẬT HIỆU ỨNG XEM TRƯỚC LÊN UI ---
            if (selectedAction != null)
            {
                AdvancedUIManager.Instance.UpdateStainsUI(currentStains, selectedAction.stainChange);
            }
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

        AdvancedUIManager.Instance.PopulateSubMenu(inventoryItems);
    }

    public void OnSkillButtonClicked(ActionData skillAction)
    {
        if (state != CombatState.PlayerTurn) return;

        // --- KIỂM TRA ĐỦ ĐIỀU KIỆN STAIN ---
        if (currentStains + skillAction.stainChange < 0)
        {
            Debug.LogWarning($"[Hệ thống] Không đủ Stain để sử dụng {skillAction.actionName}!");
            return; // Khóa không cho chọn Skill nếu không đủ
        }

        selectedAction = skillAction;
        isSelectingSelfOnly = skillAction.isSelfOnly;
        isSelectingBuffTarget = skillAction.isFriendlyAction || isSelectingSelfOnly;

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
            CameraManager.Instance.SwitchToTargetCam(currentTarget);
        }

        // --- BẬT HIỆU ỨNG XEM TRƯỚC LÊN UI ---
        AdvancedUIManager.Instance.UpdateStainsUI(currentStains, skillAction.stainChange);

        AdvancedUIManager.Instance.ShowActionMenu(false);
    }

    public void OnActionSelected(ActionData action)
    {
        selectedAction = action;
        currentOpenMenu = OpenMenuType.None;

        ResetMenuAnimations();
        AdvancedUIManager.Instance.ShowActionMenu(false);
        AdvancedUIManager.Instance.UpdateTargetUI("");

        StartCoroutine(ExecuteActionRoutine(currentActiveUnit, currentTarget, selectedAction));
    }

    private void ExecuteHealAndEndTurn()
    {
        if (currentTarget != null && selectedAction != null)
        {
            currentTarget.Heal(selectedAction.power);
        }
        EndCurrentTurn();
    }

    System.Collections.IEnumerator EnemyAICore()
    {
        yield return new WaitForSeconds(0.5f);

        List<BattleUnit> livePlayers = playerParty.Where(p => p.currentHP > 0).ToList();
        if (livePlayers.Count == 0) yield break;

        currentTarget = livePlayers[Random.Range(0, livePlayers.Count)];
        selectedAction = currentActiveUnit.defaultAttack != null ? currentActiveUnit.defaultAttack : currentActiveUnit.characterSkills.FirstOrDefault();

        CameraManager.Instance.SwitchToTargetHitCam(currentTarget);

        yield return new WaitForSeconds(0.5f);

        StartCoroutine(ExecuteActionRoutine(currentActiveUnit, currentTarget, selectedAction));
    }

    public Vector3 GetMeleeAttackPosition(BattleUnit attacker, BattleUnit target)
    {
        if (target == null) return attacker.transform.position;

        Vector3 directionToTarget = (target.transform.position - attacker.transform.position).normalized;
        float stopDistance = 1.6f;
        Vector3 attackPosition = target.transform.position - (directionToTarget * stopDistance);
        return attackPosition;
    }

    private IEnumerator ExecuteActionRoutine(BattleUnit attacker, BattleUnit target, ActionData action)
    {
        state = CombatState.Executing;

        // --- ÁP DỤNG TRỪ/CỘNG STAIN THỰC SỰ LÚC BẮT ĐẦU ANIMATION (Chỉ tính Player) ---
        if (action != null && attacker.isPlayer)
        {
            AddStain(action.stainChange);
        }

        Vector3 originalPosition = attacker.transform.position;
        isAttackAnimationFinished = false;

        if (attacker.animator != null)
        {
            attacker.animator.applyRootMotion = false;
        }

        if (action != null && action.isMelee && target != null && !action.isFriendlyAction && !action.isHeal)
        {
            Vector3 attackPosition = GetMeleeAttackPosition(attacker, target);

            attacker.animator.Play("JumpForward");
            yield return StartCoroutine(MoveToPosition(attacker.transform, attackPosition, 0.35f));

            string animTrigger = !string.IsNullOrEmpty(action.animationTriggerName) ? action.animationTriggerName : "Attack";
            attacker.animator.SetTrigger(animTrigger);

            yield return new WaitUntil(() => isAttackAnimationFinished);

            attacker.animator.Play("JumpBack");
            yield return StartCoroutine(MoveToPosition(attacker.transform, originalPosition, 0.35f));
        }
        else
        {
            string animTrigger = action != null ? action.animationTriggerName : "Attack";
            attacker.animator.SetTrigger(animTrigger);

            yield return new WaitUntil(() => isAttackAnimationFinished);
        }

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
        ActionData actionToUse = selectedAction != null ? selectedAction : attacker.defaultAttack;
        if (actionToUse == null) return;

        int totalAtk = attacker.baseAtk + attacker.GetBuffValue(ActionData.BuffStat.Atk);
        float calculatedDamage = (totalAtk * actionToUse.damageMultiplier) + actionToUse.power;
        int rawDamage = Mathf.RoundToInt(calculatedDamage);

        int critChance = attacker.baseCrit + attacker.GetBuffValue(ActionData.BuffStat.Crit);
        bool isCrit = UnityEngine.Random.Range(0, 100) < critChance;

        if (isCrit && !actionToUse.isFriendlyAction)
        {
            rawDamage = Mathf.RoundToInt(rawDamage * 1.5f);
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
                    int hpBefore = enemy.currentHP;
                    enemy.TakeDamage(rawDamage, false);
                    int actualDamageTaken = hpBefore - enemy.currentHP;

                    AdvancedUIManager.Instance.ShowDamageText(enemy.transform, actualDamageTaken, isCrit, false);
                }
            }
        }
        else
        {
            bool parried = ParrySystem.Instance.parrySuccessful;
            List<BattleUnit> targets = actionToUse.isAoE ? playerParty.Where(u => u.currentHP > 0).ToList() : new List<BattleUnit> { currentTarget };

            foreach (var ally in targets)
            {
                int hpBefore = ally.currentHP;
                ally.TakeDamage(rawDamage, parried);
                int actualDamageTaken = hpBefore - ally.currentHP;

                if (!parried)
                {
                    AdvancedUIManager.Instance.ShowDamageText(ally.transform, actualDamageTaken, isCrit, false);
                }
            }
            ParrySystem.Instance.ResetParryState();
        }
    }

    private void PlayVFX(BattleUnit attacker, BattleUnit target, ActionData action)
    {
        if (action.vfxPrefab == null || target == null) return;

        List<BattleUnit> targetList = new List<BattleUnit>();
        if (action.isAoE)
        {
            targetList = (action.isFriendlyAction || action.isHeal) ? playerParty : enemyParty;
        }
        else
        {
            targetList.Add(target);
        }

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
                StartCoroutine(MoveVFXRoutine(vfx, targetPos, action.vfxSpeed, action.hitVfxPrefab));
            }
        }
    }

    public void PlayCastVFXFromAnimation(BattleUnit attacker)
    {
        ActionData currentAction = attacker.isPlayer ? selectedAction : (selectedAction != null ? selectedAction : attacker.defaultAttack);

        if (currentAction != null && currentAction.castVfxPrefab != null)
        {
            Transform spawnPoint = attacker.handTransform != null ? attacker.handTransform : attacker.transform;
            GameObject castVfx = Instantiate(currentAction.castVfxPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
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

        if (hitPrefab != null)
        {
            GameObject hitVfx = Instantiate(hitPrefab, targetPos, Quaternion.identity);
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
            DetermineTurnOrder();
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

        // Khi cộng trừ Stain thật sự, cập nhật lại UI không có tham số preview
        AdvancedUIManager.Instance.UpdateStainsUI(currentStains, 0);
    }
}