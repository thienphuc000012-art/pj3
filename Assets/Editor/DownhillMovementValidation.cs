using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Downhill locomotion at real run speed, not just a jump onto a stationary slope.</summary>
[InitializeOnLoad]
public static class DownhillMovementValidation
{
    const string Request = "Library/CombatValidation/DownhillMovement.request", Output = "Tools/TerrainJump/Downhill-20261118";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static DownhillMovementValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/DownhillMovementValidation.cs") ||
            File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/PlayerController/PlayerScript.cs")) return;
        string command; try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try { Run(command == "baseline"); } catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    static float Speed(PlayerScript player) => (float)typeof(PlayerScript).GetField("fallingSpeed", Private).GetValue(player);
    [MenuItem("Tools/Adventure/Climbing/Validate Downhill Running")]
    public static void Run() => Run(false);
    static void Run(bool baseline)
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode."); Directory.CreateDirectory(Output);
        var original = SceneManager.GetActiveScene(); bool originalDirty = original.isDirty;
        var map = SceneManager.GetSceneByPath("Assets/Scenes/m.unity"); bool opened = !map.IsValid() || !map.isLoaded;
        if (opened) map = EditorSceneManager.OpenPreviewScene("Assets/Scenes/m.unity");
        var authored = map.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerScript>(true)).First(); var authoredPosition = authored.transform.position;
        var test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(original);
        Mesh ramp = null; var report = new StringBuilder(); int failures = 0;
        try
        {
            var stage = new GameObject("Inactive downhill fixture"); SceneManager.MoveGameObjectToScene(stage, test); stage.SetActive(false);
            stage.transform.position = new Vector3(12000, 12000, 12000);
            var actor = UnityEngine.Object.Instantiate(authored.gameObject, stage.transform, false);
            foreach (var script in actor.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var audio in actor.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var particles in actor.GetComponentsInChildren<ParticleSystem>(true)) { particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); var main = particles.main; main.playOnAwake = false; }
            var player = actor.GetComponent<PlayerScript>(); var animator = actor.GetComponent<Animator>(); player.animator = animator; player.CC = actor.GetComponent<CharacterController>();
            animator.enabled = true; animator.fireEvents = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            stage.SetActive(true); actor.SetActive(true); player.CC.enabled = true; animator.applyRootMotion = false;
            var slope = new GameObject("Long downhill ramp"); SceneManager.MoveGameObjectToScene(slope, test); slope.transform.position = stage.transform.position; slope.layer = 8;
            var colliderSlope = slope.AddComponent<MeshCollider>();
            foreach (float angle in new[] { 35f, 45f, 49f, 55f, 70f })
            {
                if (ramp != null) UnityEngine.Object.DestroyImmediate(ramp);
                float height = Mathf.Tan(angle * Mathf.Deg2Rad) * 80;
                ramp = new Mesh { vertices = new[] { new Vector3(-20, -height, -80), new Vector3(20, -height, -80), new Vector3(-20, height, 80), new Vector3(20, height, 80) }, triangles = new[] { 0, 2, 1, 1, 2, 3 } }; ramp.RecalculateNormals(); ramp.RecalculateBounds(); colliderSlope.sharedMesh = ramp;
                foreach (int fps in new[] { 30, 60, 120 })
                {
                    player.CC.enabled = false; actor.transform.SetPositionAndRotation(stage.transform.position + Vector3.up * .03f, Quaternion.Euler(0, 180, 0)); player.CC.enabled = true;
                    Set(player, "fallingSpeed", -2f); Set(player, "currentSlopeSlideSpeed", 0f); Set(player, "trackingAirborne", false); Set(player, "airTime", 0f); Set(player, "groundedFallRecoveryTime", 0f); Set(player, "landingStateGraceTime", 0f);
                    Set(player, "playerControl", true); Set(player, "moveDir", Vector3.back); Set(player, "currentSpeed", player.runSpeed);
                    animator.Rebind(); animator.Play("Base Layer.Basic Movement", 0, 0); animator.Update(0); animator.SetFloat("movementvalue", 1);
                    Physics.SyncTransforms(); for (int i = 0; i < 5; i++) player.CC.Move(Vector3.zero);
                    int unsupported = 0, fall = 0, land = 0; float maxGap = 0, fallPoseRun = 0, longestFallPose = 0; Vector3 start = actor.transform.position; float dt = 1f / fps;
                    for (int frame = 0; frame < fps * 6; frame++)
                    {
                        Call(player, "UpdateGroundingAndLanding", dt);
                        Vector3 input = (Vector3)Call(player, "GetHorizontalVelocity"); Call(player, "AdvanceVerticalSpeed", dt);
                        Vector3 displacement = (input + Vector3.up * Speed(player) + (Vector3)Call(player, "GetSlopeSlideVelocity", dt)) * dt;
                        var movement = typeof(PlayerScript).GetMethod("MoveCharacter", Private);
                        if (movement != null) movement.Invoke(player, new object[] { displacement }); else player.CC.Move(displacement);
                        Physics.SyncTransforms(); Call(player, "UpdateMovementAnimator"); animator.Update(dt);
                        var state = animator.GetCurrentAnimatorStateInfo(0); var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
                        bool fallPose = state.IsName("Base Layer.fallingidle 0") || next.IsName("Base Layer.fallingidle 0");
                        if (frame > fps / 3)
                        {
                            if (!player.HasGroundSupport) unsupported++; if (fallPose) fall++;
                            if (state.IsName("Base Layer.Land") || next.IsName("Base Layer.Land")) land++;
                            fallPoseRun = fallPose ? fallPoseRun + dt : 0; longestFallPose = Mathf.Max(longestFallPose, fallPoseRun);
                        }
                        if (Physics.Raycast(actor.transform.position + Vector3.up, Vector3.down, out var hit, 3, player.surfaceLayer)) maxGap = Mathf.Max(maxGap, actor.transform.position.y - hit.point.y);
                    }
                    bool pass = unsupported <= 2 && fall == 0 && land == 0 && actor.transform.position.y < start.y - .5f;
                    if (!pass) failures++;
                    report.AppendLine((pass ? "PASS" : "FAIL") + ": downhill " + angle + " degrees, " + fps + " FPS, run=" + player.runSpeed + "m/s; unsupported=" + unsupported + ", Fall frames=" + fall + ", Land frames=" + land + ", longest Fall=" + longestFallPose.ToString("F3") + "s, gap=" + maxGap.ToString("F3") + "m; delta=" + (actor.transform.position - start).ToString("F3"));
                }
            }
            colliderSlope.enabled = false;
            if (!baseline)
            {
                ValidateTerrain(player, animator, authored, stage.transform.position, test, report, ref failures);
                ValidateEdge(player, animator, stage.transform.position, test, report, ref failures);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(test, true); if (ramp != null) UnityEngine.Object.DestroyImmediate(ramp); if (opened) EditorSceneManager.ClosePreviewScene(map);
        }
        Check(authored == null || authored.transform.position == authoredPosition, "Live player must not move."); Check(SceneManager.GetActiveScene() == original && original.isDirty == originalDirty, "Live scene/dirty state must remain unchanged.");
        report.AppendLine("Failures: " + failures + ". Native isolated CharacterController + actual player Animator; no Play Mode, saves or map edits.");
        File.WriteAllText(Output + (baseline ? "/Baseline.txt" : "/Validation.txt"), report.ToString()); Debug.Log("[Downhill Running] " + (baseline ? "BASELINE" : failures == 0 ? "PASS" : "FAIL") + ": " + Output);
        if (!baseline) Check(failures == 0, "Downhill regressions: " + failures);
    }
    static void ValidateTerrain(PlayerScript player, Animator animator, PlayerScript authored, Vector3 offset, Scene test, StringBuilder report, ref int failures)
    {
        var source = authored.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TerrainCollider>(true)).First(t => t.name == "Terrain Main");
        var data = source.terrainData; var terrainHost = new GameObject("Actual m terrain collision copy"); SceneManager.MoveGameObjectToScene(terrainHost, test);
        terrainHost.layer = source.gameObject.layer; terrainHost.transform.position = source.transform.position + offset;
        terrainHost.AddComponent<TerrainCollider>().terrainData = data;
        var candidates = new List<Vector3>();
        for (int z = 1; z < 64; z++) for (int x = 1; x < 64; x++)
        {
            float u = x / 64f, v = z / 64f; float angle = data.GetSteepness(u, v);
            if (angle < 25 || angle > 46) continue;
            Vector3 normal = data.GetInterpolatedNormal(u, v); Vector3 direction = new Vector3(normal.x, 0, normal.z).normalized;
            Vector3 position = source.transform.position + new Vector3(u * data.size.x, data.GetInterpolatedHeight(u, v), v * data.size.z);
            bool smooth = true;
            for (float step = 0; step < 14; step += .25f)
            {
                Vector3 local = position + direction * step - source.transform.position; float a = local.x / data.size.x, b = local.z / data.size.z;
                if (a <= .01f || b <= .01f || a >= .99f || b >= .99f || data.GetSteepness(a, b) > 48) { smooth = false; break; }
            }
            if (smooth) candidates.Add(position);
        }
        var selected = new List<Vector3>();
        foreach (var point in candidates.OrderBy(p => (p - authored.transform.position).sqrMagnitude))
        { if (selected.All(p => Vector3.Distance(p, point) > 8)) selected.Add(point); if (selected.Count == 4) break; }
        Check(selected.Count >= 3, "Need several continuous real terrain slopes, rather than a flat spawn sample.");
        foreach (var point in selected) foreach (int fps in new[] { 30, 60, 120 })
        {
            Vector3 local = point - source.transform.position; Vector3 normal = data.GetInterpolatedNormal(local.x / data.size.x, local.z / data.size.z);
            Vector3 direction = new Vector3(normal.x, 0, normal.z).normalized;
            player.CC.enabled = false; player.transform.position = point + offset + Vector3.up * (.04f + player.CC.radius * (1 / normal.y - 1)); player.CC.enabled = true;
            Set(player, "fallingSpeed", -2f); Set(player, "currentSlopeSlideSpeed", 0f); Set(player, "trackingAirborne", false); Set(player, "airTime", 0f); Set(player, "groundedFallRecoveryTime", 0f); Set(player, "landingStateGraceTime", 0f);
            Set(player, "moveDir", direction); Set(player, "currentSpeed", player.runSpeed);
            animator.Rebind(); animator.Play("Base Layer.Basic Movement", 0, 0); animator.Update(0); animator.SetFloat("movementvalue", 1); Physics.SyncTransforms();
            int air = 0, land = 0, unsupported = 0; float dt = 1f / fps;
            for (int frame = 0; frame < fps * 4; frame++)
            {
                Call(player, "UpdateGroundingAndLanding", dt); var input = (Vector3)Call(player, "GetHorizontalVelocity"); Call(player, "AdvanceVerticalSpeed", dt);
                Call(player, "MoveCharacter", (input + Vector3.up * Speed(player) + (Vector3)Call(player, "GetSlopeSlideVelocity", dt)) * dt);
                Physics.SyncTransforms(); Call(player, "UpdateMovementAnimator"); animator.Update(dt);
                if (frame <= fps / 3) continue;
                if (!player.HasGroundSupport) unsupported++;
                var state = animator.GetCurrentAnimatorStateInfo(0); var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
                if (state.IsName("Base Layer.fallingidle 0") || next.IsName("Base Layer.fallingidle 0")) air++;
                if (state.IsName("Base Layer.Land") || next.IsName("Base Layer.Land")) land++;
            }
            bool pass = unsupported <= 2 && air == 0 && land == 0; if (!pass) failures++;
            report.AppendLine((pass ? "PASS" : "FAIL") + ": actual m terrain at " + point.ToString("F3") + ", " + fps + " FPS, angle=" + Vector3.Angle(normal, Vector3.up).ToString("F1") + "; unsupported=" + unsupported + ", Fall=" + air + ", Land=" + land);
        }
        terrainHost.GetComponent<TerrainCollider>().enabled = false;
    }
    static void ValidateEdge(PlayerScript player, Animator animator, Vector3 offset, Scene test, StringBuilder report, ref int failures)
    {
        BoxCollider Box(string name, Vector3 position, Vector3 size)
        {
            var host = new GameObject(name); SceneManager.MoveGameObjectToScene(host, test); host.transform.position = offset + position; host.layer = 8;
            var box = host.AddComponent<BoxCollider>(); box.size = size; return box;
        }
        var platform = Box("Edge start platform", new Vector3(0, -.5f, 0), new Vector3(12, 1, 16));
        var floor = Box("Edge landing floor", new Vector3(0, -2.5f, 25), new Vector3(12, 1, 60));
        foreach (float height in new[] { .15f, 2f }) foreach (int fps in new[] { 30, 60, 120 })
        {
            floor.transform.position = offset + new Vector3(0, -height - .5f, 25);
            player.CC.enabled = false; player.transform.position = offset + new Vector3(0, .03f, 6); player.CC.enabled = true;
            Set(player, "fallingSpeed", -2f); Set(player, "currentSlopeSlideSpeed", 0f); Set(player, "trackingAirborne", false); Set(player, "airTime", 0f); Set(player, "groundedFallRecoveryTime", 0f); Set(player, "landingStateGraceTime", 0f);
            Set(player, "currentSpeed", player.runSpeed); animator.Rebind(); animator.Play("Base Layer.Basic Movement", 0, 0); animator.Update(0); Physics.SyncTransforms();
            bool sawFall = false, sawLand = false; float dt = 1f / fps;
            for (int frame = 0; frame < fps * 6; frame++)
            {
                Set(player, "moveDir", player.transform.position.z < offset.z + 13 ? Vector3.forward : Vector3.zero);
                Call(player, "UpdateGroundingAndLanding", dt); var input = (Vector3)Call(player, "GetHorizontalVelocity"); Call(player, "AdvanceVerticalSpeed", dt);
                Call(player, "MoveCharacter", (input + Vector3.up * Speed(player) + (Vector3)Call(player, "GetSlopeSlideVelocity", dt)) * dt);
                Physics.SyncTransforms(); Call(player, "UpdateMovementAnimator"); animator.Update(dt);
                var state = animator.GetCurrentAnimatorStateInfo(0); var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
                sawFall |= animator.GetBool("IsFalling") || state.IsName("Base Layer.fallingidle 0") || next.IsName("Base Layer.fallingidle 0");
                sawLand |= state.IsName("Base Layer.Land") || next.IsName("Base Layer.Land");
            }
            bool pass = player.HasGroundSupport && animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Basic Movement") &&
                (height < .2f ? !sawFall && !sawLand : sawFall && sawLand);
            if (!pass) failures++;
            report.AppendLine((pass ? "PASS" : "FAIL") + ": " + height + "m downward step, " + fps + " FPS; Fall=" + sawFall + ", Land=" + sawLand + ", supported=" + player.HasGroundSupport + ". Small step adheres; high ledge preserves genuine fall/landing.");
        }
        platform.enabled = floor.enabled = false;
    }
}
