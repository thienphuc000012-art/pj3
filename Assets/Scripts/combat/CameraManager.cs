using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Collections;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;

    [Header("Party Turn Cameras")]
    public List<CinemachineCamera> playerPartyCams;

    [Header("Party Hit Cameras")]
    public List<CinemachineCamera> playerHitCams;

    [Header("Targeting Cameras (Zoom khi chọn mục tiêu)")]
    public List<CinemachineCamera> playerTargetCams; // Cam zoom sát đồng minh khi buff
    public List<CinemachineCamera> enemyTargetCams;  // Cam zoom sát kẻ địch khi tấn công

    private CinemachineBrain mainBrain;
    private CinemachineBlendDefinition originalBlend;

    void Awake()
    {
        Instance = this;

        if (Camera.main != null)
        {
            mainBrain = Camera.main.GetComponent<CinemachineBrain>();
            if (mainBrain != null)
            {
                originalBlend = mainBrain.DefaultBlend;
            }
        }
    }

    public void SwitchToPlayerTurnCam(int playerIndex)
    {
        // Trả lại độ mượt ban đầu cho Turn Cam
        RestoreOriginalBlend();

        ResetAllHitCams();
        ResetTargetCamWithoutCut(); // Tắt cam zoom nhưng không ép cắt cứng lạm dụng vào Turn Cam

        for (int i = 0; i < playerPartyCams.Count; i++)
        {
            if (playerPartyCams[i] != null)
                playerPartyCams[i].Priority = (i == playerIndex) ? 10 : 0;
        }
    }

    public void SwitchToTargetHitCam(BattleUnit targetUnit)
    {
        // Trả lại độ mượt ban đầu cho Hit Cam
        RestoreOriginalBlend();

        ResetAllTurnCams();
        ResetTargetCamWithoutCut();

        int targetIndex = CombatManager.Instance.playerParty.IndexOf(targetUnit);
        for (int i = 0; i < playerHitCams.Count; i++)
        {
            if (playerHitCams[i] != null)
                playerHitCams[i].Priority = (i == targetIndex) ? 10 : 0;
        }
    }

    // --- KHI CHUYỂN MỤC TIÊU BẰNG A/D HOẶC CLICK (Ép cắt ngay lập tức giữa các Target Cam) ---
    public void SwitchToTargetCam(BattleUnit targetUnit)
    {
        // Đặt thời gian chuyển cảnh về 0 để cắt cứng tức thì giữa các mục tiêu
        SetInstantCutBlend();

        CinemachineCamera targetCamToActivate = null;

        if (targetUnit.isPlayer)
        {
            int targetIndex = CombatManager.Instance.playerParty.IndexOf(targetUnit);
            if (targetIndex >= 0 && targetIndex < playerTargetCams.Count)
            {
                targetCamToActivate = playerTargetCams[targetIndex];
            }
        }
        else
        {
            int targetIndex = CombatManager.Instance.enemyParty.IndexOf(targetUnit);
            if (targetIndex >= 0 && targetIndex < enemyTargetCams.Count)
            {
                targetCamToActivate = enemyTargetCams[targetIndex];
            }
        }

        // Bật Priority của cam mới lên trước
        if (targetCamToActivate != null)
        {
            targetCamToActivate.Priority = 15;
        }

        // Hạ Priority các cam zoom khác về 0
        foreach (var cam in playerTargetCams)
        {
            if (cam != null && cam != targetCamToActivate) cam.Priority = 0;
        }
        foreach (var cam in enemyTargetCams)
        {
            if (cam != null && cam != targetCamToActivate) cam.Priority = 0;
        }
    }

    // --- KHI HỦY HOẶC KẾT THÚC CHỌN MỤC TIÊU (QUAY VỀ CAM THƯỜNG LẬP TỨC) ---
    public void ResetTargetCam()
    {
        // Đặt thời gian chuyển cảnh về 0 để cắt cứng ngay khi thoát khỏi chế độ zoom
        SetInstantCutBlend();
        ResetTargetCamWithoutCut();
    }

    // Tắt Priority tất cả Target Cam về 0
    private void ResetTargetCamWithoutCut()
    {
        foreach (var cam in playerTargetCams) { if (cam != null) cam.Priority = 0; }
        foreach (var cam in enemyTargetCams) { if (cam != null) cam.Priority = 0; }
    }

    private void ResetAllTurnCams()
    {
        foreach (var cam in playerPartyCams) { if (cam != null) cam.Priority = 0; }
    }

    private void ResetAllHitCams()
    {
        foreach (var cam in playerHitCams) { if (cam != null) cam.Priority = 0; }
    }

    // Ép Cinemachine cắt cứng (Blend time = 0)
    private void SetInstantCutBlend()
    {
        if (mainBrain != null)
        {
            mainBrain.DefaultBlend = default(CinemachineBlendDefinition);
        }
    }

    // Phục hồi lại độ mượt (blend) ban đầu cho các camera thông thường khác
    private void RestoreOriginalBlend()
    {
        if (mainBrain != null)
        {
            mainBrain.DefaultBlend = originalBlend;
        }
    }
}