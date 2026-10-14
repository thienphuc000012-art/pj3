using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldInteractionKind { Pickup, Chest, Discovery, RestPoint, Encounter }

public class WorldInteraction : MonoBehaviour
{
    public string persistentId;
    [HideInInspector] public string savedWorldId;
    public string displayName = "Tương tác";
    public WorldInteractionKind kind;
    [Min(0.5f)] public float range = 2.5f;
    [Min(0)] public int experience = 25;
    public List<ItemStack> rewards = new List<ItemStack>();
    [Header("Pickup 3D Model")]
    public GameObject pickupModelPrefab;
    public Vector3 pickupModelPosition = Vector3.zero;
    public Vector3 pickupModelRotation = Vector3.zero;
    public Vector3 pickupModelScale = Vector3.one;
    public string pickupAnimationState = "Base Layer.pickupitem";
    [Range(0f, 1f), Tooltip("Mốc tay nhặt item trong animation: 0.5 là giữa clip.")]
    public float pickupMoment = 0.5f;
    [Tooltip("Dùng event AnimEvent_PickupItem trên clip thay cho mốc phần trăm.")]
    public bool pickupUseAnimationEvent;
    [Min(0.1f)] public float pickupAnimationTimeout = 10f;
    [HideInInspector] public GameObject pickupModelInstance;
    [HideInInspector] public GameObject pickupModelSource;

    void OnEnable()
    {
        if (!Application.isPlaying || kind != WorldInteractionKind.Pickup) return;
        if (pickupModelInstance != null && pickupModelSource != pickupModelPrefab)
        {
            pickupModelInstance.SetActive(false);
            Destroy(pickupModelInstance);
            pickupModelInstance = null;
        }
        if (pickupModelInstance == null && pickupModelPrefab != null &&
            pickupModelPrefab.GetComponentInChildren<WorldInteraction>(true) == null)
        {
            pickupModelInstance = Instantiate(pickupModelPrefab, transform);
            pickupModelSource = pickupModelPrefab;
            pickupModelInstance.name = "Pickup Model";
        }
        ApplyPickupModelTransform();
    }

    public void ApplyPickupModelTransform()
    {
        if (pickupModelInstance == null) return;
        pickupModelInstance.transform.localPosition = pickupModelPosition;
        pickupModelInstance.transform.localRotation = Quaternion.Euler(pickupModelRotation);
        pickupModelInstance.transform.localScale = pickupModelScale;
    }
    public List<BattleUnit> enemyPrefabs = new List<BattleUnit>();
    [Header("Combat Scene Setup")]
    [Tooltip("ID của BattleEncounterSetup trong scene combat. Ví dụ: goblin_01, boss_dragon_01.")]
    public string combatSetupId = "default";
    public bool IsConsumed => CampaignSession.Instance != null && CampaignSession.Instance.Data.collected.Contains(Id);
    // A duplicated prefab must not share collection/defeat state with another instance.
    public string Id => string.IsNullOrEmpty(savedWorldId) ? gameObject.scene.name + ":" + persistentId + ":" + HierarchyPath(transform) : savedWorldId;
    private static string HierarchyPath(Transform t) => t.parent == null ? t.name + "#" + t.GetSiblingIndex() : HierarchyPath(t.parent) + "/" + t.name + "#" + t.GetSiblingIndex();
    void OnValidate() { if (string.IsNullOrEmpty(persistentId)) persistentId = Guid.NewGuid().ToString("N"); }
    public void Interact()
    {
        var session = CampaignSession.Instance;
        if (session == null || session.Busy || IsConsumed) return;
        if (kind == WorldInteractionKind.RestPoint) { session.OpenRest(this); return; }
        if (kind == WorldInteractionKind.Encounter) { session.BeginEncounter(this); return; }
        if (!session.IsNear(this)) return;
        if (kind == WorldInteractionKind.Pickup) { session.BeginItemPickup(this); return; }
        CompleteCollection();
    }

    public void CompleteCollection()
    {
        var session = CampaignSession.Instance;
        if (session == null || IsConsumed || !session.IsNear(this)) return;
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
