using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

[CustomEditor(typeof(EnemyEncounterBehaviour))]
public class EnemyEncounterBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var ai = (EnemyEncounterBehaviour)target;
        EditorGUILayout.HelpBox("Patrol Radius = vùng phát hiện và tuần tra. Leash Radius = giới hạn đuổi. Gán Enemy trong trận trên WorldInteraction, rồi tạo model bên dưới. Ground cần Bake NavMesh; Flying dùng collider trên Obstacle Layers. Attack Range không vượt Khoảng cách tương tác.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Create Map Model + Move Layer")) CreateModel(ai);
            if (GUILayout.Button("Mountain Dragon Ground + Air Preset"))
            {
                Undo.RecordObject(ai, "Flying patrol preset"); ai.travelMode = EnemyEncounterBehaviour.TravelMode.GroundAndFlying;
                ai.idleState = "IdleBreathe"; ai.attackState = "ClawsAttackLeft";
                ai.flyIdleState = "FlyStationary"; ai.flyMoveState = "FlyNormal"; ai.flyAttackState = "FlyStationarySpitFireBall";
                ai.bodyRadius = 1.5f; ai.bodyHeight = 3; ai.flightHeight = 4; EditorUtility.SetDirty(ai);
            }
            if (GUILayout.Button("Bake Patrol NavMesh (Ground)")) Bake(ai);
        }
    }
    static void CreateModel(EnemyEncounterBehaviour ai)
    {
        var point = ai.GetComponent<WorldInteraction>();
        var template = point.enemyPrefabs.FirstOrDefault(p => p != null);
        if (template == null) { Debug.LogError("Assign Enemy trong trận first.", ai); return; }
        Undo.RecordObject(ai, "Configure map enemy");
        if (ai.mapAnimator == null) ai.mapAnimator = ai.GetComponentInChildren<Animator>(true);
        if (ai.mapAnimator == null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(template.gameObject, ai.transform);
            Undo.RegisterCreatedObjectUndo(model, "Create map enemy model");
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            ai.mapAnimator = model.GetComponentInChildren<Animator>(true);
            EnemyEncounterBehaviour.PrepareModel(model);
        }
        if (ai.mapAnimator == null) { Debug.LogError("Enemy prefab has no Animator", ai); return; }
        var runtime = ai.mapAnimator.runtimeAnimatorController;
        if (runtime is AnimatorOverrideController) { Debug.LogWarning("Assign a map AnimatorController to configure Move Layer; override controller preserved.", ai); return; }
        var source = runtime as AnimatorController;
        if (source == null) return;
        const string folder = "Assets/Adventure/Generated/MapControllers";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        string sourcePath = AssetDatabase.GetAssetPath(source);
        string path = sourcePath.StartsWith(folder + "/") ? sourcePath : folder + "/" + source.name + "_" + AssetDatabase.AssetPathToGUID(sourcePath) + ".controller";
        if (sourcePath != path && !File.Exists(path)) AssetDatabase.CopyAsset(sourcePath, path);
        var copy = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (copy == null) throw new Exception("Could not create map controller");
        var motions = copy.layers.SelectMany(l => l.stateMachine.states).Where(x => x.state.motion != null).GroupBy(x => x.state.name).ToDictionary(g => g.Key, g => g.First().state.motion);
        if (!copy.layers.Any(l => l.name == ai.movementLayer)) copy.AddLayer(ai.movementLayer);
        var layer = copy.layers.First(l => l.name == ai.movementLayer);
        foreach (var name in new[] {ai.idleState, ai.walkState, ai.runState, ai.attackState, ai.flyIdleState, ai.flyMoveState, ai.flyAttackState, ai.takeOffState, ai.landingState})
        {
            var state = layer.stateMachine.states.FirstOrDefault(s => s.state.name == name).state;
            if (state == null)
            {
                Motion motion = motions.TryGetValue(name, out var found) ? found : name == ai.flyIdleState ? template.phase2IdleClip : null;
                if (motion == null) continue;
                state = layer.stateMachine.AddState(name); state.motion = motion;
            }
            // The map state machine is driven by the patrol component, not combat triggers.
            foreach (var transition in state.transitions) state.RemoveTransition(transition);
        }
        var layers = copy.layers; int index = Array.FindIndex(layers, l => l.name == ai.movementLayer);
        layers[index].defaultWeight = 1; layers[index].avatarMask = null; copy.layers = layers;
        Undo.RecordObject(ai.mapAnimator, "Assign map animation controller");
        ai.mapAnimator.runtimeAnimatorController = copy; ai.mapAnimator.applyRootMotion = false; ai.mapAnimator.fireEvents = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(ai.mapAnimator);
        EditorUtility.SetDirty(copy); EditorUtility.SetDirty(ai); AssetDatabase.SaveAssetIfDirty(copy);
        EditorSceneManager.MarkSceneDirty(ai.gameObject.scene);
    }
    static void Bake(EnemyEncounterBehaviour ai)
    {
        // A separate stationary object: the NavMesh must not follow the patrolling enemy.
        string name = "Patrol NavMesh " + ai.GetComponent<WorldInteraction>().persistentId;
        var surface = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None).FirstOrDefault(s => s.name == name);
        if (surface == null)
        {
            var root = new GameObject(name); Undo.RegisterCreatedObjectUndo(root, "Create patrol NavMesh");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, ai.gameObject.scene);
            surface = root.AddComponent<NavMeshSurface>();
        }
        Undo.RecordObject(surface, "Bake patrol navigation");
        surface.transform.position = ai.transform.position;
        surface.collectObjects = CollectObjects.Volume; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ai.obstacleLayers; surface.ignoreNavMeshAgent = true;
        float diameter = Mathf.Max(ai.patrolRadius, ai.leashRadius) * 2 + 8;
        surface.size = new Vector3(diameter, 30, diameter); surface.center = Vector3.zero;
        var agent = ai.GetComponent<NavMeshAgent>();
        if (agent == null) agent = Undo.AddComponent<NavMeshAgent>(ai.gameObject);
        // Bake clearance large enough for this enemy, without modifying shared agent settings.
        var settings = NavMesh.GetSettingsByID(agent.agentTypeID);
        if (settings.agentRadius < ai.bodyRadius || settings.agentHeight < ai.bodyHeight)
        { Debug.LogError("Choose an Agent Type with radius/height at least as large as this enemy in AI Navigation before baking.", ai); return; }
        surface.agentTypeID = agent.agentTypeID;
        surface.BuildNavMesh();
        if (surface.navMeshData == null) { Debug.LogError("Bake failed. Check ground colliders and Obstacle Layers.", ai); return; }
        Directory.CreateDirectory("Assets/Adventure/Generated/Navigation"); AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(surface.navMeshData, AssetDatabase.GenerateUniqueAssetPath("Assets/Adventure/Generated/Navigation/Patrol.asset"));
        EditorUtility.SetDirty(surface); EditorUtility.SetDirty(agent); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(ai.gameObject.scene);
    }
    void OnSceneGUI()
    {
        var ai = (EnemyEncounterBehaviour)target;
        Handles.color = Color.cyan;
        EditorGUI.BeginChangeCheck();
        float radius = Handles.RadiusHandle(Quaternion.identity, ai.transform.position, ai.patrolRadius);
        if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(ai, "Change patrol radius"); ai.patrolRadius = Mathf.Max(1, radius); EditorUtility.SetDirty(ai); }
    }
}
