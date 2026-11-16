using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InteractionVfxSetup
{
    const string Request = "Library/CombatValidation/InteractionVfx.request";
    const string Output = "Tools/InteractionVfx";
    const string ConfigPath = "Assets/Resources/Adventure/CampaignConfig.asset";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/InteractionVfxSetup.cs")) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if (command == "setup") Setup(); else Validate(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("Tools/Adventure/VFX/Setup Heal and Pickup Casts")]
    public static void Setup()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before assigning campaign VFX.");
        var config = AssetDatabase.LoadAssetAtPath<CampaignConfig>(ConfigPath);
        var heal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/VFX_Heal_Cast.prefab");
        var pickup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/VFX_Pickup_Cast.prefab");
        Check(config != null && heal != null && pickup != null, "Missing campaign config or requested Assets/prefab VFX.");
        Directory.CreateDirectory(Output); File.Copy(ConfigPath, Output + "/CampaignConfig-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".asset");
        Undo.RecordObject(config, "Assign exploration heal and pickup VFX");
        config.interactionVfx ??= new AdventureInteractionVfxSettings();
        config.interactionVfx.healPrefab = heal; config.interactionVfx.pickupPrefab = pickup;
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        Validate();
    }
    [MenuItem("Tools/Adventure/VFX/Validate Heal and Pickup Casts")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run VFX checks in Edit Mode.");
        Directory.CreateDirectory(Output); var report = new StringBuilder(); Scene scene = default, itemScene = default;
        CampaignConfig config = null; Material referenceMaterial = null;
        var previous = CampaignSession.Instance;
        var instanceField = typeof(CampaignSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        float timeScale = Time.timeScale;
        try
        {
            var authored = AssetDatabase.LoadAssetAtPath<CampaignConfig>(ConfigPath);
            Check(authored?.interactionVfx?.healPrefab != null && authored.interactionVfx.pickupPrefab != null, "Assign both VFX first.");
            Check(AssetDatabase.GetAssetPath(authored.interactionVfx.healPrefab) == "Assets/prefab/VFX_Heal_Cast.prefab" &&
                AssetDatabase.GetAssetPath(authored.interactionVfx.pickupPrefab) == "Assets/prefab/VFX_Pickup_Cast.prefab", "Use the requested Assets/prefab copies.");
            string configBefore = EditorJsonUtility.ToJson(authored);
            scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Isolated interaction VFX checks"); SceneManager.MoveGameObjectToScene(root, scene);
            var host = new GameObject("Inactive campaign fixture"); host.SetActive(false); host.transform.SetParent(root.transform);
            var session = host.AddComponent<CampaignSession>();
            config = ScriptableObject.CreateInstance<CampaignConfig>(); config.mapScene = SceneManager.GetActiveScene().name;
            config.interactionVfx = JsonUtility.FromJson<AdventureInteractionVfxSettings>(JsonUtility.ToJson(authored.interactionVfx));
            // Native object references are assigned explicitly, with no asset mutations.
            config.interactionVfx.healPrefab = authored.interactionVfx.healPrefab; config.interactionVfx.pickupPrefab = authored.interactionVfx.pickupPrefab;
            config.items.Add(new AdventureItem { id = "vfx-test-item", displayName = "Fixture item" });
            var template = host.AddComponent<BattleUnit>(); template.maxHP = 100; config.startingParty.Add(template);
            var data = new CampaignSave(); data.party.Add(new PartyMemberProgress { hp = 100 }); data.party.Add(new PartyMemberProgress { hp = 100 });
            Set(session, "<Config>k__BackingField", config); Set(session, "<Data>k__BackingField", data); instanceField.SetValue(null, session);
            var playerObject = new GameObject("Active map player fixture"); playerObject.transform.SetParent(root.transform);
            var player = playerObject.AddComponent<PlayerScript>(); player.enabled = false; Set(session, "<MapPlayer>k__BackingField", player);
            AdventureInteractionVfx[] Casts() => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AdventureInteractionVfx>(true)).ToArray();
            int Count() => Casts().Length;
            bool Heal() => (bool)Call(session, "RestorePartyHealth");
            Check(!Heal() && Count() == 0, "Full HP must not spawn healing VFX.");
            data.party[1].hp = 20;
            Check(Heal() && data.party[1].hp == 100 && Count() == 1, "A wounded party member must heal and cast once.");
            Check(!Heal() && Count() == 1, "Remaining at the save point must not repeat the cast at full HP.");
            data.party[0].hp = 1; data.party[1].hp = 0;
            Check(Heal() && data.party.All(p => p.hp == 100) && Count() == 2, "Multiple wounded members must produce one cast per party recovery.");
            report.AppendLine("PASS: full party HP gives no VFX; real HP recovery casts once; repeated full-HP calls stay silent; multiple wounded/defeated members receive one shared cast.");
            ClearCasts(player);

            foreach (var kind in new[] { WorldInteractionKind.Pickup, WorldInteractionKind.Chest })
            {
                var go = new GameObject(kind + " reward fixture");
                if (kind == WorldInteractionKind.Pickup) { itemScene = EditorSceneManager.NewPreviewScene(); SceneManager.MoveGameObjectToScene(go, itemScene); }
                else go.transform.SetParent(root.transform);
                go.transform.SetPositionAndRotation(new Vector3(kind == WorldInteractionKind.Pickup ? 3 : -3, 0, 2), Quaternion.Euler(0, 90, 0));
                var point = go.AddComponent<WorldInteraction>(); point.kind = kind; point.savedWorldId = "vfx-test-" + kind;
                if (kind == WorldInteractionKind.Pickup)
                {
                    point.pickupModelInstance = new GameObject("Offset visible item model"); point.pickupModelInstance.transform.SetParent(go.transform, false);
                    point.pickupModelInstance.transform.localPosition = new Vector3(.5f, .4f, .2f); point.pickupModelInstance.transform.localScale = Vector3.one * .1f;
                }
                Transform origin = point.pickupModelInstance != null ? point.pickupModelInstance.transform : point.transform;
                Vector3 expected = origin.position + origin.rotation * config.interactionVfx.pickupOffset;
                point.rewards = new List<ItemStack> { new ItemStack("vfx-test-item", 2) }; point.experience = 5;
                var previousCasts = Casts();
                int before = Count(), inventory = data.Count("vfx-test-item"), xp = data.party[0].xp;
                Check((bool)Call(point, "GrantCollectionRewards", session), "Collection reward did not complete: " + kind);
                Check(point.IsConsumed && Count() == before + 1 && data.Count("vfx-test-item") == inventory + 2 && data.party[0].xp == xp + 5, "Reward/VFX completion mismatch: " + kind);
                var createdCast = Casts().Single(c => !previousCasts.Contains(c));
                Check(createdCast.transform.parent == null && Vector3.Distance(createdCast.transform.position, expected) < .001f,
                    "Pickup cast must spawn independently at the item/model position: " + kind);
                Check(createdCast.gameObject.scene == player.gameObject.scene, "Keep the pickup cast in the map scene, not its streamed item chunk.");
                Check(Vector3.Distance(createdCast.transform.localScale, config.interactionVfx.pickupPrefab.transform.localScale * config.interactionVfx.pickupScale) < .001f,
                    "Pickup cast scale must not inherit a tiny/large item model scale.");
                Check(!(bool)Call(point, "GrantCollectionRewards", session) && Count() == before + 1 && data.Count("vfx-test-item") == inventory + 2, "Repeated event duplicated loot or VFX: " + kind);
                player.transform.position += Vector3.right * 5;
                origin.position += Vector3.up * 5;
                Check(Vector3.Distance(createdCast.transform.position, expected) < .001f, "Pickup cast followed the player or moving source.");
                go.SetActive(false);
                UnityEngine.Object.DestroyImmediate(go);
                if (itemScene.IsValid()) { EditorSceneManager.ClosePreviewScene(itemScene); itemScene = default; }
                Check(createdCast != null && createdCast.gameObject.activeInHierarchy && Vector3.Distance(createdCast.transform.position, expected) < .001f,
                    "Hiding/destroying the item or unloading its chunk also removed the VFX.");
            }
            var discovery = new GameObject("Discovery reward fixture"); discovery.transform.SetParent(root.transform);
            var discoveryPoint = discovery.AddComponent<WorldInteraction>(); discoveryPoint.kind = WorldInteractionKind.Discovery;
            int castCount = Count(); Call(discoveryPoint, "GrantCollectionRewards", session); Check(Count() == castCount, "Discovery incorrectly casts pickup VFX.");
            report.AppendLine("PASS: pickup/chest cast once at the item/model position, independent of model scale; no following the player/source; source hide/destruction and streamed chunk unload do not remove the cast; rewards and duplicate guards preserved; discovery has no pickup cast.");

            Time.timeScale = 0f;
            foreach (var cast in Casts())
            {
                var systems = cast.GetComponentsInChildren<ParticleSystem>(true);
                Check(systems.Length > 0 && systems.All(p => !p.main.loop && p.main.useUnscaledTime), "Effects must be one-shot and keep running while rest UI pauses time.");
                Call(cast, "Tick", config.interactionVfx.maximumLifetime + 1);
            }
            Check(Count() == 0, "Unscaled safety lifetime did not clean up the casts.");
            var finite = AdventureInteractionVfx.Play(config.interactionVfx.healPrefab, player.transform, Vector3.zero, 1, 12);
            foreach (var system in finite.GetComponentsInChildren<ParticleSystem>(true)) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            Call(finite, "Tick", .2f); Check(finite == null, "Finished particles should clean up before the maximum lifetime.");
            report.AppendLine("PASS: all particle systems use unscaled time; paused rest-menu cleanup and early completion cleanup work.");
            Time.timeScale = timeScale;
            config.interactionVfx.healPrefab = null; data.party[0].hp = 40;
            Check(Heal() && data.party[0].hp == 100 && Count() == 0, "Missing VFX must never prevent healing.");
            config.interactionVfx.healPrefab = authored.interactionVfx.healPrefab;
            player.transform.position = Vector3.zero;

            referenceMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); referenceMaterial.color = new Color(.35f, .38f, .4f);
            var figure = GameObject.CreatePrimitive(PrimitiveType.Capsule); SceneManager.MoveGameObjectToScene(figure, scene); figure.transform.SetParent(root.transform);
            figure.transform.position = Vector3.up * .9f; figure.transform.localScale = new Vector3(.35f, .9f, .35f); figure.GetComponent<Renderer>().sharedMaterial = referenceMaterial;
            foreach (bool heal in new[] { true, false })
            {
                var cast = AdventureInteractionVfx.Play(heal ? authored.interactionVfx.healPrefab : authored.interactionVfx.pickupPrefab, player.transform,
                    heal ? authored.interactionVfx.healOffset : authored.interactionVfx.pickupOffset, heal ? authored.interactionVfx.healScale : authored.interactionVfx.pickupScale, 12);
                foreach (var system in cast.GetComponentsInChildren<ParticleSystem>()) if (system.transform.parent.GetComponentInParent<ParticleSystem>() == null) system.Simulate(.35f, true, true, true);
                Check(cast.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount) > 0, "Requested VFX has no particles: " + cast.name);
                Check(cast.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported)), "Invalid VFX material.");
                Render(cast, scene, report, heal ? "Heal" : "Pickup"); UnityEngine.Object.DestroyImmediate(cast.gameObject);
            }
            Check(configBefore == EditorJsonUtility.ToJson(authored), "Validation modified the campaign settings asset.");
            report.AppendLine("PASS: both requested prefabs emit particles with valid materials; missing prefab does not block gameplay. Isolated Edit Mode only; no campaign save or scene writes.");
            File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[Interaction VFX] PASS: " + Output + "/Validation.txt");
        }
        catch (Exception error) { File.WriteAllText(Output + "/Validation.txt", report + "\nFAIL: " + error); throw; }
        finally
        {
            Time.timeScale = timeScale; instanceField.SetValue(null, previous);
            if (itemScene.IsValid()) EditorSceneManager.ClosePreviewScene(itemScene);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (config != null) UnityEngine.Object.DestroyImmediate(config); if (referenceMaterial != null) UnityEngine.Object.DestroyImmediate(referenceMaterial);
        }
        EscapeHealthValidation.Validate(); ChestPlayerIdleValidation.Validate();
    }
    static void ClearCasts(PlayerScript player) { foreach (var cast in player.GetComponentsInChildren<AdventureInteractionVfx>(true)) UnityEngine.Object.DestroyImmediate(cast.gameObject); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Render(AdventureInteractionVfx cast, Scene scene, StringBuilder report, string name)
    {
        var go = new GameObject("VFX preview camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(go, scene);
        var camera = go.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.transform.position = new Vector3(0, 2, -5); camera.transform.LookAt(Vector3.up);
        camera.fieldOfView = 48; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .03f, .045f);
        var rt = new RenderTexture(800, 600, 24); var texture = new Texture2D(800, 600, TextureFormat.RGB24, false); var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 800, 600), 0, 0); texture.Apply();
            string file = Output + "/" + name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png";
            File.WriteAllBytes(file, texture.EncodeToPNG()); report.AppendLine(name + " preview=" + file);
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go); }
    }
}
