using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LaurenRigRepair
{
    [Serializable] public class Repair { public string renderer; public string root; public string[] bones; }
    [Serializable] public class Request { public Repair[] repairs; }
    const string Folder = "Library/CombatValidation/";
    static LaurenRigRepair() { EditorApplication.update += Poll; }
    static string Id(UnityEngine.Object obj) => obj == null ? "null" : GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
    static string Path(Transform t) => t == null ? "null" : (t.parent == null ? t.name : Path(t.parent) + "/" + t.name);
    static UnityEngine.Object Resolve(string id)
    {
        GlobalObjectId parsed;
        return GlobalObjectId.TryParse(id, out parsed) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) : null;
    }
    static void Poll()
    {
        string request = Folder + "LaurenRig.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string json = File.ReadAllText(request); File.Delete(request);
        try
        {
            var scene = SceneManager.GetSceneByName("combattest");
            if (!scene.IsValid() || !scene.isLoaded) throw new Exception("Open combattest in Edit Mode first.");
            if (json.Trim() == "convert") ConvertGarments(scene);
            if (json.TrimStart().StartsWith("{"))
            {
                var data = JsonUtility.FromJson<Request>(json);
                // Validate every reference before changing any renderer.
                foreach (var fix in data.repairs)
                {
                    var skin = Resolve(fix.renderer) as SkinnedMeshRenderer;
                    if (skin == null || skin.gameObject.scene != scene || !skin.name.StartsWith("SK_SWORDSMANGIRL_", StringComparison.OrdinalIgnoreCase)) throw new Exception("Invalid garment renderer");
                    if (fix.bones.Length != skin.bones.Length || !(Resolve(fix.root) is Transform) || fix.bones.Any(b => !(Resolve(b) is Transform))) throw new Exception("Invalid bone mapping");
                }
                EditorSceneManager.SaveScene(scene, Folder + "combattest-before-LaurenRig.unity", true);
                foreach (var fix in data.repairs)
                {
                    var skin = (SkinnedMeshRenderer)Resolve(fix.renderer);
                    Undo.RecordObject(skin, "Bind Lauren clothing bones");
                    skin.bones = fix.bones.Select(b => (Transform)Resolve(b)).ToArray();
                    skin.rootBone = (Transform)Resolve(fix.root);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
                    EditorUtility.SetDirty(skin);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            var report = new StringBuilder();
            report.AppendLine("SELECTION " + Path(Selection.activeTransform));
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>().Where(t => t.name.ToUpperInvariant().Contains("SWORDSMANGIRL")))
                report.AppendLine("OBJECT " + Path(t) + " SCENE " + t.gameObject.scene.name + " ASSET " + AssetDatabase.GetAssetPath(t) + " COMPONENTS " + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
            foreach (var root in scene.GetRootGameObjects())
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!Path(skin.transform).ToLowerInvariant().Contains("lauren") && !skin.name.ToUpperInvariant().Contains("SWORDSMANGIRL")) continue;
                report.AppendLine("SKIN " + Path(skin.transform) + " ID " + Id(skin));
                report.AppendLine("MESH " + AssetDatabase.GetAssetPath(skin.sharedMesh) + " ROOT " + Path(skin.rootBone) + " ID " + Id(skin.rootBone));
                for (int i = 0; i < skin.bones.Length; i++) report.AppendLine(i + " " + Path(skin.bones[i]) + " ID " + Id(skin.bones[i]));
            }
            File.WriteAllText(Folder + "LaurenRig.result.txt", report.ToString());
        }
        catch (Exception e) { File.WriteAllText(Folder + "LaurenRig.result.txt", e.ToString()); Debug.LogException(e); }
    }

    static void ConvertGarments(Scene scene)
    {
        var root = scene.GetRootGameObjects().Single(g => g.name == "SK_Lauren Red");
        var rig = root.transform.Find("root");
        if (rig == null) throw new Exception("Lauren skeleton missing");
        string[] names = { "SK_SWORDSMANGIRL_SKIRT", "SK_SWORDSMANGIRL_SOCKS", "SK_SWORDSMANGIRL_BOOTS" };
        var parts = names.Select(n => root.transform.Find(n)).ToArray();
        var sources = new SkinnedMeshRenderer[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null || parts[i].GetComponent<MeshFilter>() == null || parts[i].GetComponent<MeshRenderer>() == null) throw new Exception("Expected static garment: " + names[i]);
            var mesh = parts[i].GetComponent<MeshFilter>().sharedMesh;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(mesh));
            sources[i] = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.sharedMesh == mesh);
            if (mesh.bindposes.Length == 0 || sources[i].bones.Length != mesh.bindposes.Length) throw new Exception("Source has no valid skin weights: " + names[i]);
        }
        if (!EditorSceneManager.SaveScene(scene, Folder + "combattest-before-LaurenRig.unity", true)) throw new Exception("Backup failed");
        const string output = "Assets/Adventure/Generated/LaurenRig";
        Directory.CreateDirectory(output); AssetDatabase.Refresh();
        var targetBones = rig.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
        var log = new StringBuilder();
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        try
        {
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i]; var source = sources[i];
                var bones = source.bones.Select(b => MapBone(b, targetBones)).ToArray();
                var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
                mesh.name = names[i] + "_Lauren";
                mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * part.localToWorldMatrix).ToArray();
                string asset = AssetDatabase.GenerateUniqueAssetPath(output + "/" + mesh.name + ".asset");
                AssetDatabase.CreateAsset(mesh, asset);
                var old = part.GetComponent<MeshRenderer>();
                var materials = old.sharedMaterials; bool enabled = old.enabled;
                var shadows = old.shadowCastingMode; bool receive = old.receiveShadows;
                Undo.DestroyObjectImmediate(old); Undo.DestroyObjectImmediate(part.GetComponent<MeshFilter>());
                var skin = Undo.AddComponent<SkinnedMeshRenderer>(part.gameObject);
                skin.sharedMesh = mesh; skin.bones = bones; skin.rootBone = rig;
                skin.sharedMaterials = materials; skin.enabled = enabled;
                skin.shadowCastingMode = shadows; skin.receiveShadows = receive;
                skin.localBounds = mesh.bounds; skin.updateWhenOffscreen = true;
                var baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked);
                    var before = baked.vertices; var original = mesh.vertices;
                    float error = 0;
                    for (int v = 0; v < before.Length; v++) error = Mathf.Max(error, Vector3.Distance(before[v], original[v]));
                    if (error > .01f) throw new Exception("Rest pose changed: " + names[i] + " error " + error);
                    Vector3 saved = rig.localPosition;
                    float movement = 0;
                    try
                    {
                        rig.localPosition += Vector3.up * .1f;
                        skin.BakeMesh(baked); var after = baked.vertices;
                        for (int v = 0; v < before.Length; v++) movement = Mathf.Max(movement, Vector3.Distance(before[v], after[v]));
                    }
                    finally { rig.localPosition = saved; }
                    if (movement < .01f) throw new Exception("Mesh does not follow skeleton: " + names[i]);
                    log.AppendLine(names[i] + ": PASS bones=" + bones.Length + " restError=" + error + " skeletonMovement=" + movement);
                }
                finally { UnityEngine.Object.DestroyImmediate(baked); }
                PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
            }
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Folder + "LaurenRig.validation.txt", log.ToString());
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    static Transform MapBone(Transform source, System.Collections.Generic.Dictionary<string, Transform> target)
    {
        Transform found;
        if (target.TryGetValue(source.name, out found)) return found;
        if (source.parent == null) throw new Exception("Unmatched bone root " + source.name);
        var parent = MapBone(source.parent, target);
        var child = new GameObject(source.name);
        Undo.RegisterCreatedObjectUndo(child, "Add garment bone");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = source.localPosition;
        child.transform.localRotation = source.localRotation;
        child.transform.localScale = source.localScale;
        target.Add(source.name, child.transform);
        return child.transform;
    }
}
