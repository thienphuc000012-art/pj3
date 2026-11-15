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

/// <summary>Native terrain/CharacterController regressions without entering Play Mode or changing the map.</summary>
public static class TerrainJumpValidation
{
    const string Request = "Library/CombatValidation/TerrainJump.request";
    const string Output = "Tools/TerrainJump";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Ground = typeof(PlayerScript).GetMethod("SurfaceCheck", Private);
    static readonly MethodInfo Vertical = typeof(PlayerScript).GetMethod("AdvanceVerticalSpeed", Private);
    static readonly MethodInfo Horizontal = typeof(PlayerScript).GetMethod("GetHorizontalVelocity", Private);
    static readonly MethodInfo Animation = typeof(PlayerScript).GetMethod("UpdateMovementAnimator", Private);
    static readonly MethodInfo Recovery = typeof(PlayerScript).GetMethod("RecoverStuckFallingIdle", Private);
    static readonly FieldInfo Speed = typeof(PlayerScript).GetField("fallingSpeed", Private);
    static readonly FieldInfo GroundFlag = typeof(PlayerScript).GetField("<onSurface>k__BackingField", Private);
    static readonly Vector3 Offset = new Vector3(4000, 5000, 6000);

    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/TerrainJumpValidation.cs")) return;
        try
        {
            string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
            if (command == "climb") LedgeClimbValidation.Validate(); else Validate();
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Adventure/Climbing/Validate Terrain Jump")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run terrain jump checks in Edit Mode.");
        Check(Ground != null && Vertical != null && Horizontal != null && Animation != null && Recovery != null && Speed != null && GroundFlag != null,
            "Missing player movement methods.");
        Directory.CreateDirectory(Output); var report = new StringBuilder();
        Scene original = SceneManager.GetActiveScene(), test = default;
        Mesh ramp = null; GameObject root = null;
        try
        {
            var authored = UnityEngine.Object.FindObjectsByType<PlayerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(p => p.gameObject.scene.name == "m");
            if (authored == null) authored = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Adventure/Prefabs/Exploration/ExplorationPlayer.prefab").GetComponent<PlayerScript>();
            Vector3 authoredPosition = authored.transform.position;
            var authoredController = authored.GetComponent<CharacterController>();
            report.AppendLine("Player m: " + authoredPosition.ToString("F4") + "; force=" + authored.jumpForce +
                "; CC radius=" + authoredController.radius + ", slope=" + authoredController.slopeLimit + ", skin=" + authoredController.skinWidth);
            test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(original);
            root = new GameObject("Isolated terrain jump checks"); SceneManager.MoveGameObjectToScene(root, test);
            root.transform.position = Offset;
            var actor = new GameObject("Player physics fixture"); actor.transform.SetParent(root.transform, false); actor.layer = 7;
            var cc = actor.AddComponent<CharacterController>();
            cc.height = authoredController.height; cc.radius = authoredController.radius; cc.center = authoredController.center;
            cc.slopeLimit = authoredController.slopeLimit; cc.stepOffset = authoredController.stepOffset; cc.skinWidth = authoredController.skinWidth;
            cc.minMoveDistance = authoredController.minMoveDistance;
            var player = actor.AddComponent<PlayerScript>(); player.enabled = false; player.CC = cc;
            player.surfaceLayer = authored.surfaceLayer; player.surfaceCheckRadius = authored.surfaceCheckRadius;
            player.surfaceCheckOffset = authored.surfaceCheckOffset; player.jumpForce = authored.jumpForce;
            var floor = Box(root, "Floor", new Vector3(0, -.5f, 0), new Vector3(20, 1, 20));
            var wall = Box(root, "Raised terrain side", new Vector3(0, 4, -.5f), new Vector3(20, 8, 1));
            wall.enabled = false;

            Reset(player, Offset + Vector3.up * .03f);
            Check(IsGrounded(player), "A flat floor under the feet must be grounded.");
            player.PerformJump(); Check(!player.onSurface, "Jump must immediately clear the previous grounded flag.");
            GroundFlag.SetValue(player, true); Speed.SetValue(player, player.jumpForce);
            Vertical.Invoke(player, new object[] { 1f / 60f });
            Check((float)Speed.GetValue(player) < player.jumpForce, "Even a stale ground flag must not suspend rising gravity.");
            Check(!IsGrounded(player), "A rising player must never be grounded, even with the old floor still in range.");
            report.AppendLine("PASS: flat support, immediate takeoff, stale-grounded ascent always applies gravity.");

            floor.enabled = false; wall.enabled = true;
            Reset(player, Offset + new Vector3(0, 2, .17f));
            bool oldWallGround = LegacyGround(player);
            Check(oldWallGround && !IsGrounded(player) && !player.HasGroundSupport, "Reproduce the old wall-as-ground false positive and reject it.");
            Check(((Vector3)Horizontal.Invoke(player, null)).sqrMagnitude == 0, "Airborne player must not drive forward without movement input.");
            report.AppendLine("PASS: former overlap query reports a raised side wall as ground; downward walkable-support query rejects it.");
            wall.enabled = false;

            var slopeObject = new GameObject("Terrain slope fixture"); slopeObject.transform.SetParent(root.transform, false); slopeObject.layer = 8;
            var slopeCollider = slopeObject.AddComponent<MeshCollider>();
            foreach (float angle in new[] { 35f, 70f })
            {
                if (ramp != null) UnityEngine.Object.DestroyImmediate(ramp);
                ramp = Ramp(angle); slopeCollider.sharedMesh = ramp;
                Reset(player, Offset + Vector3.up * .03f);
                Check(IsGrounded(player) == (angle <= cc.slopeLimit), "Walkable-slope support classification failed at " + angle + " degrees.");
                Check(player.HasGroundSupport, "A steep foot contact must end free fall without permitting a jump.");
                if (angle <= cc.slopeLimit)
                    for (int i = 0; i < 30; i++)
                    {
                        cc.Move((Vector3.forward + Vector3.down * 2) / 60f); Physics.SyncTransforms();
                        Check(IsGrounded(player), "Ground probe must follow the CharacterController hemisphere on a walkable slope.");
                    }
                if (angle > cc.slopeLimit)
                {
                    foreach (int fps in new[] { 30, 60, 120 })
                        Rest(player, fps, "70-degree slope", report);
                    ValidateFallRecovery(player, authored, root, report);
                }
            }
            slopeCollider.enabled = false;
            report.AppendLine("PASS: 35-degree terrain permits standing/jumping; 70-degree foot support ends Fall without becoming a jump platform.");

            floor.enabled = true; floor.isTrigger = true;
            Reset(player, Offset + Vector3.up * .03f); Check(!IsGrounded(player), "Triggers must not count as ground.");
            floor.isTrigger = false;
            floor.gameObject.layer = 0;
            Reset(player, Offset + Vector3.up * .03f); cc.Move(Vector3.down * .1f); Physics.SyncTransforms();
            Check(cc.isGrounded && IsGrounded(player), "Preserve actual controller support on a solid floor outside the custom probe mask.");
            floor.gameObject.layer = 8;
            report.AppendLine("PASS: triggers ignored; real controller support on other solid floor layers preserved.");
            foreach (int fps in new[] { 30, 60, 120 })
            {
                foreach (bool nearWall in new[] { false, true })
                {
                    wall.enabled = nearWall;
                    var result = Flight(player, Offset + new Vector3(0, .03f, .34f), Vector3.back, fps, false);
                    float expected = player.jumpForce * player.jumpForce / (-2 * Physics.gravity.y);
                    Check(result.ascendingGroundFrames == 0 && result.rise <= expected + .15f && result.rise >= expected - .3f && result.landed,
                        "Unbounded/stuck jump at " + fps + " FPS, wall=" + nearWall + ": " + result);
                    report.AppendLine("PASS: " + fps + " FPS, wall=" + nearWall + "; " + result);
                }
            }
            wall.enabled = false; floor.enabled = false;
            ValidateTerrain(player, authored, root, report);
            Check(authored.transform.position == authoredPosition, "Live/authored player must not move during checks.");
            report.AppendLine("Isolated native CharacterController/Physics tests only. No Play Mode, map/terrain edits, spawn changes or campaign save writes.");
            File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[Terrain Jump] " + report);
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (ramp != null) UnityEngine.Object.DestroyImmediate(ramp);
            if (test.IsValid()) EditorSceneManager.CloseScene(test, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
    }

    sealed class FlightResult
    {
        public float rise; public int ascendingGroundFrames; public bool landed;
        public bool controllerGrounded; public Vector3 finalDelta, floorNormal; public float floorGap;
        public override string ToString() => "rise=" + rise.ToString("F3") + "m, ascending-ground frames=" + ascendingGroundFrames + ", landed=" + landed +
            (landed ? "" : ", CC grounded=" + controllerGrounded + ", delta=" + finalDelta.ToString("F3") + ", floor gap=" + floorGap.ToString("F3") + ", normal=" + floorNormal.ToString("F3"));
    }
    static FlightResult Flight(PlayerScript player, Vector3 position, Vector3 direction, int fps, bool legacy)
    {
        Reset(player, position); player.PerformJump();
        var result = new FlightResult(); float dt = 1f / fps;
        for (int i = 0; i < fps * 3; i++)
        {
            bool grounded;
            if (legacy) { grounded = LegacyGround(player); GroundFlag.SetValue(player, grounded); }
            else { IsGrounded(player); grounded = player.HasGroundSupport; }
            float speed = (float)Speed.GetValue(player);
            if (grounded && speed > 0) result.ascendingGroundFrames++;
            if (i > fps / 5 && grounded && speed <= 0) { result.landed = true; break; }
            if (legacy) { if (grounded && speed <= 0) speed = -2; else if (!grounded) speed += Physics.gravity.y * dt; Speed.SetValue(player, speed); }
            else { Vertical.Invoke(player, new object[] { dt }); speed = (float)Speed.GetValue(player); }
            var flags = player.CC.Move((direction * (grounded ? player.walkSpeed : player.walkSpeed / 2) + Vector3.up * speed) * dt);
            if ((flags & CollisionFlags.Above) != 0 && speed > 0) Speed.SetValue(player, 0f);
            Physics.SyncTransforms();
            result.rise = Mathf.Max(result.rise, player.transform.position.y - position.y);
        }
        result.controllerGrounded = player.CC.isGrounded; result.finalDelta = player.transform.position - position;
        if (Physics.Raycast(player.transform.position + Vector3.up * .5f, Vector3.down, out var support, 10, player.surfaceLayer, QueryTriggerInteraction.Ignore))
        { result.floorGap = player.transform.position.y - support.point.y; result.floorNormal = support.normal; }
        return result;
    }
    static void ValidateTerrain(PlayerScript player, PlayerScript authored, GameObject root, StringBuilder report)
    {
        if (authored.gameObject.scene.name != "m") { report.AppendLine("Actual terrain check skipped: scene m is not loaded."); return; }
        var source = authored.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TerrainCollider>(true))
            .FirstOrDefault(t => t.name == "Terrain Main");
        Check(source != null && source.terrainData != null, "Missing Terrain Main in scene m.");
        var obj = new GameObject("Read-only Terrain Main clone"); obj.transform.SetParent(root.transform, false);
        obj.transform.position = source.transform.position + Offset; obj.layer = source.gameObject.layer;
        var terrain = obj.AddComponent<TerrainCollider>();
        var serialized = new SerializedObject(terrain);
        var trees = serialized.FindProperty("m_EnableTreeColliders");
        Check(trees != null, "Missing TerrainCollider tree-collision setting.");
        trees.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
        terrain.terrainData = source.terrainData;
        var relative = authored.transform.position - source.transform.position;
        float u = Mathf.Clamp01(relative.x / source.terrainData.size.x), v = Mathf.Clamp01(relative.z / source.terrainData.size.z);
        report.AppendLine("Terrain sample normal=" + source.terrainData.GetInterpolatedNormal(u, v).ToString("F3") + "; scene dirty=" + authored.gameObject.scene.isDirty);
        float height = source.transform.position.y + source.terrainData.GetInterpolatedHeight(u, v);
        Vector3 start = new Vector3(authored.transform.position.x, height + .04f, authored.transform.position.z) + Offset;
        for (int heading = 0; heading < 8; heading++)
        {
            Vector3 direction = Quaternion.Euler(0, heading * 45, 0) * Vector3.forward;
            var old = Flight(player, start, direction, 60, true); var current = Flight(player, start, direction, 60, false);
            Check(current.ascendingGroundFrames == 0, "Terrain sides must never ground the ascending player.");
            report.AppendLine("Actual m terrain heading " + heading * 45 + ": old [" + old + "]; current [" + current + "]");
            if (player.IsOnSteepSurface) Rest(player, 60, "m terrain heading " + heading * 45, report);
        }
        terrain.enabled = false;
    }
    static bool IsGrounded(PlayerScript player) { Ground.Invoke(player, null); return player.onSurface; }
    static bool LegacyGround(PlayerScript player) => player.CC.isGrounded || Physics.CheckSphere(player.transform.TransformPoint(player.surfaceCheckOffset),
        Mathf.Max(player.surfaceCheckRadius, player.CC.radius * .65f), player.surfaceLayer, QueryTriggerInteraction.Ignore);
    static void Reset(PlayerScript player, Vector3 position)
    {
        player.CC.enabled = false; player.transform.SetPositionAndRotation(position, Quaternion.identity); player.CC.enabled = true;
        Speed.SetValue(player, -2f); GroundFlag.SetValue(player, false); Physics.SyncTransforms();
        Set(player, "onSteepSurface", false); Set(player, "moveDir", Vector3.zero);
    }
    static void Rest(PlayerScript player, int fps, string label, StringBuilder report)
    {
        Set(player, "moveDir", Vector3.zero); Set(player, "currentSpeed", player.walkSpeed);
        for (int i = 0; i < 5; i++) { player.CC.Move(Vector3.zero); Physics.SyncTransforms(); }
        Vector3 start = player.transform.position;
        for (int i = 0; i < fps * 2; i++)
        {
            IsGrounded(player);
            Check(player.HasGroundSupport && player.IsOnSteepSurface && !player.onSurface,
                label + ": foot support must persist without enabling another jump.");
            Vertical.Invoke(player, new object[] { 1f / fps });
            var velocity = (Vector3)Horizontal.Invoke(player, null) + Vector3.up * (float)Speed.GetValue(player);
            Check(velocity.sqrMagnitude < .000001f, label + ": neutral input must not create a slide/forward drive.");
            player.CC.Move(velocity / fps); Physics.SyncTransforms();
        }
        float drift = Vector3.Distance(start, player.transform.position);
        Check(drift < .01f, label + ": unintended resting drift " + drift);
        report.AppendLine("PASS: " + label + ", " + fps + " FPS, 2s neutral drift=" + drift.ToString("F4") + "m; no jump permission.");
    }
    static void ValidateFallRecovery(PlayerScript physicsPlayer, PlayerScript authored, GameObject root, StringBuilder report)
    {
        var staging = new GameObject("Inactive animation test staging"); staging.transform.SetParent(root.transform, false); staging.SetActive(false);
        var actor = UnityEngine.Object.Instantiate(authored.gameObject, staging.transform, false);
        foreach (var script in actor.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
        foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var audio in actor.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (var ps in actor.GetComponentsInChildren<ParticleSystem>(true))
        { ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); var main = ps.main; main.playOnAwake = false; }
        var player = actor.GetComponent<PlayerScript>(); var animator = actor.GetComponent<Animator>(); player.animator = animator;
        actor.transform.SetPositionAndRotation(physicsPlayer.transform.position, Quaternion.identity);
        animator.enabled = true; animator.fireEvents = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        staging.SetActive(true); actor.SetActive(true); animator.applyRootMotion = false; animator.Rebind();
        const string fall = "Base Layer.fallingidle 0", idle = "Base Layer.Basic Movement";
        Check(animator.HasState(0, Animator.StringToHash(fall)) && animator.HasState(0, Animator.StringToHash(idle)), "Missing authored fall/locomotion states.");
        animator.Play(fall, 0, 0); animator.Update(0); animator.SetBool("IsFalling", true);
        Set(player, "fallingAnimationActive", true); Set(player, "groundedFallRecoveryTime", .2f); Set(player, "landingStateGraceTime", 0f);
        Speed.SetValue(player, -10f); IsGrounded(player);
        Check(player.IsOnSteepSurface && !player.onSurface, "Animation fixture must be on a steep non-jumpable foot contact.");
        Animation.Invoke(player, null); Recovery.Invoke(player, null);
        for (int i = 0; i < 30; i++) animator.Update(1f / 60f);
        Check(animator.GetBool("OnSurface") && !animator.GetBool("IsFalling") && animator.GetCurrentAnimatorStateInfo(0).IsName(idle),
            "Actual player Animator must exit Fall into locomotion on steep support.");
        report.AppendLine("PASS: actual player controller/avatar exits fallingidle into Basic Movement on steep support; IsFalling=false, animator OnSurface=true, jump permission=false.");
        UnityEngine.Object.DestroyImmediate(staging);
    }
    static void Set(PlayerScript player, string field, object value) => typeof(PlayerScript).GetField(field, Private).SetValue(player, value);
    static BoxCollider Box(GameObject root, string name, Vector3 position, Vector3 size)
    {
        var obj = new GameObject(name); obj.transform.SetParent(root.transform, false); obj.transform.localPosition = position; obj.layer = 8;
        var box = obj.AddComponent<BoxCollider>(); box.size = size; return box;
    }
    static Mesh Ramp(float angle)
    {
        float height = Mathf.Tan(angle * Mathf.Deg2Rad) * 5;
        var mesh = new Mesh { name = "Temporary terrain slope", vertices = new[] { new Vector3(-5, -height, -5), new Vector3(5, -height, -5),
            new Vector3(-5, height, 5), new Vector3(5, height, 5) }, triangles = new[] { 0, 2, 1, 1, 2, 3 } };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
