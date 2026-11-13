using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Samples the real enemy Animator. No Play Mode, campaign or save APIs are invoked.</summary>
public static class DemonLordAnimationValidation
{
    const string Root = "Assets/Adventure/Prefabs/Enemy/DemonLord/";
    const string Request = "Library/CombatValidation/DemonLordAnimations.request";
    const string Result = "Library/CombatValidation/DemonLordAnimations.result.txt";
    static readonly string[] OneShots = { "2HitCombo", "3HitCombo", "SwordAttack1", "SwordAttack2", "WhipAttack", "GetHit1", "GetHit2", "Roar", "SummonSword&Whip", "Death" };
    static readonly string[] Loops = { "Idle", "Walk", "Fly", "FlyBackwards" };
    [InitializeOnLoadMethod] static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Requested; return; }
        File.Delete(Request); Validate();
    }
    [MenuItem("Tools/Adventure/Combat/Validate DemonLord Single Play Animations")]
    public static void Validate()
    {
        Scene scene = default;
        GameObject staging = null;
        var report = new List<string>();
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in Edit Mode.");
            foreach (string name in OneShots) Check(Clip(name) != null && !Clip(name).isLooping, name + " must not loop.");
            foreach (string name in Loops) Check(Clip(name) != null && Clip(name).isLooping, name + " must keep looping.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "DemonLord.controller");
            foreach (string name in new[] { "SwordAttack1", "SwordAttack2", "WhipAttack" })
            {
                var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == name).state;
                Check(state.transitions.Length == 1 && state.transitions[0].hasExitTime && state.transitions[0].exitTime >= 1,
                    name + " exits before its final damage/end event.");
            }
            scene = EditorSceneManager.NewPreviewScene();
            staging = new GameObject("Isolated DemonLord Check"); staging.SetActive(false); SceneManager.MoveGameObjectToScene(staging, scene);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "DemonLord.prefab");
            var actor = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
            foreach (var script in actor.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var audio in actor.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var particle in actor.GetComponentsInChildren<ParticleSystem>(true))
            { particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var main = particle.main; main.playOnAwake = false; }
            var anim = actor.GetComponent<BattleUnit>().animator;
            Check(anim != null && anim.runtimeAnimatorController == controller, "Encounter prefab uses another Animator controller.");
            anim.enabled = true; anim.applyRootMotion = false; anim.fireEvents = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actor.SetActive(true); staging.SetActive(true); anim.Rebind(); anim.Update(0);
            int movement = anim.GetLayerIndex("Move Layer"); if (movement >= 0) anim.SetLayerWeight(movement, 0);
            Reset(anim); anim.SetTrigger("BattleStart");
            int introEntries = 0, lastState = anim.GetCurrentAnimatorStateInfo(0).shortNameHash;
            for (int i = 0; i < 600; i++)
            {
                anim.Update(1f / 120);
                int current = anim.GetCurrentAnimatorStateInfo(0).shortNameHash;
                if (current != lastState && current == Animator.StringToHash("idlestargame1")) introEntries++;
                lastState = current;
            }
            Check(introEntries == 1 && anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), "BattleStart repeats or fails to return to Idle.");
            report.Add("BattleStart: entered once and returned to Idle.");
            foreach (string action in new[] { "2HitCombo", "3HitCombo", "SwordAttack1", "SwordAttack2", "WhipAttack" })
            {
                Reset(anim); anim.Play("Base Layer.JumpForward", 0, 0); anim.Update(0);
                for (int i = 0; i < 42; i++) anim.Update(1f / 120);
                anim.SetTrigger(action);
                var clip = Clip(action); var events = clip.events; var reached = new HashSet<int>();
                bool returned = false;
                for (int i = 0; i < 1080; i++)
                {
                    anim.Update(1f / 120);
                    var state = anim.GetCurrentAnimatorStateInfo(0);
                    if (!returned && state.IsName("Base Layer." + action))
                    {
                        Check(!state.loop, action + " Animator state still loops.");
                        float time = state.normalizedTime * clip.length;
                        for (int e = 0; e < events.Length; e++) if (time + .00001f >= events[e].time) reached.Add(e);
                        if (reached.Any(e => events[e].functionName == "AnimEvent_EndAttack"))
                        {
                            // CombatManager performs this same Play when the end callback sets its flag.
                            anim.Play("Base Layer.JumpBack", 0, 0); returned = true;
                        }
                    }
                }
                int hits = reached.Count(e => events[e].functionName == "AnimEvent_DealDamage");
                int ends = reached.Count(e => events[e].functionName == "AnimEvent_EndAttack");
                int expected = action == "2HitCombo" ? 2 : action == "3HitCombo" ? 3 : 1;
                Check(hits == expected && ends == 1, action + ": playback reached " + hits + " hit markers and " + ends + " end markers.");
                Check(returned && anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), action + " did not settle to Idle.");
                report.Add(action + ": reached " + hits + " damage markers and one EndAttack before exit; JumpBack returned to Idle.");
            }
            // Without the manager requesting JumpBack, a non-looping combo must hold its last pose, not restart.
            Reset(anim); anim.Play("Base Layer.2HitCombo", 0, 0); anim.Update(0);
            for (int i = 0; i < 360; i++) anim.Update(1f / 120);
            var bones = actor.GetComponentsInChildren<Transform>();
            var rotations = bones.Select(b => b.localRotation).ToArray(); var positions = bones.Select(b => b.localPosition).ToArray();
            for (int i = 0; i < 120; i++) anim.Update(1f / 120);
            Check(bones.Select((b, i) => Quaternion.Angle(b.localRotation, rotations[i])).Max() < .01f &&
                bones.Select((b, i) => Vector3.Distance(b.localPosition, positions[i])).Max() < .0001f, "Combo restarts after its last frame.");
            report.Add("2HitCombo: final pose holds when return is delayed; does not replay.");
            Reset(anim); anim.SetTrigger("Hit");
            for (int i = 0; i < 360; i++) anim.Update(1f / 120);
            Check(anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), "Hit reaction failed to return to Idle.");
            report.Add("Hit reaction returned to Idle. All 10 one-shot clips non-looping; four locomotion/idle clips still looping.");
            report.Add("Event timestamps verified against real Animator playback. Native gameplay callbacks are not dispatched in this Edit Mode test.");
            report.Insert(0, "PASS: actual DemonLord prefab/controller sampled in an isolated Editor preview. No Play Mode, campaign or saves.");
        }
        catch (Exception error) { report.Insert(0, "FAILED: " + error); Debug.LogException(error); }
        finally
        {
            if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllLines(Result, report);
            Directory.CreateDirectory("Tools/DemonLordAnimation"); File.WriteAllLines("Tools/DemonLordAnimation/Validation.txt", report);
        }
    }
    static void Reset(Animator animator)
    {
        foreach (var parameter in animator.parameters.Where(p => p.type == AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(parameter.nameHash);
        animator.Play("Base Layer.Idle", 0, 0); animator.Update(0);
    }
    static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Anim/" + name + ".anim");
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
