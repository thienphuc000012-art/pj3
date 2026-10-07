using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation only: hidden units keep their animation and combat logic.</summary>
[DisallowMultipleComponent]
public sealed class UltimateCinematic : MonoBehaviour
{
    [Range(.05f, .25f)] public float barHeight = .12f;
    [Min(0f)] public float enterDuration = .6f;
    [Min(0f)] public float exitDuration = .35f;
    private RectTransform topBar, bottomBar;
    private float barProgress;
    private readonly Dictionary<Canvas, bool> canvases = new Dictionary<Canvas, bool>();
    private readonly Dictionary<Renderer, bool> renderers = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Light, bool> lights = new Dictionary<Light, bool>();
    private readonly List<BattleUnit> hiddenUnits = new List<BattleUnit>();
    private Canvas letterbox;
    private bool running;
    private int generation;

    public int Begin(BattleUnit caster, BattleUnit target, IEnumerable<BattleUnit> units)
    {
        End();
        generation++;
        foreach (var unit in units)
            if (unit != null && unit != caster && unit != target && !hiddenUnits.Contains(unit)) hiddenUnits.Add(unit);
        EnsureLetterbox();
        letterbox.gameObject.SetActive(true);
        SetBarProgress(Application.isPlaying ? 0f : 1f);
        running = true;
        Canvas.willRenderCanvases += HidePresentation;
        HidePresentation();
        return generation;
    }

    private void EnsureLetterbox()
    {
        if (letterbox != null) return;
        var root = new GameObject("Ultimate Cinematic Letterbox", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        letterbox = root.GetComponent<Canvas>();
        letterbox.renderMode = RenderMode.ScreenSpaceOverlay;
        letterbox.overrideSorting = true;
        // Overlay is composed after camera post-processing, above screen slash VFX.
        int highest = int.MinValue;
        foreach (var layer in SortingLayer.layers)
            if (layer.value >= highest) { highest = layer.value; letterbox.sortingLayerID = layer.id; }
        letterbox.sortingOrder = short.MaxValue;
        var blocker = MakeImage(root.transform, "Block UI clicks", Color.clear);
        SetAnchors(blocker.rectTransform, Vector2.zero, Vector2.one);
        blocker.raycastTarget = true;
        var top = MakeImage(root.transform, "Top", Color.black);
        topBar = top.rectTransform;
        SetAnchors(top.rectTransform, new Vector2(0, 1f - barHeight), Vector2.one);
        var bottom = MakeImage(root.transform, "Bottom", Color.black);
        bottomBar = bottom.rectTransform;
        SetAnchors(bottom.rectTransform, Vector2.zero, new Vector2(1, barHeight));
    }

    private static Image MakeImage(Transform parent, string label, Color colour)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void LateUpdate() { HidePresentation(); }

    private void SetBarProgress(float progress)
    {
        barProgress = Mathf.Clamp01(progress);
        float height = Mathf.Clamp(barHeight, .05f, .25f) * barProgress;
        if (topBar != null) SetAnchors(topBar, new Vector2(0, 1f - height), Vector2.one);
        if (bottomBar != null) SetAnchors(bottomBar, Vector2.zero, new Vector2(1, height));
    }

    public IEnumerator Transition(bool entering, float minimumDuration = 0f)
    {
        int ticket = generation;
        float from = barProgress;
        float duration = Mathf.Max(minimumDuration, entering ? enterDuration : exitDuration);
        float elapsed = 0f;
        while (running && ticket == generation && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetBarProgress(Mathf.Lerp(from, entering ? 1f : 0f, t));
            yield return null;
        }
        if (running && ticket == generation) SetBarProgress(entering ? 1f : 0f);
    }

    private void HidePresentation()
    {
        if (!running) return;
        // Also catches damage numbers and canvases created after the first hit.
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (canvas == letterbox || canvas.GetComponentInChildren<ScreenSlashGraphic>(true) != null) continue;
            if (!canvases.ContainsKey(canvas)) canvases.Add(canvas, canvas.enabled);
            canvas.enabled = false;
        }
        foreach (var unit in hiddenUnits)
        {
            if (unit == null) continue;
            foreach (var renderer in unit.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderers.ContainsKey(renderer)) renderers.Add(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
            foreach (var light in unit.GetComponentsInChildren<Light>(true))
            {
                if (!lights.ContainsKey(light)) lights.Add(light, light.enabled);
                light.enabled = false;
            }
        }
    }

    public void End(int ticket) { if (ticket == generation) End(); }

    public void End()
    {
        running = false;
        Canvas.willRenderCanvases -= HidePresentation;
        foreach (var entry in canvases) if (entry.Key != null) entry.Key.enabled = entry.Value;
        foreach (var entry in renderers) if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
        foreach (var entry in lights) if (entry.Key != null) entry.Key.enabled = entry.Value;
        canvases.Clear(); renderers.Clear(); lights.Clear(); hiddenUnits.Clear();
        if (letterbox != null) letterbox.gameObject.SetActive(false);
    }

    private void OnDisable() { End(); }
    private void OnDestroy() { End(); }
}
