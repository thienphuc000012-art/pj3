using UnityEngine;

[CreateAssetMenu(fileName = "NewAction", menuName = "Combat/Action Data")]
public class ActionData : ScriptableObject
{
    public string actionName;
    public Sprite icon;
    public enum ActionType { Attack, Skill, Item }
    public ActionType type;

    public int power; // Sát thương cố định hoặc giá trị cơ bản của skill

    [Header("Damage Scaling")]
    public float damageMultiplier = 1f; // Hệ số nhân sát thương theo chỉ số tấn công của nhân vật (Ví dụ: 1.0 = 100% ATK, 1.5 = 150% ATK)

    public bool isHeal;
    public string animationTriggerName = "Attack";
    public bool isFriendlyAction;

    [Header("Targeting")]
    public bool isAoE;

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