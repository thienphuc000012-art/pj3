using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RestPointSetup
{
    const string Folder = "Library/CombatValidation/";
    static RestPointSetup() { EditorApplication.update += Poll; }
    public static void Configure(WorldInteraction point)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/campfire save/Cylinder001.prefab");
        var fire = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/campfire save/vfx_Flames_01.prefab");
        if (model == null || fire == null) throw new Exception("Campfire prefabs missing");
        var existing = point.GetComponent<RestPointPresentation>();
        if (existing != null)
        {
            if (existing.flames != null)
            {
                Undo.RecordObject(existing.flames.transform, "Orient campfire flames");
                existing.flames.transform.localRotation = fire.transform.localRotation;
                existing.flames.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(existing.flames.transform);
            }
            return;
        }
        var presentation = Undo.AddComponent<RestPointPresentation>(point.gameObject);
        var mesh = (GameObject)PrefabUtility.InstantiatePrefab(model, point.transform);
        Undo.RegisterCreatedObjectUndo(mesh, "Add campfire");
        mesh.transform.localPosition = Vector3.zero;
        // Keep the model's authored -90 degree FBX rotation.
        var renderers = mesh.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            mesh.transform.position += new Vector3(point.transform.position.x - bounds.center.x,
                point.transform.position.y - bounds.min.y, point.transform.position.z - bounds.center.z);
        }
        var flames = (GameObject)PrefabUtility.InstantiatePrefab(fire, point.transform);
        Undo.RegisterCreatedObjectUndo(flames, "Add rest flames");
        flames.transform.localPosition = Vector3.up * .15f;
        flames.transform.localRotation = fire.transform.localRotation;
        flames.SetActive(false); presentation.flames = flames;
        var camera = new GameObject("Rest Camera Position");
        Undo.RegisterCreatedObjectUndo(camera, "Add rest camera marker");
        camera.transform.SetParent(point.transform, false); camera.transform.localPosition = new Vector3(2.5f, 1.5f, -3);
        presentation.cameraPose = camera.transform;
        var look = new GameObject("Rest Camera Look At");
        Undo.RegisterCreatedObjectUndo(look, "Add rest look marker");
        look.transform.SetParent(point.transform, false); look.transform.localPosition = new Vector3(0, .7f, -.7f);
        presentation.cameraLookAt = look.transform;
        EditorUtility.SetDirty(presentation);
    }
    [MenuItem("Tools/Adventure/Set Up Campfire Rest Points")]
    static void Setup()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play Mode before campfire setup");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "mapgame") throw new Exception("Open mapgame before campfire setup");
        int count = 0;
        foreach (var point in UnityEngine.Object.FindObjectsByType<WorldInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (point.gameObject.scene == scene && point.kind == WorldInteractionKind.RestPoint) { Configure(point); count++; }
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerScript>();
        if (player == null || player.animator == null) throw new Exception("Map player Animator missing");
        var controller = player.animator.runtimeAnimatorController;
        while (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
        var asset = controller as AnimatorController;
        if (asset == null) throw new Exception("Map player AnimatorController missing");
        var stateMachine = asset.layers[0].stateMachine;
        var sitting = stateMachine.states.FirstOrDefault(s => s.state.name == "Sitting").state;
        if (sitting == null)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/animation/Sitting/Sitting Idle.fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));
            if (clip == null) throw new Exception("Sitting Idle clip missing");
            const string idlePath = "Assets/animation/Sitting/RestSittingIdle.anim";
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath);
            if (idle == null)
            {
                idle = UnityEngine.Object.Instantiate(clip); idle.name = "Rest Sitting Idle";
                var settings = AnimationUtility.GetAnimationClipSettings(idle);
                settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(idle, settings);
                AssetDatabase.CreateAsset(idle, idlePath);
            }
            Directory.CreateDirectory(Folder);
            File.Copy(AssetDatabase.GetAssetPath(asset), Folder + "Player-before-rest.controller", true);
            Undo.RegisterCompleteObjectUndo(asset, "Add Sitting state");
            sitting = stateMachine.AddState("Sitting"); sitting.motion = idle;
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
        }
        if (sitting.motion == null) throw new Exception("Sitting state has no animation");
        if (sitting.motion is AnimationClip sittingClip && !sittingClip.isLooping)
        {
            const string loopPath = "Assets/animation/Sitting/RestSittingIdle.anim";
            var looping = AssetDatabase.LoadAssetAtPath<AnimationClip>(loopPath);
            if (looping == null)
            {
                looping = UnityEngine.Object.Instantiate(sittingClip); looping.name = "Rest Sitting Idle";
                var settings = AnimationUtility.GetAnimationClipSettings(looping); settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(looping, settings); AssetDatabase.CreateAsset(looping, loopPath);
            }
            Undo.RecordObject(sitting, "Loop rest animation"); sitting.motion = looping;
            EditorUtility.SetDirty(sitting); AssetDatabase.SaveAssetIfDirty(asset);
        }
        // Runtime sample rest points use the same setup, including serialized prefab references.
        const string prefabPath = "Assets/Resources/Adventure/CampfireRestPoint.prefab";
        if (!File.Exists(prefabPath))
        {
            var root = new GameObject("Campfire Rest Point");
            try
            {
                var point = root.AddComponent<WorldInteraction>(); point.kind = WorldInteractionKind.RestPoint;
                point.displayName = "Lưu game / Hồi máu"; point.experience = 0;
                Configure(point); PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        else
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try { Configure(root.GetComponent<WorldInteraction>()); PrefabUtility.SaveAsPrefabAsset(root, prefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "RestPoint.result.txt", "PASS: configured " + count + " rest points; campfire/flames references; Sitting state on " + asset.name + "; starter prefab generated. Scene is dirty: save mapgame. Gameplay camera framing still needs visual verification.");
    }
    static void Poll()
    {
        if (!File.Exists(Folder + "RestPoint.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "RestPoint.request");
        try { Setup(); }
        catch (Exception e) { File.WriteAllText(Folder + "RestPoint.result.txt", e.ToString()); }
    }
}
