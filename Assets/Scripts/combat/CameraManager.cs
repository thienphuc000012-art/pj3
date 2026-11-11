using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;

    [Header("Cinematic Combat Camera")]
    [SerializeField] bool useDynamicCombatCamera = true;
    CombatCameraDirector director;
    public bool UsesDynamicCombatCamera => useDynamicCombatCamera && ResolveSceneBrain() != null &&
        (director == null || director.isActiveAndEnabled);
    public float TurnCameraDelay => UsesDynamicCombatCamera && director != null
        ? director.IdleBlendDuration : transitionSpeed;

    [Header("Camera Transition Settings")]
    [Tooltip("Thời gian lia camera (giây). Chỉnh số này nhỏ lại (VD: 0.3 - 0.5) để cam lia nhanh hơn.")]
    public float transitionSpeed = 0.4f; // --- THÊM MỚI: Tốc độ lia cam ---
    [Min(0f), Tooltip("Thời gian lia camera khi bắt đầu Ultimate (giây).")]
    public float ultimateTransitionDuration = 0.8f;

    [Header("Battle Start Camera (Camera tổng thể đầu trận)")]
    public CinemachineCamera battleStartCam;

    [Header("Party Turn Cameras (Lia bình thường)")]
    public List<CinemachineCamera> playerPartyCams;

    [Header("Party Hit Cameras (Lia bình thường)")]
    public List<CinemachineCamera> playerHitCams;

    [Header("Targeting Cameras (Zoom chọn mục tiêu - Cắt ngay lập tức)")]
    public List<CinemachineCamera> playerTargetCams;
    public List<CinemachineCamera> enemyTargetCams;

    [Header("Enemy Action Cameras")]
    [Tooltip("Camera riêng của từng enemy khi enemy đó tới lượt hành động.")]
    public List<CinemachineCamera> enemyActionCams = new List<CinemachineCamera>();

    [Header("Menu Cameras (Skills & Items - Cắt ngay lập tức)")]
    public List<CinemachineCamera> playerMenuCams;

    [Header("Player Action Cameras (Camera riêng khi Tấn công/Dùng chiêu)")]
    public List<CinemachineCamera> playerActionCams;

    [Header("Enemy Special Cameras")]
    [Tooltip("Camera cinematic chỉ dùng khi Boss vừa bước vào Phase 2.")]
    public CinemachineCamera enemyPhase2Cam;

    [Tooltip("Camera dùng khi Enemy thực hiện Action có isAoE = true.")]
    public CinemachineCamera enemyAoECam;

    private CinemachineBrain mainBrain;

    void Awake()
    {
        Instance = this;
        director = GetComponent<CombatCameraDirector>();
        ResolveSceneBrain();
    }

    CinemachineBrain ResolveSceneBrain()
    {
        if (mainBrain != null) return mainBrain;
        // The additive Loading scene may still have its own Main Camera.
        if (gameObject.scene.isLoaded)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                var localBrain = root.GetComponentInChildren<CinemachineBrain>();
                if (localBrain == null || !localBrain.CompareTag("MainCamera")) continue;
                return mainBrain = localBrain;
            }
        Camera candidate = Camera.main;
        if (candidate != null && candidate.gameObject.scene == gameObject.scene)
            mainBrain = candidate.GetComponent<CinemachineBrain>();
        return mainBrain;
    }

    bool UseDirector()
    {
        if (!UsesDynamicCombatCamera || CombatManager.Instance == null) return false;
        if (director == null) director = gameObject.AddComponent<CombatCameraDirector>();
        return director.Initialize(mainBrain, CombatManager.Instance);
    }

    public void SwitchToBattleStartCam()
    {
        if (UseDirector()) { director.ShowIntro(); return; }
        SetFastBlend();
        ResetAllCams();

        if (battleStartCam != null)
        {
            battleStartCam.Priority = 20;
        }
    }

    public void SwitchToPlayerTurnCam(int playerIndex, bool instantCut = false)
    {
        if (UseDirector())
        {
            // The old menu calls request a cut. Dynamic framing blends from
            // the current shot so canceling a target never jumps.
            director.ShowIdle(CombatManager.Instance.currentActiveUnit);
            return;
        }
        if (instantCut) SetInstantCutBlend();
        else SetFastBlend(); // Chuyển sang lia nhanh thay vì lia mặc định

        ResetAllCams();

        for (int i = 0; i < playerPartyCams.Count; i++)
        {
            if (playerPartyCams[i] != null)
                playerPartyCams[i].Priority = (i == playerIndex) ? 10 : 0;
        }
    }

    public void SwitchToPlayerActionCam(int playerIndex, bool ultimate = false)
    {
        if (ultimate && mainBrain != null)
            mainBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, Mathf.Max(0f, ultimateTransitionDuration));
        else SetInstantCutBlend();
        ResetAllCams();

        for (int i = 0; i < playerActionCams.Count; i++)
        {
            if (playerActionCams[i] != null)
                playerActionCams[i].Priority = (i == playerIndex) ? 12 : 0;
        }
    }

    public void SwitchToPlayerMenuCam(int playerIndex)
    {
        if (UseDirector()) { director.ShowPlayerMenu(CombatManager.Instance.currentActiveUnit); return; }
        SetInstantCutBlend();
        ResetAllCams();

        for (int i = 0; i < playerMenuCams.Count; i++)
        {
            if (playerMenuCams[i] != null)
                playerMenuCams[i].Priority = (i == playerIndex) ? 15 : 0;
        }
    }

    public void SwitchToTargetHitCam(BattleUnit targetUnit)
    {
        SetFastBlend(); // Chuyển sang lia nhanh
        ResetAllCams();

        int targetIndex = CombatManager.Instance.playerParty.IndexOf(targetUnit);
        for (int i = 0; i < playerHitCams.Count; i++)
        {
            if (playerHitCams[i] != null)
                playerHitCams[i].Priority = (i == targetIndex) ? 10 : 0;
        }
    }

    public void SwitchToDebuffCam(BattleUnit unit)
    {
        if (unit == null || CombatManager.Instance == null) return;
        if (UseDirector()) { director.ShowStatus(unit); return; }
        int index = unit.isPlayer ? CombatManager.Instance.playerParty.IndexOf(unit) : CombatManager.Instance.enemyParty.IndexOf(unit);
        CinemachineCamera camera = null;
        if (unit.isPlayer && index >= 0 && index < playerHitCams.Count) camera = playerHitCams[index];
        var targets = unit.isPlayer ? playerTargetCams : enemyTargetCams;
        if (camera == null && index >= 0 && index < targets.Count) camera = targets[index];
        if (camera == null) return; // Retain the current view when no dedicated camera exists.
        SetFastBlend();
        ResetAllCams();
        camera.Priority = 20;
    }

    public void SwitchToEnemyPhase2Cam(BattleUnit boss = null)
    {
        if (boss != null && UseDirector()) { director.ShowBoss(boss); return; }
        SetFastBlend();
        ResetAllCams();

        if (enemyPhase2Cam != null)
        {
            enemyPhase2Cam.Priority = 20;
        }
    }

    public void SwitchToEnemyAoECam()
    {
        SetFastBlend();
        ResetAllCams();

        if (enemyAoECam != null)
        {
            enemyAoECam.Priority = 15;
        }
    }

    public void SwitchToTargetCam(BattleUnit targetUnit)
    {
        if (targetUnit == null) return;
        if (UseDirector()) { director.Select(CombatManager.Instance.currentActiveUnit, targetUnit); return; }
        SetInstantCutBlend();
        ResetAllCams();

        CinemachineCamera targetCamToActivate = null;

        if (targetUnit.isPlayer)
        {
            int targetIndex = CombatManager.Instance.playerParty.IndexOf(targetUnit);
            if (targetIndex >= 0 && targetIndex < playerTargetCams.Count)
                targetCamToActivate = playerTargetCams[targetIndex];
        }
        else
        {
            int targetIndex = CombatManager.Instance.enemyParty.IndexOf(targetUnit);
            if (targetIndex >= 0 && targetIndex < enemyTargetCams.Count)
                targetCamToActivate = enemyTargetCams[targetIndex];
        }

        if (targetCamToActivate != null) targetCamToActivate.Priority = 15;
    }

    public bool SwitchToEnemyActionCam(BattleUnit enemy)
    {
        if (enemy == null || CombatManager.Instance == null)
            return false;

        int enemyIndex = CombatManager.Instance.enemyParty.IndexOf(enemy);
        if (enemyIndex < 0 || enemyIndex >= enemyActionCams.Count)
            return false;

        CinemachineCamera cam = enemyActionCams[enemyIndex];
        if (cam == null)
            return false;

        SetFastBlend();
        ResetAllCams();
        cam.Priority = 15;
        return true;
    }

    public void ApplyEncounterSetup(BattleEncounterSetup setup, List<BattleUnit> enemies)
    {
        if (setup == null || enemies == null)
            return;

        if (UsesDynamicCombatCamera)
        {
            setup.SetAllCameraPriorities(0);
            return;
        }

        // Camera chung của encounter: chỉ override khi preset có gán.
        if (setup.battleStartCamera != null)
            battleStartCam = setup.battleStartCamera;

        if (setup.enemyAoECamera != null)
            enemyAoECam = setup.enemyAoECamera;

        if (setup.enemyPhase2Camera != null)
            enemyPhase2Cam = setup.enemyPhase2Camera;

        EnsureCameraListSize(enemyTargetCams, enemies.Count);
        EnsureCameraListSize(enemyActionCams, enemies.Count);

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleUnit enemy = enemies[i];
            EnemyCombatSetup enemySetup = setup.GetEnemySetup(i);

            if (enemy == null || enemySetup == null)
                continue;

            // Chỉ thay camera khi preset có gán camera riêng.
            if (enemySetup.targetCamera != null)
            {
                enemyTargetCams[i] = enemySetup.targetCamera;
                PrepareEnemyCamera(enemyTargetCams[i], enemy);
            }

            if (enemySetup.actionCamera != null)
            {
                enemyActionCams[i] = enemySetup.actionCamera;
                PrepareEnemyCamera(enemyActionCams[i], enemy);
            }
        }

        setup.SetAllCameraPriorities(0);
    }

    private static void EnsureCameraListSize(List<CinemachineCamera> list, int count)
    {
        if (list == null)
            return;

        while (list.Count < count)
            list.Add(null);
    }

    private static void PrepareEnemyCamera(CinemachineCamera cam, BattleUnit enemy)
    {
        if (cam == null || enemy == null)
            return;

        cam.Priority = 0;

        // Giữ nguyên vị trí/rotation camera đã tự setup trong scene.
        // Chỉ đổi target sang enemy runtime vừa spawn.
        cam.LookAt = enemy.transform;

        // Nếu camera được cấu hình có Follow thì cũng remap Follow sang enemy runtime.
        if (cam.Follow != null)
            cam.Follow = enemy.transform;
    }

    public void ResetTargetCam()
    {
        var combat = CombatManager.Instance;
        if (UseDirector()) { director.ShowIdle(combat.currentActiveUnit); return; }
        if (combat != null && combat.currentActiveUnit != null && combat.currentActiveUnit.isPlayer)
            SwitchToPlayerTurnCam(combat.playerParty.IndexOf(combat.currentActiveUnit), true);
        else
            ResetAllCams();
    }

    public void PrepareEnemyAttack(BattleUnit attacker, BattleUnit target, ActionData action)
    {
        if (UseDirector()) { director.Anticipate(attacker, target, action != null && action.isAoE); return; }
        if (action != null && action.isAoE) SwitchToEnemyAoECam();
        else if (!SwitchToEnemyActionCam(attacker)) SwitchToTargetHitCam(target);
    }

    public void BeginCombatAction(BattleUnit attacker, BattleUnit target, ActionData action)
    {
        if (UseDirector()) { director.BeginAction(attacker, target, action, ultimateTransitionDuration); return; }
        if (attacker.isPlayer) SwitchToPlayerActionCam(CombatManager.Instance.playerParty.IndexOf(attacker), action.isUltimate);
    }

    public void PlaySkillCinematic(BattleUnit attacker, BattleUnit target, ActionData action)
    {
        if (UsesDynamicCombatCamera && director != null) director.PlaySkill(attacker, target, action);
    }

    public void PlayCombatImpact(ActionData action, BattleUnit attacker, BattleUnit target,
        bool critical, bool parried, bool allowLocalHitStop)
    {
        if (UsesDynamicCombatCamera && director != null)
            director.PlayImpact(action, attacker, target, critical, parried, allowLocalHitStop);
    }

    public void RecoverCombatAction(BattleUnit attacker, BattleUnit target)
    {
        if (UsesDynamicCombatCamera && director != null) director.Recover(attacker, target);
    }

    public void StopSkillCinematic()
    {
        if (director != null) director.StopSkill();
    }

    public void ResetCombatCameraPresentation()
    {
        if (director != null) director.ResetPresentation();
    }

    private void ResetAllCams()
    {
        if (battleStartCam != null) battleStartCam.Priority = 0;
        if (enemyPhase2Cam != null) enemyPhase2Cam.Priority = 0;
        if (enemyAoECam != null) enemyAoECam.Priority = 0;

        foreach (var cam in playerPartyCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in playerHitCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in playerTargetCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in enemyTargetCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in enemyActionCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in playerMenuCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in playerActionCams) { if (cam != null) cam.Priority = 0; }
    }

    private void SetInstantCutBlend()
    {
        if (mainBrain != null)
            mainBrain.DefaultBlend = default(CinemachineBlendDefinition); // Cắt ngay lập tức (0 giây)
    }

    /// --- THÊM MỚI: Hàm cài đặt lia cam nhanh dựa trên biến transitionSpeed ---
    private void SetFastBlend()
    {
        ResolveSceneBrain();
        if (mainBrain != null)
        {
            // LƯU Ý: Dùng "Styles" (có chữ s) thay vì "Style" đối với Cinemachine 3.x
            mainBrain.DefaultBlend = new CinemachineBlendDefinition(
                Unity.Cinemachine.CinemachineBlendDefinition.Styles.EaseInOut,
                transitionSpeed
            );
        }
    }
    // --- THÊM MỚI: Hàm hỗ trợ cắt cứng camera khi bắt đầu hành động ---
    public void SetInstantCutBlendForAction()
    {
        SetInstantCutBlend();
    }
}
