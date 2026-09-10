using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Collections;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;

    [Header("Party Turn Cameras (Lia bình thường)")]
    public List<CinemachineCamera> playerPartyCams;

    [Header("Party Hit Cameras (Lia bình thường)")]
    public List<CinemachineCamera> playerHitCams;

    [Header("Targeting Cameras (Zoom chọn mục tiêu - Cắt ngay lập tức)")]
    public List<CinemachineCamera> playerTargetCams;
    public List<CinemachineCamera> enemyTargetCams;

    [Header("Menu Cameras (Skills & Items - Cắt ngay lập tức)")]
    public List<CinemachineCamera> playerMenuCams;

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

    // Camera lượt đi: Có thể chọn cắt ngay lập tức (khi tắt menu) hoặc lia mượt (khi đổi lượt)
    public void SwitchToPlayerTurnCam(int playerIndex, bool instantCut = false)
    {
        if (instantCut)
        {
            SetInstantCutBlend(); // Cắt ngay lập tức khi tắt menu
        }
        else
        {
            RestoreOriginalBlend(); // Lia bình thường khi đổi lượt giữa các player
        }

        ResetAllHitCams();
        ResetAllMenuCams();
        ResetTargetCamWithoutCut();

        for (int i = 0; i < playerPartyCams.Count; i++)
        {
            if (playerPartyCams[i] != null)
                playerPartyCams[i].Priority = (i == playerIndex) ? 10 : 0;
        }
    }

    // Camera menu: CẮT NGAY LẬP TỨC
    public void SwitchToPlayerMenuCam(int playerIndex)
    {
        SetInstantCutBlend(); // Cắt cứng ngay lập tức

        ResetAllHitCams();
        ResetTargetCamWithoutCut();

        for (int i = 0; i < playerMenuCams.Count; i++)
        {
            if (playerMenuCams[i] != null)
                playerMenuCams[i].Priority = (i == playerIndex) ? 15 : 0;
        }
    }

    public void ResetAllMenuCams()
    {
        foreach (var cam in playerMenuCams)
        {
            if (cam != null) cam.Priority = 0;
        }
    }

    // Camera trúng đòn: LIA BÌNH THƯỜNG
    public void SwitchToTargetHitCam(BattleUnit targetUnit)
    {
        RestoreOriginalBlend(); // Trả lại hiệu ứng lia mượt

        ResetAllTurnCams();
        ResetAllMenuCams();
        ResetTargetCamWithoutCut();

        int targetIndex = CombatManager.Instance.playerParty.IndexOf(targetUnit);
        for (int i = 0; i < playerHitCams.Count; i++)
        {
            if (playerHitCams[i] != null)
                playerHitCams[i].Priority = (i == targetIndex) ? 10 : 0;
        }
    }

    // Camera zoom chọn mục tiêu: CẮT NGAY LẬP TỨC
    public void SwitchToTargetCam(BattleUnit targetUnit)
    {
        SetInstantCutBlend(); // Cắt cứng ngay lập tức

        // QUAN TRỌNG: Phải tắt toàn bộ menu cam đang mở để không bị kẹt góc nhìn menu
        ResetAllMenuCams();

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

        if (targetCamToActivate != null)
        {
            targetCamToActivate.Priority = 15;
        }

        foreach (var cam in playerTargetCams)
        {
            if (cam != null && cam != targetCamToActivate) cam.Priority = 0;
        }
        foreach (var cam in enemyTargetCams)
        {
            if (cam != null && cam != targetCamToActivate) cam.Priority = 0;
        }
    }

    public void ResetTargetCam()
    {
        SetInstantCutBlend();
        ResetTargetCamWithoutCut();
    }

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

    private void SetInstantCutBlend()
    {
        if (mainBrain != null)
        {
            mainBrain.DefaultBlend = default(CinemachineBlendDefinition);
        }
    }

    private void RestoreOriginalBlend()
    {
        if (mainBrain != null)
        {
            mainBrain.DefaultBlend = originalBlend;
        }
    }
}