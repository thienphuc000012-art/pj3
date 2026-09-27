using UnityEngine;
using UnityEngine.UI;

/// <summary>A lightweight, non-interactive overlay owned by the battle scene.</summary>
public sealed class ScreenSlashVfx : MonoBehaviour
{
    private GameObject overlay;
    private ScreenSlashGraphic graphic;

    public void Play(ActionData action, int hitIndex)
    {
        if (overlay == null)
        {
            overlay = new GameObject("Screen Sword Slash", typeof(RectTransform), typeof(Canvas));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var child = new GameObject("Luminous Cut", typeof(RectTransform), typeof(CanvasRenderer), typeof(ScreenSlashGraphic));
            child.transform.SetParent(overlay.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            graphic = child.GetComponent<ScreenSlashGraphic>();
            graphic.raycastTarget = false;
        }
        overlay.SetActive(true);
        graphic.Begin(action.screenSlashColor, action.screenSlashDuration,
            action.screenSlashWidth, hitIndex % 2 == 0 ? action.screenSlashAngle : -action.screenSlashAngle,
            action.screenSlashCount, action.screenSlashBurstTime, action.screenSlashFinisherScale,
            action.screenSplitHold, action.screenSplitRestoreStart);
        var split = GetComponent<ScreenSpaceCut>();
        if (action.screenSpaceSplit)
        {
            if (split == null) split = gameObject.AddComponent<ScreenSpaceCut>();
            split.Play(action, hitIndex % 2 == 0 ? action.screenSlashAngle : -action.screenSlashAngle);
        }
        else if (split != null) split.Stop();
    }

    private void OnDisable()
    {
        if (overlay != null) overlay.SetActive(false);
        var split = GetComponent<ScreenSpaceCut>();
        if (split != null) split.Stop();
    }
}

/// <summary>Tapered white core, coloured feathered glow, and short fracture streaks.</summary>
public sealed class ScreenSlashGraphic : MaskableGraphic
{
    private float age, duration, width, angle;
    private Color tint;
    private bool playing;
    private int cutCount;
    private float burstTime, finisherScale;
    private bool holdCuts;
    private float restoreStart;

    public void Begin(Color colour, float seconds, float pixels, float degrees,
        int count = 1, float burst = 0, float finisher = 2f, bool hold = false, float restore = .8f)
    {
        tint = colour; duration = Mathf.Max(.1f, seconds);
        width = Mathf.Max(1f, pixels); angle = degrees;
        cutCount = Mathf.Clamp(count, 1, 24);
        burstTime = cutCount == 1 ? 0 : Mathf.Clamp(burst, 0, duration * .75f);
        finisherScale = Mathf.Clamp(finisher, 1f, 3f);
        holdCuts = hold; restoreStart = Mathf.Clamp(restore, .5f, .95f);
        age = 0; playing = true;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!playing) return;
        age += Time.unscaledDeltaTime;
        if (age >= duration) playing = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!playing) return;
        Rect rect = rectTransform.rect;
        float life = duration - burstTime;
        for (int cut = 0; cut < cutCount; cut++)
        {
            float delay = cutCount == 1 ? 0 : burstTime * cut / (cutCount - 1);
            float localAge = age - delay;
            float cutLife = holdCuts ? duration - delay : life;
            if (localAge < 0 || localAge >= cutLife) continue;
            bool finalCut = cutCount > 1 && cut == cutCount - 1;
            // Deterministic spread: no gameplay Random state is consumed by cosmetic effects.
            float degrees = cutCount == 1 || finalCut ? angle : angle + cut * 137.508f;
            Vector2 offset = cutCount == 1 || finalCut ? Vector2.zero :
                new Vector2(Mathf.Sin(cut * 2.4f + .7f) * rect.width * .2f,
                    Mathf.Cos(cut * 1.7f + .3f) * rect.height * .22f);
            DrawCut(vh, rect, rect.center + offset, degrees, localAge, cutLife,
                finalCut ? finisherScale : cutCount == 1 ? 1f : .65f + (cut % 3) * .15f);
        }
    }

    private void DrawCut(VertexHelper vh, Rect rect, Vector2 centre, float degrees, float localAge, float life, float power)
    {
        float progress = Mathf.Clamp01(localAge / life);
        float reveal = Mathf.Clamp01(localAge / Mathf.Min(.05f, life * .2f));
        float fade = Mathf.Pow(1f - progress, 1.65f);
        if (holdCuts) fade = Mathf.Lerp(.38f, 1f, Mathf.Exp(-localAge * 12f)) * ScreenSpaceCut.Closure(age, duration, restoreStart);
        float scale = rect.height / 1080f;
        float radians = degrees * Mathf.Deg2Rad;
        Vector2 axis = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        Vector2 normal = new Vector2(-axis.y, axis.x);
        float length = rect.size.magnitude * .58f;
        Vector2 start = centre - axis * length;
        Vector2 end = Vector2.Lerp(start, centre + axis * length, reveal);
        float w = width * scale * power * Mathf.Lerp(1.3f, .35f, progress);
        // Restrained cold silver bloom with a sharp luminous edge.
        Ribbon(vh, start, end, normal, w * 4f, new Color(tint.r, tint.g, tint.b, tint.a * fade * .07f));
        Ribbon(vh, start, end, normal, w * 1.6f, new Color(tint.r, tint.g, tint.b, tint.a * fade * .4f));
        Ribbon(vh, start, end, normal, w * .32f, new Color(1f, .98f, .94f, tint.a * fade));
        for (int i = 0; i < 4; i++)
        {
            float sign = i % 2 == 0 ? 1f : -1f;
            Vector2 origin = centre + axis * length * (-.45f + i * .3f);
            Vector2 tip = origin + axis * length * .16f + normal * sign * scale * (25f + i * 12f);
            if (reveal > .65f)
                Ribbon(vh, origin, tip, normal, w * .25f, new Color(tint.r, tint.g, tint.b, tint.a * fade * .65f));
        }
    }

    private static void Ribbon(VertexHelper vh, Vector2 start, Vector2 end, Vector2 normal, float halfWidth, Color colour)
    {
        int first = vh.currentVertCount;
        // Zero-alpha edges feather each ribbon without textures or post processing.
        for (int i = 0; i < 4; i++)
        {
            float t = i == 0 ? 0 : i == 1 ? .12f : i == 2 ? .88f : 1;
            float taper = i == 0 || i == 3 ? 0 : 1;
            Vector2 centre = Vector2.Lerp(start, end, t);
            Color edge = colour; edge.a = 0;
            vh.AddVert(centre - normal * halfWidth * taper, edge, Vector2.zero);
            vh.AddVert(centre, colour, Vector2.zero);
            vh.AddVert(centre + normal * halfWidth * taper, edge, Vector2.zero);
        }
        for (int i = 0; i < 3; i++)
        for (int j = 0; j < 2; j++)
        {
            int a = first + i * 3 + j;
            vh.AddTriangle(a, a + 3, a + 1);
            vh.AddTriangle(a + 1, a + 3, a + 4);
        }
    }
}
