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

    [Header("Menu Cameras (Skills & Items - Cắt ngay lập tức)")]
    public List<CinemachineCamera> playerMenuCams;

    [Header("Player Action Cameras (Camera riêng khi Tấn công/Dùng chiêu)")]
    public List<CinemachineCamera> playerActionCams;

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

    public void ResetTargetCam()
    {
        SetInstantCutBlend();
        ResetAllCams();
    }

    private void ResetAllCams()
    {
        if (battleStartCam != null) battleStartCam.Priority = 0;

        foreach (var cam in playerPartyCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in playerHitCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in playerTargetCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in enemyTargetCams) { if (cam != null) cam.Priority = 0; }
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