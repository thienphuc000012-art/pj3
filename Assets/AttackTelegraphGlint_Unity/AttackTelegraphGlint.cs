using System.Collections;
using UnityEngine;

/// <summary>
/// A short, procedural star glint to telegraph an incoming enemy attack.
/// No imported textures, shaders, packages or DOTween required.
/// Attach this to an empty GameObject at the position where the flash should appear.
/// </summary>
[DisallowMultipleComponent]
public class AttackTelegraphGlint : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private Color glintColor = new Color(1f, 0.87f, 0.58f, 1f);
    [SerializeField, Min(0.01f)] private float worldSize = 1.1f;
    [SerializeField] private int sortingOrder = 100;

    [Header("Timing")]
    [SerializeField, Min(0.03f)] private float duration = 0.22f;
    [SerializeField] private bool playOnEnable = false;

    private SpriteRenderer core;
    private SpriteRenderer halo;
    private Coroutine flashCoroutine;
    private static Sprite cachedSprite;

    private void Awake()
    {
        Sprite sprite = GetOrCreateSprite();
        halo = CreateLayer("Soft Halo", sprite, sortingOrder);
        core = CreateLayer("Sharp Star", sprite, sortingOrder + 1);
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (playOnEnable)
            PlayFlash();
    }

    [ContextMenu("Play Flash (in Play Mode)")]
    public void PlayFlash()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(AnimateFlash());
    }

    private IEnumerator AnimateFlash()
    {
        SetVisible(true);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            // Burst quickly to full intensity, then disappear rapidly.
            float intensity = t < 0.28f
                ? Mathf.SmoothStep(0f, 1f, t / 0.28f)
                : Mathf.Pow(1f - (t - 0.28f) / 0.72f, 1.6f);

            float growth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.34f));
            float scale = Mathf.Lerp(0.18f, 1.15f, growth) * worldSize;

            core.transform.localScale = Vector3.one * scale;
            halo.transform.localScale = Vector3.one * scale * 1.7f;

            Color coreColor = glintColor;
            coreColor.a *= intensity;
            core.color = coreColor;

            Color haloColor = glintColor;
            haloColor.a *= intensity * 0.28f;
            halo.color = haloColor;

            // Face the active camera. Works in 3D battles and 2D scenes.
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetVisible(false);
        flashCoroutine = null;
    }

    private SpriteRenderer CreateLayer(string objectName, Sprite sprite, int order)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(transform, false);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    private void SetVisible(bool visible)
    {
        if (core != null) core.enabled = visible;
        if (halo != null) halo.enabled = visible;
    }

    private void OnDisable()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        SetVisible(false);
    }

    private static Sprite GetOrCreateSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        const int resolution = 256;
        Texture2D texture = new Texture2D(
            resolution, resolution, TextureFormat.RGBA32, false);
        texture.name = "Procedural Star Glint";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u = (x - 127.5f) / 127.5f;
                float v = (y - 127.5f) / 127.5f;
                float r2 = u * u + v * v;

                float nucleus = Mathf.Exp(-r2 * 95f);
                float horizontal = Mathf.Exp(-Mathf.Abs(u) * 4.0f)
                                   * Mathf.Exp(-(v * v) / 0.0007f);
                float vertical = Mathf.Exp(-Mathf.Abs(v) * 4.0f)
                                 * Mathf.Exp(-(u * u) / 0.0007f);
                float d1 = (u - v) * 0.7071f;
                float d2 = (u + v) * 0.7071f;
                float diagonal = (Mathf.Exp(-(d1 * d1) / 0.0015f)
                                * Mathf.Exp(-Mathf.Abs(d2) * 11f)
                                + Mathf.Exp(-(d2 * d2) / 0.0015f)
                                * Mathf.Exp(-Mathf.Abs(d1) * 11f)) * 0.23f;

                float alpha = Mathf.Clamp01(
                    nucleus * 0.95f + horizontal * 0.85f
                    + vertical * 0.85f + diagonal);

                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        cachedSprite = Sprite.Create(texture,
            new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f), resolution);
        cachedSprite.name = "Star Glint Sprite";
        return cachedSprite;
    }
}
