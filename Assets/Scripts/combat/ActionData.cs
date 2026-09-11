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

    [Header("VFX Settings")]
    public VfxType vfxType = VfxType.None;
    public GameObject vfxPrefab;
    public float vfxSpeed = 15f;
    public GameObject castVfxPrefab;
    public GameObject hitVfxPrefab;
}