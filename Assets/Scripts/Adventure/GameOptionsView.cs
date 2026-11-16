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
}

/// <summary>Shared, authored Options panel used by both Title and the exploration menu.</summary>
public sealed class GameOptionsView : MonoBehaviour
{
    public GameVolumeRow[] rows;
    public Button languageButton, backButton;
    public TMP_Text languageValue;
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
            row.slider.onValueChanged.AddListener(value => GameAudio.SetVolume(captured.bus, value));
            row.decrease.onClick.AddListener(() => GameAudio.SetVolume(captured.bus, GameAudio.Volume(captured.bus) - .05f));
            row.increase.onClick.AddListener(() => GameAudio.SetVolume(captured.bus, GameAudio.Volume(captured.bus) + .05f));
        }
        languageButton.onClick.AddListener(GameLocalization.ToggleLanguage);
        backButton.onClick.AddListener(Close);
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
            row.slider.SetValueWithoutNotify(value);
            GameLocalization.SetText(row.percentage, Mathf.RoundToInt(value * 100) + "%");
        }
        GameLocalization.SetText(languageValue, GameLocalization.IsEnglish ? "English" : "Tiếng Việt");
    }
    public void Close()
    {
        if (!IsOpen) return;
        gameObject.SetActive(false);
        if (returnFocus != null && returnFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(returnFocus);
        var callback = closed; closed = null; callback?.Invoke();
    }
}
