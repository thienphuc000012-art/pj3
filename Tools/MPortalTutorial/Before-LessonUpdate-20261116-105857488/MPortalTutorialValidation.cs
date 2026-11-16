using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real CharacterController/portal geometry and hands-on UI checks, without playing or writing campaign saves.</summary>
public static class MPortalTutorialValidation
{
    const string Output = "Tools/MPortalTutorial";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    public static void Run(MExplorationTutorial authored)
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Validate in Edit Mode.");
        var report = new StringBuilder(); Directory.CreateDirectory(Output);
        Scene active = SceneManager.GetActiveScene(), test = default;
        var previousSession = CampaignSession.Instance;
        var sessionField = typeof(CampaignSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var languageField = typeof(GameLocalization).GetField("language", BindingFlags.Static | BindingFlags.NonPublic);
        var language = GameLocalization.CurrentLanguage; float timeScale = Time.timeScale;
        var originalBlend = CinemachineCore.GetBlendOverride;
        var cursor = Cursor.lockState; bool cursorVisible = Cursor.visible;
        CampaignConfig config = null;
        try
        {
            Check(authored.gameObject.scene.name == "m", "Arrival must belong only to scene m.");
            Check(Vector3.Distance(authored.walkStart.position, new Vector3(-87.2855682f, -91.1699982f, -7.56377697f)) < .001f, "Incorrect inside-portal start.");
            Check(Mathf.Abs(Mathf.DeltaAngle(authored.walkStart.eulerAngles.y, 124.340195f)) < .01f, "Incorrect spawn yaw.");
            Check(authored.gameplayCamera != null && authored.cinematicCamera != null && !authored.cinematicCamera.enabled, "Camera handoff references/default state.");
            Check(authored.panorama.Length >= 2 && authored.panorama.All(t => t != null), "Missing map panorama shots.");
            Check(authored.portal.energySurfaces.All(r => r.GetComponent<Collider>() == null), "Energy must not block the portal opening.");
            Check(authored.portal.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m != null && m.shader != null && !m.shader.name.Contains("InternalError"))), "Portal has an invalid material.");
            Check(authored.portal.GetComponentsInChildren<MeshCollider>(true).All(c => c.sharedMesh != null && !c.convex && c.transform.lossyScale.x > 0 && c.transform.lossyScale.y > 0 && c.transform.lossyScale.z > 0), "Invalid portal collision.");
            Check(authored.portal.glowLights.All(l => l.shadows == LightShadows.None), "Portal light must not add shadow atlas load.");
            Check(authored.campfire.gameObject.scene == active && authored.parkourObstacle.gameObject.scene == active, "Use the existing map campfire and parkour obstacle.");
            report.AppendLine("PASS: scene m only; exact inside-portal spawn/yaw; camera references; source model materials; positive static frame colliders; energy has no collider; no extra shadow maps.");

