using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayerComboBuilder
{
    const string Folder = "Library/CombatValidation/";
    const string ControllerPath = "Assets/animation/animcontroller/p2.controller";
    static PlayerComboBuilder() { EditorApplication.update += Poll; }
    static void Poll()
    {
        string request = Folder + "PlayerCombo.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Build(); }
        catch (Exception e) { File.WriteAllText(Folder + "PlayerCombo.result.txt", e.ToString()); Debug.LogException(e); }
    }
    static void Build()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        var first = states.FirstOrDefault(s => s.name == "attack 0") ?? states.Single(s => s.name == "attack");
        var second = states.FirstOrDefault(s => s.name == "slashattack 0") ?? states.Single(s => s.name == "slashattack");
        var firstClip = first.motion as AnimationClip;
        var secondClip = second.motion as AnimationClip;
        if (firstClip == null || secondClip == null) throw new Exception("Combo states must contain AnimationClips");
        var sourceAnimator = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(a => a.gameObject.scene.name == "combattest" && a.runtimeAnimatorController == controller && a.avatar != null);
        if (sourceAnimator == null) throw new Exception("Open combattest with the p2 player so the combo can be validated on its avatar.");
        var firstEvents = AnimationUtility.GetAnimationEvents(firstClip).Where(e => e.functionName != "AnimEvent_EndAttack").ToArray();
        var secondEvents = AnimationUtility.GetAnimationEvents(secondClip);
        if (!firstEvents.Any(e => e.functionName == "AnimEvent_DealDamage") || !secondEvents.Any(e => e.functionName == "AnimEvent_DealDamage") || secondEvents.Count(e => e.functionName == "AnimEvent_EndAttack") != 1)
            throw new Exception("Expected damage on both clips and one final EndAttack event");
        if (!File.Exists(Folder + "p2-before-combo.controller")) File.Copy(ControllerPath, Folder + "p2-before-combo.controller");
        const string comboPath = "Assets/animation/combatanim/combatedit/attack_ComboStart.anim";
        var combo = AssetDatabase.LoadAssetAtPath<AnimationClip>(comboPath);
        if (combo == null) { combo = UnityEngine.Object.Instantiate(firstClip); AssetDatabase.CreateAsset(combo, comboPath); }
        combo.name = "attack_ComboStart";
        AnimationUtility.SetAnimationEvents(combo, firstEvents);
        var settings = AnimationUtility.GetAnimationClipSettings(combo);
        settings.loopTime = false; AnimationUtility.SetAnimationClipSettings(combo, settings);
        Undo.RecordObject(first, "Chain player attack combo");
        first.motion = combo;
        foreach (var old in first.transitions) first.RemoveTransition(old);
        var transition = first.AddTransition(second);
        transition.hasExitTime = true;
        // Preserve all damage/trail events, but skip the first clip's long recovery.
        float lastEvent = firstEvents.Length == 0 ? combo.length : firstEvents.Max(e => e.time);
        transition.exitTime = Mathf.Clamp01((lastEvent + .08f) / combo.length);
        transition.hasFixedDuration = true; transition.duration = .1f;
        transition.offset = 0; transition.canTransitionToSelf = false;
        transition.interruptionSource = TransitionInterruptionSource.None;
        if (!controller.parameters.Any(p => p.name == "AttackCombo")) controller.AddParameter("AttackCombo", AnimatorControllerParameterType.Trigger);
        foreach (var entry in states.Where(s => s.name == "Idle" || s.name == "JumpForward"))
        {
            if (entry.transitions.Any(t => t.destinationState == first && t.conditions.Any(c => c.parameter == "AttackCombo"))) continue;
            var incoming = entry.AddTransition(first);
            incoming.hasExitTime = false; incoming.hasFixedDuration = true; incoming.duration = .1f;
            incoming.AddCondition(AnimatorConditionMode.If, 0, "AttackCombo");
        }
        const string actionPath = "Assets/Scripts/combat/ActionData/AttackSlashCombo.asset";
        if (AssetDatabase.LoadAssetAtPath<ActionData>(actionPath) == null)
        {
            var action = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ActionData>("Assets/Scripts/combat/ActionData/skill1.asset"));
            action.name = "AttackSlashCombo"; action.actionName = "Attack Slash Combo";
            action.description = "Two-hit sword combo: attack followed by slashattack. Power applies to each hit.";
            action.animationTriggerName = "AttackCombo"; action.isMelee = true;
            AssetDatabase.CreateAsset(action, actionPath);
        }
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(combo); AssetDatabase.SaveAssets();

        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var clone = UnityEngine.Object.Instantiate(sourceAnimator.gameObject);
            SceneManager.MoveGameObjectToScene(clone, preview);
            foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
            var animator = clone.GetComponent<Animator>();
            animator.enabled = true; clone.SetActive(true); animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Edit-mode Animator.Update evaluates the graph but does not dispatch
            // gameplay AnimationEvents. Validate event timing against visited states.
            animator.fireEvents = false;
            foreach (string entry in new[] { "Idle", "JumpForward" })
            {
                animator.Rebind(); animator.Play("Base Layer." + entry, 0, 0); animator.Update(.01f);
                animator.SetTrigger("AttackCombo");
                bool sawFirst = false, sawSecond = false, reachedEnd = false;
                float firstMax = 0;
                for (int i = 0; i < 360; i++)
                {
                    animator.Update(1f / 60);
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    if (state.IsName(first.name)) { sawFirst = true; firstMax = Mathf.Max(firstMax, state.normalizedTime * combo.length); }
                    if (state.IsName(second.name))
                    {
                        sawSecond = true;
                        reachedEnd = state.normalizedTime * secondClip.length >= secondEvents.Single(e => e.functionName == "AnimEvent_EndAttack").time;
                    }
                    if (reachedEnd) break;
                }
                if (!sawFirst || !sawSecond || !reachedEnd || firstMax < lastEvent || firstEvents.Count(e => e.functionName == "AnimEvent_DealDamage") != 1 || secondEvents.Count(e => e.functionName == "AnimEvent_DealDamage") != 1)
                    throw new Exception("Combo graph/event timing failed from " + entry);
            }
            File.WriteAllText(Folder + "PlayerCombo.result.txt", "PASS: AttackCombo from Idle and JumpForward -> " + first.name + " -> " + second.name + "; both damage event times reached; exactly one authored EndAttack at combo end. Edit-mode graph validation, not gameplay damage test.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
