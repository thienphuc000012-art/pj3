using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad] public static class LaurenVfxCheck {
 static LaurenVfxCheck(){EditorApplication.update+=Poll;}
 static void Poll(){const string dir="Library/CombatValidation/"; if(!File.Exists(dir+"LaurenVfx.request")||EditorApplication.isCompiling||EditorApplication.isUpdating)return; bool repair=File.ReadAllText(dir+"LaurenVfx.request").Trim()=="repair"; File.Delete(dir+"LaurenVfx.request"); var s=new StringBuilder();try{
 if(repair && EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode before repair");
 if(repair) foreach(var e in UnityEngine.Object.FindObjectsByType<BattleUnit_AnimationEvents>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
 var u=e.GetComponentInParent<BattleUnit>(true);
 if(u==null || u.name!="SK_Lauren Red" || u.gameObject.scene.name!="combattest")continue;
 Undo.RecordObject(e,"Repair Lauren animation event owner"); e.ResolveOwner();
 if(e.ownerUnit!=u)throw new Exception("Lauren receiver owner mismatch");
 PrefabUtility.RecordPrefabInstancePropertyModifications(e); EditorUtility.SetDirty(e);
 UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(e.gameObject.scene);
 s.AppendLine("REPAIRED: Lauren receiver owner; save combattest to persist scene reference.");
 }
 foreach(var u in UnityEngine.Object.FindObjectsByType<BattleUnit>(FindObjectsInactive.Include,FindObjectsSortMode.None)) { s.AppendLine($"UNIT {u.name} player={u.isPlayer} hp={u.currentHP} scene={u.gameObject.scene.name} attack={AssetDatabase.GetAssetPath(u.defaultAttack)}"); if(u.animator==null){s.AppendLine("NO ANIMATOR");continue;} DumpController(u.animator.runtimeAnimatorController,s); s.AppendLine($"ANIM {u.animator.name} controller={AssetDatabase.GetAssetPath(u.animator.runtimeAnimatorController)} fireEvents={u.animator.fireEvents}"); foreach(var e in u.GetComponentsInChildren<BattleUnit_AnimationEvents>(true))s.AppendLine($"RECEIVER {e.name} owner={(e.ownerUnit==null?"NULL":e.ownerUnit.name)} onAnimator={e.gameObject==u.animator.gameObject}"); if(u.animator.runtimeAnimatorController!=null)foreach(var clip in u.animator.runtimeAnimatorController.animationClips){if(!clip.name.ToLower().Contains("attack") && !clip.name.ToLower().Contains("rutkiem") && !clip.name.ToLower().Contains("trakiem"))continue;s.AppendLine("CLIP "+clip.name+" "+AssetDatabase.GetAssetPath(clip));foreach(var ev in AnimationUtility.GetAnimationEvents(clip))s.AppendLine(ev.time+" "+ev.functionName);}}
 foreach(var p in new[]{"Assets/Scripts/combat/ActionData/vfx1.asset","Assets/Scripts/combat/ActionData/vfx1 1.asset"}){var a=AssetDatabase.LoadAssetAtPath<ActionData>(p);s.AppendLine(p+" prefab="+(a.vfxPrefab==null?"NULL":a.vfxPrefab.name));}
 }catch(Exception e){s.AppendLine(e.ToString());}File.WriteAllText(dir+"LaurenVfx.result.txt",s.ToString()); }
 static void DumpController(RuntimeAnimatorController runtime,StringBuilder s){
 while(runtime is AnimatorOverrideController ov)runtime=ov.runtimeAnimatorController;
 var c=runtime as UnityEditor.Animations.AnimatorController;if(c==null)return;
 foreach(var layer in c.layers)DumpMachine(layer.stateMachine,s);
 }
 static void DumpMachine(UnityEditor.Animations.AnimatorStateMachine machine,StringBuilder s){
 foreach(var entry in machine.states){var state=entry.state;s.AppendLine("STATE "+state.name+" clip="+(state.motion==null?"null":state.motion.name));foreach(var t in state.transitions)s.AppendLine(" -> "+(t.destinationState==null?"exit":t.destinationState.name)+" exit="+t.hasExitTime+":"+t.exitTime+" duration="+t.duration+" offset="+t.offset);}
 foreach(var child in machine.stateMachines)DumpMachine(child.stateMachine,s);
 }
}
