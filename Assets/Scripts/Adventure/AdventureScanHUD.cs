using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bounded scan markers and a circular key/cooldown indicator.</summary>
[DisallowMultipleComponent]
public sealed class AdventureScanHUD : MonoBehaviour
{
    sealed class Marker
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image image;
        public AdventureScanDiamond diamond;
        public TMP_Text label;
        public AdventureScanTarget target;
        public float expiresAt, startedAt;
        public int lastDistance = -1, lastFlags = -1;
        public string lastName;
    }

    readonly List<Marker> markers = new List<Marker>();
    readonly Stack<Marker> pool = new Stack<Marker>();
    AdventureScanSettings settings;
    Canvas canvas;
    RectTransform canvasRect, cooldownRoot;
    AdventureScanCooldownGraphic cooldownRing;
    TMP_Text cooldownLabel;
    string lastCooldownText;

    public void Initialize(AdventureScanSettings scanSettings)
    {
        settings = scanSettings;
        if (canvas != null || settings == null) return;
        var root = new GameObject("Exploration Scan HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.layer = LayerMask.NameToLayer("UI");
        // Keep the overlay independent from the player's scale and movement.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasRect = root.GetComponent<RectTransform>();
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        cooldownRoot = CreateRect("Scan Cooldown Circle", canvasRect, new Vector2(1f, 0f), new Vector2(1f, 0f));
        cooldownRoot.pivot = new Vector2(1f, 0f);
        cooldownRoot.anchoredPosition = settings.cooldownPosition;
        cooldownRoot.sizeDelta = settings.cooldownSize;
        var ring = CreateRect("Cooldown Ring", cooldownRoot, Vector2.zero, Vector2.one);
        Stretch(ring, Vector2.zero, Vector2.zero);
        // Register the renderer before the custom Graphic's OnEnable/rebuild.
        ring.gameObject.AddComponent<CanvasRenderer>();
        cooldownRing = ring.gameObject.AddComponent<AdventureScanCooldownGraphic>();
        cooldownRing.raycastTarget = false;
        cooldownRing.color = Color.white;
        cooldownLabel = CreateLabel("Scan Key", cooldownRoot, settings.cooldownKeyFontSize);
        Stretch(cooldownLabel.rectTransform, Vector2.zero, Vector2.zero);
        cooldownLabel.alignment = TextAlignmentOptions.Center;
        cooldownLabel.fontWeight = FontWeight.Medium;
        canvas.gameObject.SetActive(false);
    }

    public void RevealTarget(AdventureScanTarget target, float expiresAt)
    {
        if (settings == null || canvas == null || target == null || !target.IsAvailable) return;
        foreach (Marker existing in markers)
        {
            if (existing.target != target) continue;
            existing.expiresAt = Mathf.Max(existing.expiresAt, expiresAt);
            return;
        }
        if (markers.Count >= Mathf.Max(1, settings.maxDetectedTargets))
        {
            int earliest = 0;
            for (int i = 1; i < markers.Count; i++)
                if (markers[i].expiresAt < markers[earliest].expiresAt) earliest = i;
            ReleaseAt(earliest);
        }
        Marker marker = pool.Count > 0 ? pool.Pop() : CreateMarker();
        marker.target = target;
        marker.expiresAt = expiresAt;
        marker.startedAt = Time.unscaledTime;
        marker.lastDistance = marker.lastFlags = -1;
        marker.lastName = null;
        Sprite sprite = target.markerIcon != null ? target.markerIcon : settings.markerSprite;
        marker.image.sprite = sprite;
        marker.image.gameObject.SetActive(sprite != null);
        marker.diamond.gameObject.SetActive(sprite == null);
        marker.image.color = marker.diamond.color = settings.markerColor;
        marker.root.sizeDelta = Vector2.one * settings.markerSize;
        marker.root.gameObject.SetActive(false);
        markers.Add(marker);
    }

    public void Tick(Transform player, Camera camera, bool visible, float cooldownRemaining)
    {
        if (settings == null || canvas == null) return;
        float now = Time.unscaledTime;
        for (int i = markers.Count - 1; i >= 0; i--)
        {
            Marker marker = markers[i];
            if (marker.target == null || !marker.target.IsAvailable || now >= marker.expiresAt)
                ReleaseAt(i);
        }
        bool show = visible && settings.showHud && player != null && camera != null;
        if (canvas.gameObject.activeSelf != show)
        {
            canvas.gameObject.SetActive(show);
            if (show)
            {
                cooldownRing.SetAllDirty();
                Canvas.ForceUpdateCanvases();
            }
        }
        if (!show) return;

        cooldownRoot.anchoredPosition = settings.cooldownPosition;
        cooldownRoot.sizeDelta = settings.cooldownSize;
        string cooldownText = settings.scanKey.ToString().ToUpperInvariant();
        if (lastCooldownText != cooldownText)
        {
            cooldownLabel.text = cooldownText;
            lastCooldownText = cooldownText;
        }
        float ready = settings.cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(cooldownRemaining / settings.cooldown);
        bool cooling = cooldownRemaining > 0f;
        cooldownLabel.color = new Color(1f, 1f, 1f, cooling ? .48f : 1f);
        cooldownLabel.fontSize = settings.cooldownKeyFontSize;
        cooldownRing.SetState(ready, cooling, settings.markerColor,
            settings.cooldownRingThickness, settings.cooldownGlowWidth);

        foreach (Marker marker in markers)
        {
            Vector3 worldPosition = marker.target.MarkerPosition;
            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            bool inView = settings.showMarkers && marker.target.showMarker && viewport.z > 0f &&
                viewport.x >= .015f && viewport.x <= .985f && viewport.y >= .015f && viewport.y <= .985f;
            if (marker.root.gameObject.activeSelf != inView) marker.root.gameObject.SetActive(inView);
            if (!inView) continue;
            Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 local))
                marker.root.anchoredPosition = local;
            float fade = Mathf.Clamp01((marker.expiresAt - now) / Mathf.Max(.01f, settings.fadeDuration));
            marker.group.alpha = fade;
            float appear = Mathf.Clamp01((now - marker.startedAt) / .18f);
            marker.root.localScale = Vector3.one * Mathf.Lerp(1.18f, 1f, appear);
            int distance = Mathf.RoundToInt(Vector3.Distance(player.position, marker.target.ScanPosition));
            int flags = (settings.showTargetNames ? 1 : 0) | (settings.showDistances ? 2 : 0);
            string name = marker.target.DisplayName;
            if (marker.lastDistance == distance && marker.lastFlags == flags && marker.lastName == name) continue;
            marker.lastDistance = distance; marker.lastFlags = flags; marker.lastName = name;
            marker.label.gameObject.SetActive(flags != 0);
            marker.label.text = flags == 3 ? name + "\n" + distance + " m"
                : flags == 1 ? name : flags == 2 ? distance + " m" : "";
        }
    }

    public void Clear()
    {
        for (int i = markers.Count - 1; i >= 0; i--) ReleaseAt(i);
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    public void RemoveTarget(AdventureScanTarget target)
    {
        for (int i = markers.Count - 1; i >= 0; i--)
            if (markers[i].target == target) ReleaseAt(i);
    }

    void ReleaseAt(int index)
    {
        Marker marker = markers[index];
        marker.target = null;
        marker.root.gameObject.SetActive(false);
        markers.RemoveAt(index);
        pool.Push(marker);
    }

    Marker CreateMarker()
    {
        var marker = new Marker();
        marker.root = CreateRect("Scan Target Marker", canvasRect, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
        marker.group = marker.root.gameObject.AddComponent<CanvasGroup>();
        marker.group.interactable = marker.group.blocksRaycasts = false;
        var imageRect = CreateRect("Icon", marker.root, Vector2.zero, Vector2.one);
        Stretch(imageRect, Vector2.zero, Vector2.zero);
        marker.image = imageRect.gameObject.AddComponent<Image>();
        marker.image.preserveAspect = true; marker.image.raycastTarget = false;
        var diamondRect = CreateRect("Fallback Diamond", marker.root, Vector2.zero, Vector2.one);
        Stretch(diamondRect, Vector2.zero, Vector2.zero);
        marker.diamond = diamondRect.gameObject.AddComponent<AdventureScanDiamond>();
        marker.diamond.raycastTarget = false;
        marker.label = CreateLabel("Name and Distance", marker.root, 15f);
        marker.label.rectTransform.anchorMin = marker.label.rectTransform.anchorMax = new Vector2(.5f, 0f);
        marker.label.rectTransform.pivot = new Vector2(.5f, 1f);
        marker.label.rectTransform.anchoredPosition = new Vector2(0f, -7f);
        marker.label.rectTransform.sizeDelta = new Vector2(200f, 44f);
        marker.label.alignment = TextAlignmentOptions.Top;
        return marker;
    }

    static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    static TMP_Text CreateLabel(string name, Transform parent, float size)
    {
        var rect = CreateRect(name, parent, Vector2.zero, Vector2.one);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size; text.fontWeight = FontWeight.Light; text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = min; rect.offsetMax = max;
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        markers.Clear(); pool.Clear();
    }
}

/// <summary>A small outline diamond that does not depend on a font glyph or texture.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AdventureScanDiamond : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * .45f;
        Vector2 a = center + Vector2.up * radius;
        Vector2 b = center + Vector2.right * radius;
        Vector2 c = center + Vector2.down * radius;
        Vector2 d = center + Vector2.left * radius;
        AddLine(vh, a, b); AddLine(vh, b, c); AddLine(vh, c, d); AddLine(vh, d, a);
    }

    void AddLine(VertexHelper vh, Vector2 from, Vector2 to)
    {
        Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * .85f;
        int start = vh.currentVertCount;
        vh.AddVert(from - normal, color, Vector2.zero); vh.AddVert(from + normal, color, Vector2.zero);
        vh.AddVert(to + normal, color, Vector2.zero); vh.AddVert(to - normal, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