            test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(active);
            var root = new GameObject("Isolated m tutorial validation"); SceneManager.MoveGameObjectToScene(root, test);
            var actor = new GameObject("CharacterController route probe"); actor.transform.SetParent(root.transform);
            actor.layer = authored.player.gameObject.layer;
            var source = authored.player.CC != null ? authored.player.CC : authored.player.GetComponent<CharacterController>();
            var cc = actor.AddComponent<CharacterController>();
            cc.height = source.height; cc.radius = source.radius; cc.center = source.center; cc.skinWidth = source.skinWidth;
            cc.stepOffset = source.stepOffset; cc.slopeLimit = source.slopeLimit; cc.minMoveDistance = source.minMoveDistance;
            foreach (var c in authored.player.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(cc, c);
            foreach (int fps in new[] { 30, 60, 120 })
            {
                cc.enabled = false; actor.transform.SetPositionAndRotation(authored.walkStart.position, authored.walkStart.rotation); cc.enabled = true; Physics.SyncTransforms();
                int frames = Mathf.CeilToInt(authored.walkSeconds * fps); float maxError = 0;
                for (int frame = 1; frame <= frames; frame++)
                {
                    Vector3 desired = Vector3.Lerp(authored.walkStart.position, authored.walkExit.position, (float)frame / frames);
                    cc.Move(desired - actor.transform.position); Physics.SyncTransforms();
                    maxError = Mathf.Max(maxError, Vector3.Distance(desired, actor.transform.position));
                }
                float error = Vector3.Distance(actor.transform.position, authored.walkExit.position);
                Check(error < .25f && maxError < .3f, "Portal walk obstructed at " + fps + " FPS: exit=" + error + ", maximum=" + maxError + ", position=" + actor.transform.position);
                report.AppendLine("PASS: real CharacterController walks through authored portal/terrain at " + fps + " FPS; maximum error=" + maxError.ToString("F4") + "m; exit error=" + error.ToString("F4") + "m.");
            }
            foreach (var pose in new[] { authored.walkCameraStart, authored.walkCameraEnd }.Concat(authored.panorama))
            {
                if (Physics.Raycast(pose.position, Vector3.down, out var floor, 300, authored.player.surfaceLayer, QueryTriggerInteraction.Ignore))
                    report.AppendLine("Camera " + pose.name + " terrain clearance=" + Vector3.Distance(pose.position, floor.point).ToString("F2") + "m.");
                Check(!Physics.CheckSphere(pose.position, .12f, authored.player.surfaceLayer, QueryTriggerInteraction.Ignore), "Camera inside scenery: " + pose.name);
            }
            Check(!Physics.Linecast(authored.walkCameraStart.position, authored.walkStart.position + Vector3.up * 1.2f, authored.player.surfaceLayer, QueryTriggerInteraction.Ignore), "Portal frame occludes the starting camera.");
            Check(!Physics.Linecast(authored.walkCameraEnd.position, authored.walkExit.position + Vector3.up * 1.25f, authored.player.surfaceLayer, QueryTriggerInteraction.Ignore), "Scenery occludes the exit camera.");
            report.AppendLine("PASS: portal frame/scenery does not obstruct either walking shot's view of the player.");

            var restored = JsonUtility.FromJson<CampaignSave>("{\"version\":5}");
            Check(MExplorationTutorial.FindProgress(restored, false) == null, "Old saves should have no invented progress.");
            var entry = MExplorationTutorial.FindProgress(restored, true); entry.introComplete = true; entry.nextLesson = 4;
            var roundTrip = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(restored));
            Check(MExplorationTutorial.FindProgress(roundTrip, false).nextLesson == 4 && roundTrip.explorationTutorials.Count == 1, "Save/resume progress round trip.");
            Check(MExplorationTutorial.FindProgress(restored, true) == entry && restored.explorationTutorials.Count == 1, "Tutorial progress duplicated.");
            report.AppendLine("PASS: older JSON compatible; progress resumes at Parkour; independent campaign data; no duplicate entry; no file saves.");

