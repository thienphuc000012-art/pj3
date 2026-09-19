#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Opt-in validation: menu command, or a one-shot request in Library.
public static class AdventureValidation
{
    private const string Request = "Library/CombatValidation/AdventureValidation.request";
    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += RunRequested;
    }
    static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        Validate();
    }
    [MenuItem("Tools/Adventure/Validate Campaign")]
    public static void Validate()
    {
        string result = "";
        try
        {
            var config = AssetDatabase.LoadAssetAtPath<CampaignConfig>("Assets/Resources/Adventure/CampaignConfig.asset");
            Require(config != null, "Campaign config imports");
            Require(config.startingParty.Count == 2, "Starting party has two members");
            foreach (var unit in config.startingParty.Concat(new[] { config.demoEnemy }))
            {
                Require(unit != null && unit.animator != null && unit.defaultAttack != null, "Battle prefab references resolve");
                Require(unit.GetComponent<BattleUnit_AnimationEvents>()?.ownerUnit == unit, "Animation events reference the cloned unit");
                Require(unit.animator.runtimeAnimatorController != null, "Battle animator controller resolves");
                Require(unit.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(), "Battle prefab contains character mesh");
                Require(PrefabUtility.GetPrefabAssetType(unit) != PrefabAssetType.MissingAsset, "Prefab variant source resolves");
            }
            Require(config.items.Select(x => x.id).Distinct().Count() == config.items.Count, "Inventory IDs unique");
            Require(config.items.All(x => !string.IsNullOrEmpty(x.id)), "Inventory IDs not empty");
            foreach (var recipe in config.recipes)
            {
                Require(config.FindItem(recipe.output.id) != null && recipe.output.count > 0, "Recipe output valid");
                Require(recipe.ingredients.All(x => config.FindItem(x.id) != null && x.count > 0), "Recipe ingredients valid");
            }
            var member = new PartyMemberProgress();
            member.GainXP(-5); Require(member.xp == 0 && member.level == 1, "Negative XP ignored");
            member.GainXP(99); Require(member.xp == 99 && member.level == 1, "XP before boundary");
            member.GainXP(1); Require(member.xp == 0 && member.level == 2 && member.statPoints == 3 && member.skillPoints == 1, "Exact level boundary awards points once");
            member.GainXP(350); Require(member.level == 4 && member.xp == 0 && member.statPoints == 9, "Multiple level gain uses increasing thresholds");
            var save = new CampaignSave(); save.party.Add(member); save.inventory.Add(new ItemStack("herb", 3)); save.collected.Add("test-chest"); save.hasCheckpoint = true; save.checkpoint = new Vector3(1, 2, 3);
            var decoded = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(save));
            Require(decoded.party[0].level == 4 && decoded.inventory[0].count == 3 && decoded.collected.Contains("test-chest") && decoded.checkpoint == save.checkpoint, "Save roundtrip preserves progress, loot, world state and checkpoint");
            var build = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => Path.GetFileNameWithoutExtension(x.path)).ToArray();
            Require(build.Contains(config.mapScene) && build.Contains(config.battleScene), "Both scenes enabled in build");
            Require(build.Contains(LoadingScreen.SceneName), "Loading scene enabled in build");
            var loadingPreview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Loading.unity");
            try
            {
                Require(loadingPreview.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LoadingScreen>(true)).Count() == 1, "Loading scene has one controller");
                Require(loadingPreview.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>(true)).Any(), "Loading scene has a camera");
            }
            finally { EditorSceneManager.ClosePreviewScene(loadingPreview); }
            var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/combattest.unity");
            try
            {
                var manager = preview.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CombatManager>(true)).Single();
                Require(manager.playerSlots.Length >= config.startingParty.Count && manager.playerSlots.All(x => x != null), "Party battle slots available");
                Require(manager.enemySlots.Length > 0 && manager.enemySlots[0] != null, "Enemy battle slot available");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            result = "PASS: prefab imports/references, animation owners, meshes, recipes, XP boundaries, save roundtrip, build scenes and battle slots.";
            Debug.Log(result);
        }
        catch (Exception e) { result = "FAIL: " + e; Debug.LogError(result); }
        Directory.CreateDirectory("Library/CombatValidation");
        File.WriteAllText("Library/CombatValidation/AdventureValidation.result.txt", result);
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
