using UnityEngine;
using UnityEngine.UI;

/// <summary>Covers destructive scene staging before the Loading canvas has its first frame.</summary>
public static class SceneTransitionCover
{
    static GameObject cover;
    public static bool IsVisible => cover != null && cover.activeSelf;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { cover = null; }
    public static void Show()
    {
        if (cover != null) { cover.SetActive(true); return; }
        cover = new GameObject("Scene Transition Cover", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        if (Application.isPlaying) Object.DontDestroyOnLoad(cover);
        var canvas = cover.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var image = new GameObject("Cover", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(cover.transform, false);
        image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.color = new Color(.004f, .004f, .015f, 1); image.raycastTarget = true;
    }
    public static void Hide()
    {
        if (cover == null) return;
        cover.SetActive(false);
        if (Application.isPlaying) Object.Destroy(cover); else Object.DestroyImmediate(cover);
        cover = null;
    }
}
