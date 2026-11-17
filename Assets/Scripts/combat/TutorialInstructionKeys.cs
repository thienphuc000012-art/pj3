using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

/// <summary>Formats translated instruction text and draws non-interactive frames around key names.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
public sealed class TutorialInstructionKeys : MonoBehaviour
{
    public Color keyColor = new Color(1f, .84f, .42f, 1f);
    public Color frameColor = new Color(.95f, .75f, .35f, .9f);
    public Vector2 padding = new Vector2(4f, 0f);
    [SerializeField] TutorialKeyFrameGraphic frames;
    TMP_Text label;
    string previousText;
    Vector2 previousSize;
    float previousFontSize;
    readonly List<Rect> boxes = new List<Rect>();
    static readonly Regex Tags = new Regex("(<[^>]+>)", RegexOptions.Compiled);
    static readonly Regex Keys = new Regex(@"(?<![\p{L}\p{N}_])(?:W\s+A\s+S\s+D|WASD|Space|SPACE|Shift|SHIFT|Esc|ESC|Enter|ENTER|Tab|TAB|Q|P|E|W|S|D|F|G)(?![\p{L}\p{N}_])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public string Format(string value)
    {
        string Key(string key) => "<space=4><color=#" + ColorUtility.ToHtmlStringRGB(keyColor) + "><b><link=\"tutorial-key\">" + key + "</link></b></color><space=4>";
        var parts = Tags.Split(value ?? string.Empty);
        for (int i = 0; i < parts.Length; i++)
            if (!parts[i].StartsWith("<")) parts[i] = Keys.Replace(parts[i], match =>
                match.Value == "WASD" || Regex.IsMatch(match.Value, @"^W\s+A\s+S\s+D$")
                ? Key("W") + " " + Key("A") + " " + Key("S") + " " + Key("D") : Key(match.Value));
        return string.Concat(parts);
    }
    public void EnsureFrames()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (frames == null) frames = transform.Find("Key Frames")?.GetComponent<TutorialKeyFrameGraphic>();
        if (frames == null)
        {
            var root = new GameObject("Key Frames", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialKeyFrameGraphic)); root.layer = gameObject.layer;
            root.transform.SetParent(transform, false); var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            frames = root.GetComponent<TutorialKeyFrameGraphic>();
        }
        frames.raycastTarget = false; frames.color = frameColor;
        frames.rectTransform.pivot = label.rectTransform.pivot;
        frames.rectTransform.offsetMin = frames.rectTransform.offsetMax = Vector2.zero;
    }
    public void RefreshFrames()
    {
        EnsureFrames();
        if (label == null || !label.gameObject.activeInHierarchy) return;
        var size = label.rectTransform.rect.size;
        if (!label.havePropertiesChanged && previousText == label.text && previousSize == size && Mathf.Approximately(previousFontSize, label.fontSize)) return;
        label.ForceMeshUpdate(); boxes.Clear();
        var info = label.textInfo;
        for (int i = 0; i < info.linkCount; i++)
        {
            var link = info.linkInfo[i]; if (link.GetLinkID() != "tutorial-key") continue;
            float left = float.PositiveInfinity, right = float.NegativeInfinity, bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            for (int c = link.linkTextfirstCharacterIndex; c < link.linkTextfirstCharacterIndex + link.linkTextLength && c < info.characterCount; c++)
            {
                var character = info.characterInfo[c]; if (!character.isVisible) continue;
                left = Mathf.Min(left, character.bottomLeft.x); right = Mathf.Max(right, character.topRight.x);
                bottom = Mathf.Min(bottom, character.descender); top = Mathf.Max(top, character.ascender);
            }
            if (right > left) boxes.Add(Rect.MinMaxRect(left - padding.x, bottom - padding.y, right + padding.x, top + padding.y));
        }
        frames.SetBoxes(boxes); previousText = label.text; previousSize = size; previousFontSize = label.fontSize;
    }
}
