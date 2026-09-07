using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;

    [Header("Party Turn Cameras")]
    public List<CinemachineCamera> playerPartyCams; // Cam riêng khi đến lượt của từng nhân vật

    [Header("Party Hit Cameras (Góc riêng khi bị đánh)")]
    public List<CinemachineCamera> playerHitCams; // Cam riêng khi từng nhân vật chịu đòn từ Enemy

    void Awake()
    {
        Instance = this;
    }

    // Chuyển camera đến nhân vật đang tới lượt
    public void SwitchToPlayerTurnCam(int playerIndex)
    {
        // Tắt toàn bộ Hit Cam đi
        foreach (var hitCam in playerHitCams)
        {
            if (hitCam != null) hitCam.Priority = 0;
        }

        // Bật Turn Cam của nhân vật hiện tại
        for (int i = 0; i < playerPartyCams.Count; i++)
        {
            if (playerPartyCams[i] != null)
            {
                playerPartyCams[i].Priority = (i == playerIndex) ? 10 : 0;
            }
        }
    }

    // Kích hoạt góc Hit Cam riêng của nhân vật đang bị Enemy tấn công
    public void SwitchToTargetHitCam(BattleUnit targetUnit)
    {
        // Tắt toàn bộ Turn Cam đi
        foreach (var turnCam in playerPartyCams)
        {
            if (turnCam != null) turnCam.Priority = 0;
        }

        // Tìm xem nhân vật bị đánh là ai trong party và bật đúng Hit Cam của người đó
        int targetIndex = CombatManager.Instance.playerParty.IndexOf(targetUnit);

        for (int i = 0; i < playerHitCams.Count; i++)
        {
            if (playerHitCams[i] != null)
            {
                playerHitCams[i].Priority = (i == targetIndex) ? 10 : 0;
            }
        }
    }
}