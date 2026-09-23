using UnityEngine;

[CreateAssetMenu(fileName = "NewAction", menuName = "Combat/Action Data")]
public class ActionData : ScriptableObject
{
    public string actionName;

    // --- THÊM MỚI: Biến lưu mô tả của kỹ năng/Vật phẩm ---
    [TextArea(2, 4)]
    public string description;

    public Sprite icon;
    public enum ActionType { Attack, Skill, Item }
    public ActionType type;

    public int power;

    [Header("Damage Scaling")]
    public float damageMultiplier = 1f;

    public bool isHeal;
    public string animationTriggerName = "Attack";
    public bool isFriendlyAction;

    [Header("Targeting")]
    public bool isAoE;
    public bool isSelfOnly;

    // --- BIẾN DÀNH CHO HỆ THỐNG STAIN ---
    [Header("Resource Cost")]
    [Tooltip("Dương (+) là hồi Stain, Âm (-) là tốn Stain. (Vd: Attack = 1, Skill = -1)")]
    public int stainChange = 0;

    // Preserve serialized values used by existing skill assets.
    public enum BuffStat { None = 0, Atk = 1, Def = 2, Crit = 3, Shield = 4, CritDamage = 5 }
    [Header("Buff & Shield Settings")]
    public BuffStat buffStat = BuffStat.None;
    [Tooltip("Crit: bonus percentage points to crit chance. CritDamage: bonus percentage points to crit damage (150 + 50 = 200%). Other stats: flat bonus.")]
    public int buffAmount;
    [Tooltip("Number of rounds the buff lasts. Buffs of the same stat stack additively.")]
    [Min(1)] public int buffDuration = 1;

    [Header("Attack Distance Type")]
    public bool isMelee = true;

    // Append values so existing serialized actions keep their VFX behavior.
    public enum VfxType { None, Shoot, SpawnAtTarget, Beam }

    // Cách projectile di chuyển khi VfxType = Shoot.
    public enum ProjectileMoveMode
    {
        Straight,       // CombatManager tự kéo VFX bay thẳng tới target.
        RFX4Prefab,     // Giữ nguyên RFX4_PhysicsMotion / RFX4_RaycastCollision của prefab.
        BezierCurve     // CombatManager tự điều khiển projectile theo đường cong Bezier.
    }

    [Header("VFX Settings")]
    public VfxType vfxType = VfxType.None;
    public GameObject vfxPrefab;
    public GameObject castVfxPrefab;
    public GameObject hitVfxPrefab;

    public enum HitVfxTiming { VfxImpact, AnimationEvent }

    [Tooltip(
        "Shoot/Beam tấn công luôn gây damage + Hit VFX khi VFX impact target, " +
        "không còn dùng Animation Event để gây damage. Field này được giữ lại " +
        "để tương thích các Action asset cũ / loại VFX khác."
    )]
    public HitVfxTiming hitVfxTiming = HitVfxTiming.VfxImpact;
    [Min(0.1f)] public float castVfxLifeTime = 5f;
    [Min(0.1f)] public float hitVfxLifeTime = 5f;

    [Header("VFX Parry Timing")]
    [Tooltip(
        "Chỉ dùng cho đòn tấn công Shoot / Beam của Enemy. " +
        "Bật để cửa sổ Parry tự căn theo VFX thay vì Animation Event."
    )]
    public bool useVfxParry = true;

    [Tooltip(
        "Shoot: mở cửa sổ Parry khi projectile ước tính còn từng này giây nữa sẽ chạm target."
    )]
    [Min(0.01f)]
    public float parryOpenBeforeImpact = 0.25f;

    [Tooltip(
        "Thời gian cửa sổ Parry được mở. Hết thời gian này mà chưa Parry thì cửa sổ đóng."
    )]
    [Min(0.01f)]
    public float parryWindowDuration = 0.30f;

    [Tooltip(
        "Beam: chờ từng này giây kể từ lúc Beam được spawn rồi mới mở cửa sổ Parry."
    )]
    [Min(0f)]
    public float beamParryDelay = 0.10f;

    [Header("Beam Settings")]
    [Tooltip(
        "Playback speed của charge/particle Beam. " +
        "Damage của Beam được resolve khi Beam impact target."
    )]
    [Min(0.01f)] public float beamPlaybackSpeed = 1f;

    [Header("Projectile Settings")]
    public ProjectileMoveMode projectileMoveMode = ProjectileMoveMode.Straight;
    [Min(0.01f)] public float vfxSpeed = 15f;
    [Min(0.1f)] public float vfxLifeTime = 5f;
    public Vector3 projectileStartOffset = Vector3.zero;
    public Vector3 projectileTargetOffset = new Vector3(0f, 1f, 0f);

    [Header("Bezier Curve Settings")]
    public float curveHeight = 2f;
    public float curveSideOffset = 0f;

    [Header("RFX4 Projectile Settings")]
    [Tooltip("Chỉ dùng khi Projectile Move Mode = RFX4Prefab.")]
    public bool rfxUseGravity = false;
}
