using TMPro;
using UnityEngine;

/// <summary>Keeps UI text white when other presenters update their contents.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(10000)]
public sealed class GameUITextColor : MonoBehaviour
{
    TMP_Text label;
    DialogueChoiceTextHover choiceHover;

    public static GameUITextColor Apply(TMP_Text text)
    {
        if (text == null) return null;
        var style = text.GetComponent<GameUITextColor>();
        if (style == null) style = text.gameObject.AddComponent<GameUITextColor>();
        style.label = text;
        style.Refresh();
        return style;
    }

    public void SetChoiceHover(DialogueChoiceTextHover source)
    {
        choiceHover = source;
        Refresh();
    }

    void LateUpdate() => Refresh();

    void Refresh()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null) return;
        Color color = choiceHover != null ? choiceHover.TextColor : Color.white;
        color.a = label.color.a;
        if (label.color != color) label.color = color;
        if (label.enableVertexGradient) label.enableVertexGradient = false;
    }
}
