using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SpreadFireValidation
{
    static SpreadFireValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Library/CombatValidation/SpreadFire.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Validate(); } catch(Exception e) { File.WriteAllText("Library/CombatValidation/SpreadFire.result.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Adventure/Validate SpreadFire beam")]
    static void Validate()
    {
        var action = AssetDatabase.LoadAssetAtPath<ActionData>("Assets/Adventure/Prefabs/Enemy/MountainDragon/ActionData/SpreadFire.asset");
        if(action == null || !action.beamParticleStream || action.animationTriggerName != "SpreadFire") throw new Exception("SpreadFire configuration invalid");
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var ownerGo = new GameObject("Test dragon"); SceneManager.MoveGameObjectToScene(ownerGo, preview);
            var targetGo = new GameObject("Test player"); SceneManager.MoveGameObjectToScene(targetGo, preview);
            var owner = ownerGo.AddComponent<BattleUnit>(); var target = targetGo.AddComponent<BattleUnit>();
            owner.SetPersistentHP(100); target.SetPersistentHP(100);
            var tick = typeof(CombatBeamVfx).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach(float distance in new[]{3f,12f,24f})
            {
                targetGo.transform.position = new Vector3(distance, 0, distance * .5f);
                var effect = UnityEngine.Object.Instantiate(action.vfxPrefab);
                SceneManager.MoveGameObjectToScene(effect, preview);
                effect.SetActive(false);
                int impacts = 0;
                var beam = effect.AddComponent<CombatBeamVfx>(); beam.Initialize(owner,target,action,()=>impacts++);
                effect.SetActive(true);
                var stream = effect.GetComponentsInChildren<ParticleSystem>().Single(x=>x.name==action.beamStreamParticleName);
                if(stream.shape.shapeType != ParticleSystemShapeType.Cone) throw new Exception("Unity must recompile the updated beam code before validation.");
                stream.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                stream.Play();
                // Follow a moving destination as well as a static one.
                for(int i=0;i<80;i++)
                {
                    if(i==12) targetGo.transform.position += Vector3.forward;
                    tick.Invoke(beam,null);
                    stream.Simulate(1f/60,false,false,false);
                    if(i<12 && impacts!=0) throw new Exception("Impact happened before flame arrival at frame " + i + ", distance " + distance);
                }
                if(impacts!=1) throw new Exception("Expected one impact at distance " + distance + ", got " + impacts);
                var direction = (target.GetVfxTargetPosition(action.projectileTargetOffset)-owner.VfxOrigin.position).normalized;
                if(Vector3.Dot(effect.transform.forward,direction)<.999f) throw new Exception("Incorrect beam orientation");
                UnityEngine.Object.DestroyImmediate(effect);
            }
            File.WriteAllText("Library/CombatValidation/SpreadFire.result.txt","PASS: flame reaches target at 3/12/24m, follows moving endpoint, one impact only, no early impact.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
