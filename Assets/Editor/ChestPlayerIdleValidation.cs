using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>Samples the real player controller against a longer chest animation; never grants loot or writes saves.</summary>
public static class ChestPlayerIdleValidation
{
    const string Request = "Library/CombatValidation/ChestPlayerIdle.request";
    const string Result = "Library/CombatValidation/ChestPlayerIdle.result.txt";
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
    [MenuItem("Tools/Adventure/Validate Chest Player Return To Idle")]
    public static void Validate()
    {
        Scene preview = default;
        CampaignConfig config = null;
        AnimationClip chestClip = null;
        IEnumerator routine = null;
        WorldInteraction chest = null;
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in Edit Mode.");
            preview = EditorSceneManager.NewPreviewScene();
            var staging = new GameObject("Isolated Chest Animation Check"); staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, preview);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Adventure/Prefabs/Exploration/ExplorationPlayer.prefab");
            Check(prefab != null, "Missing exploration player prefab.");
            var clone = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
            foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
            foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var audio in clone.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            var player = clone.GetComponentInChildren<PlayerScript>(true);
            var animator = player != null ? player.animator : null;
            Check(animator != null && animator.runtimeAnimatorController != null, "Missing player Animator.");
            animator.enabled = true; animator.fireEvents = false; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            staging.SetActive(true); clone.SetActive(true);
            animator.Rebind(); animator.Update(0);
            if (animator.parameters.Any(p => p.name == "OnSurface")) animator.SetBool("OnSurface", true);
            if (animator.parameters.Any(p => p.name == "IsFalling")) animator.SetBool("IsFalling", false);
            int idle = Animator.StringToHash("Base Layer.Basic Movement"), opening = Animator.StringToHash("Base Layer.openchest");
            Check(animator.HasState(0, idle) && animator.HasState(0, opening), "Player controller needs Basic Movement/openchest.");
            var holder = new GameObject("Isolated Chest Session"); SceneManager.MoveGameObjectToScene(holder, preview);
            var session = holder.AddComponent<CampaignSession>();
            config = ScriptableObject.CreateInstance<CampaignConfig>(); config.mapScene = SceneManager.GetActiveScene().name;
            Set(session, "Config", config); Set(session, "MapPlayer", player);
            var chestObject = new GameObject("Long Opening Chest"); SceneManager.MoveGameObjectToScene(chestObject, preview);
            chest = chestObject.AddComponent<WorldInteraction>(); chest.kind = WorldInteractionKind.Chest;
            chestObject.AddComponent<Animator>();
            new GameObject("Top_Lod").transform.SetParent(chestObject.transform, false);
            chestClip = new AnimationClip { name = "Isolated 10 Second Chest" };
            chestClip.SetCurve("Top_Lod", typeof(Transform), "localEulerAnglesRaw.x", AnimationCurve.Linear(0, 0, 10, -90));
            chest.chestOpenAnimation = chestClip; chest.chestOpenDuration = 10;
            chest.pickupAnimationTimeout = 10; chest.chestOpenMoment = .5f;
            var play = typeof(CampaignSession).GetMethod("PlayItemPickup", Private);
            var graphField = typeof(WorldInteraction).GetField("chestAnimationGraph", Private);
            foreach (bool animationEvent in new[] { false, true })
            {
                animator.Play(idle, 0, 0); animator.Update(0); animator.SetFloat("movementvalue", 1);
                chest.chestUseAnimationEvent = animationEvent;
                Set(session, "Busy", true); Set(session, "KeepHudVisibleDuringItemInteraction", true);
                routine = (IEnumerator)play.Invoke(session, new object[] { chest });
                Check(routine.MoveNext(), "Player interaction did not begin.");
                animator.Play(opening, 0, .6f); animator.Update(0);
                Check(routine.MoveNext(), "Interaction ended before the chest started.");
                var receiver = animator.GetComponent<PickupAnimationEvents>();
                Check(receiver != null, "Missing animation event receiver.");
                if (animationEvent) receiver.AnimEvent_OpenChest();
                Check(((PlayableGraph)graphField.GetValue(chest)).IsValid(), "Chest opening did not start at its moment/event.");
                animator.Play(opening, 0, 1.01f); animator.Update(0);
                Check(routine.MoveNext(), "Long chest should still be opening.");
                animator.Update(.15f);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                Check(state.fullPathHash == idle || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == idle,
                    "Player stayed in openchest while waiting for the lid.");
                Check(Mathf.Abs(animator.GetFloat("movementvalue")) < .001f && !animator.applyRootMotion,
                    "Player did not return to stationary locomotion with original root-motion settings.");
                Check(receiver.OnChestOpenMoment == null && receiver.OnPickupMoment == null, "Completed player animation still accepted interaction events.");
                Check(session.Busy && session.KeepHudVisibleDuringItemInteraction && !chest.IsConsumed,
                    "Chest wait must retain the existing interaction/HUD/collection rules.");
                var graph = (PlayableGraph)graphField.GetValue(chest);
                graph.Evaluate(0f); // Apply the initial clip seek before advancing its clock.
                double before = graph.GetRootPlayable(0).GetTime();
                graph.Evaluate(.25f);
                Check(graph.GetRootPlayable(0).GetTime() > before + .2,
                    "Chest playable did not advance after player idle: before=" + before + ", after=" + graph.GetRootPlayable(0).GetTime());
                ((IDisposable)routine).Dispose(); routine = null;
                Check(!session.Busy && !session.KeepHudVisibleDuringItemInteraction, "Cancellation left interaction locked.");
                typeof(WorldInteraction).GetMethod("StopChestAnimation", Private).Invoke(chest, null);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            File.WriteAllText(Result, "PASS\nReal exploration player returns from openchest to stationary Basic Movement before a 10-second chest finishes.\nBoth normalized-moment and Animation Event opening paths verified.\nChest playable keeps advancing, callbacks detach, existing collection/HUD rules and cancellation cleanup remain intact.\nIsolated Editor animation sampling only; no Play Mode, rewards, saves or scene-file writes.");
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllText(Result, "FAIL\n" + error); Debug.LogException(error); }
        finally
        {
            if (routine is IDisposable disposable) disposable.Dispose();
            if (chest != null) typeof(WorldInteraction).GetMethod("StopChestAnimation", Private).Invoke(chest, null);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (chestClip != null) UnityEngine.Object.DestroyImmediate(chestClip);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }
    }
    static void Set(CampaignSession session, string property, object value) =>
        typeof(CampaignSession).GetField("<" + property + ">k__BackingField", Private).SetValue(session, value);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
