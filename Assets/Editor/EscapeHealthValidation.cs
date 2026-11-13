using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// In-memory campaign and battle objects only. Never writes real saves or loads a map.
public static class EscapeHealthValidation
{
    const string Request = "Library/CombatValidation/EscapeHealth.request";
    const string Result = "Library/CombatValidation/EscapeHealth.result.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += RunRequested; }
    static void RunRequested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        File.Delete(Request); Validate();
    }

    [MenuItem("Tools/Adventure/Combat/Validate Escape HP Persistence")]
    public static void Validate()
    {
        var oldCombat = CombatManager.Instance;
        var oldUi = AdvancedUIManager.Instance;
        var oldSession = CampaignSession.Instance;
        var instance = typeof(CampaignSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Scene preview = default;
        CampaignConfig config = null;
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in Edit Mode.");
            preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Isolated Escape HP Check"); root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, preview);
            var session = root.AddComponent<CampaignSession>();
            var manager = root.AddComponent<CombatManager>();
            CombatManager.Instance = null; AdvancedUIManager.Instance = null;
            config = ScriptableObject.CreateInstance<CampaignConfig>();
            var data = new CampaignSave { currentMapScene = "mapgame", hasPosition = true,
                position = new Vector3(20, 5, -10), rotation = Quaternion.Euler(0, 123, 0) };
            Set(session, "<Config>k__BackingField", config); Set(session, "<Data>k__BackingField", data);
            manager.playerParty = new List<BattleUnit>();
            for (int i = 0; i < 3; i++)
            {
                var obj = new GameObject("Party " + i); obj.transform.SetParent(root.transform);
                var unit = obj.AddComponent<BattleUnit>(); unit.isPlayer = true; unit.maxHP = i == 1 ? 150 : 100; unit.baseDef = 5;
                unit.SetPersistentHP(unit.maxHP);
                config.startingParty.Add(unit); manager.playerParty.Add(unit);
                data.party.Add(new PartyMemberProgress { prefabIndex = i, hp = unit.maxHP });
            }
            manager.playerParty[0].TakeDamage(45, false); // 100 - (45 - 5) = 60
            manager.playerParty[1].TakeDamage(35, false); // 150 - (35 - 5) = 120
            manager.playerParty[2].TakeDamage(200, false);
            Call(session, "CaptureBattlePartyHealth", manager);
            Check(data.party.Select(x => x.hp).SequenceEqual(new[] { 60, 120, 0 }), "Escape must copy actual HP for every member, including zero.");
            var reloaded = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(data));
            Check(reloaded.party.Select(x => x.hp).SequenceEqual(new[] { 60, 120, 0 }) && reloaded.position == data.position && reloaded.rotation == data.rotation,
                "Save roundtrip must preserve wounded HP and encounter return location.");
            // Null battle entries preserve the corresponding stored member instead of resetting HP.
            var first = manager.playerParty[0]; manager.playerParty[0] = null;
            Call(session, "CaptureBattlePartyHealth", manager); Check(data.party[0].hp == 60, "Missing unit reset health.");
            manager.playerParty[0] = first;

            // A campfire at the return position must not immediately refill HP after fleeing.
            Vector3 arrival = data.position;
            Set(session, "preserveHpOnNextMapArrival", true);
            Call(session, "ResetRestHealingOnMapArrival", arrival);
            var rest = Rest("arrival-rest", arrival + Vector3.right, 2.5f);
            var overlapping = Rest("overlapping-rest", arrival, 4);
            for (int frame = 0; frame < 10; frame++)
            {
                Call(session, "UpdateArrivalRestExits", arrival);
                Check(!CanHeal(rest, true) && !CanHeal(overlapping, true), "Campfire healed HP simply because escape reloaded its scene.");
            }
            Check(data.party[0].hp == 60, "HP did not stay wounded on return.");
            Check(!CanHeal(rest, false) && !CanHeal(rest, true), "Temporarily disabling a rest chunk must not count as walking out of its radius.");
            var delayed = Rest("streamed-rest", arrival + Vector3.left, 2.5f);
            Check(!CanHeal(delayed, true), "A later-loaded rest chunk bypassed arrival protection.");
            // Even if the rest chunk unloads, leaving its cached radius unlocks future healing.
            string restId = rest.savedWorldId;
            UnityEngine.Object.DestroyImmediate(rest.gameObject);
            Call(session, "UpdateArrivalRestExits", arrival + Vector3.right * 20);
            rest = Rest(restId, arrival + Vector3.right, 2.5f);
            Check(CanHeal(rest, true) && CanHeal(overlapping, true) && CanHeal(delayed, true), "Rest healing did not resume after leaving and returning.");
            Check((bool)Call(session, "RestorePartyHealth") && data.party.Select(x => x.hp).SequenceEqual(new[] { 100, 150, 100 }),
                "Normal campfire recovery no longer works after returning to its radius.");

            // Reaching a different rest point is a deliberate approach and still heals normally.
            Set(session, "preserveHpOnNextMapArrival", true); Call(session, "ResetRestHealingOnMapArrival", arrival);
            var distantRest = Rest("new-rest", arrival + Vector3.forward * 20, 2.5f);
            Check(CanHeal(distantRest, true), "Escape must not disable every rest point on the map.");
            // Normal map arrivals retain existing auto-heal behaviour.
            Call(session, "ResetRestHealingOnMapArrival", arrival);
            Check(CanHeal(rest, true), "Normal arrivals should not inherit the previous escape flag.");

            // A failed escape handoff rolls back data without losing an existing consumed encounter.
            data.collected.Add("escape-test");
            Call(session, "RestoreEscapeData", new[] { 60, 120, 0 }, false, "escape-test");
            Check(!data.collected.Contains("escape-test") && data.party[0].hp == 60, "Failed handoff rollback corrupted health or encounter state.");
            data.collected.Add("escape-test");
            Call(session, "RestoreEscapeData", new[] { 60, 120, 0 }, true, "escape-test");
            Check(data.collected.Contains("escape-test"), "Rollback removed an already consumed encounter.");

            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            File.WriteAllText(Result, "PASS: enemy damage 100->60 / 150->120 and defeated HP 0 persist; save roundtrip and map position; null unit safety; return near overlapping campfires does not heal; delayed/unloaded chunks; leaving and re-entering restores auto-heal; other rest points and normal map arrivals unchanged; failed transition rollback. No real saves written or scenes loaded.");
            Debug.Log(File.ReadAllText(Result));

            WorldInteraction Rest(string id, Vector3 position, float range)
            {
                var obj = new GameObject(id); obj.transform.SetParent(root.transform); obj.transform.position = position;
                var point = obj.AddComponent<WorldInteraction>(); point.kind = WorldInteractionKind.RestPoint;
                point.savedWorldId = id; point.range = range; return point;
            }
            bool CanHeal(WorldInteraction point, bool near) => (bool)Call(session, "CanAutoHealAtRestPoint", point, near);
        }
        catch (Exception error)
        { Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllText(Result, "FAIL: " + error); Debug.LogException(error); }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            CombatManager.Instance = oldCombat; AdvancedUIManager.Instance = oldUi; instance.SetValue(null, oldSession);
        }
    }
    static object Call(CampaignSession target, string method, params object[] args) => typeof(CampaignSession).GetMethod(method, Private).Invoke(target, args);
    static void Set(CampaignSession target, string field, object value) => typeof(CampaignSession).GetField(field, Private).SetValue(target, value);
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
