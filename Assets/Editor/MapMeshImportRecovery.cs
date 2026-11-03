using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit recovery of the two castle models that failed during package import.
// Never rewrites source models, GUIDs, prefabs, scenes, or player saves.
public static class MapMeshImportRecovery
{
    const string Request = "Library/CombatValidation/MapMeshImportRecovery.request";
    const string Result = "Library/CombatValidation/MapMeshImportRecovery.result.txt";
    const string ValidateRequest = "Library/CombatValidation/MapMeshImportRecoveryValidate.request";
    const string MeshDirectory = "Assets/asset/LeartesStudios/FantasyCastle/HDRP/Art/Meshes/";
    const string PrefabDirectory = "Assets/asset/LeartesStudios/FantasyCastle/HDRP/Art/Prefabs/";
    static readonly string[] Models = { "SM_InteriorFloor_2", "SM_Castle_Gate_01" };

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += RunRequested;
        if (File.Exists(ValidateRequest)) EditorApplication.delayCall += ValidateRequested;
    }

    static void ValidateRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += ValidateRequested; return; }
        if (!File.Exists(ValidateRequest)) return;
        File.Delete(ValidateRequest);
        try
        {
            int floors = 0, meshReferences = 0;
            foreach (string cell in new[] { "Cell_-1_-3", "Cell_-1_-4" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/MapChunksRefined/" + cell + ".unity");
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject) != transform.gameObject ||
                            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform.gameObject) != PrefabDirectory + Models[0] + ".prefab") continue;
                        var filters = transform.GetComponentsInChildren<MeshFilter>(true);
                        if (filters.Length != 4 || filters.Any(filter => filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0))
                            throw new InvalidOperationException("Missing recovered floor mesh: " + transform.name);
                        var collider = transform.GetComponent<BoxCollider>();
                        if (collider == null || !collider.enabled) throw new InvalidOperationException("Missing floor collider: " + transform.name);
                        floors++;
                        meshReferences += filters.Length;
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            if (floors != 21) throw new InvalidOperationException("Expected 21 affected floors, found " + floors);
            File.AppendAllText(Result, "PASS: " + floors + " floor instances, " + meshReferences + " LOD mesh references and all floor BoxColliders valid in the two affected chunks.\n");
            MapStreamingBuilder.OpenNearbyChunks();
            Debug.Log("PASS: recovered floor instances validated; nearby map chunks opened for editing.");
        }
        catch (Exception exception)
        {
            File.AppendAllText(Result, "FAIL validating chunk floors: " + exception + "\n");
            Debug.LogException(exception);
        }
    }

    static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        Recover();
    }

    [MenuItem("Tools/Adventure/Repair/Reimport damaged castle meshes")]
    public static void Recover()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before recovering model imports.");
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            foreach (string model in Models)
            {
                string path = MeshDirectory + model + ".fbx";
                string guid = AssetDatabase.AssetPathToGUID(path);
                string sourceHash = Hash(path);
                string metaHash = Hash(path + ".meta");
                report.AppendLine("Reimporting " + path);
                File.WriteAllText(Result, report.ToString());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
                if (!(main is GameObject) || meshes.Length == 0 || meshes.Any(mesh => mesh.vertexCount == 0))
                    throw new InvalidOperationException(model + " did not recover as a valid model with meshes.");
                if (guid != AssetDatabase.AssetPathToGUID(path) || sourceHash != Hash(path) || metaHash != Hash(path + ".meta"))
                    throw new InvalidOperationException(model + " source/GUID/import settings unexpectedly changed.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDirectory + model + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing prefab: " + model);
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length == 0 || filters.Any(filter => filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0))
                    throw new InvalidOperationException(model + " prefab has missing mesh references.");
                report.AppendLine("PASS " + model + ": " + meshes.Length + " meshes, " + filters.Length + " prefab mesh references; source, GUID and settings unchanged.");
                File.WriteAllText(Result, report.ToString());
            }
            report.AppendLine("PASS: targeted model recovery complete. No scene, prefab, source model, import settings or save data modified.");
            File.WriteAllText(Result, report.ToString());
            SceneView.RepaintAll();
            Debug.Log(report.ToString());
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL: " + exception);
            File.WriteAllText(Result, report.ToString());
            Debug.LogException(exception);
        }
    }

    static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(stream));
    }
}
