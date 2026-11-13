using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class CombatArenaSpawnSettings
{
    [Min(2f)] public float playerDistance = 4.5f;
    [Min(0f)] public float enemyDistance;
    [Min(1f)] public float slotSpacing = 1.75f;
    [Min(0f)] public float floorSearchHeight = 2.5f;
    [Min(0f)] public float floorSearchDepth = 3f;
    [Min(.5f)] public float bossRadius = 1.25f;
    [Min(1f)] public float bossHeight = 3f;
}

internal sealed class CombatArenaLayout
{
    internal Pose[] players;
    internal Pose[] enemies;
    internal int usablePlayerSlots;
    internal int usableEnemySlots;
    internal Vector3[][] playerEntryPaths;
}

/// <summary>Moves existing environment roots without changing their world pose.</summary>
internal static class CombatArenaBuilder
{
    public static bool TryBuild(Scene combatScene, IReadOnlyList<Scene> chunks, CombatAreaScan scan,
        CombatArenaSpawnSettings settings, int playerCount, IReadOnlyList<BattleUnit> enemies,
        int playerSlotCount, int enemySlotCount, float entryDistance, out CombatArenaLayout layout, out string error)
    {
        layout = null;
        if (!combatScene.IsValid() || !combatScene.isLoaded || chunks == null || chunks.Count == 0 ||
            scan == null || settings == null || enemies == null || enemies.Count == 0 || playerCount == 0)
        {
            error = "Thiếu dữ liệu để dựng đấu trường.";
            return false;
        }

        var roots = new List<GameObject>();
        var visited = new HashSet<int>();
        foreach (Scene chunk in chunks)
        {
            if (!chunk.IsValid() || !chunk.isLoaded)
            {
                error = "Một scene chunk của đấu trường chưa load.";
                return false;
            }
            foreach (GameObject root in chunk.GetRootGameObjects())
            {
                if (!visited.Add(root.GetInstanceID())) continue;
                if (root.GetComponentInChildren<WorldInteraction>(true) != null ||
                    root.GetComponentInChildren<BattleUnit>(true) != null ||
                    root.GetComponentInChildren<PlayerScript>(true) != null ||
                    root.GetComponentInChildren<NavMeshAgent>(true) != null ||
                    root.GetComponentInChildren<Camera>(true) != null ||
                    root.GetComponentInChildren<Canvas>(true) != null)
                {
                    Debug.LogWarning("[Combat Arena] Bỏ qua root gameplay: " + root.name, root);
                    continue;
                }
                roots.Add(root);
            }
        }

        if (roots.Count == 0)
        {
            error = "Các chunk đã tải không có root môi trường hợp lệ.";
            return false;
        }

        var arena = new GameObject("CombatArenaRoot");
        SceneManager.MoveGameObjectToScene(arena, combatScene);
        arena.transform.position = scan.SpawnPose.position;
        var shadowBudget = CombatShadowBudget.Ensure(combatScene);
        shadowBudget.SetCenter(scan.SpawnPose.position);
        foreach (GameObject root in roots)
        {
            SceneManager.MoveGameObjectToScene(root, combatScene);
            root.transform.SetParent(arena.transform, true);
            shadowBudget.RegisterHierarchy(root);
        }
        shadowBudget.ApplyBudget();
        Physics.SyncTransforms();
        var solver = new CombatArenaSpawnSolver(arena.transform, scan, settings);
        if (!solver.TryPlace(playerCount, enemies, playerSlotCount, enemySlotCount, entryDistance, out layout))
        {
            error = $"Không tìm đủ vị trí đứng an toàn quanh Spawn Position {scan.SpawnPose.position}.";
            return false;
        }

        Debug.Log($"[Combat Arena] Spawn {scan.SpawnPose.position}, {chunks.Count} chunk, {roots.Count} root môi trường.", arena);
        error = null;
        return true;
    }
}

internal sealed class CombatArenaSpawnSolver
{
    const float PlayerRadius = .4f;
    const float PlayerHeight = 1.8f;
    const float EntrySampleSpacing = .25f;
    const float MaxEntryStepHeight = .22f;
    static readonly float[] SearchAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
    static readonly float[] DistanceFactors = { 1f, .72f, 1.24f };
    static readonly Vector3[] LocalNudges =
    {
        Vector3.zero, Vector3.forward, Vector3.back, Vector3.right, Vector3.left,
        Vector3.forward * 2f, Vector3.back * 2f, Vector3.right * 2f, Vector3.left * 2f
    };

