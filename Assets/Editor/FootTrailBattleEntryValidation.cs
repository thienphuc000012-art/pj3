using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CustomFootTrails;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Tests the actual player prefab and battle walk in isolation; never starts a campaign.</summary>
public static class FootTrailBattleEntryValidation
{
    const string Request = "Library/CombatValidation/FootTrails.request";
    const string Result = "Library/CombatValidation/FootTrails.result.txt";
    const string PlayerPath = "Assets/Adventure/Prefabs/player/Mele/player (3).prefab";
    static string report;

    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += RunRequested; }
    static void RunRequested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        File.Delete(Request); Run();
    }

    [MenuItem("Tools/Adventure/Combat/Validate Foot Trails During Entry Walk")]
    public static void Run()
    {
        report = "";
        Scene preview = default;
        GameObject staging = null;
        BattleEntryWalk walk = null;
        FootTrailController trails = null;
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run this test in Edit Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            preview = EditorSceneManager.NewPreviewScene();
            staging = new GameObject("Isolated Foot Trail Check"); staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, preview);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var clone = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
            var unit = clone.GetComponentInChildren<BattleUnit>(true);
            trails = clone.GetComponentInChildren<FootTrailController>(true);
            Check(unit != null && trails != null, "Player (3) is missing BattleUnit or FootTrailController.");
            foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null) script.enabled = false;
            foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            foreach (var audio in clone.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var particle in clone.GetComponentsInChildren<ParticleSystem>(true))
            { particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var main = particle.main; main.playOnAwake = false; }
            var animator = unit.animator;
            Check(animator != null && animator.runtimeAnimatorController != null, "Player Animator/controller missing.");
            animator.enabled = true; animator.fireEvents = false; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            clone.SetActive(true); unit.gameObject.SetActive(true); staging.SetActive(true);
            unit.SetPersistentHP(Mathf.Max(1, unit.maxHP));
            unit.transform.SetPositionAndRotation(new Vector3(10, 2, 6), Quaternion.Euler(0, 127, 0));
            animator.Rebind(); animator.Update(0);
            var data = new SerializedObject(trails);
            var source = data.FindProperty("leftEffect").objectReferenceValue as FootTrailEffect;
            var rightSource = data.FindProperty("rightEffect").objectReferenceValue as FootTrailEffect;
            Check(source != null && rightSource != null, "Foot effect reference missing.");
            Check(EditorUtility.IsPersistent(source) && source == rightSource,
                "Expected current player configuration: same prefab asset in both foot fields.");
            Vector3 prefabPosition = source.transform.position;
            var prefabDirty = EditorUtility.IsDirty(source.gameObject);
            trails.InitializeEffects(); trails.ResetTracking();
            data.Update();
            var left = trails.LeftRuntimeEffect; var right = trails.RightRuntimeEffect;
            Check(left != null && right != null && left != right && left != source && right != source,
                "Each prefab reference must become its own runtime instance.");
            Check(!EditorUtility.IsPersistent(left) && left.gameObject.scene == preview && left.transform.IsChildOf(clone.transform),
                "Runtime emitters must belong to the player scene and hierarchy.");
            var leftBone = data.FindProperty("leftFootBone").objectReferenceValue as Transform;
            var rightBone = data.FindProperty("rightFootBone").objectReferenceValue as Transform;
            Check(leftBone != null && rightBone != null && leftBone != rightBone, "Foot bones missing or identical.");
            if (animator.isHuman)
                Check(leftBone == animator.GetBoneTransform(HumanBodyBones.LeftFoot) && rightBone == animator.GetBoneTransform(HumanBodyBones.RightFoot),
                    "Auto Find should replace the unanimated IK helpers with the real Humanoid feet.");
            report += "Configured effect: " + source.name + "; bones: " + leftBone.name + " / " + rightBone.name + "\n";
            Transform actualFoot = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : leftBone;
            Vector3 firstFoot = actualFoot.position - unit.transform.position;
            Vector3 firstConfigured = leftBone.position - unit.transform.position;
            float maximumFootDifference = 0, actualMotion = 0, configuredMotion = 0;
            foreach (var particle in left.GetComponentsInChildren<ParticleSystem>().Concat(right.GetComponentsInChildren<ParticleSystem>()))
            {
                var renderer = particle.GetComponent<ParticleSystemRenderer>();
                Check(particle.main.simulationSpace == ParticleSystemSimulationSpace.World, "Particles must remain in world space.");
                Check(renderer != null && renderer.enabled && renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported &&
                    renderer.sharedMaterial.shader.name == "FootTrails/ParticleURP", "Particle renderer/material is not valid for URP.");
            }
            trails.InitializeEffects();
            Check(left == trails.LeftRuntimeEffect && right == trails.RightRuntimeEffect && clone.GetComponentsInChildren<FootTrailEffect>().Length == 2,
                "Repeated initialization must not duplicate effects.");
            for (int i = 0; i < 5; i++) trails.Tick(.05f);
            Check(Count(left) + Count(right) == 0, "Stationary player emitted footsteps.");
            walk = new BattleEntryWalk(unit, 3, 1.8f);
            Check(!walk.IsComplete, "Battle entry walk did not start.");
            trails.Tick(.05f);
            Check(Count(left) + Count(right) == 0, "Entry spawn teleport emitted particles.");
            int leftPeaks = 0, rightPeaks = 0;
            var sample = new ParticleSystem.Particle[200];
            bool leftSaw = false, rightSaw = false;
            for (int frame = 0; frame < 70 && !walk.IsComplete; frame++)
            {
                walk.Tick(.025f); animator.Update(.025f); trails.Tick(.025f);
                maximumFootDifference = Mathf.Max(maximumFootDifference, Vector3.Distance(actualFoot.position, leftBone.position));
                actualMotion = Mathf.Max(actualMotion, Vector3.Distance(actualFoot.position - unit.transform.position, firstFoot));
                configuredMotion = Mathf.Max(configuredMotion, Vector3.Distance(leftBone.position - unit.transform.position, firstConfigured));
                Simulate(left, .025f); Simulate(right, .025f);
                leftPeaks = Math.Max(leftPeaks, Count(left)); rightPeaks = Math.Max(rightPeaks, Count(right));
                leftSaw |= NearFoot(left, leftBone, sample); rightSaw |= NearFoot(right, rightBone, sample);
                Check(Vector3.Distance(left.transform.position, leftBone.position + new Vector3(0, -.05f, 0)) < .001f,
                    "Left emitter did not follow the animated foot.");
                Check(Vector3.Distance(right.transform.position, rightBone.position + new Vector3(0, -.05f, 0)) < .001f,
                    "Right emitter did not follow the animated foot.");
            }
            Check(walk.IsComplete && leftPeaks > 0 && rightPeaks > 0 && leftSaw && rightSaw,
                "Walking must emit visible particles from BOTH feet. Counts: " + leftPeaks + "/" + rightPeaks);
            Check(maximumFootDifference < .001f && configuredMotion > .05f, "Foot emitter bone is not following actual animation.");
            report += "PASS: actual battle-entry walk emitted both feet (peak particles " + leftPeaks + "/" + rightPeaks + "); live bones followed; world-space particles near soles.\n";
            report += "Humanoid foot: " + actualFoot.name + "; max configured/actual separation: " + maximumFootDifference.ToString("F3") +
                "m; configured motion: " + configuredMotion.ToString("F3") + "m; actual motion: " + actualMotion.ToString("F3") + "m.\n";
            Lifecycle(trails, "OnDisable"); // Edit Mode fixture explicitly invokes the runtime callback.
            Check(Count(left) + Count(right) == 0, "Disabling player did not clear owned particles.");
            Lifecycle(trails, "OnEnable");
            Check(left == trails.LeftRuntimeEffect && right == trails.RightRuntimeEffect, "Pooling/re-enable created duplicates.");
            left.StopAndClear(); right.StopAndClear();
            unit.transform.position += Vector3.right * 20; trails.Tick(.025f);
            Check(Count(left) + Count(right) == 0, "Teleport should not create footstep bursts.");
            unit.transform.position += Vector3.right * .6f; trails.Tick(.25f); Simulate(left, .1f);
            Check(Count(left) > 0, "Footsteps did not resume after teleport.");
            left.StopAndClear(); right.StopAndClear();
            trails.OnRightFootstep(); Simulate(right, .1f);
            Check(Count(right) > 0, "Animation-event footsteps failed.");
            Check(source.transform.position == prefabPosition && EditorUtility.IsDirty(source.gameObject) == prefabDirty && Count(source) == 0,
                "Original effect prefab was changed or used for particle simulation.");
            report += "PASS: source prefab untouched; URP shaders; separate emitters; idle and teleport suppressed; manual events; disable and re-enable cleanup.\n";
            // Run existing movement regressions because this uses the same battle-entry path.
            BattleEntryWalkValidation.Run();
            Check(!File.ReadAllText("Library/CombatValidation/BattleEntryWalkValidation.result.txt").Contains("FAIL:"), "Battle entry regressions failed.");
            File.WriteAllText(Result, "PASS\n" + report);
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception error)
        { Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllText(Result, "FAIL\n" + report + error); Debug.LogException(error); }
        finally
        {
            walk?.Finish(false);
            if (trails != null) Lifecycle(trails, "OnDestroy");
            if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static int Count(FootTrailEffect effect) => effect.GetComponentsInChildren<ParticleSystem>().Sum(particle => particle.particleCount);
    static void Lifecycle(FootTrailController controller, string method) => typeof(FootTrailController)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    static void Simulate(FootTrailEffect effect, float dt)
    { foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>()) particle.Simulate(dt, false, false); }
    static bool NearFoot(FootTrailEffect effect, Transform bone, ParticleSystem.Particle[] particles)
    {
        foreach (var system in effect.GetComponentsInChildren<ParticleSystem>())
        {
            int count = system.GetParticles(particles);
            for (int i = 0; i < count; i++)
                if (Vector3.Distance(particles[i].position, bone.position) < .65f && particles[i].GetCurrentColor(system).a > 0) return true;
        }
        return false;
    }
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
