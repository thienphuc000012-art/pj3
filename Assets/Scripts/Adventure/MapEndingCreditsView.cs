using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

public sealed class MapEndingCreditsView : MonoBehaviour
{
    static readonly Color Gold = new Color(.91f, .75f, .51f);
    static readonly Color Ivory = new Color(.93f, .9f, .84f);
    TMP_Text heading, body;
    RectTransform rollArea;
    CanvasGroup textFade;
    MapEndingCreditsClip currentClip;
    float rollHeight;

    public IEnumerator Play(TimelineAsset timeline)
    {
        BuildCanvas();
        var director = gameObject.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.playableAsset = timeline;
        foreach (var track in timeline.GetOutputTracks())
            if (track is MapEndingCreditsTrack) director.SetGenericBinding(track, this);
        director.Play();
        double duration = timeline.duration;
        for (double time = 0; time < duration; time += Time.unscaledDeltaTime)
        {
            director.time = time;
            director.Evaluate();
            yield return null;
        }
        HideText();
        director.Stop();
        yield return null;
    }

    void BuildCanvas()
    {
        var overlay = new GameObject("Credits Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32700;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        var background = new GameObject("Black", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(overlay.transform, false);
        Stretch((RectTransform)background.transform, Vector2.zero, Vector2.one);
        var image = background.GetComponent<Image>();
        image.color = new Color(.019f, .016f, .015f);
        image.raycastTarget = false;

        var area = new GameObject("Credits Text", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup));
        area.transform.SetParent(overlay.transform, false);
        rollArea = (RectTransform)area.transform;
        Stretch(rollArea, new Vector2(.08f, .065f), new Vector2(.92f, .935f));
        textFade = area.GetComponent<CanvasGroup>();
        textFade.blocksRaycasts = false;
        textFade.interactable = false;
        heading = AddLabel("Title", 72, Gold);
        Stretch(heading.rectTransform, new Vector2(0f, .43f), new Vector2(1f, .72f));
        body = AddLabel("Body", 37, Ivory);
        HideText();
    }

    TMP_Text AddLabel(string label, float size, Color color)
    {
        var item = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.transform.SetParent(rollArea, false);
        var text = item.GetComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("Adventure/UI/OptionsFont") ?? TMP_Settings.defaultFontAsset;
        text.color = color;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public void Show(MapEndingCreditsClip clip, float time)
    {
        if (textFade == null || clip == null) return;
        if (currentClip != clip)
        {
            currentClip = clip;
            heading.text = clip.title;
            heading.gameObject.SetActive(!clip.scroll && !string.IsNullOrEmpty(clip.title));
            body.text = clip.body;
            body.alignment = clip.scroll ? TextAlignmentOptions.Top : TextAlignmentOptions.Center;
            if (clip.scroll)
            {
                Canvas.ForceUpdateCanvases();
                rollHeight = body.GetPreferredValues(clip.body, rollArea.rect.width, Mathf.Infinity).y + 40f;
                var rect = body.rectTransform;
                rect.pivot = new Vector2(.5f, 1f);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.right;
                rect.sizeDelta = new Vector2(0f, rollHeight);
            }
            else
            {
                body.rectTransform.pivot = new Vector2(.5f, .5f);
                Stretch(body.rectTransform, new Vector2(0f, .28f), new Vector2(1f, .49f));
            }
        }
        if (clip.scroll)
            body.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-20f, rollArea.rect.height + rollHeight, time));
        float fade = clip.scroll ? .035f : .17f;
        textFade.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(time, 1f - time) / fade));
    }

    public void HideText()
    {
        if (textFade != null) textFade.alpha = 0f;
    }
}
