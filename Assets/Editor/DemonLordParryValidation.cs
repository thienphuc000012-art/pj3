using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Authoring and isolated pose checks for skinned DemonLord weapons. Never starts Play Mode.</summary>
public static class DemonLordParryValidation
{
    const string Prefab = "Assets/Adventure/Prefabs/Enemy/DemonLord/DemonLord.prefab";
    const string Request = "Library/CombatValidation/DemonLordParry.request";
    const string Result = "Library/CombatValidation/DemonLordParry.result.txt";
    [InitializeOnLoadMethod] static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Requested; return; }
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        if (command == "setup") Setup(); else if (command == "validate") Validate(); else Inspect();
    }
    [MenuItem("Tools/Adventure/Combat/Set Up DemonLord Bone Parry Colliders")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode.");
        string backup = "Tools/DemonLordParry/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup); File.Copy(Prefab, backup + "/DemonLord.prefab"); File.Copy(Prefab + ".meta", backup + "/DemonLord.prefab.meta");
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(Prefab);
            foreach (string name in new[] { "SK_FireSword", "SK_LavaWhip" })
            {
                var renderer = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == name);
                var marker = renderer.GetComponent<EnemyMeleeParryHitbox>();
                if (marker == null) throw new InvalidOperationException("Missing original parry marker on " + name);
                var parts = new List<Collider>();
                for (int b = 0; b < renderer.bones.Length; b++)
                {
                    Bounds bounds = BoneBounds(renderer.sharedMesh, b);
                    var bone = renderer.bones[b];
                    string partName = "Parry Hitbox - " + name + " - " + bone.name;
                    var child = bone.Find(partName);
                    if (child == null) { child = new GameObject(partName).transform; child.SetParent(bone, false); }
                    child.localPosition = Vector3.zero; child.localRotation = Quaternion.identity; child.localScale = Vector3.one;
                    child.gameObject.layer = renderer.gameObject.layer;
                    var box = child.GetComponent<BoxCollider>(); if (box == null) box = child.gameObject.AddComponent<BoxCollider>();
                    box.isTrigger = true; box.enabled = true; box.center = bounds.center;
                    // Small contact allowance at the joints; never use one straight box over the entire bent whip.
                    box.size = bounds.size + Vector3.one * .08f;
                    parts.Add(box);
                }
                // Retain the user's old boxes disabled for reference. They live on stationary mesh objects.
                foreach (var old in renderer.GetComponents<Collider>()) old.enabled = false;
                marker.hitbox = parts[0]; marker.additionalHitboxes = parts.Skip(1).ToArray(); marker.weaponRenderer = renderer;
            }
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        Validate();
    }

    static Bounds BoneBounds(Mesh mesh, int bone)
    {
        var vertices = mesh.vertices; var weights = mesh.boneWeights; var bind = mesh.bindposes[bone];
        bool found = false; Bounds bounds = default;
        for (int v = 0; v < vertices.Length; v++)
        {
            var w = weights[v];
            if (!((w.boneIndex0 == bone && w.weight0 > .05f) || (w.boneIndex1 == bone && w.weight1 > .05f) ||
                (w.boneIndex2 == bone && w.weight2 > .05f) || (w.boneIndex3 == bone && w.weight3 > .05f))) continue;
            Vector3 p = bind.MultiplyPoint3x4(vertices[v]);
            if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; } else bounds.Encapsulate(p);
        }
        if (!found) throw new InvalidOperationException("No weighted vertices for weapon bone " + bone);
        return bounds;
    }

    [MenuItem("Tools/Adventure/Combat/Validate DemonLord Bone Parry Colliders")]
    public static void Validate()
    {
        Scene scene = default; GameObject staging = null; Mesh baked = null;
        var report = new List<string>();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode.");
            scene = EditorSceneManager.NewPreviewScene();
            staging = new GameObject("Isolated DemonLord Parry Check"); staging.SetActive(false); SceneManager.MoveGameObjectToScene(staging, scene);
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), staging.transform, false);
            foreach (var script in actor.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var audio in actor.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var ps in actor.GetComponentsInChildren<ParticleSystem>(true))
            { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var main = ps.main; main.playOnAwake = false; }
            var markers = actor.GetComponentsInChildren<EnemyMeleeParryHitbox>(true);
            Check(markers.Length == 2, "Expected sword and whip marker.");
            foreach (var marker in markers)
            {
                var renderer = marker.GetComponent<SkinnedMeshRenderer>(); var parts = Parts(marker);
                Check(parts.Length == renderer.bones.Length, "Missing bone colliders on " + marker.name);
                Check(marker.weaponRenderer == renderer, "Missing weapon visibility guard.");
                Check(marker.GetComponents<Collider>().All(c => !c.enabled), "Old stationary box still enabled.");
                for (int b = 0; b < parts.Length; b++)
                {
                    Check(parts[b] != null && parts[b].transform.parent == renderer.bones[b], "Hitbox is not attached to its weapon bone.");
                    parts[b].enabled = true;
                }
                marker.enabled = true; renderer.enabled = true;
                report.Add(marker.name + ": " + parts.Length + " bone-mounted trigger boxes; original stationary box disabled.");
            }
            var guardRoot = new GameObject("Isolated guard probe"); guardRoot.transform.SetParent(staging.transform, false);
            var guard = guardRoot.AddComponent<SphereCollider>(); guard.isTrigger = true; guard.radius = .035f;
            var zone = guardRoot.AddComponent<PlayerMeleeParryZone>(); zone.guardCollider = guard;
            var anim = actor.GetComponent<BattleUnit>().animator;
            anim.enabled = true; anim.applyRootMotion = false; anim.fireEvents = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actor.SetActive(true); staging.SetActive(true); anim.Rebind(); anim.Update(0);
            int move = anim.GetLayerIndex("Move Layer"); if (move >= 0) anim.SetLayerWeight(move, 0);
            baked = new Mesh();
            int probes = 0, poses = 0; float worstGap = 0;
            foreach (string action in new[] { "Idle", "2HitCombo", "3HitCombo", "SwordAttack1", "SwordAttack2", "WhipAttack" })
            {
                float actionGap = 0;
                var origins = markers.SelectMany(Parts).Select(c => c.bounds.center).ToArray(); float movement = 0;
                // No controller transitions/events: sample each full clip, including the final poses.
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Adventure/Prefabs/Enemy/DemonLord/Anim/" + action + ".anim");
                Check(clip != null, "Missing clip " + action);
                for (int f = 0; f <= 60; f++)
                {
                    clip.SampleAnimation(actor, clip.length * f / 60f); Physics.SyncTransforms(); poses++;
                    movement = Mathf.Max(movement, markers.SelectMany(Parts).Select((c, i) => Vector3.Distance(c.bounds.center, origins[i])).Max());
                    foreach (var marker in markers)
                    {
                        var renderer = (SkinnedMeshRenderer)marker.weaponRenderer; var parts = Parts(marker);
                        renderer.BakeMesh(baked);
                        foreach (var vertex in baked.vertices)
                        {
                            Vector3 p = renderer.transform.TransformPoint(vertex);
                            float gap = parts.Min(c => Vector3.Distance(p, c.ClosestPoint(p)));
                            actionGap = Mathf.Max(actionGap, gap);
                        }
                        foreach (BoxCollider part in parts)
                        {
                            // Query the same contact function used at DealDamage, before another physics tick.
                            guard.transform.position = part.transform.TransformPoint(part.center); Physics.SyncTransforms();
                            Check(marker.TryGetContact(zone, out _), action + ": guard missed moving " + part.name); probes++;
                        }
                        guard.transform.position = actor.transform.position + Vector3.one * 100; Physics.SyncTransforms();
                        Check(!marker.TryGetContact(zone, out _), "Guard far away incorrectly contacted weapon.");
                        renderer.enabled = false;
                        guard.transform.position = parts[0].bounds.center; Physics.SyncTransforms();
                        Check(!marker.TryGetContact(zone, out _), "Hidden weapon remained parryable."); renderer.enabled = true;
                    }
                }
                worstGap = Mathf.Max(worstGap, actionGap);
                if (action != "Idle") Check(movement > .5f, action + ": clip did not animate weapon bones.");
                report.Add(action + ": 61 poses, maximum baked weapon surface distance outside hitboxes = " + actionGap.ToString("F4") +
                    " m; collider movement=" + movement.ToString("F2") + " m.");
            }
            Check(worstGap < .10f, "Bone collider coverage differs from the animated mesh by more than 10 cm.");
            actor.transform.SetPositionAndRotation(new Vector3(-31, 260, -94), Quaternion.Euler(0, 147, 0));
            actor.transform.localScale = Vector3.one * .65f;
            var attack = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Adventure/Prefabs/Enemy/DemonLord/Anim/3HitCombo.anim");
            attack.SampleAnimation(actor, attack.length * .45f); Physics.SyncTransforms();
            foreach (var marker in markers)
                foreach (BoxCollider part in Parts(marker))
                {
                    guard.transform.position = part.transform.TransformPoint(part.center); Physics.SyncTransforms();
                    Check(marker.TryGetContact(zone, out _), "Contact failed with translated/rotated/scaled encounter actor.");
                    guard.enabled = false; Check(!marker.TryGetContact(zone, out _), "Disabled guard accepted contact."); guard.enabled = true;
                }
            report.Add("All 13 boxes still contact correctly at a different position, yaw 147 and scale 0.65; disabled guards rejected.");
            // Compound collider/layer/enable guards must preserve existing parry behaviour.
            var test = markers[0]; var testPart = Parts(test)[0];
            guard.transform.position = testPart.bounds.center; Physics.SyncTransforms();
            int mask = test.parryZoneLayers; test.parryZoneLayers = 0;
            Check(!test.TryGetContact(zone, out _), "Layer filter ignored."); test.parryZoneLayers = mask;
            test.enabled = false; Check(!test.TryGetContact(zone, out _), "Disabled parry marker accepted contact.");
            report.Insert(0, "PASS: " + poses + " sampled poses; " + probes + " real primitive contact checks. No Play Mode or campaign/save APIs.");
        }
        catch (Exception error) { report.Insert(0, "FAILED: " + error); Debug.LogException(error); }
        finally
        {
            if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
            if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllLines(Result, report);
            Directory.CreateDirectory("Tools/DemonLordParry"); File.WriteAllLines("Tools/DemonLordParry/Validation.txt", report);
        }
    }
    static Collider[] Parts(EnemyMeleeParryHitbox marker) => new[] { marker.hitbox }.Concat(marker.additionalHitboxes ?? new Collider[0]).ToArray();
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    [MenuItem("Tools/Adventure/Combat/Inspect DemonLord Weapon Bones")]
    public static void Inspect()
    {
        Scene scene = default;
        GameObject staging = null;
        var report = new List<string>();
        try
        {
            scene = EditorSceneManager.NewPreviewScene();
            staging = new GameObject("Isolated DemonLord Parry Check"); staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, scene);
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), staging.transform, false);
            foreach (var script in actor.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var audio in actor.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var ps in actor.GetComponentsInChildren<ParticleSystem>(true))
            { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var main = ps.main; main.playOnAwake = false; }
            foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == "SK_FireSword" || r.name == "SK_LavaWhip"))
            {
                var mesh = renderer.sharedMesh;
                report.Add(renderer.name + ": " + mesh.vertexCount + " vertices; bones=" + renderer.bones.Length + "; bounds=" + mesh.bounds);
                var weights = mesh.boneWeights; var vertices = mesh.vertices; var bindposes = mesh.bindposes;
                for (int b = 0; b < renderer.bones.Length; b++)
                {
                    var bone = renderer.bones[b]; var points = new List<Vector3>();
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        var w = weights[v];
                        if ((w.boneIndex0 == b && w.weight0 > .05f) || (w.boneIndex1 == b && w.weight1 > .05f) ||
                            (w.boneIndex2 == b && w.weight2 > .05f) || (w.boneIndex3 == b && w.weight3 > .05f))
                            points.Add(bindposes[b].MultiplyPoint3x4(vertices[v]));
                    }
                    Bounds bounds = new Bounds();
                    if (points.Count > 0) { bounds = new Bounds(points[0], Vector3.zero); foreach (var p in points) bounds.Encapsulate(p); }
                    report.Add("  " + b + " " + bone.name + " parent=" + bone.parent.name + " local=" + bone.localPosition.ToString("F4") +
                        " scale=" + bone.lossyScale.ToString("F3") + " vertices=" + points.Count + " " + bounds.ToString("F4"));
                }
            }
        }
        catch (Exception error) { report.Insert(0, "FAILED: " + error); Debug.LogException(error); }
        finally
        {
            if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllLines(Result, report);
        }
    }
}
