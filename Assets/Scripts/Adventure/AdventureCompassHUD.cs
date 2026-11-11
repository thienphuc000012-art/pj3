using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates the lightweight, camera-facing compass strip on the exploration HUD.</summary>
[DisallowMultipleComponent]
public sealed class AdventureCompassHUD : MonoBehaviour
{
    const float VisibleHalfAngle = 70f;
    const int TickStep = 5;
    static readonly string[] DirectionNames = { "B", "ĐB", "Đ", "ĐN", "N", "TN", "T", "TB" };

    readonly List<CompassMark> ticks = new List<CompassMark>(360 / TickStep);
    readonly CompassMark[] directions = new CompassMark[8];
    RectTransform strip;
    RectTransform objectiveIcon;
    RectTransform objectiveDistance;
    TMP_FontAsset font;
    float stripWidth;

    sealed class CompassMark
    {
        public RectTransform rect;
        public float bearing;
    }

    void Awake()
    {
        var hud = transform.Find("HUD");
        if (hud == null) { enabled = false; return; }

        var canvasRoot = GetComponent<AdventureCanvasRoot>();
        font = canvasRoot != null && canvasRoot.defaultFont != null
            ? canvasRoot.defaultFont
            : TMP_Settings.defaultFontAsset;

        var go = new GameObject("Horizontal Compass HUD", typeof(RectTransform), typeof(PreserveAuthoredUI));
        strip = (RectTransform)go.transform;
        strip.SetParent(hud, false);
        strip.anchorMin = strip.anchorMax = new Vector2(.5f, 1f);
        strip.pivot = new Vector2(.5f, 1f);
        strip.anchoredPosition = new Vector2(0f, -14f);
        strip.sizeDelta = new Vector2(720f, 80f);

        CreateBaseline();

        for (int bearing = 0; bearing < 360; bearing += TickStep)
        {
            bool major = bearing % 15 == 0;
            var rect = CreateLine("Tick " + bearing, strip, Vector2.zero, Vector2.up * (major ? 10f : 5f),
                new Color(1f, 1f, 1f, major ? .6f : .35f));
            ticks.Add(new CompassMark { rect = rect, bearing = bearing });
        }

        for (int i = 0; i < directions.Length; i++)
        {
            var rect = CreateText("Direction " + DirectionNames[i], DirectionNames[i], 17f,
                new Color(1f, 1f, 1f, .92f), new Vector2(0f, 15f), new Vector2(42f, 24f));
            directions[i] = new CompassMark { rect = rect, bearing = i * 45f };
        }

        objectiveIcon = CreateQuestMarker();
        objectiveDistance = CreateText("Tracked Quest Distance", "", 20f, new Color(1f, 1f, 1f, .95f),
            new Vector2(0f, 30f), new Vector2(92f, 26f));
        objectiveIcon.gameObject.SetActive(false);
        objectiveDistance.gameObject.SetActive(false);

        ResizeStrip();
    }

    void LateUpdate()
    {
        var session = CampaignSession.Instance;
        bool visible = session != null && session.OnMap && session.MapPlayer != null;
        if (strip == null) return;
        if (strip.gameObject.activeSelf != visible) strip.gameObject.SetActive(visible);
        if (!visible) return;

        ResizeStrip();
        float heading = ReadHeading(session.MapPlayer.transform);
        foreach (var tick in ticks) Position(tick, heading, 5f);
        foreach (var direction in directions) Position(direction, heading, 15f);
        UpdateTrackedObjective(session, heading);
    }

