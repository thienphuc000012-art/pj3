using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Adventure/Quest Portal Sequence")]
public sealed class QuestPortalDefinition : ScriptableObject
{
    public string sequenceId = "m.main-quest.portal";
    public AdventureQuest quest;
    public string mapScene = "m", questMarkerId = "12";
    public List<string> enemyWorldIds = new List<string>();
    static QuestPortalDefinition cached;
    public static QuestPortalDefinition Current => cached != null ? cached : cached = Resources.Load<QuestPortalDefinition>("Adventure/MainQuestPortal");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ClearCachedDefinition() => cached = null;
    public QuestPortalProgress Progress(CampaignSave data, bool create)
    {
        if (data == null) return null;
        data.questPortals ??= new List<QuestPortalProgress>();
        var progress = data.questPortals.Find(p => p != null && p.id == sequenceId);
        if (progress != null) { progress.defeated ??= new List<string>(); progress.presented ??= new List<string>(); return progress; }
        if (!create) return null;
        progress = new QuestPortalProgress { id = sequenceId }; data.questPortals.Add(progress);
        // Older saves have no cutscene flags. Already-earned kills stay earned without replaying old scenes.
        int oldKills = data.quests?.Find(p => p != null && quest != null && p.questId == quest.Id)?.objectiveCount ?? 0;
        foreach (var id in enemyWorldIds)
            if (oldKills > 0 && data.collected != null && data.collected.Contains(id)) { progress.defeated.Add(id); progress.presented.Add(id); oldKills--; }
        return progress;
    }
    public bool RecordVictory(CampaignSave data, string scene, string marker, string worldId)
    {
        if (quest == null || scene != mapScene || marker != questMarkerId || !enemyWorldIds.Contains(worldId) ||
            data?.quests?.Find(p => p != null && p.questId == quest.Id) == null) return false;
        var progress = Progress(data, true);
        if (progress.defeated.Contains(worldId)) return false;
        progress.defeated.Add(worldId); return true;
    }
    public bool Pending(CampaignSave data, string id)
    {
        var progress = Progress(data, false);
        return progress != null && progress.defeated.Contains(id) && !progress.presented.Contains(id);
    }
    public bool IsUnlocked(CampaignSave data)
    {
        var progress = Progress(data, false);
        return enemyWorldIds.Count > 0 && progress != null && enemyWorldIds.TrueForAll(id => progress.defeated.Contains(id) && progress.presented.Contains(id));
    }
    public static bool KeepForPresentation(WorldInteraction point)
    {
        var definition = Current; var data = CampaignSession.Instance?.Data;
        if (definition == null || point == null || point.kind != WorldInteractionKind.Encounter || point.gameObject.scene.name != definition.mapScene || !definition.Pending(data, point.Id)) return false;
        var patrol = point.GetComponent<EnemyEncounterBehaviour>(); if (patrol != null) patrol.enabled = false;
        var agent = point.GetComponent<NavMeshAgent>(); if (agent != null) agent.enabled = false;
        return true;
    }
}

[Serializable]
public sealed class QuestPortalProgress
{
    public string id;
    public List<string> defeated = new List<string>();
    public List<string> presented = new List<string>();
}
