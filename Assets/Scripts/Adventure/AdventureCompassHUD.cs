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
    static readonly string[] DirectionNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    readonly List<CompassMark> ticks = new List<CompassMark>(360 / TickStep);
    readonly CompassMark[] directions = new CompassMark[8];
    RectTransform strip;
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

        var go = new GameObject("Horizontal Compass HUD", typeof(RectTransform));
        strip = (RectTransform)go.transform;
        strip.SetParent(hud, false);
        strip.anchorMin = strip.anchorMax = new Vector2(.5f, 1f);
        strip.pivot = new Vector2(.5f, 1f);
        strip.anchoredPosition = new Vector2(0f, -14f);
        strip.sizeDelta = new Vector2(720f, 54f);

        // A single center marker and sparse ticks keep the HUD readable without a panel,
        // border, or opaque backing image.
        CreateText("Heading Marker", "│", 19f, new Color(1f, 1f, 1f, .95f),
            new Vector2(0f, 17f), new Vector2(20f, 24f));

        for (int bearing = 0; bearing < 360; bearing += TickStep)
        {
            bool major = bearing % 15 == 0;
            var rect = CreateText("Tick " + bearing, major ? "│" : "·", major ? 15f : 12f,
                new Color(1f, 1f, 1f, major ? .78f : .48f), new Vector2(0f, 10f),
                new Vector2(16f, major ? 19f : 14f));
            ticks.Add(new CompassMark { rect = rect, bearing = bearing });
        }

        for (int i = 0; i < directions.Length; i++)
        {
            var rect = CreateText("Direction " + DirectionNames[i], DirectionNames[i], 17f,
                new Color(1f, 1f, 1f, .92f), new Vector2(0f, -7f), new Vector2(42f, 24f));
            directions[i] = new CompassMark { rect = rect, bearing = i * 45f };
        }

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
        foreach (var tick in ticks) Position(tick, heading, 10f);
        foreach (var direction in directions) Position(direction, heading, -7f);
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
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.fontWeight = FontWeight.Light;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return rect;
    }
}
