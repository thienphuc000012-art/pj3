using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Only m owns this lighting rig. Retained exploration scenes cannot drive combat lighting.</summary>
[DisallowMultipleComponent]
public sealed class MDayNightController : MonoBehaviour
{
    [Header("Clock (shared with the exploration HUD)")]
    public AdventureWorldTimeSettings clockSettings;
    [Header("Scene m references")]
    public Light sun;
    public Light moonLight;
    public Light playerNightLight;
    public Transform skyVisuals;
    public Transform clouds;
    public ParticleSystem stars;
    public Transform moon;
    public Transform player;
    public Camera viewCamera;
    public Material skyMaterial;
    [Header("Sun / moon")]
    [Tooltip("Orientation of the shared daily orbit. The moon stays 180 degrees opposite the sun.")]
    [Range(0, 360)] public float sunAzimuth = 322f;
    [Min(0)] public float daySunIntensity = 1.35f;
    [Min(0)] public float nightMoonIntensity = .32f;
    public Color daylightColor = new Color(1f, .94f, .83f);
    public Color twilightColor = new Color(1f, .43f, .22f);
    public Color moonlightColor = new Color(.57f, .69f, 1f);
    [Header("Night visibility")]
    public Color nightAmbientSky = new Color(.22f, .29f, .42f);
    public Color nightAmbientEquator = new Color(.16f, .20f, .29f);
    public Color nightAmbientGround = new Color(.10f, .12f, .17f);
    [Min(0)] public float playerLightIntensity = .8f;
    [Min(1)] public float playerLightRange = 7f;
    public Color playerLightColor = new Color(1f, .91f, .8f);
    [Tooltip("Offset in player-local meters. Keep the light above/behind the character rather than inside the head.")]
    public Vector3 playerLightOffset = new Vector3(0, 2.8f, -1.4f);
    [Header("Sky layout (meters from camera)")]
    [Range(80, 400)] public float celestialDistance = 320f;
    [Range(40, 160)] public float cloudHeight = 110f;
    [Range(.05f, .6f)] public float moonScale = .22f;
    [Min(0)] public float fogDensity = .0014f;
    [Header("Editor preview (context menu)")]
    [Range(0, 24)] public float previewHour = 8;

    Material runtimeSky;
    Material originalSky;
    Color oldSky, oldEquator, oldGround, oldFog;
    float oldReflection, oldFogDensity;
    AmbientMode oldAmbientMode;
    Light originalSun;
    Quaternion oldSunRotation;
    Color oldSunColor;
    float oldSunIntensity;
    bool captured;
    bool oldSunEnabled;
    Renderer moonRenderer, starRenderer;
    Renderer[] cloudRenderers;
    MaterialPropertyBlock block;
    static readonly int Tint = Shader.PropertyToID("_Tint");

