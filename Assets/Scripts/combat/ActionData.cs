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

    public enum BuffStat { None, Atk, Def, Crit, Shield }
    [Header("Buff & Shield Settings")]
    public BuffStat buffStat = BuffStat.None;
    public int buffAmount;
    public int buffDuration;

    [Header("Attack Distance Type")]
    public bool isMelee = true;

    public enum VfxType { None, Shoot, SpawnAtTarget }

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