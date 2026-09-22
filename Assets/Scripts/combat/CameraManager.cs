using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;

    [Header("Camera Transition Settings")]
    [Tooltip("Thời gian lia camera (giây). Chỉnh số này nhỏ lại (VD: 0.3 - 0.5) để cam lia nhanh hơn.")]
    public float transitionSpeed = 0.4f; // --- THÊM MỚI: Tốc độ lia cam ---

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

        if (Camera.main != null)
        {
            mainBrain = Camera.main.GetComponent<CinemachineBrain>();
        }
    }

    public void SwitchToBattleStartCam()
    {
        SetFastBlend();
        ResetAllCams();

        if (battleStartCam != null)
        {
            battleStartCam.Priority = 20;
        }
    }

    public void SwitchToPlayerTurnCam(int playerIndex, bool instantCut = false)
    {
        if (instantCut) SetInstantCutBlend();
        else SetFastBlend(); // Chuyển sang lia nhanh thay vì lia mặc định

        ResetAllCams();

        for (int i = 0; i < playerPartyCams.Count; i++)
        {
            if (playerPartyCams[i] != null)
                playerPartyCams[i].Priority = (i == playerIndex) ? 10 : 0;
        }
    }

    public void SwitchToPlayerActionCam(int playerIndex)
    {
        // QUAN TRỌNG: Ép cắt ngay lập tức (Instant Cut) từ Targeting Cam sang Action Cam
        SetInstantCutBlend();
        ResetAllCams();

        for (int i = 0; i < playerActionCams.Count; i++)
        {
            if (playerActionCams[i] != null)
                playerActionCams[i].Priority = (i == playerIndex) ? 12 : 0;
        }
    }

    public void SwitchToPlayerMenuCam(int playerIndex)
    {
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

    public void SwitchToEnemyPhase2Cam()
    {
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
        SetInstantCutBlend();
        ResetAllCams();
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