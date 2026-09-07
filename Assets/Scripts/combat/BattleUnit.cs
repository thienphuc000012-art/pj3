using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ActiveBuff
{
    public ActionData.BuffStat stat;
    public int amount;
    public int duration;
}

public class BattleUnit : MonoBehaviour
{
    public string unitName;
    public int maxHP = 100;

    [Header("Base Stats")]
    public int baseAtk = 10; // --- THÊM CHỈ SỐ TẤN CÔNG CƠ BẢN ---
    public int baseDef = 5;
    [Range(0, 100)] public int baseCrit = 10;

    [Header("Character Skills")]
    public ActionData defaultAttack;
    public List<ActionData> characterSkills;

    [Header("Turn System")]
    public int speed;
    public Sprite unitPortrait;
    public int currentHP { get; private set; }
    public bool isPlayer;

    [Header("VFX References")]
    public Transform handTransform;

    public Animator animator;

    public event Action<int, int, int> OnStatsChanged;

    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    void Awake()
    {
        currentHP = maxHP;
    }

    void Start()
    {
        if (handTransform == null && animator != null)
        {
            if (animator.isHuman) handTransform = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        UpdateUI();
    }

    public void AddBuff(ActionData.BuffStat stat, int amount, int duration)
    {
        // Nếu đã có buff loại này rồi thì cộng dồn hoặc refresh thời gian tùy ý, ở đây tạo mới độc lập
        activeBuffs.Add(new ActiveBuff { stat = stat, amount = amount, duration = duration });
        UpdateUI();
    }

    public int GetBuffValue(ActionData.BuffStat stat)
    {
        return activeBuffs.Where(b => b.stat == stat).Sum(b => b.amount);
    }

    public int GetTotalShield()
    {
        return GetBuffValue(ActionData.BuffStat.Shield);
    }

    public void TickBuffs()
    {
        // Giảm thời gian của tất cả các buff đang sở hữu
        foreach (var buff in activeBuffs)
        {
            buff.duration--;
        }

        // Loại bỏ các buff đã hết hạn (duration <= 0)
        activeBuffs.RemoveAll(b => b.duration <= 0);

        UpdateUI();
    }

    public void TakeDamage(int rawDamage, bool isParried)
    {
        if (isParried)
        {
            if (animator != null) animator.SetTrigger("Parry");
            return;
        }

        if (animator != null) animator.SetTrigger("Hit");

        int totalDef = baseDef + GetBuffValue(ActionData.BuffStat.Def);
        int finalDamage = Mathf.Max(1, rawDamage - totalDef);

        // Trừ Giáp Ảo (Shield) trước
        foreach (var buff in activeBuffs.Where(b => b.stat == ActionData.BuffStat.Shield).ToList())
        {
            if (finalDamage <= 0) break;

            if (buff.amount >= finalDamage)
            {
                buff.amount -= finalDamage;
                finalDamage = 0;
            }
            else
            {
                finalDamage -= buff.amount;
                buff.amount = 0;
            }
        }
        activeBuffs.RemoveAll(b => b.amount <= 0 && b.stat == ActionData.BuffStat.Shield);

        currentHP = Mathf.Max(0, currentHP - finalDamage);

        UpdateUI();
        if (currentHP == 0 && animator != null) animator.SetTrigger("Die");
    }

    public void Heal(int amount)
    {
        if (amount > 0)
        {
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            UpdateUI();
        }
    }

    public void UpdateUI()
    {
        OnStatsChanged?.Invoke(currentHP, maxHP, GetTotalShield());
    }
}