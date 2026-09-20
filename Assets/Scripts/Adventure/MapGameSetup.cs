using System.Collections.Generic;
using UnityEngine;

public class MapGameSetup : MonoBehaviour
{
    public CampaignConfig config;
    public PlayerScript player;
    public bool createStarterInteractions = true;
    private System.Collections.IEnumerator Start()
    {
        if (config == null) config = Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
        if (player == null) player = FindFirstObjectByType<PlayerScript>();
        if (config == null || player == null) { Debug.LogError("MapGameSetup requires campaign config and map player."); yield break; }
        // Use the original spawn for sample placement, even after loading a save.
        Vector3 origin = player.transform.position;
        var session = CampaignSession.Ensure(config);
        session.BindMap(player);
        var streaming = GetComponent<MapChunkStreamer>();
        if (streaming != null)
        {
            yield return streaming.Initialize(player);
            if (!streaming.Ready) yield break;
        }
        if (createStarterInteractions) CreateStarterPoints(origin);
        session.RefreshWorld();
    }
    void CreateStarterPoints(Vector3 origin)
    {
        CreatePoint("starter-rest", "Điểm nghỉ • Lưu game / Hồi máu", WorldInteractionKind.RestPoint, Ground(origin + new Vector3(2, 0, 0)), Color.cyan, 0, null);
        CreatePoint("starter-herb", "Nhặt thảo dược", WorldInteractionKind.Pickup, Ground(origin + new Vector3(-2, 0, 2)), Color.green, 0, new List<ItemStack> { new ItemStack("herb", 3) });
        CreatePoint("starter-chest", "Mở rương", WorldInteractionKind.Chest, Ground(origin + new Vector3(4, 0, 4)), new Color(.8f, .55f, .15f), 30, new List<ItemStack> { new ItemStack("potion", 2), new ItemStack("ore", 3), new ItemStack("herb", 2) });
        CreatePoint("starter-discovery", "Khám phá khu vực mới", WorldInteractionKind.Discovery, Ground(origin + new Vector3(-6, 0, 5)), Color.blue, 50, null);
        if (config.demoEnemy != null)
        {
            var point = CreatePoint("starter-enemy", "Tấn công quái", WorldInteractionKind.Encounter, Ground(origin + new Vector3(0, 0, 9)), Color.red, 100,
                new List<ItemStack> { new ItemStack("herb", 2), new ItemStack("ore", 2) });
            point.enemyPrefabs.Add(config.demoEnemy);
            var model = Instantiate(config.demoEnemy, point.transform.position, Quaternion.identity, point.transform);
            model.gameObject.SetActive(true);
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (model.animator != null) model.animator.applyRootMotion = false;
        }
    }
    Vector3 Ground(Vector3 position)
    {
        int mask = player.surfaceLayer.value != 0 ? player.surfaceLayer.value : Physics.DefaultRaycastLayers;
        if (Physics.Raycast(position + Vector3.up * 3, Vector3.down, out RaycastHit hit, 8, mask, QueryTriggerInteraction.Ignore)) return hit.point;
        return position;
    }
    WorldInteraction CreatePoint(string id, string label, WorldInteractionKind kind, Vector3 position, Color color, int xp, List<ItemStack> rewards)
    {
        var obj = new GameObject(label); obj.transform.SetParent(transform); obj.transform.position = position;
        var point = obj.AddComponent<WorldInteraction>(); point.persistentId = id; point.displayName = label; point.kind = kind; point.experience = xp;
        if (rewards != null) point.rewards = rewards;
        if (kind != WorldInteractionKind.Encounter && kind != WorldInteractionKind.Discovery)
        {
            var marker = GameObject.CreatePrimitive(kind == WorldInteractionKind.Chest ? PrimitiveType.Cube : PrimitiveType.Sphere);
            marker.name = "Marker"; marker.transform.SetParent(obj.transform, false); marker.transform.localPosition = Vector3.up * .45f;
            marker.transform.localScale = kind == WorldInteractionKind.Chest ? new Vector3(.9f, .65f, .65f) : Vector3.one * .55f;
            Destroy(marker.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color;
            marker.GetComponent<Renderer>().material = material;
            obj.AddComponent<AdventureMarkerMaterial>().material = material;
        }
        return point;
    }
}
