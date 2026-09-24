using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BossPhaseIdleValidation
{
    static BossPhaseIdleValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Library/CombatValidation/BossPhaseIdle.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Validate(); } catch(Exception e) { File.WriteAllText("Library/CombatValidation/BossPhaseIdle.result.txt",e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Adventure/Validate boss phase idle")]
    static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Adventure/Prefabs/Enemy/MountainDragon/MountainDragon_PBR 1.prefab");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var clone = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(clone,scene);
            var boss = clone.GetComponent<BattleUnit>();
            boss.SetPersistentHP(boss.maxHP);
            var animator = boss.animator;
            var original = animator.runtimeAnimatorController;
            animator.Rebind(); animator.Play("Base Layer.Idle",0,0); animator.Update(.01f);
            if(!animator.GetCurrentAnimatorClipInfo(0).Any(x=>x.clip==boss.baseIdleClip)) throw new Exception("Phase 1 idle incorrect");
            boss.TakeDamage(6,false,true);
            AssertHit(boss,boss.criticalHitClip,"Phase 1 crit");
            boss.ReturnToCombatIdle(); Step(animator,.3f);
            boss.TakeDamage(6,false,false);
            AssertHit(boss,boss.baseHitClip,"Normal hit after crit");
            boss.ReturnToCombatIdle(); Step(animator,.3f);
            boss.SetPersistentHP(boss.maxHP/2);
            if(boss.CheckPhase()!=2) throw new Exception("Boss did not enter phase 2");
            boss.PreparePhase2Idle();
            animator.SetTrigger("Phase2"); animator.Update(.01f);
            for(int i=0;i<130;i++) animator.Update(1f/60);
            if(!animator.GetCurrentAnimatorClipInfo(0).Any(x=>x.clip==boss.phase2IdleClip)) throw new Exception("Intro did not exit directly to flying idle");
            boss.EnterPhase2Idle(); Step(animator,.3f);
            foreach(var trigger in new[]{"SpitFireBall","SpreadFire","ClawsAttackLeft","ClawsAttackRight","clawsAttack2HitCombo"})
            {
                // CombatManager enters JumpForward before triggering melee actions.
                if(trigger.StartsWith("Claws") || trigger.StartsWith("claws"))
                { animator.Play("JumpForward",0,0); Step(animator,.3f); }
                animator.SetTrigger(trigger); Step(animator,.3f);
                if(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !animator.IsInTransition(0)) throw new Exception("Skill did not leave flying idle: "+trigger);
                boss.ReturnToCombatIdle(); Step(animator,.3f);
                if(!animator.GetCurrentAnimatorClipInfo(0).Any(x=>x.clip==boss.phase2IdleClip)) throw new Exception("Skill returned to wrong idle: "+trigger);
            }
            if(prefab.GetComponent<BattleUnit>().animator.runtimeAnimatorController!=original) throw new Exception("Shared controller was changed");
            if(boss.CheckPhase()!=0) throw new Exception("Phase intro can repeat");
            boss.TakeDamage(6,false,true);
            AssertHit(boss,boss.phase2HitClip,"Phase 2 crit fallback stays airborne");
            Step(animator,1.3f);
            if(!animator.GetCurrentAnimatorClipInfo(0).Any(x=>x.clip==boss.phase2IdleClip)) throw new Exception("Phase 2 hit did not return to flying idle");
            var separateCrit = new AnimationClip();
            boss.phase2CriticalHitClip=separateCrit;
            if(boss.ResolveHitReactionClip(true)!=separateCrit || boss.ResolveHitReactionClip(false)!=boss.phase2HitClip) throw new Exception("Phase 2 crit selection failed");
            boss.phase2CriticalHitClip=null; UnityEngine.Object.DestroyImmediate(separateCrit);
            int hp=boss.currentHP;
            boss.TakeDamage(500,true,true);
            if(boss.currentHP!=hp) throw new Exception("Parry took damage");
            boss.AddBuff(ActionData.BuffStat.Shield,100,1);
            boss.TakeDamage(6,false,true);
            if(boss.currentHP!=hp) throw new Exception("Shield took HP damage");
            boss.TakeDamage(10000,false,true); Step(animator,.4f);
            if(!boss.IsDead || !animator.GetCurrentAnimatorStateInfo(0).IsName("death")) throw new Exception("Lethal crit did not prioritize death");
            File.WriteAllText("Library/CombatValidation/BossPhaseIdle.result.txt","PASS: Phase2 intro/idle; five skills; critical/normal hit reset; airborne hit returns to FlyStationary; Phase2 critical fallback/custom; parry/shield; lethal crit death; isolated controller.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void Step(Animator animator,float seconds)
    {
        for(int i=0;i<Mathf.CeilToInt(seconds*60);i++) animator.Update(1f/60);
    }
    static void AssertHit(BattleUnit boss,AnimationClip expected,string context)
    {
        var controller=boss.animator.runtimeAnimatorController as AnimatorOverrideController;
        if(controller==null || controller[boss.baseHitClip]!=expected) throw new Exception("Wrong hit clip: "+context);
    }
}
