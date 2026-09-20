using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldInteractionKind
{
    Pickup,
    Chest,
    Discovery,
    RestPoint,
    Encounter
}

public class WorldInteraction : MonoBehaviour
{
    [Header("Persistent Save ID")]
    [SerializeField]
    public string persistentId;

    [HideInInspector]
    public string savedWorldId;

    [Header("Interaction")]
    public string displayName = "Tương tác";

    public WorldInteractionKind kind;

    [Min(0.5f)]
    public float range = 2.5f;

    [Min(0)]
    public int experience = 25;

    public List<ItemStack> rewards = new List<ItemStack>();

    [Header("Encounter")]
    public List<BattleUnit> enemyPrefabs = new List<BattleUnit>();


    // ============================================================
    // ID
    // ============================================================

    public string Id
    {
        get
        {
            // Nếu editor/tool đã gán một world ID cố định
            if (!string.IsNullOrEmpty(savedWorldId))
                return savedWorldId;

            return
                gameObject.scene.name +
                ":" +
                persistentId +
                ":" +
                HierarchyPath(transform);
        }
    }


    public bool IsConsumed
    {
        get
        {
            CampaignSession session = CampaignSession.Instance;

            if (session == null)
                return false;

            if (session.Data == null)
                return false;

            if (session.Data.collected == null)
                return false;

            return session.Data.collected.Contains(Id);
        }
    }


    static string HierarchyPath(Transform t)
    {
        if (t == null)
            return "";

        if (t.parent == null)
        {
            return
                t.name +
                "#" +
                t.GetSiblingIndex();
        }

        return
            HierarchyPath(t.parent) +
            "/" +
            t.name +
            "#" +
            t.GetSiblingIndex();
    }


    // ============================================================
    // EDITOR VALIDATION
    // ============================================================

    void OnValidate()
    {
        // Mỗi interaction cần có ID.
        if (string.IsNullOrEmpty(persistentId))
        {
            persistentId =
                Guid.NewGuid().ToString("N");
        }
    }


    // ============================================================
    // REGENERATE ID
    //
    // Trong Inspector:
    // Component -> menu ba chấm -> Regenerate Persistent ID
    //
    // Dùng khi vừa tạo Enemy Encounter mới nhưng save cũ
    // đang nhận nhầm nó là encounter đã chết.
    // ============================================================

    [ContextMenu("Regenerate Persistent ID")]
    public void RegeneratePersistentId()
    {
        string oldId = persistentId;

        persistentId =
            Guid.NewGuid().ToString("N");

        // savedWorldId cũ cũng phải bỏ
        // nếu muốn coi đây là encounter hoàn toàn mới.
        savedWorldId = "";

        Debug.Log(
            "[Adventure] Regenerated WorldInteraction ID\n" +
            "Object: " + name + "\n" +
            "Old: " + oldId + "\n" +
            "New: " + persistentId,
            this
        );

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }


    // ============================================================
    // DEBUG
    // ============================================================

    [ContextMenu("Print Interaction Save State")]
    public void PrintSaveState()
    {
        Debug.Log(
            "[Adventure Interaction]\n" +
            "Name: " + name + "\n" +
            "Kind: " + kind + "\n" +
            "Persistent ID: " + persistentId + "\n" +
            "Full ID: " + Id + "\n" +
            "Consumed: " + IsConsumed,
            this
        );
    }


    // ============================================================
    // INTERACT
    // ============================================================

    public void Interact()
    {
        CampaignSession session =
            CampaignSession.Instance;


        if (session == null)
            return;


        if (session.Busy)
            return;


        if (IsConsumed)
        {
            Debug.LogWarning(
                "[Adventure] Interaction bị chặn vì đã Consumed.\n" +
                "Object: " + name + "\n" +
                "ID: " + Id,
                this
            );

            return;
        }


        // ========================================================
        // REST POINT
        // ========================================================

        if (kind == WorldInteractionKind.RestPoint)
        {
            session.OpenRest(this);
            return;
        }


        // ========================================================
        // ENCOUNTER
        // ========================================================

        if (kind == WorldInteractionKind.Encounter)
        {
            if (enemyPrefabs == null ||
                enemyPrefabs.Count == 0)
            {
                Debug.LogWarning(
                    "[Adventure] Encounter không có Enemy Prefab.",
                    this
                );

                return;
            }


            Debug.Log(
                "[Adventure] Starting Encounter\n" +
                "Name: " + name + "\n" +
                "ID: " + Id + "\n" +
                "Enemy count: " + enemyPrefabs.Count,
                this
            );


            session.BeginEncounter(this);

            return;
        }


        // ========================================================
        // NORMAL INTERACTION
        // ========================================================

        if (!session.IsNear(this))
            return;


        if (rewards != null)
        {
            foreach (ItemStack reward in rewards)
            {
                if (reward == null)
                    continue;

                session.AddItem(
                    reward.id,
                    reward.count
                );
            }
        }


        session.GainXP(experience);


        if (!session.Data.collected.Contains(Id))
        {
            session.Data.collected.Add(Id);
        }


        session.Notify(
            displayName +
            " • +" +
            experience +
            " EXP"
        );


        session.Save();


        gameObject.SetActive(false);
    }


    // ============================================================
    // GIZMOS
    // ============================================================

    void OnDrawGizmosSelected()
    {
        if (kind == WorldInteractionKind.Encounter)
            Gizmos.color = Color.red;
        else
            Gizmos.color = Color.cyan;


        Gizmos.DrawWireSphere(
            transform.position,
            range
        );
    }
}