    public bool OwnsActiveScene => gameObject.scene.name == "m" && gameObject.scene == SceneManager.GetActiveScene();
    public static float Daylight(float hour) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5, 7.5f, hour)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(17, 19.5f, hour)));
    public static float Night(float hour) => 1 - Mathf.SmoothStep(0, 1, Daylight(hour) / .65f);

    void OnEnable() { SceneManager.activeSceneChanged += ActiveSceneChanged; }
    void Start()
    {
        if (gameObject.scene.name != "m") { enabled = false; return; }
        if (player == null) player = CampaignSession.Instance?.MapPlayer?.transform;
        if (viewCamera == null) viewCamera = Camera.main;
        if (sun == null || skyMaterial == null) { Debug.LogError("[Day Night] Missing sun or sky material in m.", this); enabled = false; }
    }
    void LateUpdate()
    {
        var session = CampaignSession.Instance;
        if (!OwnsActiveScene || (session != null && (!session.OnMap || session.InEncounter))) { SetVisuals(false); return; }
        if (session != null && session.MapPlayer != null && session.MapPlayer.gameObject.scene == gameObject.scene) player = session.MapPlayer.transform;
        if (viewCamera == null || viewCamera.gameObject.scene != gameObject.scene) viewCamera = Camera.main;
        Apply((float)(AdventureWorldTime.Read(session?.Data) % 24), viewCamera != null ? viewCamera.transform.position : player != null ? player.position : transform.position);
    }
    void ActiveSceneChanged(Scene previous, Scene next)
    {
        // RenderSettings are owned by the newly active scene already. Do not restore m's settings into it.
        if (next != gameObject.scene) { SetVisuals(false); if (sun != null) sun.enabled = false; }
    }
    void OnDisable()
    {
        SceneManager.activeSceneChanged -= ActiveSceneChanged;
        if (OwnsActiveScene) RestoreEnvironment();
        SetVisuals(false);
    }
    void OnDestroy() { if (runtimeSky != null) Destroy(runtimeSky); }

    void Capture()
    {
        if (captured) return;
        captured = true; originalSky = RenderSettings.skybox; originalSun = RenderSettings.sun;
        oldSky = RenderSettings.ambientSkyColor; oldEquator = RenderSettings.ambientEquatorColor; oldGround = RenderSettings.ambientGroundColor;
        oldAmbientMode = RenderSettings.ambientMode; oldFog = RenderSettings.fogColor; oldFogDensity = RenderSettings.fogDensity; oldReflection = RenderSettings.reflectionIntensity;
        if (sun != null) { oldSunRotation = sun.transform.rotation; oldSunColor = sun.color; oldSunIntensity = sun.intensity; oldSunEnabled = sun.enabled; }
        runtimeSky = new Material(skyMaterial) { name = "m Sky (instance)", hideFlags = HideFlags.DontSave };
        block = new MaterialPropertyBlock();
        moonRenderer = moon != null ? moon.GetComponentInChildren<Renderer>(true) : null;
        starRenderer = stars != null ? stars.GetComponent<Renderer>() : null;
        cloudRenderers = clouds != null ? clouds.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        SeedStars();
    }
    void SeedStars()
    {
        if (stars == null) return;
        stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // The original effect fades particles in from zero size/alpha over their lifetime.
        // A persistent sky field instead fades as a whole with the time of day.
        var color = stars.colorOverLifetime; color.enabled = false;
        var size = stars.sizeOverLifetime; size.enabled = false;
        var velocity = stars.velocityOverLifetime; velocity.enabled = false;
        var noise = stars.noise; noise.enabled = false;
        stars.Play(true);
        var particles = new ParticleSystem.Particle[280]; var random = new System.Random(1977);
        for (int i = 0; i < particles.Length; i++)
        {
            float y = Mathf.Lerp(.12f, 1, (float)random.NextDouble()); float angle = (float)random.NextDouble() * Mathf.PI * 2;
            float ring = Mathf.Sqrt(1 - y * y);
            particles[i].position = new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring) * celestialDistance;
            particles[i].startSize = Mathf.Lerp(.35f, 1.2f, (float)random.NextDouble());
            particles[i].startColor = new Color(.83f, .9f, 1f, Mathf.Lerp(.45f, 1, (float)random.NextDouble()));
            particles[i].startLifetime = particles[i].remainingLifetime = 100000;
        }
        stars.SetParticles(particles, particles.Length);
        stars.Pause(true);
    }
    public void Apply(float hour, Vector3 skyCenter)
    {
        if (!OwnsActiveScene) { SetVisuals(false); if (sun != null) sun.enabled = false; return; }
        if (sun == null || skyMaterial == null) return;
        bool reactivated = skyVisuals != null && !skyVisuals.gameObject.activeSelf;
        SetVisuals(true);
        bool alreadyCaptured = captured;
        Capture();
        if (reactivated && alreadyCaptured) SeedStars();
        sun.enabled = true;
        hour = Mathf.Repeat(hour, 24); float day = Daylight(hour), night = Night(hour);
        sun.transform.rotation = Quaternion.Euler((hour - 6) * 15, sunAzimuth, 0);
        Vector3 sunDirection = -sun.transform.forward;
        Vector3 moonDirection = -sunDirection;
        float elevation = sunDirection.y;
        sun.color = Color.Lerp(twilightColor, daylightColor, Mathf.SmoothStep(0, 1, Mathf.Abs(elevation) / .55f));
        sun.intensity = daySunIntensity * day * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.03f, .55f, elevation));
        if (moonLight != null)
        {
            moonLight.transform.rotation = sun.transform.rotation * Quaternion.Euler(180, 0, 0);
            moonLight.color = moonlightColor;
            moonLight.intensity = nightMoonIntensity * night * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.03f, .4f, moonDirection.y));
        }
        RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(nightAmbientSky, new Color(.45f, .55f, .66f), day);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightAmbientEquator, new Color(.31f, .36f, .36f), day);
        RenderSettings.ambientGroundColor = Color.Lerp(nightAmbientGround, new Color(.18f, .20f, .18f), day);
        float warm = Mathf.Sin(day * Mathf.PI);
        RenderSettings.fogColor = Color.Lerp(Color.Lerp(new Color(.075f, .10f, .17f), new Color(.52f, .63f, .68f), day), new Color(.61f, .34f, .26f), warm * .6f);
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.reflectionIntensity = Mathf.Lerp(.35f, 1, day);
        runtimeSky.SetColor("_TopColor", Color.Lerp(new Color(.009f, .018f, .05f), new Color(.15f, .36f, .62f), day));
        runtimeSky.SetColor("_HorizonColor", Color.Lerp(RenderSettings.fogColor, new Color(.94f, .43f, .19f), warm * .5f));
        runtimeSky.SetColor("_GroundColor", RenderSettings.ambientGroundColor);
        runtimeSky.SetVector("_SunDirection", sunDirection);
        runtimeSky.SetColor("_SunColor", sun.color * Mathf.Clamp01(day * 2));
        RenderSettings.skybox = runtimeSky;
        if (skyVisuals != null) skyVisuals.position = skyCenter;
        if (clouds != null) clouds.localPosition = Vector3.up * cloudHeight;
        if (stars != null) stars.transform.localPosition = Vector3.zero;
        if (moon != null)
        {
            // Keep the same prefab instance in orbit, including while it travels below the horizon.
            moon.position = skyCenter + moonDirection * celestialDistance;
            moon.rotation = sun.transform.rotation;
            moon.localScale = Vector3.one * moonScale;
        }
        SetTint(starRenderer, new Color(1, 1, 1, night));
        SetTint(moonRenderer, new Color(.78f, .85f, 1, 1));
        foreach (var r in cloudRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            var tint = Color.Lerp(new Color(.14f, .19f, .28f), Color.Lerp(Color.white, new Color(1f, .63f, .43f), warm * .5f), day);
            block.SetColor("_CloudColor", tint); block.SetColor("_EmissionColor", tint); r.SetPropertyBlock(block);
        }
        if (playerNightLight != null)
        {
            playerNightLight.enabled = player != null && night > .005f;
            if (player != null) playerNightLight.transform.position = player.position + player.rotation * playerLightOffset;
            playerNightLight.color = playerLightColor; playerNightLight.range = playerLightRange; playerNightLight.intensity = playerLightIntensity * night;
            playerNightLight.shadows = LightShadows.None;
        }
    }
    void SetTint(Renderer r, Color color) { if (r == null) return; r.GetPropertyBlock(block); block.SetColor(Tint, color); r.SetPropertyBlock(block); }
    void SetVisuals(bool value)
    {
        if (skyVisuals != null && skyVisuals.gameObject.activeSelf != value) skyVisuals.gameObject.SetActive(value);
        if (moonLight != null) moonLight.enabled = value;
        if (!value && playerNightLight != null) playerNightLight.enabled = false;
    }
    public void RestoreEnvironment()
    {
        if (!captured) return;
        if (OwnsActiveScene)
        {
            RenderSettings.skybox = originalSky; RenderSettings.sun = originalSun;
            RenderSettings.ambientMode = oldAmbientMode; RenderSettings.ambientSkyColor = oldSky; RenderSettings.ambientEquatorColor = oldEquator; RenderSettings.ambientGroundColor = oldGround;
            RenderSettings.fogColor = oldFog; RenderSettings.fogDensity = oldFogDensity; RenderSettings.reflectionIntensity = oldReflection;
        }
        if (sun != null) { sun.transform.rotation = oldSunRotation; sun.color = oldSunColor; sun.intensity = oldSunIntensity; sun.enabled = oldSunEnabled; }
        if (runtimeSky != null) { if (Application.isPlaying) Destroy(runtimeSky); else DestroyImmediate(runtimeSky); }
        captured = false;
    }
    [ContextMenu("Preview Time (m only)")] void Preview() { Apply(previewHour, viewCamera != null ? viewCamera.transform.position : player != null ? player.position : transform.position); }
    [ContextMenu("Restore Editor Lighting")] void RestorePreview() { RestoreEnvironment(); SetVisuals(false); }
}
