using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EnemyTimeOfDay { Always, DayOnly, NightOnly }

[Serializable]
public sealed class EnemySpawnSettings
{
    public bool respawn = true;
    [Min(1), Tooltip("Giây thời gian chơi, không tính lúc tắt game hoặc màn hình tải. Mặc định 300 giây = 5 phút.")]
    public float respawnSeconds = 300f;
    [Tooltip("Ban ngày: 06:00–18:00. Boss bỏ qua toàn bộ các thiết lập này.")]
    public EnemyTimeOfDay timeOfDay;
}

[Serializable]
public sealed class EnemyRespawnProgress
{
    public string worldId;
    public double readyAtPlaySeconds;
}

/// <summary>Save-only rules, also used by isolated Edit Mode validation.</summary>
public static class EnemySpawnRules
{
    public static bool IsPermanent(WorldInteraction point, CampaignConfig config)
    {
        if (point == null || point.kind != WorldInteractionKind.Encounter) return true;
        if (point.enemyPrefabs?.Any(enemy => enemy != null && enemy.isBoss) == true) return true;
        if (config != null && config.IsGuidedTutorialEncounter(point)) return true;
        var portal = QuestPortalDefinition.Current;
        return portal != null && portal.enemyWorldIds.Contains(point.Id);
    }
    public static bool IsTimeAllowed(EnemyTimeOfDay period, double hours)
    {
        if (double.IsNaN(hours) || double.IsInfinity(hours)) return period == EnemyTimeOfDay.Always;
        double hour = (hours % 24 + 24) % 24;
        bool day = hour >= 6 && hour < 18;
        return period == EnemyTimeOfDay.Always || (period == EnemyTimeOfDay.DayOnly ? day : !day);
    }
    public static void Schedule(CampaignSave data, string id, float delay)
    {
        if (data == null || string.IsNullOrEmpty(id)) return;
        data.enemyRespawns ??= new List<EnemyRespawnProgress>();
        var timer = data.enemyRespawns.Find(value => value != null && value.worldId == id);
        if (timer == null) { timer = new EnemyRespawnProgress { worldId = id }; data.enemyRespawns.Add(timer); }
        timer.readyAtPlaySeconds = Math.Max(0, data.playTimeSeconds) + (float.IsNaN(delay) || float.IsInfinity(delay) ? 300 : Math.Max(1, delay));
    }
    public static bool IsReady(CampaignSave data, string id)
    {
        var timer = data?.enemyRespawns?.Find(value => value != null && value.worldId == id);
        return timer != null && data.playTimeSeconds >= timer.readyAtPlaySeconds;
    }
    public static bool Revive(CampaignSave data, string id)
    {
        if (!IsReady(data, id)) return false;
        data.collected.Remove(id);
        data.enemyRespawns.RemoveAll(value => value == null || value.worldId == id);
        return true;
    }
}

/// <summary>Lives on the session, so disabled enemies can return without an Update on their root.</summary>
[DisallowMultipleComponent]
public sealed class WorldEnemySpawns : MonoBehaviour
{
    sealed class Entry { public WorldInteraction point; public bool hidden; }
    readonly Dictionary<WorldInteraction, Entry> entries = new Dictionary<WorldInteraction, Entry>();
    readonly List<WorldInteraction> stale = new List<WorldInteraction>();
    float nextCheck;
    public void Register(WorldInteraction point)
    {
        var session = GetComponent<CampaignSession>();
        if (point == null || session?.Data == null || EnemySpawnRules.IsPermanent(point, session.Config) || entries.ContainsKey(point)) return;
        // Only take ownership of authored active enemies. Explicitly disabled objects stay disabled.
        if (!point.gameObject.activeInHierarchy || !point.enabled) return;
        EnemyCombatOrigin.CaptureFor(point);
        entries.Add(point, new Entry { point = point });
        var settings = point.enemySpawn ??= new EnemySpawnSettings();
        if (settings.respawn && point.IsConsumed && session.Data.enemyRespawns?.Any(timer => timer != null && timer.worldId == point.Id) != true)
            EnemySpawnRules.Schedule(session.Data, point.Id, settings.respawnSeconds);
        Apply(entries[point], session);
    }
    void Update()
    {
        var session = GetComponent<CampaignSession>();
        if (Time.unscaledTime < nextCheck || session?.Data == null || !session.OnMap || session.MapPlayer == null ||
            session.Busy || session.PresentationActive || AdventureCheatPanel.IsOpen || LoadingScreen.IsLoading || MapChunkStreamer.MovementBlocked) return;
        nextCheck = Time.unscaledTime + .5f;
        bool changed = false, revived = false;
        stale.Clear();
        foreach (var pair in entries)
        {
            if (pair.Key == null) { stale.Add(pair.Key); continue; }
            var entry = pair.Value;
            var point = entry.point;
            var settings = point.enemySpawn ??= new EnemySpawnSettings();
            if (point.IsConsumed && settings.respawn && EnemySpawnRules.IsReady(session.Data, point.Id) &&
                EnemySpawnRules.IsTimeAllowed(settings.timeOfDay, AdventureWorldTime.Read(session.Data)))
            {
                var origin = point.GetComponent<EnemyCombatOrigin>();
                Vector3 spawn = origin != null && origin.HasSpawnPoint ? origin.SpawnPosition : point.transform.position;
                // Wait until the player leaves the attack radius before materializing an enemy.
                if ((session.MapPlayer.transform.position - spawn).sqrMagnitude > Mathf.Pow(Mathf.Max(6, point.range + 2), 2))
                    revived |= EnemySpawnRules.Revive(session.Data, point.Id);
            }
            changed |= Apply(entry, session);
        }
        foreach (var point in stale) entries.Remove(point);
        if (changed || revived) session.RefreshWorld();
        if (revived) session.Save(capturePosition: false);
    }
    static bool Apply(Entry entry, CampaignSession session)
    {
        var point = entry.point;
        bool allowed = !point.IsConsumed && EnemySpawnRules.IsTimeAllowed(point.enemySpawn.timeOfDay, AdventureWorldTime.Read(session.Data));
        if (!allowed && point.gameObject.activeSelf)
        { entry.hidden = true; point.gameObject.SetActive(false); return true; }
        if (allowed && entry.hidden)
        {
            entry.hidden = false;
            point.gameObject.SetActive(true);
            point.GetComponent<EnemyEncounterBehaviour>()?.ResetForRespawn();
            return true;
        }
        return false;
    }
}
