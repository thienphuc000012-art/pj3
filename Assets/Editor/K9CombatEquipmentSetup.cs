using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class K9CombatEquipmentSetup
{
    const string ScenePath = "Assets/Scenes/combattest.unity";
    const string Request = "Library/CombatValidation/K9Equipment.request";
    const string Output = "Tools/K9Equipment/Result.txt";
    static K9CombatEquipmentSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try { Run(command); }
        catch (Exception error) { Directory.CreateDirectory("Tools/K9Equipment"); File.WriteAllText(Output, "FAILED\n" + error); Debug.LogException(error); }
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [MenuItem("Tools/Combat/Inspect k9 Equipment")]
    public static void Inspect() => Run("inspect");
    static string PathOf(Transform item) => item.parent == null ? item.name : PathOf(item.parent) + "/" + item.name;
    static void Describe(StringBuilder report, GameObject go)
    {
        report.AppendLine("OBJECT " + PathOf(go.transform) + " id=" + GlobalObjectId.GetGlobalObjectIdSlow(go));
        report.AppendLine($"  world position={go.transform.position:F4} rotation={go.transform.eulerAngles:F3} scale={go.transform.lossyScale:F4}");
        report.AppendLine($"  local position={go.transform.localPosition:F4} rotation={go.transform.localEulerAngles:F3} scale={go.transform.localScale:F4}");
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            report.AppendLine($"  RENDERER {PathOf(renderer.transform)} center={renderer.bounds.center:F4} size={renderer.bounds.size:F4} materials={string.Join(",", renderer.sharedMaterials.Select(m => m != null ? m.name : "null"))}");
            var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null) report.AppendLine($"    mesh bounds center={mesh.bounds.center:F4} size={mesh.bounds.size:F4}");
        }
    }
    static void Run(string command)
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode.");
        var scene = SceneManager.GetSceneByPath(ScenePath); Check(scene.IsValid() && scene.isLoaded, "Open combattest first.");
        var objects = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
        var unit = objects.Select(t => t.GetComponent<BattleUnit>()).Where(u => u != null && u.unitName == "k9").Single();
        var sword = objects.Where(t => t.name == "SM_DKM_Sword").Single();
        var shield = objects.Where(t => t.name == "SHEILD_DRAGON_ENGINE").Single();
        var animator = unit.GetComponentInChildren<Animator>(true);
        var report = new StringBuilder(); report.AppendLine("Scene dirty before: " + scene.isDirty + "; animation preview=" + AnimationMode.InAnimationMode());
        if (command.StartsWith("apply "))
        {
            // Leave animation preview before authoring, so sampled bone poses
            // cannot be baked into the saved scene.
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            var pose = JsonUtility.FromJson<EquipmentPose>(command.Substring(6));
            Check(animator != null, "k9 needs an Animator.");
            var right = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : unit.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == pose.rightHand);
            var left = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : unit.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == pose.leftHand);
            Check(right != null && left != null, "Both hand bones must exist.");
            Directory.CreateDirectory("Tools/K9Equipment");
            string backup = "Tools/K9Equipment/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            File.Copy(ScenePath, backup + "-disk.unity");
            Check(EditorSceneManager.SaveScene(scene, backup + "-live.unity", true), "Back up the unsaved scene first.");
            Undo.RecordObjects(new UnityEngine.Object[] { sword, shield }, "Attach k9 sword and shield");
            Undo.SetTransformParent(sword, right, "Attach sword to right hand");
            Undo.SetTransformParent(shield, left, "Attach shield to left hand");
            sword.localPosition = pose.swordPosition; sword.localEulerAngles = pose.swordEuler; sword.localScale = pose.swordScale;
            shield.localPosition = pose.shieldPosition; shield.localEulerAngles = pose.shieldEuler; shield.localScale = pose.shieldScale;
            foreach (var item in new[] { sword, shield })
            {
                item.gameObject.SetActive(true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.gameObject);
            }
            if (pose.restoreCamera)
            {
                var camera = objects.Single(t => t.name == "Main Camera" && t.GetComponent<Camera>() != null);
                Undo.RecordObject(camera, "Preserve authored combat camera");
                camera.localPosition = pose.originalCameraPosition;
            }
            EditorSceneManager.MarkSceneDirty(scene); Check(EditorSceneManager.SaveScene(scene), "Save the equipment to combattest.");
            report.AppendLine("APPLIED: sword on right hand, shield on left hand. Backup: " + backup);
        }
        Describe(report, unit.gameObject); Describe(report, sword.gameObject); Describe(report, shield.gameObject);
        if (command.StartsWith("preview ")) Preview(unit, sword, shield, JsonUtility.FromJson<EquipmentPose>(command.Substring(8)), report);
        report.AppendLine("UNIT TRANSFORMS: " + string.Join(", ", unit.GetComponentsInChildren<Transform>(true).Select(t => t.name)));
        foreach (var skin in unit.GetComponentsInChildren<SkinnedMeshRenderer>(true).Take(1))
        {
            report.AppendLine("ROOT BONE: " + (skin.rootBone != null ? PathOf(skin.rootBone) : "none"));
            report.AppendLine("SKIN BONES: " + string.Join(", ", skin.bones.Select(t => t != null ? PathOf(t) : "null")));
        }
        report.AppendLine("Animator: " + (animator != null ? animator.name + " human=" + animator.isHuman : "none"));
        foreach (var bone in objects.Where(t => t.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("forearm", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("lowerarm", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("weapon", StringComparison.OrdinalIgnoreCase) >= 0).Where(t => t.IsChildOf(unit.transform) || t.root.name == "k9"))
            report.AppendLine($"BONE {PathOf(bone)} localPos={bone.localPosition:F4} worldPos={bone.position:F4} rot={bone.eulerAngles:F3} scale={bone.lossyScale:F4}");
        Directory.CreateDirectory("Tools/K9Equipment"); File.WriteAllText(Output, report.ToString());
        Debug.Log("k9 equipment " + command.Split(' ')[0] + ": " + Output);
    }
    [Serializable] class EquipmentPose
    {
        public string rightHand, leftHand;
        public Vector3 swordPosition, swordEuler, swordScale = Vector3.one;
        public Vector3 shieldPosition, shieldEuler, shieldScale = Vector3.one;
        public string clip;
        public bool restoreCamera;
        public Vector3 originalCameraPosition;
    }

    static void Preview(BattleUnit unit, Transform sword, Transform shield, EquipmentPose pose, StringBuilder report)
    {
        var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(preview);
        var ambientMode = RenderSettings.ambientMode; Color ambient = RenderSettings.ambientLight;
        GameObject root = null; RenderTexture target = null; Texture2D pixels = null;
        try
        {
            root = new GameObject("k9 equipment preview");
            var actor = UnityEngine.Object.Instantiate(unit.gameObject, Vector3.zero, Quaternion.identity, root.transform);
            foreach (var script in actor.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            Transform Bone(string name) => actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
            void Mount(Transform original, string hand, Vector3 position, Vector3 angles, Vector3 scale)
            {
                var item = actor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == original.name);
                if (item == null) item = UnityEngine.Object.Instantiate(original.gameObject, root.transform).transform;
                item.SetParent(Bone(hand), false); item.localPosition = position; item.localEulerAngles = angles; item.localScale = scale;
            }
            Mount(sword, pose.rightHand, pose.swordPosition, pose.swordEuler, pose.swordScale);
            Mount(shield, pose.leftHand, pose.shieldPosition, pose.shieldEuler, pose.shieldScale);
            foreach (var animation in actor.GetComponentsInChildren<Animator>(true)) animation.enabled = false;
            if (!string.IsNullOrEmpty(pose.clip))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(pose.clip); Check(clip != null, "Preview clip missing: " + pose.clip);
                clip.SampleAnimation(actor, clip.length * .35f);
            }
            foreach (var transform in actor.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 30;
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
            var light = new GameObject("Preview Light", typeof(Light)); light.transform.SetParent(root.transform); light.transform.rotation = Quaternion.Euler(35, 210, 0);
            light.GetComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().intensity = 1.5f; light.GetComponent<Light>().cullingMask = 1 << 30;
            var cameraObject = new GameObject("Preview Camera", typeof(Camera)); cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .18f, .22f); camera.fieldOfView = 35;
            target = new RenderTexture(1400, 1400, 24); camera.targetTexture = target;
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            foreach (var angle in new[] { "Front", "Back" })
            {
                camera.transform.position = angle == "Front" ? new Vector3(2.7f, 1.65f, 4f) : new Vector3(-2.7f, 1.65f, -4f);
                camera.transform.LookAt(new Vector3(0, .95f, 0)); camera.Render();
                var oldTarget = RenderTexture.active; RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(); RenderTexture.active = oldTarget;
                string path = "Tools/K9Equipment/Preview-" + angle + ".png"; File.WriteAllBytes(path, pixels.EncodeToPNG()); report.AppendLine("PREVIEW: " + path);
            }
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root); if (target != null) UnityEngine.Object.DestroyImmediate(target); if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
            EditorSceneManager.CloseScene(preview, true); if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            Check(active.isDirty == dirty, "Preserve the scene while previewing equipment.");
        }
    }
}
