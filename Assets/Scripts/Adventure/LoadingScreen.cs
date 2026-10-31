using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class LoadingScreen : MonoBehaviour
{
    public const string SceneName = "Loading";
    static string destination;
    public static bool IsLoading { get; private set; }
    public static string Error { get; private set; }
    float progress;
    string target;
    bool failed;
    bool loadingMapScene;
    bool loadingChunks;
    Scene pendingMapScene;
    AdventureCanvasRoot view;
    void Awake()
    {
        view = AdventureCanvasRoot.Acquire(AdventureCanvasKind.Loading);
        view.Click("Retry", () => Load("mapgame"));
        view.Active("Retry", false);
        view.Fill("Progress/Fill", 0);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { destination = null; IsLoading = false; Error = null; }
    public static bool Load(string scene)
    {
        if (IsLoading) return false;
        if (string.IsNullOrEmpty(scene) || scene == SceneName || !Application.CanStreamedLevelBeLoaded(scene) || !Application.CanStreamedLevelBeLoaded(SceneName))
        { Error = "Không tìm thấy scene đích hoặc scene Loading trong Build Settings."; Debug.LogError(Error); return false; }
        Error = null; destination = scene; IsLoading = true;
        ScreenSpaceCut.StopActive();
        Time.timeScale = 1;
        if (MapChunkStreamer.Instance != null) MapChunkStreamer.Instance.TransitionTo(SceneName);
        else SceneManager.LoadSceneAsync(SceneName);
        return true;
    }
    IEnumerator Start()
    {
        target = destination;
        if (string.IsNullOrEmpty(target)) target = Resources.Load<CampaignConfig>("Adventure/CampaignConfig")?.mapScene ?? "mapgame";
        if (!Application.CanStreamedLevelBeLoaded(target) || target == SceneName)
        { Fail("Scene đích chưa được thêm vào Build Settings."); yield break; }
        IsLoading = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
        // Present at least one loading frame before beginning the expensive scene load.
        yield return null;
        CampaignConfig config = Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
        string targetName = Path.GetFileNameWithoutExtension(target);
        loadingMapScene = config != null && (config.IsMapScene(target) || config.IsMapScene(targetName));
        if (loadingMapScene)
        {
            yield return LoadMapAndInitialChunks();
            yield break;
        }

        yield return LoadSingleScene();
    }

    private IEnumerator LoadSingleScene()
    {
        float began = Time.unscaledTime;
        var operation = SceneManager.LoadSceneAsync(target);
        if (operation == null) { Fail("Không thể tải scene."); yield break; }
        operation.allowSceneActivation = false;
        while (operation.progress < .9f || Time.unscaledTime - began < 1.2f)
        {
            progress = Mathf.Clamp01(operation.progress / .9f);
            yield return null;
        }
        progress = 1;
        yield return null;
        destination = null;
        operation.allowSceneActivation = true;
        while (!operation.isDone) yield return null;
    }

    private IEnumerator LoadMapAndInitialChunks()
    {
        string targetName = Path.GetFileNameWithoutExtension(target);
        pendingMapScene = default;
        SceneManager.sceneLoaded += OnDestinationSceneLoaded;
        AsyncOperation operation = SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);
        if (operation == null)
        {
            SceneManager.sceneLoaded -= OnDestinationSceneLoaded;
            Fail("Không thể tải map đích.");
            yield break;
        }

        operation.allowSceneActivation = false;
        float sceneLoadStartedAt = Time.unscaledTime;
        while (operation.progress < .9f || Time.unscaledTime - sceneLoadStartedAt < 1.2f)
        {
            progress = Mathf.Clamp01(operation.progress / .9f) * .72f;
            yield return null;
        }
        operation.allowSceneActivation = true;
        while (!operation.isDone) yield return null;
        SceneManager.sceneLoaded -= OnDestinationSceneLoaded;

        Scene mapScene = pendingMapScene.IsValid() && pendingMapScene.isLoaded
            ? pendingMapScene
            : FindLoadedScene(target, targetName);
        if (!mapScene.IsValid() || !mapScene.isLoaded)
        {
            Fail("Map đích chưa được tải.");
            yield break;
        }
        if (SceneManager.GetActiveScene() != mapScene)
            SceneManager.SetActiveScene(mapScene);

        MapChunkStreamer streamer = FindStreamer(mapScene);

        // Maps without chunk streaming still use the same single loading screen.
        if (streamer != null)
        {
            streamer.SuppressOverlay = true;
            loadingChunks = true;
            float chunkDeadline = Time.unscaledTime + 120f;
            while (!streamer.Ready && string.IsNullOrEmpty(streamer.Error))
            {
                progress = .72f + .27f * Mathf.Clamp01(streamer.InitialLoadProgress);
                if (Time.unscaledTime >= chunkDeadline)
                {
                    Fail("Tải chunk ban đầu quá lâu. Hãy kiểm tra scene chunk trong Build Settings.");
                    yield break;
                }
                yield return null;
            }

            if (!string.IsNullOrEmpty(streamer.Error))
            {
                Fail(streamer.Error);
                yield break;
            }
            streamer.SuppressOverlay = false;
        }

        loadingChunks = false;
        // Chunk activation alone is not the end of map initialization. Keep the
        // loading screen over the final interaction/checkpoint setup as well.
        MapGameSetup setup = FindMapSetup(mapScene);
        float setupDeadline = Time.unscaledTime + 30f;
        while (setup != null && !setup.StartupReady)
        {
            if (!string.IsNullOrEmpty(setup.StartupError)) { Fail(setup.StartupError); yield break; }
            if (Time.unscaledTime >= setupDeadline) { Fail("Map chưa hoàn tất khởi tạo. Hãy kiểm tra MapGameSetup."); yield break; }
            progress = .99f;
            yield return null;
        }
        progress = 1f;
        yield return null;
        destination = null;
        Scene loadingScene = gameObject.scene;
        PreserveMapEventSystem(mapScene, loadingScene);
        if (loadingScene.IsValid() && loadingScene.isLoaded)
            SceneManager.UnloadSceneAsync(loadingScene);
    }

    private void OnDestinationSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != Path.GetFileNameWithoutExtension(target) && scene.path != target) return;
        pendingMapScene = scene;
        SceneManager.SetActiveScene(scene);

        var streamer = FindStreamer(scene);
        if (streamer != null) streamer.SuppressOverlay = true;
    }

    private static Scene FindLoadedScene(string path, string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && (scene.path == path || scene.name == sceneName)) return scene;
        }
        return default;
    }

    private static MapChunkStreamer FindStreamer(Scene scene)
    {
        var current = MapChunkStreamer.Instance;
        if (current != null && current.gameObject.scene == scene) return current;
        foreach (var root in scene.GetRootGameObjects())
        {
            var streamer = root.GetComponentInChildren<MapChunkStreamer>(true);
            if (streamer != null) return streamer;
        }
        return null;
    }

    private static MapGameSetup FindMapSetup(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var setup = root.GetComponentInChildren<MapGameSetup>();
            if (setup != null) return setup;
        }
        return null;
    }

    private static void PreserveMapEventSystem(Scene mapScene, Scene loadingScene)
    {
        EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (systems.Any(system => system != null && system.gameObject.scene == mapScene)) return;

        EventSystem loadingSystem = systems.FirstOrDefault(system => system != null && system.gameObject.scene == loadingScene);
        if (loadingSystem != null)
        {
            if (loadingSystem.transform.parent != null)
                loadingSystem.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(loadingSystem.gameObject, mapScene);
            return;
        }

        if (!systems.Any(system => system != null))
            AdventureCanvasRoot.EnsureEventSystem();
    }

    private void Fail(string message)
    {
        failed = true;
        Error = message;
        IsLoading = false;
        Cursor.visible = true;
    }
    void OnDestroy() { IsLoading = false; }
    void Update()
    {
        if (view == null) return;
        view.Component<RectTransform>("Spinner").Rotate(0, 0, -18 * Time.unscaledDeltaTime);
        view.Text("Message", failed ? Error : loadingChunks ? "Đang tải khu vực quanh điểm bắt đầu." : loadingMapScene ? "Đang chuẩn bị bản đồ khám phá." : target == "combattest" ? "Chuẩn bị đối mặt với thử thách." : "Mỗi bước chân mở ra một câu chuyện.");
        view.Fill("Progress/Fill", progress);
        view.Text("Status", failed ? "TẢI KHÔNG THÀNH CÔNG" : loadingChunks ? "ĐANG TẢI KHU VỰC • " + Mathf.FloorToInt(progress * 100) + "%" : progress >= 1 ? "ĐANG MỞ CẢNH • 100%" : "ĐANG TẢI • " + Mathf.FloorToInt(progress * 100) + "%");
        view.Active("Retry", failed);
        if (failed) Cursor.visible = true;
    }
}
