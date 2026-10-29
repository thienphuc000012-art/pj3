using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public enum GameLanguage
{
    Vietnamese = 0,
    English = 1
}

[Serializable]
public sealed class GameLocalizationEntry
{
    [Tooltip("Tên gợi nhớ để bạn tìm dòng dịch trong Inspector.")]
    public string key;
    [Tooltip("Câu nguồn tiếng Việt. Giữ nguyên các chỗ đánh dấu như {0}, {1} để chèn dữ liệu động.")]
    [TextArea] public string vietnamese;
    [Tooltip("Bản dịch tiếng Anh; dùng đúng các dấu đánh dấu giống cột tiếng Việt.")]
    [TextArea] public string english;
}

[CreateAssetMenu(menuName = "Adventure/Localization Table", fileName = "LocalizationTable")]
public sealed class GameLocalizationTable : ScriptableObject
{
    public GameLanguage defaultLanguage = GameLanguage.Vietnamese;
    public List<GameLocalizationEntry> entries = new List<GameLocalizationEntry>();

    [NonSerialized] Dictionary<string, GameLocalizationEntry> exact;
    [NonSerialized] List<FormatEntry> formatted;

    sealed class FormatEntry
    {
        public GameLocalizationEntry entry;
        public Regex vietnamesePattern;
        public Regex englishPattern;
    }

    void OnEnable() => BuildIndex();

    void OnValidate() => BuildIndex();

    void BuildIndex()
    {
        exact = new Dictionary<string, GameLocalizationEntry>(StringComparer.Ordinal);
        formatted = new List<FormatEntry>();
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.vietnamese) || string.IsNullOrEmpty(entry.english)) continue;
            exact[entry.vietnamese] = entry;
            exact[entry.english] = entry;
            if (entry.vietnamese.Contains("{") || entry.english.Contains("{"))
                formatted.Add(new FormatEntry
                {
                    entry = entry,
                    vietnamesePattern = BuildPattern(entry.vietnamese),
                    englishPattern = BuildPattern(entry.english)
                });
        }
    }

    public string Translate(string source, GameLanguage language)
    {
        if (string.IsNullOrEmpty(source)) return source;
        if (exact == null || formatted == null) BuildIndex();
        if (exact.TryGetValue(source, out var item))
            return language == GameLanguage.English ? item.english : item.vietnamese;

        foreach (var template in formatted)
        {
            string to = language == GameLanguage.English ? template.entry.english : template.entry.vietnamese;
            Regex pattern = language == GameLanguage.English ? template.vietnamesePattern : template.englishPattern;
            var match = pattern.Match(source);
            if (!match.Success) continue;
            return Regex.Replace(to, @"\{(\d+)\}", token =>
            {
                int index = int.Parse(token.Groups[1].Value);
                return match.Groups["p" + index].Success ? match.Groups["p" + index].Value : string.Empty;
            });
        }
        return source;
    }

    static Regex BuildPattern(string template)
    {
        var output = new StringBuilder("^");
        var seen = new HashSet<int>();
        int position = 0;
        foreach (Match token in Regex.Matches(template, @"\{(\d+)\}"))
        {
            output.Append(Regex.Escape(template.Substring(position, token.Index - position)));
            int index = int.Parse(token.Groups[1].Value);
            output.Append(seen.Add(index) ? "(?<p" + index + ">.*?)" : "\\k<p" + index + ">");
            position = token.Index + token.Length;
        }
        output.Append(Regex.Escape(template.Substring(position))).Append("$");
        return new Regex(output.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}