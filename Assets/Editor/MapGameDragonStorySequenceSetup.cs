using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time scene wiring for the mapgame story sequence, requested through Library marker.</summary>
public static class MapGameDragonStorySequenceSetup
{
    const string Request = "Library/Adventure/MapGameDragonStorySequence.setup-request";
    const string Result = "Library/Adventure/MapGameDragonStorySequence.setup-result.txt";
    const string ScenePath = "Assets/Scenes/mapgame.unity";
    const string MountainPath = "Assets/Adventure/Prefabs/Enemy/MountainDragon/MountainDragon_PBR 1.prefab";
    const string DragonidePath = "Assets/Adventure/Prefabs/Enemy/Dragonide/Dragonide.prefab";
    const string MountainActionPath = "Assets/Adventure/Prefabs/Enemy/MountainDragon/ActionData/SpitFireBall.asset";
    const string MountainDeathPath = "Assets/Adventure/Prefabs/Enemy/MountainDragon/Anim/DeathHitTheGround.anim";
    const string DragonideDeathPath = "Assets/Adventure/Prefabs/Enemy/Dragonide/Anim/DeathUnarmed.anim";
    const string RingPath = "Assets/asset/model item/ring-of-noobs/source/Ring/Ring/RING.obj";
    const string GatePath = "Assets/asset/model item/european-and-american-game-scencemagic-portal/source/low textures/low.fbx";
    const string GateFallbackPath = "Assets/asset/model item/free-ancient-stone-door-with-carved-dragon/source/portal_COMPLETE.fbx";

    [InitializeOnLoadMethod]
    static void ScheduleRequestedSetup()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += RunRequestedSetup;
    }

    static void RunRequestedSetup()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequestedSetup; return; }
        try { File.Delete(Request); } catch { }
        Setup();
    }

    [MenuItem("Tools/Adventure/Story/Install mapgame dragon sequence")]
    public static void Setup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Result));
        string backupDirectory = "Library/Adventure/MapGameDragonStory/BeforeSetup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string report = "";
        Scene scene = default;
        bool opened = false;
        Scene active = SceneManager.GetActiveScene();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before installing the story sequence.");
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("mapgame scene not found.", ScenePath);
            var mountainAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MountainPath);
            var dragonideAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DragonidePath);
            var mountain = mountainAsset != null ? mountainAsset.GetComponent<BattleUnit>() : null;
            var dragon = dragonideAsset != null ? dragonideAsset.GetComponent<BattleUnit>() : null;
            var action = AssetDatabase.LoadAssetAtPath<ActionData>(MountainActionPath);
            var mountainDeath = AssetDatabase.LoadAssetAtPath<AnimationClip>(MountainDeathPath);
            var dragonDeath = AssetDatabase.LoadAssetAtPath<AnimationClip>(DragonideDeathPath);
            var ring = AssetDatabase.LoadAssetAtPath<GameObject>(RingPath);
            var gate = AssetDatabase.LoadAssetAtPath<GameObject>(GatePath) ?? AssetDatabase.LoadAssetAtPath<GameObject>(GateFallbackPath);
            if (mountain == null || dragon == null || action == null || mountainDeath == null || dragonDeath == null || ring == null || gate == null)
                throw new InvalidOperationException("Required enemy, action, animation, ring, or portal asset could not be loaded.");

            scene = SceneManager.GetSceneByPath(ScenePath);
            bool alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (alreadyOpen)
            {
                if (scene.isDirty)
                {
                    Directory.CreateDirectory(backupDirectory);
                    EditorSceneManager.SaveScene(scene, backupDirectory + "/mapgame.unity", true);
                }
                else
                {
                    Directory.CreateDirectory(backupDirectory);
                    File.Copy(ScenePath, backupDirectory + "/mapgame.unity", true);
                }
            }
            else
            {
                Directory.CreateDirectory(backupDirectory);
                File.Copy(ScenePath, backupDirectory + "/mapgame.unity", true);
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            MapGameSetup mapSetup = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                mapSetup = root.GetComponentInChildren<MapGameSetup>(true);
                if (mapSetup != null) break;
            }
            if (mapSetup == null) throw new InvalidOperationException("MapGameSetup is missing from mapgame scene.");
            const string triggerMarkerName = "Dragon Story Trigger - Quest 12";
            GameObject triggerMarkerObject = null;
            foreach (var root in scene.GetRootGameObjects())
                if (string.Equals(root.name, triggerMarkerName, StringComparison.Ordinal))
                { triggerMarkerObject = root; break; }
            if (triggerMarkerObject == null)
            {
                triggerMarkerObject = new GameObject(triggerMarkerName);
                SceneManager.MoveGameObjectToScene(triggerMarkerObject, scene);
                Undo.RegisterCreatedObjectUndo(triggerMarkerObject, "Create dragon story trigger marker");
            }
            triggerMarkerObject.transform.SetPositionAndRotation(
                new Vector3(-21.197134f, 291.01001f, -23.7513275f), Quaternion.identity);
            GameObject sceneRing = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!string.Equals(candidate.name, "RING", StringComparison.OrdinalIgnoreCase)) continue;
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject);
                    if (source == null || AssetDatabase.GetAssetPath(source) == RingPath)
                    { sceneRing = candidate.gameObject; break; }
                }
                if (sceneRing != null) break;
            }
            var sequence = mapSetup.GetComponent<MapGameDragonStorySequence>();
            if (sequence == null) sequence = Undo.AddComponent<MapGameDragonStorySequence>(mapSetup.gameObject);
            sequence.mapSetup = mapSetup;
            sequence.triggerMarker = triggerMarkerObject.transform;
            sequence.mountainDragon = mountain;
            sequence.dragonide = dragon;
            sequence.mountainSpitFireBall = action;
            sequence.mountainDragonDeath = mountainDeath;
            sequence.dragonideDeath = dragonDeath;
            sequence.ringPrefab = ring;
            sequence.sceneRingObject = sceneRing;
            sequence.gatePrefab = gate;
            sequence.gatePositionOffset = new Vector3(1.18f, 4.62f, -.15f);
            sequence.gateRotationEuler = new Vector3(0, 34.3f, 0);
            sequence.gateScale = Vector3.one * 3f;
            sequence.requiredQuestId = "12";
            sequence.triggerPosition = new Vector3(-21.197134f, 291.01001f, -23.7513275f);
            sequence.encounterPosition = new Vector3(-21.197134f, 291.01001f, -23.7513275f);
            sequence.mountainDragonStartPosition = new Vector3(-21.197134f, 299.100006f, -23.7513275f);
            EditorUtility.SetDirty(sequence);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.SetDirty(triggerMarkerObject.transform);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save mapgame scene.");
            report = "PASS\nMapGameDragonStorySequence is attached to MapGameSetup.\n" +
                     "Quest 12; trigger and MountainDragon positions configured.\n" +
                     "MountainDragon SpitFireBall and both death clips assigned.\n" +
                     (sceneRing != null ? "Existing scene RING will be reused at the Dragonide death location.\n" : "RING prefab will be spawned at the Dragonide death location.\n") +
                     "Scene m portal model assigned.\nBackup: " + backupDirectory;
        }
        catch (Exception error) { report = "FAIL\n" + error; Debug.LogException(error); }
        finally
        {
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            File.WriteAllText(Result, report);
            Debug.Log(report);
        }
    }
}
