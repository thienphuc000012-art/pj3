using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent, DefaultExecutionOrder(-500)]
public sealed class CombatPauseMenu : MonoBehaviour
{
    static CombatPauseMenu current;
    public static bool IsPaused => current != null && current.open;
    public static bool BlocksInput => current != null && (current.open || current.inputBlockedFrame == Time.frameCount);
    public static float UnscaledTime => current != null
        ? (current.open ? current.pausedAt : Time.unscaledTime) - current.totalPausedTime
        : Time.unscaledTime;
    public static float UnscaledDeltaTime => IsPaused ? 0f : Time.unscaledDeltaTime;

    public static WaitUntil WaitForCombatSeconds(float seconds)
    {
        float deadline = UnscaledTime + Mathf.Max(0f, seconds);
        return new WaitUntil(() => !IsPaused && UnscaledTime >= deadline);
    }

    internal static bool OwnsCanvas(Canvas candidate) => current != null && current.canvas == candidate;

    CombatManager combat;
    Canvas canvas;
    CanvasGroup pausePanel;
    GameObject pauseButton;
    Button continueButton, optionsButton;
    TMP_Text errorText;
    GameOptionsView options;
    GameUIThemeSettings theme;
    TMP_FontAsset font;
    GameObject previousSelection;
    float previousTimeScale, pausedAt, totalPausedTime;
    CursorLockMode previousCursor;
    bool previousCursorVisible, open, leaving;
    int inputBlockedFrame = -1;
    readonly Dictionary<Animator, float> pausedAnimators = new Dictionary<Animator, float>();
    readonly List<(CanvasGroup group, bool interactable, bool raycasts)> blockedCanvases =
        new List<(CanvasGroup, bool, bool)>();

    void Awake()
    {
        current = this;
        combat = GetComponent<CombatManager>();
        BuildView();
    }

    bool CanOpen => combat != null && combat.isActiveAndEnabled && combat.PresentationReady &&
        combat.state != CombatState.Won && combat.state != CombatState.Lost &&
        !LoadingScreen.IsLoading && !AdventureCheatPanel.IsOpen;

    void Update()
    {
        bool available = CanOpen && !open;
        if (pauseButton.activeSelf != available) pauseButton.SetActive(available);
        if (leaving || !EscapePressed()) return;
        if (options != null && options.IsOpen) options.Close();
        else if (open) Resume();
        else if (available) Open();
    }

    void Open()
    {
        if (open || !CanOpen) return;
        AdventureCanvasRoot.EnsureEventSystem(gameObject.scene);
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        previousTimeScale = Time.timeScale;
        previousCursor = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        pausedAt = Time.unscaledTime;
        open = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PauseAnimators(combat.playerParty);
        PauseAnimators(combat.enemyParty);
        foreach (var other in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (other == canvas || !other.isRootCanvas || other.gameObject.scene != gameObject.scene) continue;
            if (!other.TryGetComponent<CanvasGroup>(out var group)) group = other.gameObject.AddComponent<CanvasGroup>();
            blockedCanvases.Add((group, group.interactable, group.blocksRaycasts));
            group.interactable = group.blocksRaycasts = false;
        }
        pauseButton.SetActive(false);
        GameLocalization.SetText(errorText, "");
        pausePanel.alpha = 1f;
        pausePanel.gameObject.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(continueButton.gameObject);
    }

    void PauseAnimators(List<BattleUnit> units)
    {
        foreach (var unit in units)
            if (unit != null)
                foreach (var animator in unit.GetComponentsInChildren<Animator>(true))
                    if (animator.updateMode == AnimatorUpdateMode.UnscaledTime &&
                        !pausedAnimators.ContainsKey(animator))
                    {
                        pausedAnimators.Add(animator, animator.speed);
                        animator.speed = 0f;
                    }
    }

    void Resume()
    {
        if (!open || leaving) return;
        if (options != null && options.IsOpen) options.Close();
        pausePanel.gameObject.SetActive(false);
        ReleasePause();
        var selection = previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null;
        EventSystem.current?.SetSelectedGameObject(selection);
        previousSelection = null;
    }

    void ReleasePause()
    {
        if (!open) return;
        open = false;
        inputBlockedFrame = Time.frameCount;
        totalPausedTime += Time.unscaledTime - pausedAt;
        foreach (var pair in pausedAnimators)
            if (pair.Key != null) pair.Key.speed = pair.Value;
        pausedAnimators.Clear();
        foreach (var (group, interactable, raycasts) in blockedCanvases)
            if (group != null)
            {
                group.interactable = interactable;
                group.blocksRaycasts = raycasts;
            }
        blockedCanvases.Clear();
        if (leaving || LoadingScreen.IsLoading) return;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursor;
        Cursor.visible = previousCursorVisible;
    }

