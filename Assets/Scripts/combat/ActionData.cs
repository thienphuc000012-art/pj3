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
    [Tooltip("VfxImpact: hit plays on projectile arrival / beam emission. AnimationEvent: use the damage event.")]
    public HitVfxTiming hitVfxTiming = HitVfxTiming.VfxImpact;
    [Min(0.1f)] public float castVfxLifeTime = 5f;
    [Min(0.1f)] public float hitVfxLifeTime = 5f;

    [Header("Beam Settings")]
    [Tooltip("Playback speed of a beam's charge-up and particles. Damage still uses animation events.")]
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