    readonly Transform arena;
    readonly CombatAreaScan scan;
    readonly CombatArenaSpawnSettings settings;
    readonly RaycastHit[] floorHits = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[64];
    readonly List<(Vector3 position, float radius)> placed = new List<(Vector3, float)>();

    public CombatArenaSpawnSolver(Transform arena, CombatAreaScan scan, CombatArenaSpawnSettings settings)
    {
        this.arena = arena;
        this.scan = scan;
        this.settings = settings;
    }

    public bool TryPlace(int playerCount, IReadOnlyList<BattleUnit> enemies, int playerSlotCount, int enemySlotCount,
        float entryDistance, out CombatArenaLayout result)
    {
        result = null;
        var playerPoses = new Pose[Mathf.Max(playerCount, playerSlotCount)];
        var enemyPoses = new Pose[Mathf.Max(enemySlotCount, enemies.Count)];
        Vector3 center = scan.SpawnPose.position;
        Vector3 forward = Vector3.ProjectOnPlane(scan.SpawnPose.rotation * Vector3.forward, Vector3.up);
        forward = forward.sqrMagnitude > .001f ? forward.normalized : Vector3.forward;
        bool hasBoss = false;
        foreach (var enemy in enemies)
            if (enemy != null && enemy.isBoss) hasBoss = true;
        float spacing = Mathf.Max(settings.slotSpacing, hasBoss ? settings.bossRadius * 2f : 1f);
        int bestWalkingCount = -1;
        float bestWalkingDistance = -1f;

        foreach (float angle in SearchAngles)
        {
            Quaternion enemyRotation = Quaternion.LookRotation(Quaternion.AngleAxis(angle, Vector3.up) * forward);
            Quaternion playerRotation = Quaternion.LookRotation(-(enemyRotation * Vector3.forward));
            foreach (float factor in DistanceFactors)
            {
                placed.Clear();
                Vector3 direction = enemyRotation * Vector3.forward;
                Vector3 enemyCenter = center - direction * settings.enemyDistance;
                Vector3 playerCenter = center + direction * Mathf.Max(hasBoss ? settings.bossRadius + 1.25f : 2f,
                    settings.playerDistance * factor);
                int i = 0;
                for (; i < enemies.Count; i++)
                {
                    bool boss = enemies[i] != null && enemies[i].isBoss;
                    Vector3 position = enemyCenter + enemyRotation * Vector3.right * ((i - (enemies.Count - 1) * .5f) * spacing);
                    if (!TryPlaceNear(position, enemyRotation, boss ? settings.bossRadius : .55f,
                        boss ? settings.bossHeight : 2.1f, out enemyPoses[i])) break;
                }
                if (i != enemies.Count) continue;
                for (i = 0; i < playerCount; i++)
                {
                    Vector3 position = playerCenter + playerRotation * Vector3.right * (((playerCount - 1) * .5f - i) * settings.slotSpacing);
                    if (!TryPlaceNear(position, playerRotation, PlayerRadius, PlayerHeight, out playerPoses[i])) break;
                }
                if (i != playerCount) continue;

                Vector3[][] paths = null;
                int walkingCount = 0;
                float walkingDistance = 0f;
                bool fullWalk = entryDistance <= .001f;
                if (!fullWalk)
                {
                    fullWalk = true;
                    paths = new Vector3[playerCount][];
                    for (int player = 0; player < playerCount; player++)
                    {
                        paths[player] = BuildEntryPath(playerPoses[player], entryDistance, out float length);
                        if (length > 0f) walkingCount++;
                        walkingDistance += length;
                        fullWalk &= length >= entryDistance - .001f;
                    }
                }

                if (!fullWalk && (walkingCount < bestWalkingCount ||
                    (walkingCount == bestWalkingCount && walkingDistance <= bestWalkingDistance + .01f))) continue;

                int usableEnemies = AddExtraSlots(enemyPoses, enemies.Count, enemyCenter, enemyRotation, spacing, .55f, 2.1f);
                int usablePlayers = AddExtraSlots(playerPoses, playerCount, playerCenter, playerRotation,
                    settings.slotSpacing, PlayerRadius, PlayerHeight);
                result = new CombatArenaLayout
                {
                    players = (Pose[])playerPoses.Clone(),
                    enemies = (Pose[])enemyPoses.Clone(),
                    usablePlayerSlots = usablePlayers,
                    usableEnemySlots = usableEnemies,
                    playerEntryPaths = paths
                };
                if (fullWalk) return true;
                bestWalkingCount = walkingCount;
                bestWalkingDistance = walkingDistance;
            }
        }
        return result != null;
    }

