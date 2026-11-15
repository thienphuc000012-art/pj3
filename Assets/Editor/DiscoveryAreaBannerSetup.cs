using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DiscoveryAreaBannerSetup
{
    const string Prefab = "Assets/Resources/Adventure/UI/AdventureCanvas.prefab";
    const string Divider = "Assets/asset/Ui/White Ornamental Divider Collection.png";
    const string Request = "Library/CombatValidation/DiscoveryAreaBanner.request";
    const string Output = "Tools/DiscoveryArea";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/DiscoveryAreaBannerSetup.cs")) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if (command == "setup") Setup(); else Validate(); }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", ex.ToString());
            Debug.LogException(ex);
        }
    }

    [MenuItem("Tools/Adventure/UI/Setup Discovery Area Banner")]
    public static void Setup()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before editing the Canvas prefab.");
        Directory.CreateDirectory(Output);
        File.Copy(Prefab, Output + "/Before-AdventureCanvas-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".prefab");
        var source = AssetDatabase.LoadAllAssetsAtPath(Divider).OfType<Sprite>().FirstOrDefault();
        Check(source != null, "Import the selected white ornamental divider as a Sprite first.");
        var obj = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var root = obj.GetComponent<AdventureCanvasRoot>();
            var banner = obj.GetComponent<DiscoveryAreaBanner>();
            if (banner == null) banner = obj.AddComponent<DiscoveryAreaBanner>();
            var panel = obj.transform.Find("HUD/Discovery Area") as RectTransform;
            if (panel == null) panel = Rect("Discovery Area", obj.transform.Find("HUD"));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f); panel.pivot = new Vector2(.5f, 1f);
            panel.anchoredPosition = new Vector2(0, -145); panel.sizeDelta = new Vector2(1000, 190);
            if (panel.GetComponent<PreserveAuthoredUI>() == null) panel.gameObject.AddComponent<PreserveAuthoredUI>();
            banner.panel = panel.GetComponent<CanvasGroup>();
            if (banner.panel == null) banner.panel = panel.gameObject.AddComponent<CanvasGroup>();
            banner.panel.blocksRaycasts = banner.panel.interactable = false; banner.panel.alpha = 0;
            banner.topReveal = Reveal(panel, "Top Reveal", 66f, source, true, out var top);
            banner.topDivider = top;
            var oldBottom = panel.Find("Bottom Reveal");
            if (oldBottom != null) UnityEngine.Object.DestroyImmediate(oldBottom.gameObject);
            var title = panel.Find("Area Name")?.GetComponent<TMP_Text>();
            if (title == null) title = Rect("Area Name", panel).gameObject.AddComponent<TextMeshProUGUI>();
            Center(title.rectTransform, new Vector2(0, 1), new Vector2(910, 84));
            title.text = "TÊN KHU VỰC"; title.font = root.defaultFont;
            title.fontSize = 52; title.enableAutoSizing = true; title.fontSizeMin = 28; title.fontSizeMax = 52;
            title.fontStyle = FontStyles.Normal; title.fontWeight = FontWeight.Light;
            title.characterSpacing = 4f; title.alignment = TextAlignmentOptions.Center;
            title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Ellipsis;
            title.color = new Color(.98f, .98f, .96f, 1f); title.raycastTarget = false;
            banner.areaName = title; banner.nameGroup = title.GetComponent<CanvasGroup>();
            if (banner.nameGroup == null) banner.nameGroup = title.gameObject.AddComponent<CanvasGroup>();
            banner.nameGroup.blocksRaycasts = banner.nameGroup.interactable = false;
            panel.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(obj, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(obj); }
        Validate();
    }
    static RectTransform Reveal(RectTransform parent, string name, float y, Sprite sprite, bool flip, out Image image)
    {
        var mask = parent.Find(name) as RectTransform;
        if (mask == null) mask = Rect(name, parent);
        float height = 900f * sprite.rect.height / sprite.rect.width;
        Center(mask, new Vector2(0, y), new Vector2(900, height));
        if (mask.GetComponent<RectMask2D>() == null) mask.gameObject.AddComponent<RectMask2D>();
        image = mask.Find("Divider")?.GetComponent<Image>();
        if (image == null) image = Rect("Divider", mask).gameObject.AddComponent<Image>();
        Center(image.rectTransform, Vector2.zero, new Vector2(900, height));
        image.rectTransform.localScale = new Vector3(1, flip ? -1 : 1, 1);
        image.sprite = sprite; image.preserveAspect = true; image.color = new Color(1, 1, 1, .84f); image.raycastTarget = false;
        return mask;
    }
    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
    }

    [MenuItem("Tools/Adventure/UI/Validate Discovery Area Banner")]
    public static void Validate()
    {
        Directory.CreateDirectory(Output); var report = new StringBuilder();
        var preview = EditorSceneManager.NewPreviewScene();
        var languageField = typeof(GameLocalization).GetField("language", BindingFlags.Static | BindingFlags.NonPublic);
        var tableField = typeof(GameLocalization).GetField("table", BindingFlags.Static | BindingFlags.NonPublic);
        var oldLanguage = GameLocalization.CurrentLanguage; var oldTable = tableField.GetValue(null);
        GameLocalizationTable fixtureTable = null; Texture2D reference = null, combatFrame = null;
        try
        {
            ValidateTriggers(preview, report);
            fixtureTable = UnityEngine.Object.Instantiate((GameLocalizationTable)oldTable);
            fixtureTable.entries.Add(new GameLocalizationEntry { key = "validation.discovery", vietnamese = "Vùng nước bay", english = "FLYING WATERS" });
            typeof(GameLocalizationTable).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixtureTable, null);
            tableField.SetValue(null, fixtureTable);
            var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            SceneManager.MoveGameObjectToScene(go, preview);
            var banner = go.GetComponent<DiscoveryAreaBanner>();
            Check(banner != null && banner.panel != null, "Discovery Area component and authored panel are missing.");
            go.transform.Find("Menu").gameObject.SetActive(false); go.transform.Find("Toast").gameObject.SetActive(false);
            foreach (Transform child in go.transform.Find("HUD")) child.gameObject.SetActive(child == banner.panel.transform);
            var canvas = go.GetComponent<Canvas>(); var cameraGo = new GameObject("Discovery preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGo, preview);
            var camera = cameraGo.GetComponent<Camera>(); camera.enabled = false; camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.orthographic = true; camera.orthographicSize = 540; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .045f, .045f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            go.GetComponent<CanvasScaler>().enabled = false;
            if (File.Exists(Output + "/ReferenceVideo.png"))
            {
                reference = new Texture2D(2, 2); reference.LoadImage(File.ReadAllBytes(Output + "/ReferenceVideo.png"));
                combatFrame = new Texture2D(640, 360); combatFrame.SetPixels(reference.GetPixels(0, 1080, 640, 360)); combatFrame.Apply();
                var background = Rect("Preview reference only", go.transform); background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
                background.offsetMin = background.offsetMax = Vector2.zero; background.SetAsFirstSibling();
                var raw = background.gameObject.AddComponent<RawImage>(); raw.texture = combatFrame; raw.raycastTarget = false;
            }
            Check(banner.Enqueue("first-area", "Vùng nước bay"), "First discovery must start its announcement.");
            Check(!banner.Enqueue("first-area", "Vùng nước bay"), "A repeated callback must not restart the same announcement.");
            Check(banner.Enqueue("second-area", "Khu vực thứ hai") && banner.PendingCount == 1, "Overlapping discoveries must queue, without replacing the first name.");
            banner.Sample(.15f);
            Check(banner.topReveal.rect.width > 0 && banner.topReveal.rect.width < banner.topDivider.rectTransform.rect.width,
                "The divider must reveal from the center without stretching its source artwork.");
            Check(banner.nameGroup.alpha == 0f, "The frame must appear before the title.");
            Render(camera, canvas, banner, "Reveal");
            banner.Sample(1.2f);
            Check(banner.panel.alpha == 1f && banner.nameGroup.alpha == 1f, "The full title must be visible during the hold.");
            foreach (var language in new[] { GameLanguage.Vietnamese, GameLanguage.English })
            {
                languageField.SetValue(null, language); GameLocalization.BindHierarchy(go);
                Check(banner.areaName.text == (language == GameLanguage.English ? "FLYING WATERS" : "Vùng nước bay"), "Discovery titles must use the existing localization system.");
                Render(camera, canvas, banner, language.ToString());
            }
            Check(!banner.panel.blocksRaycasts && !banner.panel.interactable && banner.panel.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget),
                "Discovery animation must never block movement or UI input.");
            Check(AssetDatabase.GetAssetPath(banner.topDivider.sprite) == Divider, "Use the white ornamental sprite selected by the user.");
            Check(banner.panel.transform.Find("Bottom Reveal") == null && banner.panel.GetComponentsInChildren<Image>(true).Length == 1,
                "Discovery banner must have only the top ornamental frame.");
            var frameSize = banner.topDivider.rectTransform.sizeDelta;
            banner.Sample(banner.Duration - banner.fadeOutSeconds * .5f);
            Check(banner.panel.alpha > 0 && banner.panel.alpha < 1f, "Discovery must fade away smoothly.");
            Check(banner.topDivider.rectTransform.sizeDelta == frameSize, "Reveal must clip the frame, preserving its proportions.");
            banner.Tick(banner.Duration + .1f);
            Check(banner.IsPlaying && banner.PendingCount == 0 && banner.areaName.text == "Khu vực thứ hai", "Queued area must play after the first finishes.");
            banner.Tick(banner.Duration + .1f);
            Check(!banner.IsPlaying && !banner.panel.gameObject.activeSelf, "Banner must hide completely after the queue finishes.");
            Check(banner.Enqueue("first-area", "Vùng nước bay"), "A completed title may play again when the session cooldown allows a new entry.");
            report.AppendLine("PASS: native reveal/hold/fade, fixed-aspect ornamental frame, staggered title fade/rise, VI/EN localization, and non-interactive HUD.");
            report.AppendLine("PASS: duplicate suppression while playing/queued, later replay, final panel hides, and only the selected top ornamental frame remains.");
            report.AppendLine("Integration: CampaignSession retains consumed discovery zones; first visit grants/saves rewards under the consumed-ID guard; later eligible entries only announce the title.");
            report.AppendLine("Edit Mode only. No campaign saves, map scenes or player input settings were written.");
            File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[Discovery Area Banner] " + report);
        }
        finally
        {
            languageField.SetValue(null, oldLanguage); tableField.SetValue(null, oldTable);
            EditorSceneManager.ClosePreviewScene(preview);
            if (fixtureTable != null) UnityEngine.Object.DestroyImmediate(fixtureTable);
            if (reference != null) UnityEngine.Object.DestroyImmediate(reference);
            if (combatFrame != null) UnityEngine.Object.DestroyImmediate(combatFrame);
        }
    }
    static void ValidateTriggers(Scene preview, StringBuilder report)
    {
        var a = new GameObject("Discovery A").AddComponent<WorldInteraction>(); SceneManager.MoveGameObjectToScene(a.gameObject, preview);
        a.kind = WorldInteractionKind.Discovery; a.savedWorldId = "test-discovery-a"; a.range = 5f; a.discoveryCooldownSeconds = 30f;
        var b = new GameObject("Discovery B").AddComponent<WorldInteraction>(); SceneManager.MoveGameObjectToScene(b.gameObject, preview);
        b.kind = WorldInteractionKind.Discovery; b.savedWorldId = "test-discovery-b"; b.range = 5f; b.discoveryCooldownSeconds = 30f;
        var state = new DiscoveryAreaTriggerState();
        Check(state.TryEnter(a, "mapgame", 0f), "First entry must trigger immediately.");
        Check(!state.TryEnter(a, "mapgame", 10f), "Standing inside must not auto-repeat.");
        state.ObservePlayer("mapgame", Vector3.right * 20);
        Check(!state.TryEnter(a, "mapgame", 29.99f), "Re-entry before 30 seconds must be suppressed.");
        Check(state.TryEnter(b, "mapgame", 29.99f), "Different zones must have independent cooldowns.");
        state.ObservePlayer("mapgame", Vector3.right * 20);
        Check(state.TryEnter(a, "mapgame", 30f), "Re-entry at 30 seconds must work.");
        a.gameObject.SetActive(false); state.ObservePlayer("mapgame", Vector3.right * 20);
        var recreated = new GameObject("Streamed Discovery A").AddComponent<WorldInteraction>(); SceneManager.MoveGameObjectToScene(recreated.gameObject, preview);
        recreated.kind = WorldInteractionKind.Discovery; recreated.savedWorldId = a.savedWorldId; recreated.range = a.range;
        Check(!state.TryEnter(recreated, "mapgame", 40f), "Recreating a streamed chunk must retain cooldown by area ID.");
        state.ObservePlayer("mapgame", Vector3.right * 20);
        Check(state.TryEnter(recreated, "mapgame", 60f), "A streamed area must trigger again after the next cooldown.");
        Check(!state.TryEnter(recreated, "mapgame", 95f), "Remaining inside must not repeat when the cooldown expires.");

        var instanceProperty = typeof(CampaignSession).GetProperty("Instance"); var oldSession = CampaignSession.Instance;
        var sessionGo = new GameObject("Read-only discovery save fixture"); sessionGo.SetActive(false); SceneManager.MoveGameObjectToScene(sessionGo, preview);
        var session = sessionGo.AddComponent<CampaignSession>();
        try
        {
            var data = new CampaignSave(); data.collected.Add(recreated.Id);
            typeof(CampaignSession).GetProperty("Data").SetValue(session, data);
            instanceProperty.SetValue(null, session);
            Check(recreated.IsConsumed, "Fixture must represent a previously discovered area in an old save.");
            bool registered = (bool)typeof(CampaignSession).GetMethod("PrepareWorldPoint", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { recreated });
            Check(registered && recreated.gameObject.activeSelf, "Previously discovered zones must remain active and registered after loading.");
        }
        finally { instanceProperty.SetValue(null, oldSession); }
        report.AppendLine("PASS: immediate first entry, exact 30-second boundary, early re-entry suppression, no standing repeats, per-area cooldowns, streamed chunk recreation, and consumed areas from old saves remain registered.");
    }
    static void Render(Camera camera, Canvas canvas, DiscoveryAreaBanner banner, string stage)
    {
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        canvas.scaleFactor = target.width / 1920f;
        var old = RenderTexture.active; Texture2D image = null;
        try
        {
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true)) graphic.SetAllDirty();
            banner.areaName.ForceMeshUpdate(true); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new UnityEngine.Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/Preview-" + stage + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = old; camera.targetTexture = null;
            if (image != null) UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
