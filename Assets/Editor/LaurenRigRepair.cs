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
            if (json.Trim() == "skirtcoverage") ImproveSkirtCoverage(scene);
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

    static void ImproveSkirtCoverage(Scene scene)
    {
        var root = scene.GetRootGameObjects().Single(g => g.name == "SK_Lauren Red");
        var skin = root.transform.Find("SK_SWORDSMANGIRL_SKIRT").GetComponent<SkinnedMeshRenderer>();
        // Always start from the preserved original conversion, so repeating this command is safe.
        var original = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Adventure/Generated/LaurenRig/SK_SWORDSMANGIRL_SKIRT_Lauren.asset");
        int pelvis = Array.FindIndex(skin.bones, b => b != null && b.name == "pelvis");
        if (original == null || pelvis < 0) throw new Exception("Skirt mesh/pelvis missing");
        var mesh = UnityEngine.Object.Instantiate(original);
        mesh.name = "SK_SWORDSMANGIRL_SKIRT_Lauren_Coverage";
        var weights = mesh.boneWeights;
        for (int v = 0; v < weights.Length; v++)
        {
            var w = weights[v];
            int[] indices = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
            var combined = new System.Collections.Generic.Dictionary<int, float>();
            for (int j = 0; j < 4; j++)
            {
                if (values[j] <= 0) continue;
                float transfer = skin.bones[indices[j]].name.StartsWith("thigh", StringComparison.OrdinalIgnoreCase) ? values[j] * .75f : 0;
                if (!combined.ContainsKey(indices[j])) combined[indices[j]] = 0;
                combined[indices[j]] += values[j] - transfer;
                if (!combined.ContainsKey(pelvis)) combined[pelvis] = 0;
                combined[pelvis] += transfer;
            }
            var sorted = combined.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(4).ToArray();
            float sum = sorted.Sum(x => x.Value);
            var result = new BoneWeight();
            for (int j = 0; j < sorted.Length; j++)
            {
                int bone = sorted[j].Key; float value = sorted[j].Value / sum;
                if (j == 0) { result.boneIndex0 = bone; result.weight0 = value; }
                if (j == 1) { result.boneIndex1 = bone; result.weight1 = value; }
                if (j == 2) { result.boneIndex2 = bone; result.weight2 = value; }
                if (j == 3) { result.boneIndex3 = bone; result.weight3 = value; }
            }
            weights[v] = result;
        }
        mesh.boneWeights = weights;
        var vertices = mesh.vertices;
        Vector3 center = original.bounds.center;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 radial = new Vector3(vertices[i].x - center.x, 0, vertices[i].z - center.z);
            float hem = 1f - Mathf.InverseLerp(original.bounds.min.y, original.bounds.max.y, vertices[i].y);
            vertices[i] += radial.normalized * Mathf.Lerp(.004f, .018f, hem);
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        // Validate deformations on a temporary copy, never pose the user's scene character.
        var preview = EditorSceneManager.NewPreviewScene();
        var log = new StringBuilder();
        try
        {
            var clone = UnityEngine.Object.Instantiate(root); SceneManager.MoveGameObjectToScene(clone, preview);
            foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (var animator in clone.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var test = clone.transform.Find("SK_SWORDSMANGIRL_SKIRT").GetComponent<SkinnedMeshRenderer>();
            test.sharedMesh = mesh;
            var left = test.bones.First(b => b.name == "thigh_l");
            var right = test.bones.First(b => b.name == "thigh_r");
            Quaternion l = left.localRotation, r = right.localRotation;
            var baked = new Mesh();
            try
            {
                test.BakeMesh(baked); var rest = baked.vertices;
                test.sharedMesh = original; test.BakeMesh(baked); var oldRest = baked.vertices;
                foreach (float angle in new[] { -60f, 0f, 60f })
                {
                    left.localRotation = l * Quaternion.Euler(angle, 0, 20);
                    right.localRotation = r * Quaternion.Euler(-angle, 0, -20);
                    test.sharedMesh = original; test.BakeMesh(baked); var oldPose = baked.vertices;
                    test.sharedMesh = mesh;
                    test.BakeMesh(baked);
                    if (baked.vertexCount != original.vertexCount || baked.triangles.Length != original.triangles.Length || baked.vertices.Any(p => float.IsNaN(p.x) || float.IsInfinity(p.x))) throw new Exception("Invalid skirt deformation");
                    var pose = baked.vertices;
                    float oldPull = 0, newPull = 0;
                    for (int v = 0; v < pose.Length; v++)
                    {
                        oldPull += Vector3.Distance(oldPose[v], oldRest[v]);
                        newPull += Vector3.Distance(pose[v], rest[v]);
                    }
                    if (newPull >= oldPull) throw new Exception("Skirt leg pull was not reduced");
                    log.AppendLine("PASS leg pose " + angle + ": mean leg pull " + oldPull / pose.Length + " -> " + newPull / pose.Length + "; " + baked.triangles.Length / 3 + " triangles retained");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        if (!EditorSceneManager.SaveScene(scene, Folder + "combattest-before-SkirtCoverage.unity", true)) throw new Exception("Backup failed");
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Adventure/Generated/LaurenRig/" + mesh.name + ".asset");
        AssetDatabase.CreateAsset(mesh, path);
        Undo.RecordObject(skin, "Improve skirt coverage");
        skin.sharedMesh = mesh;
        skin.localBounds = mesh.bounds; skin.updateWhenOffscreen = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
        EditorUtility.SetDirty(skin); EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        File.WriteAllText(Folder + "SkirtCoverage.validation.txt", log.ToString());
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
