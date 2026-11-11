using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[DisallowMultipleComponent]
public sealed class AdventureTitleMenu : MonoBehaviour
{
    public const string SceneName = "Title";
    [Header("New Game")]
    [SerializeField] string introScene = "MangaIntro";
    [Tooltip("Tên scene có trong Campaign Config và Build Settings.")]
    [SerializeField] string startMapScene = "mapgame";
    [Tooltip("Nếu tắt, dùng vị trí Player đã đặt sẵn trong scene bản đồ.")]
    [SerializeField] bool overrideSpawnPosition;
    [SerializeField] Vector3 spawnPosition;
    [SerializeField] float spawnYaw;

    [Header("Title UI")]
    [SerializeField] CanvasGroup pressScreen;
    [SerializeField] CanvasGroup selectionScreen;
    [SerializeField] Button continueButton;
    [SerializeField] Button newGameButton;
    [SerializeField] Button quitButton;
    [SerializeField] TMP_Text status;
    [SerializeField, Min(0f)] float menuFadeSeconds = .22f;

    CampaignConfig config;
    AdventureSaveSlotsView saveSlotsView;
    bool menuOpened;
    bool leaving;
    static string startupError;

    void Awake()
    {
        config = Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        continueButton.onClick.AddListener(ContinueGame);
        newGameButton.onClick.AddListener(NewGame);
        quitButton.onClick.AddListener(Quit);
        GameLocalization.BindHierarchy(gameObject);
        if (!string.IsNullOrEmpty(startupError))
        {
            ShowError(startupError);
            startupError = null;
        }
    }

    void Start() => AdventureCanvasRoot.EnsureEventSystem();

    void Update()
    {
        if (!menuOpened && WasButtonPressed()) ShowMenu();
        else if (saveSlotsView != null && saveSlotsView.IsOpen && WasBackPressed())
            saveSlotsView.HandleBack();
    }

    public void ShowMenu()
    {
        if (menuOpened) return;
        menuOpened = true;
        continueButton.gameObject.SetActive(GetTitleSlots().Exists(slot => !slot.isEmpty && !slot.isCorrupt));
        StartCoroutine(RevealMenu());
    }

