using UnityEngine;
using LineworkLite.FreeOutline;

[CreateAssetMenu(menuName = "Adventure/Exploration Scan Settings")]
public sealed class AdventureScanSettings : ScriptableObject
{
    [Header("Input and range")]
    public bool scanEnabled = true;
    public KeyCode scanKey = KeyCode.Q;
    [Min(1f)] public float radius = 45f;
    [Min(1f)] public float waveSpeed = 22f;
    [Min(0f)] public float cooldown = 8f;
    [Min(.1f)] public float revealDuration = 6f;
    [Min(.05f)] public float fadeDuration = .6f;
    [Range(1, 128)] public int maxDetectedTargets = 48;
    public LayerMask detectionLayers = ~0;
    [Tooltip("Bật nếu chỉ muốn phát hiện vật thể nhìn thấy trực tiếp. Tắt để scan xuyên tường.")]
    public bool requireLineOfSight;
    public LayerMask obstructionLayers = ~0;

    [Header("Environment hologram")]
    [ColorUsage(false, true)] public Color scanColor = new Color(.05f, .8f, 1.4f, 1f);
    [Min(.05f)] public float waveWidth = 1.6f;
    [Min(0f)] public float waveIntensity = 1.2f;
    [Min(.1f)] public float revealTrailWidth = 12f;
    [Min(.05f), Tooltip("Số ô lưới trên một mét.")]
    public float gridScale = 1.5f;
    [Range(0f, 2f)] public float gridIntensity = .18f;
    [Range(0f, 3f)] public float edgeIntensity = .4f;
    [Range(0f, 1f)] public float environmentTint = .05f;
    [Min(1f)] public float hologramScanlineDensity = 18f;
    [Range(0f, 1f)] public float hologramScanlineStrength = .06f;

    [Header("Detected object outline")]
    [Tooltip("FreeOutline features của các URP renderer đang dùng. Không sửa shared outline settings lúc runtime.")]
    public FreeOutline[] outlineFeatures = new FreeOutline[0];
    public bool showOutline = true;
    [ColorUsage(false, true)] public Color outlineColor = new Color(.08f, 1.2f, 1.8f, 1f);
    [Range(0f, 15f)] public float outlineWidth = 3.5f;
    [Range(0f, .8f)] public float outlinePulseAmount = .15f;
    [Min(0f)] public float outlinePulseSpeed = 1.4f;
    [Range(0, 31), Tooltip("Rendering Layer dành riêng cho scan, không phải Physics Layer. Tránh layer 24–27 đang dùng cho target combat.")]
    public int outlineRenderingLayer = 29;

    [Header("HUD markers")]
    public bool showHud = true;
    public bool showMarkers = true;
    public bool showTargetNames = true;
    public bool showDistances = true;
    public Sprite markerSprite;
    public Color markerColor = new Color(.45f, .92f, 1f, 1f);
    [Min(8f)] public float markerSize = 24f;
    public Vector2 cooldownPosition = new Vector2(-32f, 112f);
    [Tooltip("Kích thước vùng HUD tròn. Dùng Width và Height bằng nhau để giữ hình tròn.")]
    public Vector2 cooldownSize = new Vector2(72f, 72f);
    [Min(1f)] public float cooldownRingThickness = 2.5f;
    [Min(0f)] public float cooldownGlowWidth = 7f;
    [Min(8f)] public float cooldownKeyFontSize = 28f;
    [Header("Audio (optional)")]
    public AudioClip scanSound;
    [Range(0f, 1f)] public float volume = .6f;
}

// Only the active exploration scanner publishes these values. The renderer
// consumes a snapshot, so battle/preview cameras never keep a stale pulse.
public static class AdventureScanRuntime
{
    public static AdventureScanner Owner;
    public static bool Active;
    public static Vector3 Origin;
    public static float Radius, Strength;
    public static AdventureScanSettings Settings;
    public static Camera TargetCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Owner = null; Active = false; Origin = Vector3.zero; Radius = Strength = 0;
        Settings = null; TargetCamera = null;
    }
}
