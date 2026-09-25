using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Effect9Validation
{
    static Effect9Validation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Library/CombatValidation/Effect9.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Validate(); }
        catch (Exception e) { File.WriteAllText("Library/CombatValidation/Effect9.result.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Adventure/Validate Effect9 beam")]
    static void Validate()
    {
        var action = AssetDatabase.LoadAssetAtPath<ActionData>("Assets/Scripts/combat/ActionData/Effect9_Skill.asset");
        if (action.animationTriggerName != "Attack9ice" || action.hitVfxPrefab == null) throw new Exception("Wrong trigger or missing hit prefab");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/animation/combatanim/combatedit/vfx/Attack9ice.anim");
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime || settings.keepOriginalOrientation) throw new Exception("Clip loop/orientation incorrect");
        if (!AnimationUtility.GetAnimationEvents(clip).Any(e => e.functionName == "AnimEvent_PlayVFX")) throw new Exception("Missing VFX event");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var ownerGo = new GameObject("Effect9 caster"); SceneManager.MoveGameObjectToScene(ownerGo, scene);
            var targetGo = new GameObject("Effect9 target"); SceneManager.MoveGameObjectToScene(targetGo, scene);
            var owner = ownerGo.AddComponent<BattleUnit>(); var target = targetGo.AddComponent<BattleUnit>();
            owner.SetPersistentHP(100); target.SetPersistentHP(100);
            var staging = new GameObject("Inactive VFX staging"); SceneManager.MoveGameObjectToScene(staging, scene); staging.SetActive(false);
            var tick = typeof(CombatBeamVfx).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (float distance in new[] { 3f, 12f, 24f })
            {
                targetGo.transform.position = new Vector3(distance, 0, distance * .5f);
                var effect = UnityEngine.Object.Instantiate(action.vfxPrefab, staging.transform);
                // Isolate the beam simulation from authored audio/raycast side effects in Edit Mode.
                foreach (var script in effect.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                int impacts = 0;
                var beam = effect.AddComponent<CombatBeamVfx>(); beam.Initialize(owner, target, action, () => impacts++);
                foreach (var audio in effect.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
                effect.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(effect, scene);
                effect.SetActive(true);
                var particles = effect.GetComponentsInChildren<ParticleSystem>(true)
                    .Where(p => action.beamParticleNames.Contains(p.name)).ToArray();
                if (particles.Length != 4) throw new Exception("Expected four Trail/TrailDistortion systems");
                foreach (var p in particles) p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                tick.Invoke(beam, null);
                if (impacts != 0) throw new Exception("Impact before beam emission");
                foreach (var p in particles) p.Simulate(.3f, false, true, false);
                tick.Invoke(beam, null); tick.Invoke(beam, null);
                if (impacts != 1) throw new Exception("Expected exactly one impact, got " + impacts);
                targetGo.transform.position += Vector3.right;
                tick.Invoke(beam, null);
                var delta = target.GetVfxTargetPosition(action.projectileTargetOffset) - owner.VfxOrigin.position;
                if (Vector3.Dot(effect.transform.forward, delta.normalized) < .999f) throw new Exception("Beam facing incorrect");
                foreach (var p in particles)
                    if (Mathf.Abs(p.GetComponent<ParticleSystemRenderer>().lengthScale * p.main.startSize.constantMax - delta.magnitude) > .01f)
                        throw new Exception("Beam length incorrect");
                UnityEngine.Object.DestroyImmediate(effect);
            }
            File.WriteAllText("Library/CombatValidation/Effect9.result.txt", "PASS: Attack9ice configuration/events; four beam emitters; one impact after emission at 3/12/24m; moving target orientation and length.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
