using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Stores a serialized component identity that remains the same in the built scene.</summary>
public static class GuidedTutorialEncounterAuthoring
{
    const string Prefix = "guided-tutorial:v2:";
    public static string NativeIdentity(WorldInteraction point)
    {
        var identity = GlobalObjectId.GetGlobalObjectIdSlow(point);
        return identity.targetObjectId == 0 || identity.assetGUID.ToString() == new string('0', 32) ? null : identity.ToString();
    }
    static WorldInteraction[] Points(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<WorldInteraction>(true)).Where(point => point.kind == WorldInteractionKind.Encounter).ToArray();

    public static WorldInteraction Resolve(CampaignConfig config, Scene scene)
    {
        if (config == null || !config.HasGuidedTutorialEncounter || !scene.IsValid() || !scene.isLoaded) return null;
        var points = Points(scene); string key = config.GuidedTutorialSelectionKey;
        if (key.StartsWith(Prefix, StringComparison.Ordinal))
        {
            string id = key.Substring(Prefix.Length);
            var matches = points.Where(point => point.GuidedTutorialEncounterId == id).ToArray();
            // A normal Unity duplicate copies serialized fields. Prefer the original component.
            var original = matches.Where(point => NativeIdentity(point) == id).ToArray();
            return original.Length == 1 ? original[0] : matches.Length == 1 ? matches[0] : null;
        }
        var exact = points.Where(point => point.LegacyGuidedTutorialEncounterKey == key).ToArray();
        if (exact.Length == 1) return exact[0];
        string[] parts = key.Split('|');
        if (parts.Length < 2 || Path.GetFileNameWithoutExtension(parts[0]) != scene.name) return null;
        // Old selection may have moved in the Hierarchy. Recover only an unambiguous world ID.
        var byId = points.Where(point => point.Id == parts[1] ||
            (string.IsNullOrEmpty(point.savedWorldId) && !string.IsNullOrEmpty(point.persistentId) && parts[1].Contains(":" + point.persistentId + ":"))).ToArray();
        return byId.Length == 1 ? byId[0] : null;
    }
    public static bool Synchronize(CampaignConfig config, Scene scene, WorldInteraction preferred = null)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded) return false;
        var selected = preferred != null ? preferred : Resolve(config, scene);
        bool changed = false;
        var used = new System.Collections.Generic.HashSet<string>();
        // Process the selected original first when a newly created object still has no file ID.
        foreach (var point in Points(scene).OrderByDescending(point => point == selected))
        {
            if (point != selected && string.IsNullOrEmpty(point.GuidedTutorialEncounterId)) continue;
            string id = NativeIdentity(point);
            if (string.IsNullOrEmpty(id))
                id = !string.IsNullOrEmpty(point.GuidedTutorialEncounterId) && !used.Contains(point.GuidedTutorialEncounterId)
                    ? point.GuidedTutorialEncounterId : Guid.NewGuid().ToString("N");
            if (!used.Add(id)) throw new InvalidOperationException("Encounter tutorial identity is duplicated.");
            if (point.GuidedTutorialEncounterId == id) continue;
            Undo.RecordObject(point, "Persist guided tutorial identity");
            var serialized = new SerializedObject(point); serialized.FindProperty("guidedTutorialEncounterId").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(point)) PrefabUtility.RecordPrefabInstancePropertyModifications(point);
            EditorUtility.SetDirty(point); changed = true;
        }
        if (selected != null)
        {
            string before = config.GuidedTutorialSelectionKey;
            Undo.RecordObject(config, "Persist guided tutorial selection"); config.SetGuidedTutorialEncounter(selected);
            if (before != config.GuidedTutorialSelectionKey) { EditorUtility.SetDirty(config); changed = true; }
        }
        if (changed) EditorSceneManager.MarkSceneDirty(scene);
        return changed;
    }
    static string Backup(CampaignConfig config, Scene scene)
    {
        string directory = "Tools/GuidedTutorialIdentity/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(directory);
        if (File.Exists(scene.path)) File.Copy(scene.path, directory + "/scene-disk.unity");
        if (!EditorSceneManager.SaveScene(scene, directory + "/scene-live.unity", true)) throw new IOException("Could not back up the selected encounter scene.");
        string configPath = AssetDatabase.GetAssetPath(config);
        if (File.Exists(configPath)) File.Copy(configPath, directory + "/CampaignConfig.asset");
        return directory;
    }
    public static void Select(CampaignConfig config, WorldInteraction point)
    {
        if (point == null)
        {
            Undo.RecordObject(config, "Clear guided tutorial selection"); config.SetGuidedTutorialEncounter(null);
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config); return;
        }
        Scene scene = point.gameObject.scene; Backup(config, scene);
        Synchronize(config, scene, point);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the tutorial encounter identity.");
        // Newly added scene objects receive their final file IDs on the first save.
        if (Synchronize(config, scene, point) && !EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the final tutorial identity.");
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
    }
    public static WorldInteraction PrepareConfiguredSelection(CampaignConfig config)
    {
        if (config == null || !config.HasGuidedTutorialEncounter) return null;
        string key = config.GuidedTutorialSelectionKey, path = null;
        if (key.StartsWith(Prefix, StringComparison.Ordinal) && GlobalObjectId.TryParse(key.Substring(Prefix.Length), out var id))
            path = AssetDatabase.GUIDToAssetPath(id.assetGUID.ToString());
        else if (key.Contains("|")) path = key.Split('|')[0];
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new BuildFailedException("Không tìm thấy scene của Guided Tutorial. Hãy chọn lại encounter trong scene map.");
        var active = SceneManager.GetActiveScene(); var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var selected = Resolve(config, scene);
            if (selected == null) throw new BuildFailedException("Không xác định duy nhất enemy Guided Tutorial. Chọn lại đúng Enemy Encounter trong Inspector trước khi build.");
            Backup(config, scene); bool changed = Synchronize(config, scene, selected);
            if (changed && !EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save migrated tutorial identity.");
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            if (!config.IsGuidedTutorialEncounter(selected)) throw new BuildFailedException("Guided Tutorial identity could not be saved.");
            return selected;
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded && !scene.isDirty) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }
}

public sealed class GuidedTutorialEncounterBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;
    public void OnPreprocessBuild(BuildReport report) => GuidedTutorialEncounterAuthoring.PrepareConfiguredSelection(Resources.Load<CampaignConfig>("Adventure/CampaignConfig"));
}
