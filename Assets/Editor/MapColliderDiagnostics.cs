using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MapColliderDiagnostics
{
    static MapColliderDiagnostics() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string playRequest = "Library/CombatValidation/ColliderPlay.request";
        if (File.Exists(playRequest) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete(playRequest);
            SessionState.SetBool("ColliderPlay", true);
            SessionState.SetBool("ColliderPlayStarted", !EditorApplication.isPlaying);
            SessionState.SetFloat("ColliderPlayTime", -1);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }
        if (SessionState.GetBool("ColliderPlay", false) && EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            if (SessionState.GetFloat("ColliderPlayTime", -1) < 0) SessionState.SetFloat("ColliderPlayTime", Time.realtimeSinceStartup);
            if (Time.realtimeSinceStartup - SessionState.GetFloat("ColliderPlayTime", -1) > 4)
            {
                SessionState.SetBool("ColliderPlay", false);
                Inspect();
                File.Copy("Library/CombatValidation/ColliderDiagnostics.result.txt", "Library/CombatValidation/ColliderPlay.result.txt", true);
                if (SessionState.GetBool("ColliderPlayStarted", false)) EditorApplication.isPlaying = false;
            }
        }
        const string request = "Library/CombatValidation/ColliderDiagnostics.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(request); Inspect();
    }
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    [MenuItem("Tools/Adventure/Inspect player ground colliders")]
    static void Inspect()
    {
        var text = new StringBuilder();
        text.AppendLine("Playing: " + EditorApplication.isPlaying + "; InputBlocked: " + CampaignSession.InputBlocked + "; StreamingBlocked: " + MapChunkStreamer.MovementBlocked);
        for (int i=0;i<SceneManager.sceneCount;i++) text.AppendLine("Scene: " + SceneManager.GetSceneAt(i).path);
        Physics.SyncTransforms();
        foreach (var player in Object.FindObjectsByType<PlayerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var cc = player.GetComponent<CharacterController>();
            text.AppendLine("Player: " + Path(player.transform) + " position=" + player.transform.position.ToString("F4") + " enabled=" + player.enabled + " active=" + player.gameObject.activeInHierarchy + " ground=" + player.onSurface);
            text.AppendLine("Scale=" + player.transform.lossyScale + "; surfaceLayer=" + player.surfaceLayer.value + "; probe=" + player.surfaceCheckOffset + "; radius=" + player.surfaceCheckRadius);
            if (cc != null) text.AppendLine("CC enabled=" + cc.enabled + " grounded=" + cc.isGrounded + " center=" + cc.center + " height=" + cc.height + " radius=" + cc.radius + " skin=" + cc.skinWidth + " step=" + cc.stepOffset + " minMove=" + cc.minMoveDistance + " feet=" + cc.bounds.min.y);
            foreach (string name in new[]{"playerControl","playerHanging","isMountingLadder","isClimbingLadder","fallingSpeed"})
            {
                var field=typeof(PlayerScript).GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                if(field!=null) text.AppendLine(name+"="+field.GetValue(player));
            }
            foreach(var hit in Physics.RaycastAll(player.transform.position+Vector3.up*3,Vector3.down,30,~0,QueryTriggerInteraction.Ignore).OrderBy(x=>x.distance).Take(18))
            {
                var c=hit.collider;
                text.AppendLine("GROUND " + Path(c.transform)+" type="+c.GetType().Name+" layer="+LayerMask.LayerToName(c.gameObject.layer)+" point="+hit.point.ToString("F4")+" normal="+hit.normal+" bounds="+c.bounds);
                foreach(var r in c.GetComponentsInChildren<Renderer>()) text.AppendLine("  renderer="+r.name+" bounds="+r.bounds);
                if(c is BoxCollider box) text.AppendLine("  box local center="+box.center+" size="+box.size+" scale="+box.transform.lossyScale);
            }
            foreach(var c in Physics.OverlapSphere(player.transform.position+Vector3.up*.7f,1.3f,~0,QueryTriggerInteraction.Ignore).Take(25))
                text.AppendLine("NEAR " + Path(c.transform)+" type="+c.GetType().Name+" bounds="+c.bounds);
            CompareVisualGround(player.transform.position, text);
        }
        var save = System.IO.Path.Combine(Application.persistentDataPath, "adventure-save-v1.json");
        if (File.Exists(save))
        {
            var data = JsonUtility.FromJson<CampaignSave>(File.ReadAllText(save));
            if (data != null && data.hasPosition) CompareVisualGround(data.position, text);
        }
        File.WriteAllText("Library/CombatValidation/ColliderDiagnostics.result.txt",text.ToString());
        Debug.Log("Collider report: Library/CombatValidation/ColliderDiagnostics.result.txt");
    }
    static void CompareVisualGround(Vector3 position, StringBuilder text)
    {
        text.AppendLine("COMPARE at " + position.ToString("F4"));
        var ray = new Ray(position + Vector3.up * 3, Vector3.down);
        foreach (var hit in Physics.RaycastAll(ray, 30, ~0, QueryTriggerInteraction.Ignore).OrderBy(x=>x.distance).Take(12))
            text.AppendLine("PHYSICS " + Path(hit.transform) + " y=" + hit.point.y.ToString("F4"));
        var method = typeof(HandleUtility).GetMethod("IntersectRayMesh", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[]{typeof(Ray),typeof(Mesh),typeof(Matrix4x4),typeof(RaycastHit).MakeByRefType()}, null);
        if(method==null) { text.AppendLine("Visual ray API unavailable"); return; }
        var hits = new System.Collections.Generic.List<string>();
        foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            var r=filter.GetComponent<Renderer>();
            if(r==null || filter.sharedMesh==null || !r.bounds.IntersectRay(ray,out float distance) || distance>30) continue;
            object[] args={ray,filter.sharedMesh,filter.transform.localToWorldMatrix,default(RaycastHit)};
            if((bool)method.Invoke(null,args))
            {
                var hit=(RaycastHit)args[3];
                if(hit.distance<=30) hits.Add("VISUAL " + Path(filter.transform) + " y="+hit.point.y.ToString("F4")+" normal="+hit.normal);
            }
        }
        foreach(var hit in hits.Take(60)) text.AppendLine(hit);
    }
}