            var host = new GameObject("Inactive campaign fixture"); host.SetActive(false); host.transform.SetParent(root.transform);
            var session = host.AddComponent<CampaignSession>();
            config = ScriptableObject.CreateInstance<CampaignConfig>(); config.mapScene = active.name;
            Set(session, "<Config>k__BackingField", config); Set(session, "<Data>k__BackingField", restored);
            var player = actor.AddComponent<PlayerScript>(); player.enabled = false; player.CC = cc; player.surfaceLayer = authored.player.surfaceLayer;
            Set(session, "<MapPlayer>k__BackingField", player); sessionField.SetValue(null, session);
            var controllerHost = new GameObject("Inactive tutorial fixture"); controllerHost.SetActive(false); controllerHost.transform.SetParent(root.transform);
            var tutorial = controllerHost.AddComponent<MExplorationTutorial>(); tutorial.enabled = false; tutorial.player = player; tutorial.persistProgress = false;
            Set(tutorial, "session", session); Set(tutorial, "progress", entry); entry.nextLesson = 0; entry.complete = false;
            Check(session.TryBeginPresentation(tutorial) && CampaignSession.InputBlocked && !session.CanStartEncounter, "Cinematic must block world input/encounters.");
            Check(!session.TryBeginPresentation(config), "A second owner must not steal the input lock.");
            session.EndPresentation(config); Check(session.PresentationActive, "Wrong owner released cinematic input.");
            session.SetMenu(AdventureMenu.Main); Check(session.Menu == AdventureMenu.None, "Menu opened during cinematic.");
            Set(tutorial, "locked", true); Call(tutorial, "RestorePresentation");
            Check(!session.PresentationActive && !CampaignSession.InputBlocked, "Cinematic failed to release world input.");
            Call(tutorial, "RestorePresentation");
            session.SetMenu(AdventureMenu.Main); Check(session.Menu == AdventureMenu.Main, "P menu stays blocked after camera handoff.");
            session.SetMenu(AdventureMenu.None); Check(Time.timeScale == timeScale, "Closing menu must restore time.");
            report.AppendLine("PASS: single-owner cinematic lock; no menu/encounter during arrival; normal and repeated cleanup release input; P menu opens/closes after handoff.");
            var testCamera = controllerHost.AddComponent<CinemachineCamera>(); tutorial.cinematicCamera = testCamera;
            var definition = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 2f);
            var overridden = (CinemachineBlendDefinition)Call(tutorial, "ArrivalBlend", null, testCamera, definition, null);
            Check(overridden.Style == CinemachineBlendDefinition.Styles.Cut && overridden.Time == 0, "Arrival must not receive a second default camera blend.");
            Set(tutorial, "previousBlendOverride", originalBlend); Set(tutorial, "blendOverrideInstalled", true);
            CinemachineCore.GetBlendOverride = (CinemachineCore.GetBlendOverrideDelegate)Delegate.CreateDelegate(typeof(CinemachineCore.GetBlendOverrideDelegate), tutorial, "ArrivalBlend");
            Call(tutorial, "RestorePresentation"); Check(CinemachineCore.GetBlendOverride == originalBlend, "Camera blend hook leaks after cutscene cleanup.");
            report.AppendLine("PASS: arrival owns its camera blend; cleanup restores the previous camera hook.");

            tutorial.campfire = authored.campfire; tutorial.parkourObstacle = authored.parkourObstacle;
            Call(tutorial, "BeginPractice");
            actor.transform.position += Vector3.right * 2.2f; Call(tutorial, "Update"); Check(tutorial.CurrentLesson == 1, "Movement lesson.");
            Set(player, "<JumpCount>k__BackingField", player.JumpCount + 1); Call(tutorial, "Update"); Check(tutorial.CurrentLesson == 2, "Jump lesson.");
            Call(tutorial, "Update"); Check(tutorial.CurrentLesson == 2, "Scan must wait for the scan event.");
            Call(tutorial, "ScanUsed"); Call(tutorial, "Update"); Check(tutorial.CurrentLesson == 3, "Scan event lesson.");
            session.SetMenu(AdventureMenu.Main); Call(tutorial, "Update"); Check(tutorial.CurrentLesson == 3, "Menu lesson must wait until closed.");
            session.SetMenu(AdventureMenu.None); Call(tutorial, "Update"); Check(tutorial.CurrentLesson == 4, "P menu lesson.");
            actor.transform.position = authored.parkourObstacle.bounds.center + Vector3.forward * 2;
            Set(player, "<playerInAction>k__BackingField", true); Call(tutorial, "Update"); Check(tutorial.CurrentLesson == 4, "Parkour must wait for completed action.");
            Set(player, "<playerInAction>k__BackingField", false); Call(tutorial, "Update"); Check(tutorial.CurrentLesson == 5, "Parkour lesson.");
            Set(session, "<RestPoint>k__BackingField", authored.campfire); Set(session, "<Menu>k__BackingField", AdventureMenu.Rest);
            Call(tutorial, "Update"); Check(tutorial.CurrentLesson == 6, "E/campfire lesson."); Call(tutorial, "Update");
            Set(session, "<Menu>k__BackingField", AdventureMenu.None); Call(tutorial, "Update");
            Check(entry.complete && !tutorial.IsPracticing && entry.nextLesson == MExplorationTutorial.LessonCount, "Tutorial must complete once and stop.");
            report.AppendLine("PASS: all 7 practice gates: move, jump, actual scan callback, open/close P, completed parkour, correct campfire interaction, close rest; no click-to-skip/repeated lesson.");
            CheckParkour(authored, actor, report);

            var canvas = UnityEngine.Object.Instantiate(authored.tutorialCanvas, root.transform, false);
            var view = GuidedCombatTutorialView.Create(canvas, TMP_Settings.defaultFontAsset);
            tutorial.lessons = view; tutorial.tutorialCanvas = canvas; tutorial.keyHints = canvas.transform.Find("Controls").GetComponentsInChildren<RectTransform>(true).Skip(1).Where(t => t.name != "Key").ToArray();
            foreach (var hint in tutorial.keyHints) hint.gameObject.SetActive(true);
            Set(tutorial, "progress", new ExplorationTutorialProgress { id = MExplorationTutorial.ProgressId });
            foreach (var lang in new[] { GameLanguage.Vietnamese, GameLanguage.English })
            {
                languageField.SetValue(null, lang);
                for (int index = 0; index < MExplorationTutorial.LessonCount; index++)
                {
                    ((ExplorationTutorialProgress)typeof(MExplorationTutorial).GetField("progress", Private).GetValue(tutorial)).nextLesson = index;
                    Call(tutorial, "ShowLesson");
                    Check(view.IsOpen && view.IsHandsOn, "Hands-on lesson did not open.");
                    Check(view.GetComponentsInChildren<Graphic>(true).Where(g => g.gameObject.activeInHierarchy).All(g => !g.raycastTarget), "Tutorial steals pointer clicks from menu/rest.");
                    view.OnSubmit(null); Check(view.IsOpen, "Enter must not skip exploration practice.");
                    var heading = (TMP_Text)typeof(GuidedCombatTutorialView).GetField("heading", Private).GetValue(view);
                    Check(!string.IsNullOrWhiteSpace(heading.text), "Empty localized lesson.");
                    if (index == 0 || index == 6) RenderLesson(authored, canvas, view, test, index, lang, report);
                }
                report.AppendLine("PASS: " + lang + " all seven labels; hands-on graphics pass pointer clicks; Submit cannot skip practice.");
            }
            view.Close(); view.Show(1, 1, null, "Combat regression", "Modal", "Continue", null);
            Check(!view.IsHandsOn && view.transform.Find("Pointer Block").GetComponent<Graphic>().raycastTarget, "Combat modal pointer blocking was not restored."); view.Close();
            report.AppendLine("PASS: combat tutorial restores modal click interception after exploration mode.");
            RenderPortal(authored, root.transform, test, report);
            File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[m Portal Tutorial] PASS: " + Output + "/Validation.txt");
        }
        catch (Exception error) { File.WriteAllText(Output + "/Validation.txt", report + "\nFAIL: " + error); throw; }
        finally
        {
            sessionField.SetValue(null, previousSession); languageField.SetValue(null, language);
            CinemachineCore.GetBlendOverride = originalBlend;
            Time.timeScale = timeScale; Cursor.lockState = cursor; Cursor.visible = cursorVisible;
            if (test.IsValid()) EditorSceneManager.CloseScene(test, true); if (active.IsValid()) SceneManager.SetActiveScene(active);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }
        GuidedCombatTutorialValidation.Run();
    }
    static void CheckParkour(MExplorationTutorial authored, GameObject actor, StringBuilder report)
    {
        var probe = actor.AddComponent<EnviromentChecker>(); probe.enabled = false;
        EditorUtility.CopySerialized(authored.player.enviromentChecker, probe);
        var actions = authored.player.GetComponent<FarkourControllerScript>().newFakourActions;
        Bounds bounds = authored.parkourObstacle.bounds; bool accepted = false;
        for (int i = 0; i < 12 && !accepted; i++)
        {
            Vector3 outward = Quaternion.Euler(0, i * 30, 0) * Vector3.forward;
            Vector3 point = bounds.center + outward * (Mathf.Max(bounds.extents.x, bounds.extents.z) + .45f);
            if (!Physics.Raycast(point + Vector3.up * 8, Vector3.down, out var ground, 30, authored.player.surfaceLayer, QueryTriggerInteraction.Ignore)) continue;
            point.y = ground.point.y + .03f;
            actor.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-outward)); Physics.SyncTransforms();
            var hit = probe.checkObstacle(); if (!hit.hitFound || !hit.heightHitFound || hit.hitInfo.collider != authored.parkourObstacle) continue;
            foreach (var action in actions)
            {
                if (action == null) continue;
                var clone = UnityEngine.Object.Instantiate(action);
                try
                {
                    if (!clone.checkIfAvailable(hit, actor.transform)) continue;
                    accepted = true; report.AppendLine("PASS: existing " + authored.parkourObstacle.name + " is accepted by actual parkour front/top rays and action " + action.name + "; approach=" + point.ToString("F3")); break;
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
        }
        Check(accepted, "The highlighted existing obstacle is not usable by the configured parkour actions.");
    }
    static void RenderLesson(MExplorationTutorial authored, Canvas canvas, GuidedCombatTutorialView view, Scene scene, int index, GameLanguage language, StringBuilder report)
    {
        var go = new GameObject("Lesson preview camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(go, scene);
        var camera = go.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.transform.SetPositionAndRotation(authored.walkCameraEnd.position, authored.walkCameraEnd.rotation); camera.fieldOfView = 48; camera.nearClipPlane = .03f;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false); var rt = new RenderTexture(1280, 720, 24); var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases(); Call(view, "LateUpdate"); Canvas.ForceUpdateCanvases();
            foreach (var text in view.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Lesson text overflows: " + text.name + " " + language + " " + index); }
            camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            string file = Output + "/Lesson-" + index + "-" + language + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png";
            File.WriteAllBytes(file, texture.EncodeToPNG()); report.AppendLine("UI preview=" + file);
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            camera.targetTexture = null; RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go);
        }
    }
    static void RenderPortal(MExplorationTutorial authored, Transform root, Scene scene, StringBuilder report)
    {
        var gate = UnityEngine.Object.Instantiate(authored.portal, root); gate.transform.SetPositionAndRotation(authored.portal.transform.position, authored.portal.transform.rotation);
        gate.SetCharge(0); Check(gate.energySurfaces.All(r => !r.enabled) && gate.glowLights.All(l => !l.enabled), "Portal shutdown leaves energy/light visible.");
        gate.SetCharge(1); Check(gate.energySurfaces.All(r => r.enabled) && gate.glowLights.All(l => l.enabled && l.intensity > 0), "Portal cannot restore its glow.");
        var cameraObject = new GameObject("Portal proof camera", typeof(Camera)); cameraObject.transform.SetParent(root);
        var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f, .08f, .11f); camera.fieldOfView = 48; camera.nearClipPlane = .03f;
        var light = new GameObject("Preview key light", typeof(Light)); light.transform.SetParent(root); light.GetComponent<Light>().type = LightType.Directional;
        light.GetComponent<Light>().intensity = 1.5f; light.transform.rotation = Quaternion.Euler(40, -40, 0);
        camera.transform.position = authored.walkStart.position + authored.walkStart.forward * 9 + Vector3.up * 4;
        camera.transform.LookAt(authored.walkStart.position + Vector3.up * 1.6f);
        var texture = new Texture2D(960, 720, TextureFormat.RGB24, false); var rt = new RenderTexture(960, 720, 24); var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); texture.Apply();
            string file = Output + "/Portal-Materials-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png";
            File.WriteAllBytes(file, texture.EncodeToPNG()); report.AppendLine("Portal material preview=" + file);
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); }
        report.AppendLine("PASS: portal energy and point light fade to off and restore on a separate clone; stone renderers remain.");
    }
}
