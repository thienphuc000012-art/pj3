using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldInteractionKind { Pickup, Chest, Discovery, RestPoint, Encounter }

public class WorldInteraction : MonoBehaviour
{
    public string persistentId;
    public string displayName = "Tương tác";
    public WorldInteractionKind kind;
    [Min(0.5f)] public float range = 2.5f;
    [Min(0)] public int experience = 25;
    public List<ItemStack> rewards = new List<ItemStack>();
    public List<BattleUnit> enemyPrefabs = new List<BattleUnit>();
    public bool IsConsumed => CampaignSession.Instance != null && CampaignSession.Instance.Data.collected.Contains(Id);
    // A duplicated prefab must not share collection/defeat state with another instance.
    public string Id => gameObject.scene.name + ":" + persistentId + ":" + HierarchyPath(transform);
    private static string HierarchyPath(Transform t) => t.parent == null ? t.name + "#" + t.GetSiblingIndex() : HierarchyPath(t.parent) + "/" + t.name + "#" + t.GetSiblingIndex();
    void OnValidate() { if (string.IsNullOrEmpty(persistentId)) persistentId = Guid.NewGuid().ToString("N"); }
    public void Interact()
    {
        var session = CampaignSession.Instance;
        if (session == null || session.Busy || IsConsumed) return;
        if (kind == WorldInteractionKind.RestPoint) { session.OpenRest(this); return; }
        if (kind == WorldInteractionKind.Encounter) { session.BeginEncounter(this); return; }
        if (!session.IsNear(this)) return;
        foreach (var reward in rewards) session.AddItem(reward.id, reward.count);
        session.GainXP(experience);
        session.Data.collected.Add(Id);
        session.Notify(displayName + " • +" + experience + " EXP");
        session.Save();
        gameObject.SetActive(false);
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == WorldInteractionKind.Encounter ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
