using System.Collections;
using UnityEngine;
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
        Time.timeScale = 1;
        SceneManager.LoadSceneAsync(SceneName);
        return true;
    }
    IEnumerator Start()
    {
        target = destination;
        if (string.IsNullOrEmpty(target)) target = Resources.Load<CampaignConfig>("Adventure/CampaignConfig")?.mapScene ?? "mapgame";
        if (!Application.CanStreamedLevelBeLoaded(target) || target == SceneName)
        { failed = true; IsLoading = false; Error = "Scene đích chưa được thêm vào Build Settings."; yield break; }
        IsLoading = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
        // Present at least one loading frame before beginning the expensive scene load.
        yield return null;
        float began = Time.unscaledTime;
        var operation = SceneManager.LoadSceneAsync(target);
        if (operation == null) { failed = true; IsLoading = false; Error = "Không thể tải scene."; yield break; }
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
    void OnDestroy() { IsLoading = false; }
    void Update()
    {
        if (view == null) return;
        view.Component<RectTransform>("Spinner").Rotate(0, 0, -18 * Time.unscaledDeltaTime);
        view.Text("Message", failed ? Error : target == "combattest" ? "Chuẩn bị đối mặt với thử thách." : "Mỗi bước chân mở ra một câu chuyện.");
        view.Fill("Progress/Fill", progress);
        view.Text("Status", failed ? "TẢI KHÔNG THÀNH CÔNG" : progress >= 1 ? "ĐANG MỞ CẢNH • 100%" : "ĐANG TẢI • " + Mathf.FloorToInt(progress * 100) + "%");
        view.Active("Retry", failed);
        if (failed) Cursor.visible = true;
    }
}
