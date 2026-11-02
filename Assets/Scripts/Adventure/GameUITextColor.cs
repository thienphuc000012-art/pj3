using TMPro;
using UnityEngine;

/// <summary>Keeps shared UI text colors consistent with explicit selection styles.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(10000)]
public sealed class GameUITextColor : MonoBehaviour
{
    TMP_Text label;
    DialogueChoiceTextHover choiceHover;
    Color? colorOverride;

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

    public void SetColorOverride(Color? value)
    {
        colorOverride = value;
        Refresh();
    }

    void LateUpdate() => Refresh();

    void Refresh()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null) return;
        Color color = colorOverride ?? (choiceHover != null ? choiceHover.TextColor : Color.white);
        color.a = label.color.a;
        if (label.color != color) label.color = color;
        if (label.enableVertexGradient) label.enableVertexGradient = false;
    }
}
