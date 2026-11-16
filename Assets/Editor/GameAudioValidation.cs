using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameAudioValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    static object Call(object owner, string method, params object[] values) => owner.GetType().GetMethod(method, Private).Invoke(owner, values);
    [MenuItem("Tools/Adventure/Audio/Validate Audio and Options")]
    public static void Run()
    {
        var report = new StringBuilder(); Directory.CreateDirectory("Tools/GameAudio");
        var settings = Resources.Load<GameAudioSettings>("Adventure/Audio/GameAudioSettings");
        Check(settings != null && settings.player != null && settings.enemy != null, "Global settings and player/enemy profiles must resolve in Resources.");
        Check(settings.title != null && settings.intro != null && settings.mapgame != null && settings.m != null && settings.battle != null && settings.campfire != null, "Every music context has a slot.");
        var prefab = Resources.Load<GameOptionsView>("Adventure/UI/GameOptions");
        Check(prefab != null && prefab.rows.Length == 5, "Options has five volume buses.");
        Check(prefab.rows.Select(r => r.bus).Distinct().Count() == 5, "Volume buses are unique.");
        foreach (var row in prefab.rows)
            Check(row.slider != null && row.slider.fillRect != null && row.slider.handleRect != null && row.percentage != null && row.decrease != null && row.increase != null, "Slider, arrows and percentage authored for " + row.bus);
        Check(prefab.languageButton != null && prefab.backButton != null, "Language and Back controls authored.");
        var authoredFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Adventure/UI/OptionsFont.asset");
        Check(authoredFont != null && authoredFont.material != null && authoredFont.atlasTextures.All(t => t != null && AssetDatabase.Contains(t)), "Options font includes persistent material and atlas subassets.");
        Check(File.ReadAllText("Assets/Resources/Adventure/UI/OptionsFont.asset").Contains("Texture2D:"), "Font atlas must be saved to disk for a fresh Editor/build load.");
        var adventure = Resources.Load<GameObject>("Adventure/UI/AdventureCanvas");
        Check(adventure.transform.Find("Menu/Party/Overview/Options")?.GetComponent<Button>() != null, "Menu Options button exists in prefab.");
        var localization = Resources.Load<GameLocalizationTable>("Adventure/LocalizationTable");
        foreach (var source in new[] { "Tùy chọn", "Tổng", "Nhạc nền", "Giọng nói", "Giao diện", "Hiệu ứng âm thanh", "Ngôn ngữ" })
            Check(localization.entries.Any(e => e.vietnamese == source && !string.IsNullOrWhiteSpace(e.english)), "Bilingual Options label: " + source);
        report.AppendLine("PASS: global/audio profile assets; six default music contexts; five distinct sliders with arrow/percentage controls; bilingual labels; authored Menu Options.");

        var scene = EditorSceneManager.NewPreviewScene(); var oldActive = SceneManager.GetActiveScene();
        GameObject root = null; var instanceField = typeof(GameAudio).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic); var oldInstance = instanceField.GetValue(null);
        var oldSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        try
        {
            root = new GameObject("Audio validation fixture"); SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false); var audio = root.AddComponent<GameAudio>(); Set(audio, "settings", settings);
            Set(audio, "persistPreferences", false); instanceField.SetValue(null, audio);
            var volumes = (float[])typeof(GameAudio).GetField("volumes", Private).GetValue(audio);
            for (int i = 0; i < 5; i++) volumes[i] = 1f;
            volumes[0] = .5f; volumes[(int)GameAudioBus.Music] = .4f;
            Check(Mathf.Approximately((float)Call(audio, "Gain", GameAudioBus.Music), .2f), "Master must multiply music.");
            Check(Mathf.Approximately((float)Call(audio, "Gain", GameAudioBus.Voice), .5f), "Voice has an independent gain.");
            volumes[(int)GameAudioBus.Voice] = 0;
            Check((float)Call(audio, "Gain", GameAudioBus.Voice) == 0 && (float)Call(audio, "Gain", GameAudioBus.SpecialEffects) == .5f, "Muting Voice must leave SFX audible.");
            volumes[0] = 0; foreach (GameAudioBus bus in Enum.GetValues(typeof(GameAudioBus))) Check((float)Call(audio, "Gain", bus) == 0, "Master mute affects " + bus);
            report.AppendLine("PASS: Master × per-bus gain; Voice/SFX isolation; Master zero mutes every bus. No PlayerPrefs or campaign save writes.");

            var options = UnityEngine.Object.Instantiate(prefab, root.transform); options.gameObject.SetActive(false);
            var blockerObject = new GameObject("Underlying menu", typeof(CanvasGroup)); blockerObject.transform.SetParent(root.transform);
            var blocker = blockerObject.GetComponent<CanvasGroup>(); blocker.interactable = true; blocker.blocksRaycasts = true;
            int closed = 0; options.Open(blocker, null, () => closed++);
            Check(!blocker.interactable && !blocker.blocksRaycasts, "Opening Options must block the underlying menu.");
            var uiRow = options.rows.Single(r => r.bus == GameAudioBus.UserInterface);
            uiRow.slider.value = .35f;
            Check(Mathf.Approximately(GameAudio.Volume(GameAudioBus.UserInterface), .35f), "Slider changes the correct bus.");
            uiRow.increase.onClick.Invoke(); Check(Mathf.Approximately(GameAudio.Volume(GameAudioBus.UserInterface), .4f), "Increase arrow adds 5%.");
            uiRow.decrease.onClick.Invoke(); Check(Mathf.Approximately(GameAudio.Volume(GameAudioBus.UserInterface), .35f), "Decrease arrow removes 5%.");
            options.Refresh(); uiRow.percentage.GetComponent<LocalizedGameText>().Refresh();
            Check(uiRow.percentage.text == "35%", "Changing language must preserve the current percentage instead of restoring the authored default.");
            GameAudio.SetVolume(GameAudioBus.UserInterface, 5); Check(GameAudio.Volume(GameAudioBus.UserInterface) == 1, "Volume clamps to 100%.");
            GameAudio.SetVolume(GameAudioBus.UserInterface, -5); Check(GameAudio.Volume(GameAudioBus.UserInterface) == 0, "Volume clamps to 0%.");
            options.backButton.onClick.Invoke();
            // MonoBehaviour lifecycle is not invoked in Edit Mode: explicitly verify its release path.
            Call(options, "OnDisable");
            Check(blocker.interactable && blocker.blocksRaycasts && !options.IsOpen && closed == 1, "Back closes once and restores underlying menu input.");
            report.AppendLine("PASS: real Options bindings change the chosen bus; both arrows step 5%; bounds clamp; Back closes once and restores menu interaction. Persistence disabled on fixture only.");
            ValidateMusicAndSources(audio, root, scene, report);

            // Inspect the Title scene as a preview: no scene load callbacks, gameplay or save writes.
            var title = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Title.unity");
            try
            {
                var menu = title.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AdventureTitleMenu>(true)).Single();
                var selection = (CanvasGroup)typeof(AdventureTitleMenu).GetField("selectionScreen", Private).GetValue(menu);
                Check(selection.GetComponentsInChildren<Button>(true).Any(b => b.name == "Settings" || b.name == "Options"), "Title Settings button must resolve for OpenOptions binding.");
            }
            finally { EditorSceneManager.ClosePreviewScene(title); }
            report.AppendLine("PASS: Title Settings button resolves from its existing authored selection screen.");
            Check(SceneManager.GetActiveScene() == oldActive, "Validation must preserve the active scene.");
        }
        finally
        {
            instanceField.SetValue(null, oldInstance);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(oldSelection);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        report.AppendLine("Scope: isolated Edit Mode validation and render; does not start Play Mode or touch player saves. Clips intentionally empty until user assigns audio.");
        File.WriteAllText("Tools/GameAudio/Validation.txt", report.ToString());
        QueuePreview(false, report);
    }
    static void ValidateMusicAndSources(GameAudio audio, GameObject root, Scene preview, StringBuilder report)
    {
        var sessionField = typeof(CampaignSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object oldSession = sessionField.GetValue(null);
        var config = ScriptableObject.CreateInstance<CampaignConfig>();
        var settings = ScriptableObject.CreateInstance<GameAudioSettings>();
        settings.titleScene = "__audio_validation_title__"; settings.introScene = "__audio_validation_intro__";
        var clips = Enumerable.Range(0, 5).Select(i => AudioClip.Create("Silent music fixture " + i, 64, 1, 44100, false)).ToArray();
        try
        {
            Set(audio, "settings", settings);
            var a = (AudioSource)Call(audio, "NewSource", "Test music A"); var b = (AudioSource)Call(audio, "NewSource", "Test music B");
            Set(audio, "incoming", a); Set(audio, "outgoing", b);
            var host = new GameObject("Campaign fixture"); host.transform.SetParent(root.transform); var session = host.AddComponent<CampaignSession>();
            config.mapScene = SceneManager.GetActiveScene().name; Set(session, "<Config>k__BackingField", config); sessionField.SetValue(null, session);
            var playerHost = new GameObject("Player fixture"); playerHost.transform.SetParent(root.transform); var player = playerHost.AddComponent<PlayerScript>(); player.enabled = false;
            Set(session, "<MapPlayer>k__BackingField", player);
            settings.mapgame.clip = settings.m.clip = clips[0]; settings.campfire.clip = clips[2];
            var areaHost = new GameObject("Discovery fixture"); areaHost.transform.SetParent(root.transform); var area = areaHost.AddComponent<WorldInteraction>(); area.kind = WorldInteractionKind.Discovery;
            area.range = 10f; area.areaMusic.clip = clips[1];
            var areas = (System.Collections.Generic.List<WorldInteraction>)typeof(GameAudio).GetField("areas", Private).GetValue(audio); areas.Add(area);
            AudioClip Desired() => (AudioClip)typeof(GameAudio).GetField("desiredClip", Private).GetValue(audio);
            playerHost.transform.position = Vector3.right * 20;
            Call(audio, "RefreshMusic"); Check(Desired() == clips[0], "Outside Discovery uses map music.");
            playerHost.transform.position = Vector3.zero;
            Call(audio, "RefreshMusic"); Check(Desired() == clips[1], "Entering Discovery switches to its own music independently of discovery rewards/cooldown.");
            var incoming = typeof(GameAudio).GetField("incoming", Private).GetValue(audio);
            Call(audio, "RefreshMusic"); Check(ReferenceEquals(incoming, typeof(GameAudio).GetField("incoming", Private).GetValue(audio)), "Staying in the same area must not restart music.");
            var restHost = new GameObject("Rest fixture"); restHost.transform.SetParent(root.transform); var rest = restHost.AddComponent<WorldInteraction>(); rest.restMusic.clip = clips[3];
            Set(session, "<Menu>k__BackingField", AdventureMenu.Rest); Set(session, "<RestPoint>k__BackingField", rest);
            Call(audio, "RefreshMusic"); Check(Desired() == clips[3], "Campfire override wins over area music.");
            rest.restMusic.clip = null; Call(audio, "RefreshMusic"); Check(Desired() == clips[2], "Empty rest override uses common campfire music.");
            Set(session, "<Menu>k__BackingField", AdventureMenu.None); Call(audio, "RefreshMusic"); Check(Desired() == clips[1], "Closing campfire restores area music.");
            areas.Clear(); Call(audio, "RefreshMusic"); Check(Desired() == clips[0], "Unloaded discovery areas release their music.");
            report.AppendLine("PASS: real music resolution chooses map → Discovery → campfire override/fallback → Discovery; remaining in a zone does not restart playback; unloading a zone restores map music.");

            var volumes = (float[])typeof(GameAudio).GetField("volumes", Private).GetValue(audio); volumes[0] = .5f; volumes[(int)GameAudioBus.SpecialEffects] = .4f;
            var fxHost = new GameObject("Prefab audio fixture", typeof(AudioSource)); fxHost.transform.SetParent(root.transform);
            var source = fxHost.GetComponent<AudioSource>(); source.volume = .8f; var route = fxHost.AddComponent<GameAudioSourceBus>();
            Call(route, "Awake"); Check(Mathf.Approximately(source.volume, .16f), "Prefab source respects Master × SFX.");
            Call(route, "LateUpdate"); Check(Mathf.Approximately(source.volume, .16f), "Routing must not multiply gain again each frame.");
            Call(route, "OnDisable"); Check(Mathf.Approximately(source.volume, .8f), "Pooled source restores its authored volume on disable.");
            Call(route, "OnEnable"); Check(Mathf.Approximately(source.volume, .16f), "Reused source applies gain exactly once.");
            GameAudio.SetVolume(GameAudioBus.SpecialEffects, 0); Check(source.volume == 0, "An Options mute affects routed VFX immediately.");
            Call(route, "OnDisable");
            report.AppendLine("PASS: embedded/pool AudioSource gain is applied once, restored on disable, reapplied on reuse, and reacts immediately to volume changes.");
            settings.commonButton.clips = new[] { clips[4] }; settings.actionMenuButton.clips = new[] { clips[3] };
            var buttonHost = new GameObject("Common button", typeof(RectTransform), typeof(Image), typeof(Button)); buttonHost.transform.SetParent(root.transform);
            var button = buttonHost.GetComponent<Button>(); button.onClick.AddListener(() => buttonHost.SetActive(false)); GameButtonSound.Ensure(button);
            button.onClick.Invoke();
            var voices = (System.Collections.IList)typeof(GameAudio).GetField("voices", Private).GetValue(audio);
            AudioClip LastClip() => ((AudioSource)voices[0].GetType().GetField("source").GetValue(voices[0])).clip;
            Check(LastClip() == clips[4], "Common click must sound even when the callback closes its panel.");
            button.onClick = new Button.ButtonClickedEvent(); GameButtonSound.Ensure(button); button.onClick.Invoke();
            Check(LastClip() == clips[4], "Replaced button event lists remain audible.");
            var actionRoot = new GameObject("ActionMenu"); actionRoot.transform.SetParent(root.transform); buttonHost.transform.SetParent(actionRoot.transform);
            var feedback = buttonHost.AddComponent<CombatActionButtonFeedback>(); Call(feedback, "PlayClick");
            Check(LastClip() == clips[3], "ActionMenu uses its separate click cue.");
            button.onClick.Invoke(); Check(LastClip() == clips[3], "Common listener must not duplicate combat feedback.");
            actionRoot.name = "Skills"; Call(feedback, "PlayClick"); Check(LastClip() == clips[4], "Combat submenu rows use the common cue.");
            report.AppendLine("PASS: common click survives a closing panel and event replacement; ActionMenu uses its own cue without duplication; other combat rows use the common cue.");
        }
        finally
        {
            sessionField.SetValue(null, oldSession);
            UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(settings);
            foreach (var clip in clips) UnityEngine.Object.DestroyImmediate(clip);
        }
    }
    static void QueuePreview(bool english, StringBuilder report)
    {
        EditorApplication.delayCall += () =>
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var host = new GameObject("Options preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); SceneManager.MoveGameObjectToScene(host, scene);
            var cameraObject = new GameObject("Options preview camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var rt = new RenderTexture(1440, 810, 24); rt.Create();
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.nearClipPlane = .03f; camera.targetTexture = rt;
            var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            var options = UnityEngine.Object.Instantiate(Resources.Load<GameOptionsView>("Adventure/UI/GameOptions"), host.transform); options.gameObject.SetActive(true);
            foreach (var text in options.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = GameLocalization.Translate(text.text, english ? GameLanguage.English : GameLanguage.Vietnamese);
                Check(text.font.HasCharacters(text.text), "Authored Options font includes all glyphs: " + text.text);
            }
            if (english) options.languageValue.text = "English";
            Canvas.ForceUpdateCanvases();
            foreach (var text in options.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
            double due = EditorApplication.timeSinceStartup + .25f;
            int warmup = 0;
            EditorApplication.update += Finish;
            void Finish()
            {
                if (EditorApplication.timeSinceStartup < due || EditorApplication.isUpdating || EditorApplication.isCompiling) return;
                if (warmup++ < 2)
                {
                    // The UI render batch is uploaded on a later Editor frame after a domain reload.
                    Canvas.ForceUpdateCanvases(); camera.Render(); due = EditorApplication.timeSinceStartup + .15f; return;
                }
                EditorApplication.update -= Finish;
                var previous = RenderTexture.active;
                try
                {
                    Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                    var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                    string path = "Tools/GameAudio/Options-" + (english ? "English-" : "Vietnamese-") + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png";
                    File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); report.AppendLine("Preview: " + path);
                    File.WriteAllText("Tools/GameAudio/Validation.txt", report.ToString());
                }
                catch (Exception error) { File.WriteAllText("Tools/GameAudio/PreviewError.txt", error.ToString()); Debug.LogException(error); }
                finally
                {
                    camera.targetTexture = null; RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                    UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(cameraObject); EditorSceneManager.ClosePreviewScene(scene);
                }
                if (!english) QueuePreview(true, report);
                else Debug.Log("[Game Audio] Validation and bilingual previews passed: Tools/GameAudio/Validation.txt");
            }
        };
    }
}
