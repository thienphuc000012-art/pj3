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

    [Header("Ultimate Energy")]
    [Tooltip("Requires a full personal Ultimate gauge and consumes it on use.")]
    public bool isUltimate;
    [Range(0, 100), Tooltip("Percentage points gained per resolved hit (or heal/buff event). Multi-hit combos charge each hit; Items and Ultimates never charge.")]
    public float ultimateEnergyGain = 10f;

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

    [Header("Enemy AI Cooldown")]
    [Min(0), Tooltip("Cooldown chỉ áp dụng cho Enemy AI. 2 nghĩa là dùng ở lượt Enemy 1 thì sẵn sàng dùng lại ở lượt Enemy 3. 0 = không cooldown.")]
    public int enemyCooldownTurns;

    // Preserve serialized values used by existing skill assets.
    public enum BuffStat { None = 0, Atk = 1, Def = 2, Crit = 3, Shield = 4, CritDamage = 5 }
    [Header("Buff & Shield Settings")]
    public BuffStat buffStat = BuffStat.None;
    [Tooltip("Crit: bonus percentage points to crit chance. CritDamage: bonus percentage points to crit damage (150 + 50 = 200%). Other stats: flat bonus.")]
    public int buffAmount;
    [Tooltip("Number of rounds the buff lasts. Buffs of the same stat stack additively.")]
    [Min(1)] public int buffDuration = 1;

    public enum DebuffType { Burn, Paralysis, Poison }
    public enum CleanseType { None, RemoveDebuffs, RemoveBuffs }
    [System.Serializable]
    public class DebuffSettings
    {
        public DebuffType type;
        [Tooltip("Icon hiển thị trong BuffContainer khi nhân vật đang dính debuff.")]
        public Sprite icon;
        [Range(0, 100)] public float applyChance = 100;
        [Min(1)] public int turns = 3;
        [Min(0)] public int burnDamage = 10;
        [Range(0, 100)] public float burnAttackReductionPercent = 20;
        [Range(0, 100)] public float paralysisSkipChance = 25;
        [Range(0, 100)] public float poisonMaxHpPercent = 5;
        [Tooltip("Burn/Poison: cuối lượt. Paralysis: đầu mỗi lượt còn tê liệt.")]
        public GameObject turnVfxPrefab;
        public Vector3 turnVfxLocalOffset = Vector3.up;
        [Tooltip("Thời gian hiển thị VFX và giữ camera sau khi debuff gây sát thương. Particle bị dọn khi hết thời gian này.")]
        [Min(0.1f)] public float turnVfxLifetime = 2;
    }
    [Header("Debuffs & Cleanse")]
    [Tooltip("Tối đa 1 debuff mỗi nhân vật. Đang có debuff thì không nhận debuff mới, không làm mới thời gian. Khi trống, lấy debuff đầu tiên dính theo thứ tự danh sách. Parry chặn debuff.")]
    public DebuffSettings[] debuffs = new DebuffSettings[0];
    public CleanseType cleanse;
    [Tooltip("Bật cho skill chỉ gây trạng thái/xóa hiệu ứng, không gây damage trực tiếp. Friendly Action chọn đồng minh.")]
    public bool statusOnly;

    [Header("Attack Distance Type")]
    public bool isMelee = true;

    [Header("Screen Slash")]
    [Tooltip("Draw a luminous screen-space cut on each melee damage event.")]
    public bool screenSlash;
    public Color screenSlashColor = new Color(.25f, .8f, 1f, 1f);
    [Min(.1f)] public float screenSlashDuration = .42f;
    [Min(1f)] public float screenSlashWidth = 10f;
    [Range(-80f, 80f)] public float screenSlashAngle = -24f;
    [Range(1, 24)] public int screenSlashCount = 1;
    [Tooltip("Time spent releasing the cuts. Included in Screen Slash Duration.")]
    [Min(0f)] public float screenSlashBurstTime = .45f;
    [Range(1f, 3f)] public float screenSlashFinisherScale = 2f;
    [Tooltip("Briefly split the live camera image along the slash, then restore it.")]
    public bool screenSpaceSplit;
    [Range(1, 6)] public int screenSplitFrames = 2;
    [Range(0f, 40f)] public float screenSplitPixels = 18f;
    [Tooltip("Keep all cuts open until the final portion of Screen Slash Duration.")]
    public bool screenSplitHold;
    [Range(.5f, .95f)] public float screenSplitRestoreStart = .8f;
    [Range(.05f, 1f)] public float screenSlashTimeScale = 1f;

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
    [Min(0.01f), Tooltip("Runtime playback multiplier for every Particle System in the main VFX prefab and its children. This is separate from the Scene view preview speed.")]
    public float vfxPlaybackSpeed = 1f;
    [Tooltip("Optional aura spawned on the character using this Ultimate when the Ultimate starts.")]
    public GameObject ultimateAuraVfxPrefab;
    [Tooltip("Local offset from the Ultimate caster's root for the optional aura VFX.")]
    public Vector3 ultimateAuraVfxLocalOffset = new Vector3(0f, 1f, 0f);
    [Min(0.01f), Tooltip("Particle playback multiplier for the optional Ultimate aura. Defaults to the prefab's normal speed.")]
    public float ultimateAuraVfxPlaybackSpeed = 1f;
    public GameObject castVfxPrefab;
    public GameObject hitVfxPrefab;
    [Tooltip("Use a separate position for Hit VFX, for example the target root for a full-body effect. Otherwise uses Projectile Target Offset.")]
    public bool useCustomHitVfxOffset;
    [Tooltip("World-space offset from the target root. Zero places the Hit VFX at the target pivot/feet.")]
    public Vector3 hitVfxOffset = Vector3.zero;

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
    [Tooltip("Exact particle object names to stretch for a beam. Empty uses the RFX4 Distance naming convention.")]
    public string[] beamParticleNames = new string[0];
    [Tooltip("Fit a forward particle stream (e.g. Flamethrower) to the selected target.")]
    public bool beamParticleStream;
    public string beamStreamParticleName = "Flames";
    [Min(0.05f)] public float beamStreamTravelTime = 0.45f;

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

    [Header("Sword Slash VFX")]
    [Tooltip("Optional slash effect played at each resolved sword hit. Leave empty for actions that are not sword strikes.")]
    public GameObject swordSlashVfxPrefab;
    [Tooltip("Small position correction relative to the sword midpoint and the automatically aligned slash plane.")]
    public Vector3 swordSlashVfxLocalOffset = Vector3.zero;
    [Tooltip("Small rotation correction after the slash has been aligned to the current sword swing.")]
    public Vector3 swordSlashVfxLocalEuler = Vector3.zero;
    public Vector3 swordSlashVfxScale = Vector3.one;
    [Min(0.1f)] public float swordSlashVfxLifetime = 2f;
    [Tooltip("Shoot/Beam actions with a Sword Slash VFX launch it from the sword toward the target.")]
    public bool swordSlashTravelsToTarget = true;
    [Min(0.1f), Tooltip("Travel speed for a sword slash used by a Shoot or Beam action.")]
    public float swordSlashTravelSpeed = 24f;

    [Header("Enemy Claw Attack Trail")]
    [Tooltip("Emit three fine, tapered claw streaks from the animated hand during this attack.")]
    public bool useClawAttackTrailVfx;
    public Color clawAttackTrailColor = new Color(.88f, .92f, .96f, 1f);
    [Min(.05f)] public float clawAttackTrailLifetime = .14f;
    [Tooltip("Trail width as a multiplier of the enemy's estimated hand width. 1 = about one hand-width before the per-line taper.")]
    [Min(.1f)] public float clawAttackTrailWidth = 1f;
    [Tooltip("Spacing between the three lines as a fraction of the estimated hand width.")]
    [Min(0f)] public float clawAttackTrailHandSpread = .5f;
}
