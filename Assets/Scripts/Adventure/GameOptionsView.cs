using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public sealed class GameVolumeRow
{
    public GameAudioBus bus;
    public Slider slider;
    public TMP_Text percentage;
    public Button decrease, increase;

    [NonSerialized] internal Image fillImage;
    [NonSerialized] internal Color fillColor;
    [NonSerialized] internal float targetVolume, velocity;
    [NonSerialized] internal int targetPercentage = -1;
}

/// <summary>Shared, authored Options panel used by both Title and the exploration menu.</summary>
public sealed class GameOptionsView : MonoBehaviour
{
    public GameVolumeRow[] rows;
    public Button languageButton, backButton;
    public TMP_Text languageValue;
    [SerializeField, Min(.01f)] float volumeAnimationSeconds = .12f;
    CanvasGroup blockedGroup;
    bool oldInteractable, oldRaycasts;
    GameObject returnFocus;
    Action closed;
    bool bound;
    public bool IsOpen => gameObject.activeSelf;

    public static GameOptionsView Create(Transform canvas, bool showLanguage = true)
    {
        var prefab = Resources.Load<GameOptionsView>("Adventure/UI/GameOptions");
        if (prefab == null) { Debug.LogError("GameOptions prefab missing. Run Tools > Adventure > Audio > Setup Audio and Options."); return null; }
        var view = Instantiate(prefab, canvas, false); view.name = "Options"; view.gameObject.SetActive(false);
        view.SetLanguageVisible(showLanguage);
        GameLocalization.BindHierarchy(view.gameObject);
        return view;
    }
    public void SetLanguageVisible(bool visible)
    {
        var heading = transform.Find("Volume Panel/Language Label");
        if (heading != null) heading.gameObject.SetActive(visible);
        if (languageButton != null) languageButton.gameObject.SetActive(visible);
    }
    void Awake() => Bind();
    void Bind()
    {
        if (bound) return; bound = true;
        foreach (var row in rows)
        {
            var captured = row;
            row.slider.minValue = 0; row.slider.maxValue = 1;
            if (row.slider.fillRect != null)
            {
                row.fillImage = row.slider.fillRect.GetComponent<Image>();
                if (row.fillImage != null) row.fillColor = row.fillImage.color;
            }
            row.slider.onValueChanged.AddListener(value =>
            {
                // Direct input has already moved both parts of the Slider.
                captured.velocity = 0f;
                if (captured.fillImage != null) captured.fillImage.color = captured.fillColor;
                GameAudio.SetVolume(captured.bus, value);
            });
            row.decrease.onClick.AddListener(() => GameAudio.SetVolume(captured.bus, GameAudio.Volume(captured.bus) - .05f));
            row.increase.onClick.AddListener(() => GameAudio.SetVolume(captured.bus, GameAudio.Volume(captured.bus) + .05f));
        }
        languageButton.onClick.AddListener(GameLocalization.ToggleLanguage);
        backButton.onClick.AddListener(Close);
        foreach (var button in GetComponentsInChildren<Button>(true)) SmoothButtonHoverScale.Ensure(button);
    }
    public void Open(CanvasGroup background, GameObject focus, Action onClosed = null)
    {
        if (IsOpen) return;
        Bind(); blockedGroup = background; returnFocus = focus; closed = onClosed;
        if (blockedGroup != null)
        {
            oldInteractable = blockedGroup.interactable; oldRaycasts = blockedGroup.blocksRaycasts;
            blockedGroup.interactable = blockedGroup.blocksRaycasts = false;
        }
        transform.SetAsLastSibling(); gameObject.SetActive(true); Refresh();
        EventSystem.current?.SetSelectedGameObject(rows.Length > 0 ? rows[0].slider.gameObject : backButton.gameObject);
    }
    void OnEnable()
    {
        GameAudio.VolumesChanged += Refresh;
        GameLocalization.LanguageChanged += Refresh;
        Refresh();
        foreach (var row in rows) ShowVolume(row, row.targetVolume);
    }
    void OnDisable()
    {
        GameAudio.VolumesChanged -= Refresh;
        GameLocalization.LanguageChanged -= Refresh;
        if (blockedGroup != null) { blockedGroup.interactable = oldInteractable; blockedGroup.blocksRaycasts = oldRaycasts; blockedGroup = null; }
        GameAudio.FlushPreferences();
    }
    public void Refresh()
    {
        foreach (var row in rows)
        {
            float value = GameAudio.Volume(row.bus);
            row.targetVolume = value;
            if (!isActiveAndEnabled || !Application.isPlaying) ShowVolume(row, value);
            int percent = Mathf.RoundToInt(value * 100f);
            if (percent != row.targetPercentage)
            {
                row.targetPercentage = percent;
                GameLocalization.SetText(row.percentage, percent + "%");
            }
        }
        GameLocalization.SetText(languageValue, GameLocalization.IsEnglish ? "English" : "Tiếng Việt");
    }

    void Update()
    {
        foreach (var row in rows)
        {
            float displayedVolume = row.slider.value;
            if (displayedVolume == row.targetVolume) continue;
            float value = Mathf.SmoothDamp(displayedVolume, row.targetVolume, ref row.velocity,
                volumeAnimationSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            ShowVolume(row, Mathf.Abs(value - row.targetVolume) < .0005f ? row.targetVolume : value);
        }
    }

    static void ShowVolume(GameVolumeRow row, float value)
    {
        // Use the Slider to update both the fill edge and the handle in one step.
        row.slider.SetValueWithoutNotify(value);
        if (value == row.targetVolume) row.velocity = 0f;
        if (row.fillImage != null)
        {
            float accent = Mathf.Clamp01(Mathf.Abs(row.targetVolume - value) * 20f);
            row.fillImage.color = Color.Lerp(row.fillColor, new Color(1f, .80f, .48f, row.fillColor.a), accent);
        }
    }
    public void Close()
    {
        if (!IsOpen) return;
        gameObject.SetActive(false);
        if (returnFocus != null && returnFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(returnFocus);
        var callback = closed; closed = null; callback?.Invoke();
    }
}