    IEnumerator RevealMenu()
    {
        selectionScreen.gameObject.SetActive(true);
        selectionScreen.alpha = 0;
        // Never pass the opening button press through to a menu command.
        yield return null;
        for (float elapsed = 0; elapsed < menuFadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0, 1, elapsed / menuFadeSeconds);
            pressScreen.alpha = 1 - progress;
            selectionScreen.alpha = progress;
            yield return null;
        }
        pressScreen.gameObject.SetActive(false);
        selectionScreen.alpha = 1;
        selectionScreen.interactable = selectionScreen.blocksRaycasts = true;
        EventSystem.current?.SetSelectedGameObject(continueButton.gameObject.activeSelf
            ? continueButton.gameObject : newGameButton.gameObject);
    }

    public void ContinueGame()
    {
        if (leaving || (saveSlotsView != null && saveSlotsView.IsOpen)) return;
        if (saveSlotsView == null)
        {
            var template = Resources.Load<AdventureSaveSlotsView>("Adventure/UI/AdventureCanvas");
            if (template == null || template.slotsPanel == null || template.confirmationPanel == null)
            {
                ShowError("Không thể mở danh sách bản lưu.");
                return;
            }
            saveSlotsView = gameObject.AddComponent<AdventureSaveSlotsView>();
            saveSlotsView.BindTitle(template, continueButton, GetTitleSlots, LoadTitleSlot, RestoreMenuFocus);
        }
        ShowError("");
        selectionScreen.alpha = 0;
        selectionScreen.interactable = selectionScreen.blocksRaycasts = false;
        saveSlotsView.OpenTitle();
    }

    void RestoreMenuFocus()
    {
        if (leaving || !isActiveAndEnabled) return;
        selectionScreen.alpha = 1;
        selectionScreen.interactable = selectionScreen.blocksRaycasts = true;
        continueButton.gameObject.SetActive(GetTitleSlots().Exists(slot => !slot.isEmpty && !slot.isCorrupt));
        EventSystem.current?.SetSelectedGameObject(continueButton.gameObject.activeSelf
            ? continueButton.gameObject : newGameButton.gameObject);
    }

    public void NewGame()
    {
        if (leaving) return;
        string map = string.IsNullOrWhiteSpace(startMapScene) ? config?.mapScene : startMapScene.Trim();
        string intro = introScene?.Trim();
        if (config?.startingParty == null || !config.startingParty.Exists(unit => unit != null) ||
            !CanLoadMap(map) || string.IsNullOrEmpty(intro) || !Application.CanStreamedLevelBeLoaded(intro) ||
            intro == gameObject.scene.name || intro == map || intro == LoadingScreen.SceneName ||
            LoadingScreen.IsLoading || CampaignSession.Instance != null ||
            overrideSpawnPosition && (!Finite(spawnPosition) || !float.IsFinite(spawnYaw)))
        {
            ShowError("Kiểm tra scene intro, bản đồ và điểm xuất phát.");
            return;
        }

        var save = CampaignSession.CreateFreshSave(config);
        save.currentMapScene = map;
        if (overrideSpawnPosition)
        {
            save.position = spawnPosition;
            save.rotation = Quaternion.Euler(0, spawnYaw, 0);
            save.hasPosition = true;
        }

        if (!BeginIntro(intro, gameObject.scene.name, config, save))
        {
            ShowError("Không thể tải intro.");
            return;
        }
        leaving = true;
        selectionScreen.interactable = selectionScreen.blocksRaycasts = false;
    }

    static bool BeginIntro(string intro, string titleScene, CampaignConfig config, CampaignSave save)
    {
        void Fail(string message)
        {
            startupError = message;
            Debug.LogError(message);
            SceneManager.LoadSceneAsync(titleScene);
        }

        void BindIntro(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            SceneManager.sceneLoaded -= BindIntro;
            if (scene.name != intro) return;
            PlayableDirector master = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "MangaIntro_Master" && root.TryGetComponent(out master)) break;

            if (master == null || master.playableAsset == null)
            {
                Fail("Không tìm thấy Timeline chính của intro.");
                return;
            }
            void CompleteIntro(PlayableDirector director)
            {
                director.stopped -= CompleteIntro;
                if (SceneManager.GetActiveScene() != scene) return;
                if (!CampaignSession.TryBackupAutosave() || !CampaignSession.BeginFromTitle(config, save))
                    Fail("Không thể bắt đầu game mới.");
            }

            master.stopped += CompleteIntro;
            if (master.state != PlayState.Playing) master.Play();
        }

        SceneManager.sceneLoaded += BindIntro;
        try
        {
            if (SceneManager.LoadSceneAsync(intro) != null) return true;
        }
        catch (Exception exception) { Debug.LogException(exception); }
        SceneManager.sceneLoaded -= BindIntro;
        return false;
    }

    int ManualSlotCount => config != null ? Mathf.Clamp(config.saveSlotCount, 1, CampaignSaveSlots.MaximumSlots) : 0;

    List<CampaignSaveSlotInfo> GetTitleSlots()
    {
        var entries = new List<CampaignSaveSlotInfo>();
        if (ReadTitleSlot(AdventureSaveSlotsView.AutosaveSlot, out var autosave) != null)
            entries.Add(autosave);
        for (int slot = 1; slot <= ManualSlotCount; slot++)
        {
            ReadTitleSlot(slot, out var info);
            entries.Add(info);
        }
        return entries;
    }

    CampaignSave ReadTitleSlot(int slot, out CampaignSaveSlotInfo info)
    {
        bool autosave = slot == AdventureSaveSlotsView.AutosaveSlot;
        CampaignSave save;
        if (autosave)
        {
            save = CampaignSession.ReadAutosave();
            info = new CampaignSaveSlotInfo { slot = slot, isEmpty = save == null };
        }
        else if (!CampaignSaveSlots.TryLoad(Application.persistentDataPath, slot, ManualSlotCount, out save, out info, out _))
            return null;

        if (save == null) return null;
        string map = string.IsNullOrWhiteSpace(save.currentMapScene) ? config?.mapScene : save.currentMapScene;
        if (!CampaignSession.IsCompatibleSave(config, save) || !CanLoadMap(map) || (save.hasPosition && !Finite(save.position)))
        {
            info.isCorrupt = true;
            return null;
        }
        try { save.MigrateSkillUnlocks(); }
        catch (Exception exception)
        {
            Debug.LogWarning(exception.Message);
            info.isCorrupt = true;
            return null;
        }
        save.currentMapScene = map;
        save.unlockedRestPoints ??= new List<UnlockedRestPoint>();
        if (autosave)
        {
            info.savedAtUtc = save.savedAtUtc;
            info.playTimeSeconds = save.playTimeSeconds;
            info.mapScene = map;
            info.locationName = save.saveLocationName;
        }
        return save;
    }

    bool LoadTitleSlot(int slot, out string error)
    {
        error = null;
        if (leaving) return false;
        var save = ReadTitleSlot(slot, out _);
        if (save == null)
        {
            error = GameLocalization.Translate("Không tìm thấy bản lưu hợp lệ.");
            return false;
        }
        leaving = CampaignSession.BeginFromTitle(config, save);
        if (!leaving) error = GameLocalization.Translate("Không thể tải bản đồ.");
        return leaving;
    }

    bool CanLoadMap(string scene) => !string.IsNullOrEmpty(scene) && config != null && config.IsMapScene(scene) &&
        Application.CanStreamedLevelBeLoaded(scene) && Application.CanStreamedLevelBeLoaded(LoadingScreen.SceneName);

    static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    void ShowError(string message)
    {
        if (status != null) status.text = GameLocalization.Translate(message);
    }

    public void Quit()
    {
        if (leaving) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static bool WasBackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
            Mouse.current?.rightButton.wasPressedThisFrame == true ||
            Gamepad.current?.buttonEast.wasPressedThisFrame == true) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }

    static bool WasButtonPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current?.anyKey.wasPressedThisFrame == true) return true;
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
            mouse.middleButton.wasPressedThisFrame || mouse.forwardButton.wasPressedThisFrame ||
            mouse.backButton.wasPressedThisFrame)) return true;
        foreach (var gamepad in Gamepad.all)
            foreach (var control in gamepad.allControls)
                if (control is ButtonControl button && !button.synthetic && button.wasPressedThisFrame)
                    return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.anyKeyDown;
#else
        return false;
#endif
    }
}
