using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit, isolated checks of the actual player prefabs. Never loads or saves a game scene.</summary>
[InitializeOnLoad]
public static class BattleEntryWalkValidation
{
    const string RequestPath = "Library/CombatValidation/BattleEntryWalkValidation.request";
    const string ResultPath = "Library/CombatValidation/BattleEntryWalkValidation.result.txt";
    const float Distance = 3f;
    const float MoveSpeed = 1.8f;
    const float OriginalAnimationSpeed = .73f;
    static readonly Vector3 Slot = new Vector3(12.5f, 4.25f, -8.75f);
    static readonly Quaternion SlotRotation = Quaternion.Euler(0f, 127f, 0f);
    static readonly string[] PrefabPaths =
    {
        "Assets/Adventure/Prefabs/player/Mele/player (3).prefab",
        "Assets/Adventure/Prefabs/player/Mage/SK_Lauren Red.prefab",
        "Assets/Adventure/Prefabs/player/player (1).prefab"
    };

    static BattleEntryWalkValidation() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        Run();
    }

    [MenuItem("Tools/Adventure/Combat/Validate Player Battle Entry Walk")]
    public static void Run() => Validate(false);

    public static void RunBatch() => Validate(true);

    static void Validate(bool rethrow)
    {
        Scene preview = default;
        var results = new List<string>();
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run battle-entry validation in Edit Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            preview = EditorSceneManager.NewPreviewScene();
            foreach (string path in PrefabPaths) ValidatePrefab(path, preview, results);
            Check(new BattleEntryWalk(null, Distance, MoveSpeed).IsComplete, "A missing member must finish immediately.");
            results.Add("PASS: null member completes immediately. All checks used isolated prefab clones, manual Animator evaluation and disabled gameplay events. No live scenes, controllers, prefabs or saves were written.");
            File.WriteAllLines(ResultPath, results);
            Debug.Log(string.Join("\n", results));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            results.Add("FAIL: " + exception);
            File.WriteAllLines(ResultPath, results);
            Debug.LogException(exception);
            if (rethrow) throw;
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static void ValidatePrefab(string path, Scene preview, List<string> results)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Check(prefab != null, "Player prefab missing: " + path);
        var staging = new GameObject("Isolated Battle Entry / " + prefab.name);
        staging.SetActive(false);
        SceneManager.MoveGameObjectToScene(staging, preview);
        BattleEntryWalk walk = null;
        try
        {
            var clone = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
            var unit = clone.GetComponentInChildren<BattleUnit>(true);
            Check(unit != null, "BattleUnit missing: " + path);
            // Inactive staging prevents gameplay OnEnable callbacks before their scripts are disabled.
            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour != unit) behaviour.enabled = false;
            foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            foreach (var audio in clone.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var particles in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false;
            }
            var animator = unit.animator != null ? unit.animator : unit.GetComponentInChildren<Animator>(true);
            Check(animator != null && animator.runtimeAnimatorController != null, "Animator/controller missing: " + path);
            unit.animator = animator;
            unit.enabled = true;
            unit.gameObject.SetActive(true);
            clone.SetActive(true);
            animator.gameObject.SetActive(true);
            animator.enabled = true;
            animator.fireEvents = false;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            staging.SetActive(true);
            unit.SetPersistentHP(Mathf.Max(1, unit.maxHP));
            Check(!unit.IsDead, "The isolated player must be alive.");
            unit.transform.SetPositionAndRotation(Slot, SlotRotation);
            animator.Rebind();
            animator.Play(unit.idleStateName, 0, 0f);
            animator.Update(0f);
            Check(animator.HasState(0, Animator.StringToHash(unit.battleEntryWalkStateName)),
                "BattleEntryWalk state missing; run its controller setup first: " + path);
            Check(animator.HasState(0, Animator.StringToHash(unit.idleStateName)), "Combat Idle state missing: " + path);
            var expectedClip = WalkClip(animator.runtimeAnimatorController, unit.battleEntryWalkStateName);
            Check(expectedClip != null && expectedClip.length > 0f && expectedClip.isLooping,
                "BattleEntryWalk must contain an actual looping walk clip: " + path);
            var bones = PoseBones(animator);
            Check(bones.Length > 0, "No animated rig bones found: " + path);
            animator.speed = OriginalAnimationSpeed;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            unit.battleEntryAnimationSpeed = 1f;
            Vector3 forward = Vector3.ProjectOnPlane(SlotRotation * Vector3.forward, Vector3.up).normalized;
            Vector3 start = Slot - forward * Distance;
            var modelPosition = animator.transform.localPosition;
            var modelRotation = animator.transform.localRotation;

            walk = new BattleEntryWalk(unit, Distance, MoveSpeed);
            Check(!walk.IsComplete, "A configured walk should start running: " + path);
            AssertPose(unit, start, "initial spawn");
            Check(Mathf.Approximately(animator.speed, 1f) && animator.cullingMode == AnimatorCullingMode.AlwaysAnimate,
                "Entry must own the requested animation speed and animate offscreen.");
            Check(!animator.applyRootMotion, "Entry must keep root motion off.");
            AssertWalkClip(animator, unit, expectedClip);
            var firstPose = BonePose(bones);
            bool poseChanged = false;
            float halfTime = Distance / MoveSpeed * .5f;
            float step = halfTime / 10f;
            float elapsed = 0f;
            for (int frame = 0; frame < 10; frame++)
            {
                walk.Tick(step);
                elapsed += step;
                animator.Update(step);
                AssertPose(unit, start + forward * MoveSpeed * elapsed, "walking sample " + frame);
                AssertModelRoot(animator, unit, modelPosition, modelRotation);
                AssertWalkClip(animator, unit, expectedClip);
                poseChanged |= PoseChanged(firstPose, BonePose(bones));
            }
            Check(poseChanged, "The walk state advanced but rig bones stayed frozen: " + path);
            AssertPose(unit, Vector3.Lerp(start, Slot, .5f), "halfway constant-speed travel");
            walk.Tick(-1f);
            animator.Update(0f);
            AssertPose(unit, Vector3.Lerp(start, Slot, .5f), "negative delta must not move backwards");
            for (int frame = 0; frame < 20 && !walk.IsComplete; frame++)
            {
                walk.Tick(step);
                elapsed += step;
                animator.Update(step);
                AssertPose(unit, start + forward * Mathf.Min(Distance, MoveSpeed * elapsed), "arrival sample " + frame);
                AssertModelRoot(animator, unit, modelPosition, modelRotation);
            }
            Check(walk.IsComplete, "Walk did not finish within its bounded distance/speed duration.");
            AssertRestored(unit, animator, "natural completion");
            AssertPose(unit, Slot, "final slot");
            SettleIdle(animator);
            AssertIdle(unit, animator);
            AssertModelRoot(animator, unit, modelPosition, modelRotation);
            walk.Finish();
            walk.Finish(false);
            walk.Tick(99f);
            AssertPose(unit, Slot, "repeated Finish/Tick");
            AssertRestored(unit, animator, "idempotent completion");

            walk = new BattleEntryWalk(unit, Distance, MoveSpeed);
            walk.Tick(halfTime);
            animator.Update(.1f);
            Vector3 interrupted = unit.transform.position;
            walk.Finish(false);
            walk.Finish();
            AssertPose(unit, interrupted, "cancel without snapping");
            AssertRestored(unit, animator, "interrupted re-entry");
            SettleIdle(animator);
            AssertIdle(unit, animator);

            unit.transform.SetPositionAndRotation(Slot, SlotRotation);
            walk = new BattleEntryWalk(unit, Distance, MoveSpeed);
            AssertPose(unit, start, "re-entry spawn");
            walk.Finish();
            SettleIdle(animator);
            AssertPose(unit, Slot, "explicit Finish snaps to slot");
            AssertRestored(unit, animator, "explicit completion");
            AssertIdle(unit, animator);

            string configuredWalk = unit.battleEntryWalkStateName;
            unit.battleEntryWalkStateName = "Base Layer.__MissingBattleEntryWalkValidation__";
            walk = new BattleEntryWalk(unit, Distance, MoveSpeed);
            Check(walk.IsComplete, "A missing walk state must finish immediately.");
            walk.Tick(99f);
            walk.Finish();
            AssertPose(unit, Slot, "missing walk state");
            AssertRestored(unit, animator, "missing walk state");
            unit.battleEntryWalkStateName = configuredWalk;
            walk = new BattleEntryWalk(unit, 0f, MoveSpeed);
            Check(walk.IsComplete, "Zero-distance entry must finish immediately.");
            walk.Tick(99f);
            walk.Finish();
            AssertPose(unit, Slot, "zero-distance entry");
            AssertRestored(unit, animator, "zero-distance entry");

            Vector3 groundedStart = Slot - forward + Vector3.down * .35f;
            Vector3 landing = Slot - forward * .5f;
            walk = new BattleEntryWalk(unit, Distance, MoveSpeed, new[] { groundedStart, landing, Slot });
            AssertPose(unit, groundedStart, "shortened arena path starts on the ground");
            float uphillDistance = Vector3.Distance(groundedStart, landing);
            walk.Tick(uphillDistance / MoveSpeed * .5f);
            AssertPose(unit, Vector3.Lerp(groundedStart, landing, .5f), "arena path follows the slope");
            walk.Tick((uphillDistance * .5f + .25f) / MoveSpeed);
            AssertPose(unit, Vector3.Lerp(landing, Slot, .5f), "arena walk crosses a waypoint at constant speed");
            walk.Tick(1f);
            Check(walk.IsComplete, "Arena walk must finish after the shortened path.");
            AssertPose(unit, Slot, "arena walk arrives at the slot");
            AssertRestored(unit, animator, "arena walk completion");
            SettleIdle(animator);
            AssertIdle(unit, animator);
            walk = new BattleEntryWalk(unit, Distance, MoveSpeed, Array.Empty<Vector3>());
            Check(walk.IsComplete, "A blocked arena path must leave this player at the slot.");
            AssertPose(unit, Slot, "blocked arena path");
            results.Add("PASS: " + path + " / " + expectedClip.name + ". Offset behind yaw-127° slot; constant-speed travel; animated bones; Idle return; speed/culling restored; cancellation; missing-state fallback; grounded arena path and blocked slot.");
        }
        finally
        {
            walk?.Finish(false);
            UnityEngine.Object.DestroyImmediate(staging);
        }
    }

    static AnimationClip WalkClip(RuntimeAnimatorController runtime, string statePath)
    {
        var overrides = runtime as AnimatorOverrideController;
        var controller = (overrides != null ? overrides.runtimeAnimatorController : runtime) as AnimatorController;
        if (controller == null || controller.layers.Length == 0) return null;
        string stateName = statePath.Substring(statePath.LastIndexOf('.') + 1);
        var state = controller.layers[0].stateMachine.states.Select(child => child.state).FirstOrDefault(item => item.name == stateName);
        var clip = state != null ? state.motion as AnimationClip : null;
        return clip != null && overrides != null ? overrides[clip] : clip;
    }

    static Transform[] PoseBones(Animator animator)
    {
        if (animator.isHuman)
            return new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot }.Select(animator.GetBoneTransform).Where(bone => bone != null).ToArray();
        return animator.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .SelectMany(renderer => renderer.bones).Where(bone => bone != null && bone != animator.transform).Distinct().ToArray();
    }

    static Pose[] BonePose(Transform[] bones) => bones.Select(bone => new Pose(bone.localPosition, bone.localRotation)).ToArray();

    static bool PoseChanged(Pose[] before, Pose[] after) => before.Where((pose, index) =>
        Vector3.Distance(pose.position, after[index].position) > .0001f ||
        Quaternion.Angle(pose.rotation, after[index].rotation) > .1f).Any();

    static void AssertWalkClip(Animator animator, BattleUnit unit, AnimationClip clip)
    {
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName(unit.battleEntryWalkStateName), "Entry left its walk state before arrival.");
        Check(animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip == clip && info.weight > .01f),
            "The configured walk clip is not being evaluated.");
    }

    static void AssertPose(BattleUnit unit, Vector3 position, string context)
    {
        Check(Vector3.Distance(unit.transform.position, position) < .002f &&
            Quaternion.Angle(unit.transform.rotation, SlotRotation) < .05f,
            "Player root position/rotation drift during " + context + ": " + unit.transform.position + ".");
    }

    static void AssertModelRoot(Animator animator, BattleUnit unit, Vector3 position, Quaternion rotation)
    {
        if (animator.transform == unit.transform) return;
        Check(Vector3.Distance(animator.transform.localPosition, position) < .002f &&
            Quaternion.Angle(animator.transform.localRotation, rotation) < .05f,
            "Animator model root changed height/orientation during in-place walking.");
    }

    static void AssertRestored(BattleUnit unit, Animator animator, string context)
    {
        Check(Mathf.Approximately(animator.speed, OriginalAnimationSpeed) &&
            animator.cullingMode == AnimatorCullingMode.CullCompletely && !animator.applyRootMotion,
            "Animator speed/culling/root motion was not restored after " + context + ".");
    }

    static void AssertIdle(BattleUnit unit, Animator animator) =>
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName(unit.idleStateName), "Player did not return to its combat Idle.");

    static void SettleIdle(Animator animator)
    {
        // No preview camera renders these clones. Temporarily evaluate the returned
        // Idle even when the entry correctly restored CullCompletely on arrival.
        var original = animator.cullingMode;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        try { animator.Update(.3f); }
        finally { animator.cullingMode = original; }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
