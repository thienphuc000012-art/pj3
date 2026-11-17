using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalizedGameText : MonoBehaviour
{
    TMP_Text target;
    string source;
    bool hasSource;
    ActionData actionSource;
    string actionSuffix;
    bool hasActionSource;
    int actionRevision;
    GameLanguage actionLanguage;

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
        hasActionSource = false; actionSource = null; actionSuffix = null;
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

    public void SetActionDescription(ActionData action, string suffix)
    {
        int revision = CombatActionDescription.Revision(action);
        if (hasActionSource && actionSource == action && actionSuffix == suffix && actionRevision == revision && actionLanguage == GameLocalization.CurrentLanguage) return;
        hasActionSource = true; actionSource = action; actionSuffix = suffix;
        Refresh();
    }

    void LateUpdate()
    {
        if (hasActionSource && actionRevision != CombatActionDescription.Revision(actionSource)) Refresh();
    }

    public void Refresh()
    {
        if (target == null) target = GetComponent<TMP_Text>();
        if (target == null) return;
        if (hasActionSource)
        {
            actionLanguage = GameLocalization.CurrentLanguage;
            actionRevision = CombatActionDescription.Revision(actionSource);
            string value = CombatActionDescription.Build(actionSource, actionLanguage);
            if (!string.IsNullOrEmpty(actionSuffix)) value += "\n" + GameLocalization.Translate(actionSuffix.TrimStart('\r', '\n'));
            if (target.text != value) target.SetText(value);
            return;
        }
        if (!hasSource) { source = target.text; hasSource = true; }
        string translated = GameLocalization.Translate(source);
        var keys = GetComponent<TutorialInstructionKeys>();
        if (keys != null) translated = keys.Format(translated);
        if (target.text != translated) target.text = translated;
    }
}