    void OpenOptions()
    {
        if (!open || leaving) return;
        if (options == null) options = GameOptionsView.Create(canvas.transform, false);
        if (options == null || options.IsOpen) return;
        pausePanel.alpha = 0f;
        options.Open(pausePanel, optionsButton.gameObject, () => pausePanel.alpha = 1f);
    }

    void ReturnToTitle()
    {
        if (!open || leaving || LoadingScreen.IsLoading) return;
        var session = CampaignSession.Instance;
        bool accepted = session != null
            ? session.ReturnToTitleFromBattle()
            : LoadingScreen.Load(AdventureTitleMenu.SceneName);
        if (accepted)
        {
            leaving = true;
            pausePanel.gameObject.SetActive(false);
        }
        else GameLocalization.SetText(errorText, "Không thể quay về Title.");
    }

    void Quit()
    {
        if (!open || leaving) return;
        GameAudio.FlushPreferences();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDisable()
    {
        if (options != null && options.IsOpen) options.Close();
        if (pausePanel != null) pausePanel.gameObject.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        ReleasePause();
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }

    static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    void BuildView()
    {
        theme = Resources.Load<GameUIThemeSettings>("Adventure/UI/GameUIThemeSettings");
        var optionsPrefab = Resources.Load<GameOptionsView>("Adventure/UI/GameOptions");
        var fontLabel = optionsPrefab != null && optionsPrefab.backButton != null
            ? optionsPrefab.backButton.GetComponentInChildren<TMP_Text>(true) : null;
        font = fontLabel != null ? fontLabel.font : TMP_Settings.defaultFontAsset;
        var root = new GameObject("Combat Pause Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PreserveAuthoredUI));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        int topLayer = int.MinValue;
        foreach (var layer in SortingLayer.layers)
            if (layer.value >= topLayer) { topLayer = layer.value; canvas.sortingLayerID = layer.id; }
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        var button = Button(root.transform, "Pause", "Esc   II", Vector2.one,
            new Vector2(-110f, -28f), new Vector2(150f, 42f), Open, 23f);
        pauseButton = button.gameObject;
        pauseButton.SetActive(false);

        var overlay = Rect(root.transform, "Pause Panel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        pausePanel = overlay.gameObject.AddComponent<CanvasGroup>();
        overlay.gameObject.AddComponent<Image>().color = new Color(.01f, .01f, .015f, .78f);
        var center = new Vector2(.5f, .5f);
        var window = Rect(overlay, "Commands", center, center, Vector2.zero, new Vector2(500f, 440f));
        var backdrop = window.gameObject.AddComponent<Image>();
        backdrop.color = new Color(.095f, .083f, .072f);
        if (theme != null && theme.pauseBackground != null)
        {
            var art = Rect(window, "Background", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12f, -12f))
                .gameObject.AddComponent<Image>();
            art.sprite = theme.pauseBackground;
            art.color = new Color(.92f, .85f, .74f, .65f);
            art.raycastTarget = false;
        }
        var border = Rect(window, "Frame", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero)
            .gameObject.AddComponent<GameUIFrameGraphic>();
        border.Configure(new Color(.83f, .67f, .38f, .92f), true, continuousBorder: true);
        Text(window, "Heading", "TẠM DỪNG", new Vector2(0f, 162f), new Vector2(440f, 52f), 37f)
            .color = new Color(.95f, .83f, .62f);
        var size = new Vector2(356f, 52f);
        Button(window, "Return To Title", "Về Title", center, new Vector2(0, 73), size, ReturnToTitle);
        continueButton = Button(window, "Continue", "Tiếp tục", center, new Vector2(0, 10), size, Resume);
        optionsButton = Button(window, "Options", "Tùy chọn", center, new Vector2(0, -53), size, OpenOptions);
        Button(window, "Quit Game", "Thoát", center, new Vector2(0, -116), size, Quit);
        errorText = Text(window, "Status", "", new Vector2(0, -178), new Vector2(440, 42), 21f);
        overlay.gameObject.SetActive(false);
    }

    Button Button(Transform parent, string name, string label, Vector2 anchor, Vector2 position,
        Vector2 size, UnityAction action, float fontSize = 27f)
    {
        var rect = Rect(parent, name, anchor, anchor, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = theme != null ? theme.skillBackground : null;
        image.color = image.sprite != null ? Color.white : new Color(.23f, .19f, .12f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = colors.selectedColor = new Color(1f, .86f, .63f);
        colors.pressedColor = new Color(.82f, .68f, .46f);
        button.colors = colors;
        button.onClick.AddListener(action);
        Text(rect, "Label", label, Vector2.zero, size - new Vector2(16f, 0f), fontSize);
        SmoothButtonHoverScale.Ensure(button);
        GameButtonSound.Ensure(button);
        return button;
    }

    TMP_Text Text(Transform parent, string name, string label, Vector2 position, Vector2 size, float fontSize)
    {
        var center = new Vector2(.5f, .5f);
        var text = Rect(parent, name, center, center, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        GameLocalization.SetText(text, label);
        return text;
    }

    static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
