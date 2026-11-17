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
    public IReadOnlyList<GameObject> EnvironmentObjects { get; }
    public bool UsesSceneEnvironment => EnvironmentObjects.Count > 0;

    internal CombatAreaScan(Pose spawnPose, float radius, List<string> chunkPaths, List<GameObject> environmentObjects = null)
    {
        SpawnPose = spawnPose;
        Radius = radius;
        ChunkPaths = chunkPaths.AsReadOnly();
        EnvironmentObjects = (environmentObjects ?? new List<GameObject>()).AsReadOnly();
    }
}

/// <summary>Reads streamed chunks or nearby static scenery in a map whose environment lives directly in its scene.</summary>
public static class CombatAreaScanner
{
    public static bool TryScan(WorldInteraction encounter, MapChunkStreamer streamer, out CombatAreaScan scan, out string error)
        => TryScan(encounter, streamer, false, out scan, out error);

    // Authoring uses the current placed pose; inspecting must not mutate the runtime patrol origin.
    public static bool TryScanAuthored(WorldInteraction encounter, MapChunkStreamer streamer, out CombatAreaScan scan, out string error)
        => TryScan(encounter, streamer, true, out scan, out error);

    static bool TryScan(WorldInteraction encounter, MapChunkStreamer streamer, bool authored, out CombatAreaScan scan, out string error)
    {
        scan = null;
        if (encounter == null || encounter.kind != WorldInteractionKind.Encounter)
        {
            error = "Không tìm thấy Enemy Encounter hợp lệ.";
            return false;
        }

        var origin = encounter.GetComponent<EnemyCombatOrigin>();
        Pose spawn = new Pose(encounter.transform.position, encounter.transform.rotation);
        if (!authored && (origin == null || !origin.TryGetSpawnPoint(out spawn)))
        {
            error = "Encounter chưa lưu Enemy Combat Origin. Hãy đặt enemy và lưu spawn trước khi chạy patrol.";
            return false;
        }
        float radius = CombatArenaScanSettings.ResolveRadius(encounter) +
            Mathf.Max(0f, encounter.combatArenaScan?.edgePadding ?? 0f);
        var paths = new List<string>();
        if (streamer == null || streamer.gameObject.scene != encounter.gameObject.scene || streamer.chunks == null || streamer.chunks.Count == 0)
        {
            var objects = CombatSceneEnvironment.Collect(encounter.gameObject.scene, spawn.position, radius);
            if (objects.Count == 0)
            {
                error = $"Không có địa hình hoặc môi trường trong vùng quét {radius:0.#} m quanh Spawn Position {spawn.position}.";
                return false;
            }
            scan = new CombatAreaScan(spawn, radius, paths, objects);
            error = null;
            return true;
        }
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