    int AddExtraSlots(Pose[] poses, int count, Vector3 center, Quaternion rotation, float spacing, float radius, float height)
    {
        int usable = count;
        while (usable < poses.Length)
        {
            int extra = usable - count;
            float side = (extra & 1) == 0 ? 1f : -1f;
            Vector3 position = center + rotation * Vector3.right * (side * ((count + 1) * .5f + extra / 2) * spacing);
            if (!TryPlaceNear(position, rotation, radius, height, out poses[usable])) break;
            usable++;
        }
        return usable;
    }

    bool TryPlaceNear(Vector3 center, Quaternion rotation, float radius, float height, out Pose pose)
    {
        foreach (Vector3 offset in LocalNudges)
        {
            if (!TryGround(center + rotation * offset, out Vector3 position) || !ClearCapsule(position, radius, height) ||
                !HasFooting(position, rotation * Vector3.right * (radius * .55f))) continue;
            bool occupied = false;
            foreach (var other in placed)
                if (Vector3.ProjectOnPlane(position - other.position, Vector3.up).sqrMagnitude <
                    Mathf.Pow(radius + other.radius + .15f, 2f))
                {
                    occupied = true;
                    break;
                }
            if (occupied) continue;
            placed.Add((position, radius));
            pose = new Pose(position, rotation);
            return true;
        }
        pose = default;
        return false;
    }

    bool HasFooting(Vector3 center, Vector3 offset) =>
        TryGround(center + offset, out Vector3 right) && Mathf.Abs(right.y - center.y) <= .4f &&
        TryGround(center - offset, out Vector3 left) && Mathf.Abs(left.y - center.y) <= .4f;

    bool TryGround(Vector3 desired, out Vector3 position)
    {
        position = default;
        Vector3 flat = Vector3.ProjectOnPlane(desired - scan.SpawnPose.position, Vector3.up);
        if (flat.sqrMagnitude > scan.Radius * scan.Radius) return false;
        float above = Mathf.Max(.15f, settings.floorSearchHeight);
        int hits = Physics.RaycastNonAlloc(desired + Vector3.up * above, Vector3.down, floorHits,
            above + Mathf.Max(.15f, settings.floorSearchDepth), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity;
        for (int i = 0; i < hits; i++)
        {
            RaycastHit hit = floorHits[i];
            if (hit.normal.y < .7f || !IsArenaCollider(hit.collider)) continue;
            float delta = Mathf.Abs(hit.point.y - desired.y);
            if (delta >= closest) continue;
            closest = delta;
            position = hit.point;
        }
        return !float.IsPositiveInfinity(closest);
    }

    bool ClearCapsule(Vector3 position, float radius, float height)
    {
        Vector3 bottom = position + Vector3.up * (radius + .08f);
        Vector3 top = position + Vector3.up * Mathf.Max(radius + .08f, height - radius);
        int hits = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == overlaps.Length) return false;
        for (int i = 0; i < hits; i++)
            if (IsArenaCollider(overlaps[i])) return false;
        return true;
    }

    Vector3[] BuildEntryPath(Pose slot, float distance, out float length)
    {
        length = 0f;
        int samples = Mathf.CeilToInt(distance / EntrySampleSpacing);
        var path = new List<Vector3>(samples + 1) { slot.position };
        Vector3 backwards = slot.rotation * Vector3.back;
        Vector3 footingOffset = slot.rotation * Vector3.right * (PlayerRadius * .55f);
        Vector3 previous = slot.position;
        for (int i = 1; i <= samples; i++)
        {
            float offset = Mathf.Min(distance, i * EntrySampleSpacing);
            Vector3 sample = slot.position + backwards * offset;
            sample.y = previous.y;
            if (!TryGround(sample, out Vector3 floor) || Mathf.Abs(floor.y - previous.y) > MaxEntryStepHeight ||
                !ClearCapsule(floor, PlayerRadius, PlayerHeight) || !HasFooting(floor, footingOffset)) break;

            path.Add(floor);
            previous = floor;
            length = offset;
        }
        if (path.Count < 2) return Array.Empty<Vector3>();
        path.Reverse();
        return path.ToArray();
    }

    bool IsArenaCollider(Collider collider) => collider != null && collider.transform.IsChildOf(arena);
}
