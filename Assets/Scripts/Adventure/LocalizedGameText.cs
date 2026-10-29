using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalizedGameText : MonoBehaviour
{
    TMP_Text target;
    string source;
    bool hasSource;

    void Awake()
    {
        target = GetComponent<TMP_Text>();
        if (!hasSource && target != null) { source = target.text; hasSource = true; }
    }

    void OnEnable()
    {
        GameLocalization.LanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable() => GameLocalization.LanguageChanged -= Refresh;

    public void SetSource(string value)
    {
        if (target == null) target = GetComponent<TMP_Text>();
        value ??= string.Empty;
        if (hasSource && string.Equals(source, value, StringComparison.Ordinal))
        {
            Refresh();
            return;
        }
        source = value;
        hasSource = true;
        Refresh();
    }

    public void Refresh()
    {
        if (target == null) target = GetComponent<TMP_Text>();
        if (target == null) return;
        if (!hasSource) { source = target.text; hasSource = true; }
        string translated = GameLocalization.Translate(source);
        if (target.text != translated) target.text = translated;
    }
}