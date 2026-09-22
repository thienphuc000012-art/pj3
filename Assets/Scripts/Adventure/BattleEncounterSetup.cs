using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

[Serializable]
public class EnemyCombatSetup
{
    [Header("Enemy Spawn")]
    [Tooltip("Vị trí và rotation đứng mặc định của enemy trong scene combat.")]
    public Transform spawnPoint;

    [Header("Player Melee Slot -> đánh Enemy này")]
    [Tooltip("Một vị trí duy nhất mà Player sẽ lao tới khi dùng đòn Melee lên enemy này.")]
    public Transform playerMeleeSlot;

    [Header("Enemy Melee Slots -> đánh từng Player")]
    [Tooltip("Element 0 = vị trí enemy đứng khi đánh Player 0, Element 1 = Player 1, ...")]
    public List<Transform> enemyMeleeSlots = new List<Transform>();

    [Header("Camera khi Player chọn Enemy")]
    [Tooltip("Camera dùng khi Player đang chọn enemy này làm mục tiêu.")]
    public CinemachineCamera targetCamera;

    [Header("Camera khi Enemy hành động")]
    [Tooltip("Camera dùng khi chính enemy này tới lượt và thực hiện đòn đơn mục tiêu.")]
    public CinemachineCamera actionCamera;

    public Transform GetEnemyMeleeSlot(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= enemyMeleeSlots.Count)
            return null;

        return enemyMeleeSlots[playerIndex];
    }
}

public class BattleEncounterSetup : MonoBehaviour
{
    [Header("Encounter Setup ID")]
    [Tooltip("Phải giống Combat Setup ID trên WorldInteraction ở map.")]
    public string setupId = "default";

    [Header("Enemy Setup - thứ tự phải giống enemyPrefabs")]
    public List<EnemyCombatSetup> enemies = new List<EnemyCombatSetup>();

    [Header("Optional Cameras cho Encounter")]
    [Tooltip("Nếu để trống sẽ dùng Battle Start Camera mặc định trong CameraManager.")]
    public CinemachineCamera battleStartCamera;

    [Tooltip("Nếu để trống sẽ dùng Enemy AoE Camera mặc định trong CameraManager.")]
    public CinemachineCamera enemyAoECamera;

    [Tooltip("Nếu để trống sẽ dùng Enemy Phase 2 Camera mặc định trong CameraManager.")]
    public CinemachineCamera enemyPhase2Camera;

    public EnemyCombatSetup GetEnemySetup(int index)
    {
        if (index < 0 || index >= enemies.Count)
            return null;

        return enemies[index];
    }

    public void SetAllCameraPriorities(int priority)
    {
        if (battleStartCamera != null)
            battleStartCamera.Priority = priority;

        if (enemyAoECamera != null)
            enemyAoECamera.Priority = priority;

        if (enemyPhase2Camera != null)
            enemyPhase2Camera.Priority = priority;

        foreach (EnemyCombatSetup setup in enemies)
        {
            if (setup == null)
                continue;

            if (setup.targetCamera != null)
                setup.targetCamera.Priority = priority;

            if (setup.actionCamera != null)
                setup.actionCamera.Priority = priority;
        }
    }

    private void Awake()
    {
        SetAllCameraPriorities(0);
    }
}
