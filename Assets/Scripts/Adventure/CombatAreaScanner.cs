using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CombatArenaScanSettings
{
    [Min(0f), Tooltip("0 chọn tự động: 18 m cho một enemy, 23 m cho nhóm, 35 m nếu có Boss.")]
    public float scanRadius;
    [Min(0f), Tooltip("Nới vùng quét để giữ thêm địa hình quanh mép đấu trường.")]
    public float edgePadding = 4f;
    [Tooltip("Hiển thị bán kính quét quanh Spawn Position khi chọn encounter.")]
    public bool debugScanArea = true;

    public static float ResolveRadius(WorldInteraction encounter)
    {
        var settings = encounter.combatArenaScan;
        if (settings != null && settings.scanRadius > 0f) return settings.scanRadius;

        int count = 0;
        if (encounter.enemyPrefabs != null)
            foreach (BattleUnit unit in encounter.enemyPrefabs)
            {
                if (unit == null) continue;
                if (unit.isBoss) return 35f;
                count++;
            }
        return count > 1 ? 23f : 18f;
    }
}

public sealed class CombatAreaScan
{
    public Pose SpawnPose { get; }
    public float Radius { get; }
    public IReadOnlyList<string> ChunkPaths { get; }

    internal CombatAreaScan(Pose spawnPose, float radius, List<string> chunkPaths)
    {
        SpawnPose = spawnPose;
        Radius = radius;
        ChunkPaths = chunkPaths.AsReadOnly();
    }
}

/// <summary>Reads the exploration streamer's catalogue, including chunks that are currently unloaded.</summary>
public static class CombatAreaScanner
{
    public static bool TryScan(WorldInteraction encounter, MapChunkStreamer streamer, out CombatAreaScan scan, out string error)
    {
        scan = null;
        if (encounter == null || encounter.kind != WorldInteractionKind.Encounter)
        {
            error = "Không tìm thấy Enemy Encounter hợp lệ.";
            return false;
        }

        var origin = encounter.GetComponent<EnemyCombatOrigin>();
        if (origin == null || !origin.TryGetSpawnPoint(out Pose spawn))
        {
            error = "Encounter chưa lưu Enemy Combat Origin. Hãy đặt enemy và lưu spawn trước khi chạy patrol.";
            return false;
        }
        if (streamer == null)
        {
            error = "Map chưa có danh mục chunk trong Map Chunk Streamer.";
            return false;
        }

        float radius = CombatArenaScanSettings.ResolveRadius(encounter) +
            Mathf.Max(0f, encounter.combatArenaScan?.edgePadding ?? 0f);
        var paths = new List<string>();
        streamer.CollectChunkPaths(spawn.position, radius, paths);
        if (paths.Count == 0)
        {
            error = $"Không có chunk giao với vùng quét {radius:0.#} m quanh Spawn Position {spawn.position}.";
            return false;
        }

        scan = new CombatAreaScan(spawn, radius, paths);
        error = null;
        return true;
    }
}
