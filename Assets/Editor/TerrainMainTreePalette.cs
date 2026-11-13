using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adds existing vegetation prefabs to the m terrain palette; never paints or removes instances.</summary>
public static class TerrainMainTreePalette
{
    const string ScenePath = "Assets/Scenes/m.unity";
    const string Folder = "Assets/asset/Toby Fredson/Rocky Hills Environment - Whitebark Pine/RHEWP_Demo/Prefabs/Prefabs_Vegetation/PV_Trees";
    const string VegetationFolder = "Assets/asset/Toby Fredson/Rocky Hills Environment - Whitebark Pine/RHEWP_Demo/Prefabs/Prefabs_Vegetation";
    const string LocalDataPath = "Assets/Adventure/Terrain/m_TerrainMain.asset";
    const string Request = "Library/CombatValidation/TerrainMainTrees.request";
    const string Result = "Library/CombatValidation/TerrainMainTrees.result.txt";

    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Requested; return; }
        string action = File.ReadAllText(Request).Trim(); File.Delete(Request);
        if (action == "inspect") InspectCopy();
        else if (action == "plants") AddBushesAndFlowers();
        else AddTrees();
    }

    [MenuItem("Tools/Adventure/Terrain/Add PV_Trees to Terrain Main in m")]
    public static void AddTrees() => AddPrefabs(new[] { Folder }, "PV_Trees");

    [MenuItem("Tools/Adventure/Terrain/Add PV_Bushes and PV_Flowers to Terrain Main in m")]
    public static void AddBushesAndFlowers() => AddPrefabs(new[]
        { VegetationFolder + "/PV_Bushes", VegetationFolder + "/PV_Flowers" }, "PV_Bushes + PV_Flowers");

    static void AddPrefabs(string[] folders, string label)
    {
        string backup = null;
        Terrain terrain = null;
        TerrainData original = null, data = null;
        TreePrototype[] previous = null;
        bool assigned = false, sceneSaved = false;
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Check(scene.IsValid() && scene.isLoaded, "Open scene m in Edit Mode first.");
            var candidates = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true))
                .Where(t => t.name == "Terrain Main").ToArray();
            Check(candidates.Length == 1, "Expected exactly one Terrain Main in m.");
            terrain = candidates[0]; original = terrain.terrainData;
            var collider = terrain.GetComponent<TerrainCollider>();
            Check(original != null && collider != null && collider.terrainData == original,
                "Terrain Main and its TerrainCollider must reference the same TerrainData.");
            string sourcePath = AssetDatabase.GetAssetPath(original);
            Check(folders.All(AssetDatabase.IsValidFolder), "Missing vegetation folder: " + label);
            var prefabs = AssetDatabase.FindAssets("t:Prefab", folders).Distinct()
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
            Check(prefabs.Length > 0 && prefabs.All(p => p != null && p.GetComponentsInChildren<MeshRenderer>(true).Length > 0),
                "Expected usable vegetation prefabs in " + label + ".");
            previous = original.treePrototypes;
            var missing = prefabs.Where(p => !previous.Any(t => t.prefab == p)).ToArray();
            if (missing.Length == 0)
            {
                File.WriteAllText(Result, "PASS: All " + prefabs.Length + " " + label + " prefabs are already present; no data changed. TerrainData: " + sourcePath);
                Selection.activeGameObject = terrain.gameObject;
                return;
            }

            backup = "Tools/TerrainPaintTrees/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, backup + "/m-on-disk.unity");
            File.Copy(sourcePath, backup + "/TerrainData-on-disk.asset");
            File.Copy(sourcePath + ".meta", backup + "/TerrainData-on-disk.asset.meta");
            // Keep an exact copy of unsaved scene edits without saving over the live scene yet.
            string snapshot = AssetDatabase.GenerateUniqueAssetPath("Assets/TerrainPaletteSceneBackup.unity");
            try
            {
                Check(EditorSceneManager.SaveScene(scene, snapshot, true), "Could not back up the current scene.");
                File.Copy(snapshot, backup + "/m-live-before.unity");
            }
            finally { if (File.Exists(snapshot)) AssetDatabase.DeleteAsset(snapshot); }

            var instances = original.treeInstances;
            var layers = original.terrainLayers;
            var details = original.detailPrototypes;
            var height = Hash(original.GetHeights(0, 0, original.heightmapResolution, original.heightmapResolution));
            var alphaPixels = original.GetAlphamaps(0, 0, original.alphamapWidth, original.alphamapHeight);
            var alpha = Hash(alphaPixels);
            var holes = Hash(original.GetHoles(0, 0, original.holesResolution, original.holesResolution));
            var detailLayers = new List<int[,]>();
            for (int i = 0; i < details.Length; i++)
                detailLayers.Add(original.GetDetailLayer(0, 0, original.detailWidth, original.detailHeight, i));
            Vector3 size = original.size;

            // The supplied demo terrain is also used by map1 and the package demo scenes.
            // A native TerrainData clone preserves the current in-memory painting and instances.
            bool fork = sourcePath.StartsWith("Assets/asset/", StringComparison.OrdinalIgnoreCase);
            data = fork ? UnityEngine.Object.Instantiate(original) : original;
            string dataPath = sourcePath;
            if (fork)
            {
                Directory.CreateDirectory("Assets/Adventure/Terrain"); AssetDatabase.Refresh();
                dataPath = AssetDatabase.GenerateUniqueAssetPath(LocalDataPath);
                data.name = "m Terrain Main";
                AssetDatabase.CreateAsset(data, dataPath);
                // TerrainData cloning does not reliably preserve live GPU-painted
                // alphamaps as new asset subtextures. Copy the current paint explicitly.
                data.SetAlphamaps(0, 0, alphaPixels);
                for (int i = 0; i < detailLayers.Count; i++) data.SetDetailLayer(0, 0, i, detailLayers[i]);
            }
            var prototypes = new List<TreePrototype>(previous);
            foreach (var prefab in missing) prototypes.Add(new TreePrototype { prefab = prefab, bendFactor = 0 });
            Undo.RecordObject(data, "Add tree painting prototypes");
            data.treePrototypes = prototypes.ToArray();
            data.RefreshPrototypes();
            Check(data.treeInstances.SequenceEqual(instances), "Existing tree/grass instances changed.");
            Check(data.size == size && data.terrainLayers.SequenceEqual(layers) && data.detailPrototypes.Length == details.Length,
                "Terrain dimensions, layers or details changed.");
            Check(height == Hash(data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution)) &&
                  alpha == Hash(data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight)) &&
                  holes == Hash(data.GetHoles(0, 0, data.holesResolution, data.holesResolution)), "Terrain painting changed.");
            for (int i = 0; i < detailLayers.Count; i++)
                Check(Hash(detailLayers[i]) == Hash(data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, i)), "Detail grass painting changed.");
            for (int i = 0; i < previous.Length; i++)
                Check(data.treePrototypes[i].prefab == previous[i].prefab && data.treePrototypes[i].bendFactor == previous[i].bendFactor,
                    "An existing palette entry was moved or changed.");
            EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
            if (fork)
            {
                Undo.RecordObjects(new UnityEngine.Object[] { terrain, collider }, "Use map-specific TerrainData");
                terrain.terrainData = data; collider.terrainData = data; assigned = true;
                EditorSceneManager.MarkSceneDirty(scene);
                Check(EditorSceneManager.SaveScene(scene), "Could not save m with its private TerrainData.");
                sceneSaved = true;
                // No request to save or change the shared package TerrainData.
                Check(original.treePrototypes.Length == previous.Length && original.treeInstances.SequenceEqual(instances),
                    "Shared demo terrain changed.");
            }
            terrain.Flush();
            AssetDatabase.ImportAsset(dataPath, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            Check(reloaded != null && reloaded.treePrototypes.Length == prototypes.Count && reloaded.treeInstances.SequenceEqual(instances),
                "Saved palette/instances did not reload correctly.");
            Check(height == Hash(reloaded.GetHeights(0, 0, reloaded.heightmapResolution, reloaded.heightmapResolution)) &&
                  alpha == Hash(reloaded.GetAlphamaps(0, 0, reloaded.alphamapWidth, reloaded.alphamapHeight)) &&
                  holes == Hash(reloaded.GetHoles(0, 0, reloaded.holesResolution, reloaded.holesResolution)), "Saved terrain painting did not reload correctly.");
            Check(prefabs.All(p => reloaded.treePrototypes.Count(t => t.prefab == p) == 1), "Missing or duplicate tree prefab.");
            Selection.activeGameObject = terrain.gameObject;
            ActiveEditorTracker.sharedTracker.ForceRebuild(); SceneView.RepaintAll();
            var report = new List<string>
            {
                "PASS: Added " + missing.Length + " vegetation prefabs from " + label + " to Terrain Main / Paint Trees in scene m.",
                "Source folder prefab counts: " + string.Join(", ", folders.Select(f => Path.GetFileName(f) + "=" + prefabs.Count(p => AssetDatabase.GetAssetPath(p).StartsWith(f + "/", StringComparison.Ordinal)))),
                "Already present and not duplicated: " + (prefabs.Length - missing.Length),
                "TerrainData: " + dataPath, "Previous palette count: " + previous.Length,
                "Current palette count: " + reloaded.treePrototypes.Length,
                "Existing tree/grass instances unchanged: " + instances.Length,
                "Heightmap, texture painting, holes, terrain size and previous palette indices unchanged.",
                fork ? "Shared package TerrainData unchanged. Scene and collider reference the map-specific TerrainData." : "Updated the map-specific TerrainData; shared package assets unchanged.",
                "Backup: " + backup
            };
            report.AddRange(prefabs.Select(p => p.name));
            File.WriteAllLines(Result, report);
            File.WriteAllLines("Tools/TerrainPaintTrees/Result.txt", report);
            Debug.Log(report[0]);
        }
        catch (Exception error)
        {
            if (assigned && !sceneSaved && terrain != null && original != null)
            { terrain.terrainData = original; terrain.GetComponent<TerrainCollider>().terrainData = original; }
            File.WriteAllText(Result, "FAILED: " + error + "\nBackup: " + backup);
            Debug.LogException(error);
        }
    }
    static Hash128 Hash(Array values)
    {
        var bytes = new byte[Buffer.ByteLength(values)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return Hash128.Compute(bytes);
    }
    static void InspectCopy()
    {
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var source = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single(t => t.name == "Terrain Main").terrainData;
            var copy = AssetDatabase.LoadAssetAtPath<TerrainData>(LocalDataPath);
            var lines = new List<string> { "Source: " + AssetDatabase.GetAssetPath(source), "Copy: " + AssetDatabase.GetAssetPath(copy),
                "Source/copy palette: " + source.treePrototypes.Length + "/" + copy.treePrototypes.Length,
                Difference("Height", source.GetHeights(0, 0, source.heightmapResolution, source.heightmapResolution), copy.GetHeights(0, 0, copy.heightmapResolution, copy.heightmapResolution)),
                Difference("Alpha", source.GetAlphamaps(0, 0, source.alphamapWidth, source.alphamapHeight), copy.GetAlphamaps(0, 0, copy.alphamapWidth, copy.alphamapHeight)),
                Difference("Holes", source.GetHoles(0, 0, source.holesResolution, source.holesResolution), copy.GetHoles(0, 0, copy.holesResolution, copy.holesResolution)) };
            File.WriteAllLines("Library/CombatValidation/TerrainMainTrees.inspect.txt", lines);
        }
        catch (Exception e) { File.WriteAllText("Library/CombatValidation/TerrainMainTrees.inspect.txt", e.ToString()); }
    }
    static string Difference(string name, Array a, Array b)
    {
        if (a.Length != b.Length) return name + " length: " + a.Length + "/" + b.Length;
        var ea = a.GetEnumerator(); var eb = b.GetEnumerator(); int differences = 0; double maximum = 0;
        while (ea.MoveNext() && eb.MoveNext()) if (!Equals(ea.Current, eb.Current))
        { differences++; if (ea.Current is float) maximum = Math.Max(maximum, Math.Abs((float)ea.Current - (float)eb.Current)); }
        return name + " hash: " + Hash(a) + "/" + Hash(b) + "; changed values: " + differences + "; maximum delta: " + maximum;
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
