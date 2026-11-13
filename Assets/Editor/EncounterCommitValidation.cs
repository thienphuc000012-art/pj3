using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Stops the actual transition at its VFX wait: never loads combat, runs Play Mode or writes saves.</summary>
public static class EncounterCommitValidation
{
    const string Request = "Library/CombatValidation/EncounterCommit.request";
    const string Result = "Library/CombatValidation/EncounterCommit.result.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Requested; return; }
        File.Delete(Request); Validate();
    }
    [MenuItem("Tools/Adventure/Combat/Validate Guaranteed Encounter Attack")]
    public static void Validate()
    {
        Scene preview = default;
        CampaignConfig config = null;
        IEnumerator routine = null;
        float timeScale = Time.timeScale;
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in Edit Mode.");
            preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Isolated Encounter Commit Test"); root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, preview);
            var session = root.AddComponent<CampaignSession>();
            config = ScriptableObject.CreateInstance<CampaignConfig>();
            config.mapScene = SceneManager.GetActiveScene().name;
            config.encounterVfxDuration = 10f; // This wait prevents the test from ever reaching LoadScene.
            Set(session, "Config", config);
            var player = new GameObject("Player Without Contact Collider"); player.transform.SetParent(root.transform);
            var playerScript = player.AddComponent<PlayerScript>();
            Set(session, "MapPlayer", playerScript);
            var enemy = new GameObject("Encounter Enemy"); enemy.transform.SetParent(root.transform);
            enemy.transform.localPosition = Vector3.forward * 2;
            var patrol = enemy.AddComponent<EnemyEncounterBehaviour>();
            var point = enemy.GetComponent<WorldInteraction>(); point.kind = WorldInteractionKind.Encounter;
            var claw = new GameObject("Claw Far From Player"); claw.transform.SetParent(enemy.transform);
            claw.transform.localPosition = Vector3.right * 1000;
            patrol.attackContactPoint = claw.transform;
            patrol.attackContactRadius = .01f;
            patrol.attackContactWindow = .001f;
            patrol.attackWindup = 0f;

            var method = typeof(CampaignSession).GetMethod("PlayEncounterTransition", Private);
            Check(method != null, "Missing encounter transition.");
            Quaternion playerRotation = player.transform.rotation, enemyRotation = enemy.transform.rotation;
            Set(session, "Busy", true);
            routine = (IEnumerator)method.Invoke(session, new object[] { point, false });
            Check(routine.MoveNext() && routine.Current is WaitForSecondsRealtime,
                "Enemy attack missed/aborted when the player had no CharacterController or the claw was far away.");
            Check(Time.timeScale == 0f && session.Busy, "Committed encounter did not reach the VFX/freeze phase.");
            ((IDisposable)routine).Dispose(); routine = null;
            Check(!session.Busy && Time.timeScale == timeScale && Quaternion.Angle(player.transform.rotation, playerRotation) < .01f &&
                Quaternion.Angle(enemy.transform.rotation, enemyRotation) < .01f, "Cancellation did not restore control, rotations or time scale.");

            var controller = player.AddComponent<CharacterController>(); controller.enabled = false;
            patrol.attackWindup = .5f;
            Set(session, "Busy", true);
            routine = (IEnumerator)method.Invoke(session, new object[] { point, false });
            Check(routine.MoveNext() && routine.Current == null && Time.timeScale == timeScale,
                "Hit ignored the configured windup.");
            var fields = routine.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var hitTime = fields.Single(f => f.Name.StartsWith("<hitStartsAt>"));
            hitTime.SetValue(routine, Time.unscaledTime - .01f); // Advance only the isolated iterator's clock checkpoint.
            Check(routine.MoveNext() && routine.Current is WaitForSecondsRealtime && Time.timeScale == 0f,
                "Enemy hit still required a collider after windup, or a closed old contact window cancelled it.");
            Check((bool)fields.Single(f => f.Name.StartsWith("<hitStarted>")).GetValue(routine), "Hit was not latched once.");
            ((IDisposable)routine).Dispose(); routine = null;

            Set(session, "Busy", true);
            routine = (IEnumerator)method.Invoke(session, new object[] { point, true });
            Check(routine.MoveNext() && routine.Current is WaitForSecondsRealtime, "Player-initiated encounter regressed.");
            ((IDisposable)routine).Dispose(); routine = null;
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            File.WriteAllText(Result, "PASS\nActual encounter coroutine reaches VFX/freeze without a player collider, with a disabled CharacterController, and with an out-of-range claw/expired old contact window.\nAttack Windup delays Hit; Hit is latched once.\nPlayer-initiated encounter and cancellation cleanup preserved.\nStopped before scene loading; no Play Mode, campaign save or scene-file writes.");
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllText(Result, "FAIL\n" + error); Debug.LogException(error); }
        finally
        {
            if (routine is IDisposable disposable) disposable.Dispose();
            Time.timeScale = timeScale;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }
    }
    static void Set(CampaignSession target, string property, object value) =>
        typeof(CampaignSession).GetField("<" + property + ">k__BackingField", Private).SetValue(target, value);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
