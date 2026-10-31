using System;
using TMPro;
using UnityEngine;

public static class GameLocalization
{
    const string LanguagePreference = "Adventure.Language";
    static GameLocalizationTable table;
    static bool initialized;
    static GameLanguage language;

    public static event Action LanguageChanged;
    public static GameLanguage CurrentLanguage { get { EnsureInitialized(); return language; } }
    public static bool IsEnglish => CurrentLanguage == GameLanguage.English;
    public static string LanguageButtonLabel => IsEnglish ? "Language: English" : "Ngôn ngữ: Tiếng Việt";

    static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        table = Resources.Load<GameLocalizationTable>("Adventure/LocalizationTable");
        int fallback = table != null ? (int)table.defaultLanguage : (int)GameLanguage.Vietnamese;
        language = (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(LanguagePreference, fallback), 0, 1);
    }

    public static string Translate(string source)
    {
        EnsureInitialized();
        return table != null ? table.Translate(source, language) : source;
    }

    public static void ToggleLanguage() => SetLanguage(IsEnglish ? GameLanguage.Vietnamese : GameLanguage.English);

    public static void SetLanguage(GameLanguage value)
    {
        EnsureInitialized();
        if (language == value) return;
        language = value;
        PlayerPrefs.SetInt(LanguagePreference, (int)language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    public static void SetText(TMP_Text target, string source)
    {
        if (target == null) return;
        GameUITextColor.Apply(target);
        var binding = target.GetComponent<LocalizedGameText>();
        if (binding == null) binding = target.gameObject.AddComponent<LocalizedGameText>();
        binding.SetSource(source);
    }

    public static void BindHierarchy(GameObject root)
    {
        if (root == null) return;
        foreach (var target in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (target == null) continue;
            var binding = target.GetComponent<LocalizedGameText>();
            if (binding == null) target.gameObject.AddComponent<LocalizedGameText>();
            else binding.Refresh();
        }
    }
}