    void UpdateTrackedObjective(CampaignSession session, float heading)
    {
        Vector3 targetPosition = Vector3.zero;
        bool visible = session.MapPlayer != null && session.TryGetTrackedQuestPosition(out targetPosition);
        if (objectiveIcon.gameObject.activeSelf != visible) objectiveIcon.gameObject.SetActive(visible);
        if (objectiveDistance.gameObject.activeSelf != visible) objectiveDistance.gameObject.SetActive(visible);
        if (!visible) return;

        Vector3 from = session.MapPlayer.transform.position;
        Vector3 direction = Vector3.ProjectOnPlane(targetPosition - from, Vector3.up);
        float bearing = direction.sqrMagnitude > .0001f
            ? Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f)
            : heading;
        float relative = Mathf.Clamp(Mathf.DeltaAngle(heading, bearing), -VisibleHalfAngle, VisibleHalfAngle);
        float markerX = relative / VisibleHalfAngle * stripWidth * .5f;
        float edgePadding = 48f;
        markerX = Mathf.Clamp(markerX, -stripWidth * .5f + edgePadding, stripWidth * .5f - edgePadding);
        objectiveIcon.anchoredPosition = new Vector2(markerX, 0f);
        objectiveDistance.anchoredPosition = new Vector2(markerX, 30f);
        direction.y = 0f;
        GameLocalization.SetText(objectiveDistance.GetComponent<TMP_Text>(), Mathf.RoundToInt(direction.magnitude) + " m");
    }

    float ReadHeading(Transform player)
    {
        Transform facing = Camera.main != null ? Camera.main.transform : player;
        Vector3 forward = Vector3.ProjectOnPlane(facing.forward, Vector3.up);
        if (forward.sqrMagnitude < .0001f) forward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
        return Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
    }

    void Position(CompassMark mark, float heading, float y)
    {
        float relative = Mathf.DeltaAngle(heading, mark.bearing);
        bool inView = Mathf.Abs(relative) <= VisibleHalfAngle;
        if (mark.rect.gameObject.activeSelf != inView) mark.rect.gameObject.SetActive(inView);
        if (!inView) return;
        mark.rect.anchoredPosition = new Vector2(relative / VisibleHalfAngle * stripWidth * .5f, y);
    }

    void ResizeStrip()
    {
        if (strip == null) return;
        var parent = strip.parent as RectTransform;
        float availableWidth = parent != null ? parent.rect.width - 64f : 720f;
        stripWidth = Mathf.Clamp(Mathf.Min(720f, availableWidth), 280f, 720f);
        if (Mathf.Abs(strip.sizeDelta.x - stripWidth) > .5f)
            strip.sizeDelta = new Vector2(stripWidth, strip.sizeDelta.y);
    }

    void CreateBaseline()
    {
        var white = new Color(1f, 1f, 1f, .65f);
        var left = CreateLine("Compass Line Left", strip, Vector2.zero, Vector2.right, white);
        left.anchorMin = new Vector2(0f, .5f);
        left.anchorMax = new Vector2(.5f, .5f);
        left.offsetMin = new Vector2(0f, -.6f);
        left.offsetMax = new Vector2(-8f, .6f);
        var right = CreateLine("Compass Line Right", strip, Vector2.zero, Vector2.right, white);
        right.anchorMin = new Vector2(.5f, .5f);
        right.anchorMax = new Vector2(1f, .5f);
        right.offsetMin = new Vector2(8f, -.6f);
        right.offsetMax = new Vector2(0f, .6f);
        CreateLine("Center Notch Left", strip, new Vector2(-8f, 0f), new Vector2(0f, -5f), white);
        CreateLine("Center Notch Right", strip, new Vector2(0f, -5f), new Vector2(8f, 0f), white);
        var heading = new GameObject("Heading Marker", typeof(RectTransform));
        var rect = (RectTransform)heading.transform;
        rect.SetParent(strip, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(8f, 8f);
        CreatePolygon(rect, 4, 3.5f, new Color(1f, 1f, 1f, .9f));
    }

    RectTransform CreateQuestMarker()
    {
        var go = new GameObject("Tracked Quest Marker", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(strip, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(26f, 26f);
        var gold = new Color(.98f, .8f, .48f, 1f);
        CreatePolygon(rect, 6, 11f, gold);
        CreatePolygon(rect, 4, 4f, gold);
        return rect;
    }

    void CreatePolygon(RectTransform parent, int sides, float radius, Color color)
    {
        for (int side = 0; side < sides; side++)
        {
            float a = side * Mathf.PI * 2f / sides;
            float b = (side + 1) * Mathf.PI * 2f / sides;
            CreateLine("Edge " + (side + 1), parent,
                new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, color);
        }
    }

    RectTransform CreateLine(string objectName, Transform parent, Vector2 start, Vector2 end, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = (start + end) * .5f;
        rect.sizeDelta = new Vector2(Vector2.Distance(start, end), 1.5f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    RectTransform CreateText(string objectName, string value, float size, Color color, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(strip, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        var text = go.GetComponent<TextMeshProUGUI>();
        GameLocalization.SetText(text, value);
        text.font = font;
        text.fontSize = size;
        text.fontWeight = FontWeight.Light;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return rect;
    }
}
