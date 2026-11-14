using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>Player-facing text derived from the same ActionData fields used by combat resolution.</summary>
public static class CombatActionDescription
{
    static string Pick(GameLanguage language, string vi, string en) => language == GameLanguage.English ? en : vi;
    static string Number(float value, GameLanguage language) => value.ToString("0.##", CultureInfo.GetCultureInfo(language == GameLanguage.English ? "en-US" : "vi-VN"));

    public static string ElementName(ActionData.ElementType element, GameLanguage language)
    {
        switch (element)
        {
            case ActionData.ElementType.Fire: return Pick(language, "Lửa", "Fire");
            case ActionData.ElementType.Water: return Pick(language, "Nước", "Water");
            case ActionData.ElementType.Nature: return Pick(language, "Thiên nhiên", "Nature");
            default: return Pick(language, "Trung tính", "Neutral");
        }
    }

    public static string DebuffName(ActionData.DebuffType type, GameLanguage language)
    {
        switch (type)
        {
            case ActionData.DebuffType.Burn: return Pick(language, "Thiêu đốt", "Burn");
            case ActionData.DebuffType.Paralysis: return Pick(language, "Tê liệt", "Paralysis");
            default: return Pick(language, "Nhiễm độc", "Poison");
        }
    }

    // Also used by the status-effect tooltip, so skill previews and active effects agree.
    public static string DebuffBody(ActionData.DebuffSettings effect, GameLanguage language)
    {
        if (effect == null) return string.Empty;
        switch (effect.type)
        {
            case ActionData.DebuffType.Burn:
                return Pick(language,
                    $"Trừ {Mathf.Max(0, effect.burnDamage)} máu cuối lượt và giảm sức tấn công {Number(Mathf.Clamp(effect.burnAttackReductionPercent, 0, 100), language)}%.",
                    $"Lose {Mathf.Max(0, effect.burnDamage)} HP at turn end and reduce attack by {Number(Mathf.Clamp(effect.burnAttackReductionPercent, 0, 100), language)}%.");
            case ActionData.DebuffType.Paralysis:
                return Pick(language,
                    $"Có {Number(Mathf.Clamp(effect.paralysisSkipChance, 0, 100), language)}% khả năng mất lượt.",
                    $"{Number(Mathf.Clamp(effect.paralysisSkipChance, 0, 100), language)}% chance to skip a turn.");
            default:
                return Pick(language,
                    $"Mất {Number(Mathf.Clamp(effect.poisonMaxHpPercent, 0, 100), language)}% máu tối đa cuối lượt.",
                    $"Lose {Number(Mathf.Clamp(effect.poisonMaxHpPercent, 0, 100), language)}% of maximum HP at turn end.");
        }
    }

    static string Formula(ActionData action, GameLanguage language)
    {
        string result = Number(action.damageMultiplier * 100, language) + "% ATK";
        if (action.power != 0) result += (action.power > 0 ? " + " : " − ") + System.Math.Abs((long)action.power);
        return result;
    }

    static string BuffName(ActionData.BuffStat stat, GameLanguage language)
    {
        switch (stat)
        {
            case ActionData.BuffStat.Atk: return Pick(language, "sức tấn công", "attack");
            case ActionData.BuffStat.Def: return Pick(language, "phòng thủ", "defense");
            case ActionData.BuffStat.Crit: return Pick(language, "tỷ lệ chí mạng", "critical chance");
            case ActionData.BuffStat.CritDamage: return Pick(language, "sát thương chí mạng", "critical damage");
            default: return Pick(language, "lá chắn", "shield");
        }
    }

    public static string Build(ActionData action, GameLanguage language)
    {
        if (action == null) return Pick(language, "Chưa chọn kỹ năng.", "No skill selected.");
        if (action.type == ActionData.ActionType.Item)
            return GameLocalization.Translate(VietnameseGameUIText.ManualActionDescription(action), language);

        var lines = new List<string>();
        bool friendly = action.isFriendlyAction || action.isHeal;
        bool dealsDamage = !friendly && !action.statusOnly && !action.isSummonSkill;
        string target = action.isSelfOnly || action.isSummonSkill ? Pick(language, "bản thân", "self") :
            friendly ? (action.isAoE ? Pick(language, "toàn bộ đồng minh", "all allies") : Pick(language, "một đồng minh", "one ally")) :
            action.isAoE ? Pick(language, "toàn bộ kẻ địch", "all enemies") : Pick(language, "một kẻ địch", "one enemy");
        lines.Add(Pick(language, $"Nguyên tố: {ElementName(action.element, language)} • Mục tiêu: {target}.",
            $"Element: {ElementName(action.element, language)} • Target: {target}."));

        if (action.isSummonSkill)
        {
            string name = action.summonPrefab != null ? action.summonPrefab.unitName : null;
            lines.Add(string.IsNullOrWhiteSpace(name) ? Pick(language, "Chưa cấu hình đơn vị triệu hồi.", "No summon configured.") :
                Pick(language, $"Triệu hồi tối đa {Mathf.Clamp(action.summonCount, 1, 2)} {name} vào các vị trí đồng minh còn trống.",
                    $"Summon up to {Mathf.Clamp(action.summonCount, 1, 2)} {name} into available allied slots."));
            if (action.summonDeathOwnerMaxHpCostPercent > 0)
                lines.Add(Pick(language, $"Mỗi đơn vị triệu hồi chết khiến người triệu hồi mất {Number(Mathf.Clamp(action.summonDeathOwnerMaxHpCostPercent, 0, 100), language)}% HP tối đa.",
                    $"Each summon death costs its summoner {Number(Mathf.Clamp(action.summonDeathOwnerMaxHpCostPercent, 0, 100), language)}% of their maximum HP."));
        }
        else
        {
            if (dealsDamage)
                lines.Add(Pick(language, $"Mỗi đòn: {Formula(action, language)} sát thương cơ bản, trước phòng thủ, chí mạng và khắc hệ.",
                    $"Per hit: {Formula(action, language)} base damage, before defense, critical hits and elemental matchups."));
            else if (action.isHeal)
                lines.Add(Pick(language, $"Hồi HP bằng {Formula(action, language)} cho mục tiêu (không vượt HP tối đa).",
                    $"Restore {Formula(action, language)} HP to the target, up to maximum HP."));
            else lines.Add(Pick(language, "Không gây sát thương trực tiếp.", "Deals no direct damage."));

            if (dealsDamage && action.damageCondition == ActionData.DamageCondition.CasterHasBurn)
            {
                lines.Add(Pick(language,
                    $"Điều kiện: người dùng đang Thiêu đốt → sát thương ×{Number(Mathf.Max(0, action.damageMultiplierWhenCasterBurned), language)}; không Thiêu đốt → ×{Number(Mathf.Max(0, action.damageMultiplierWhenCasterNotBurned), language)}.",
                    $"Condition: caster has Burn → damage ×{Number(Mathf.Max(0, action.damageMultiplierWhenCasterBurned), language)}; without Burn → ×{Number(Mathf.Max(0, action.damageMultiplierWhenCasterNotBurned), language)}."));
                if (action.ignoreBurnAttackReductionWhenEmpowered)
                    lines.Add(Pick(language, "Khi người dùng đang Thiêu đốt, bỏ qua phần giảm ATK của Thiêu đốt.", "While the caster has Burn, ignore Burn's ATK reduction."));
            }
            if (dealsDamage && action.damageCondition == ActionData.DamageCondition.TargetHasPoison)
                lines.Add(Pick(language,
                    $"Điều kiện: mục tiêu Nhiễm độc → sát thương ×{Number(Mathf.Max(0, action.damageMultiplierWhenTargetPoisoned), language)}; chưa Nhiễm độc → ×{Number(Mathf.Max(0, action.damageMultiplierWhenTargetNotPoisoned), language)}.",
                    $"Condition: target has Poison → damage ×{Number(Mathf.Max(0, action.damageMultiplierWhenTargetPoisoned), language)}; without Poison → ×{Number(Mathf.Max(0, action.damageMultiplierWhenTargetNotPoisoned), language)}."));

            if (friendly && action.buffStat != ActionData.BuffStat.None && action.buffDuration > 0)
            {
                bool percent = action.buffStat == ActionData.BuffStat.Crit || action.buffStat == ActionData.BuffStat.CritDamage;
                string value = (action.buffAmount >= 0 ? "+" : "") + action.buffAmount;
                if (percent) value += Pick(language, " điểm %", " percentage points");
                string label = action.buffAmount < 0 ? "Debuff" : "Buff";
                if (action.buffStat == ActionData.BuffStat.Shield && action.buffAmount >= 0)
                    lines.Add(Pick(language, $"Buff: lá chắn hấp thụ tối đa {action.buffAmount} sát thương, trong {action.buffDuration} lượt.",
                        $"Buff: shield absorbs up to {action.buffAmount} damage for {action.buffDuration} turns."));
                else lines.Add(Pick(language, $"{label}: {BuffName(action.buffStat, language)} {value}, trong {action.buffDuration} lượt.",
                    $"{label}: {value} {BuffName(action.buffStat, language)} for {action.buffDuration} turns."));
            }
            if (action.cleanse != ActionData.CleanseType.None)
                lines.Add(action.cleanse == ActionData.CleanseType.RemoveDebuffs ?
                    Pick(language, "Xóa toàn bộ debuff của mục tiêu trước khi áp dụng hiệu ứng mới.", "Remove all target debuffs before applying new effects.") :
                    Pick(language, "Xóa toàn bộ buff của mục tiêu trước khi áp dụng hiệu ứng mới.", "Remove all target buffs before applying new effects."));

            int effects = 0;
            if (action.debuffs != null)
                foreach (var effect in action.debuffs)
                {
                    if (effect == null || effect.turns <= 0 || effect.applyChance <= 0) continue;
                    effects++;
                    lines.Add(Pick(language,
                        $"Debuff: {Number(Mathf.Clamp(effect.applyChance, 0, 100), language)}% gây {DebuffName(effect.type, language)} trong {effect.turns} lượt. {DebuffBody(effect, language)}",
                        $"Debuff: {Number(Mathf.Clamp(effect.applyChance, 0, 100), language)}% chance to apply {DebuffName(effect.type, language)} for {effect.turns} turns. {DebuffBody(effect, language)}"));
                }
            if (effects > 0)
                lines.Add(Pick(language, "Chỉ nhận khi chưa có debuff; không cộng dồn hay làm mới thời gian." + (effects > 1 ? " Lấy hiệu ứng đầu tiên áp dụng thành công theo thứ tự trên." : ""),
                    "Only applies to targets without a debuff; no stacking or duration refresh." + (effects > 1 ? " Apply the first successful effect in the order above." : "")));
            if (dealsDamage && action.useLifeSteal && action.lifeStealPercent > 0)
                lines.Add(Pick(language, $"Hút máu: hồi {Number(action.lifeStealPercent, language)}% lượng HP thực sự gây mất cho người dùng.",
                    $"Life steal: restore {Number(action.lifeStealPercent, language)}% of actual HP damage dealt to the caster."));
        }
        if (action.isUltimate)
            lines.Add(Pick(language, "Cần đầy thanh Tuyệt kỹ; tiêu hao toàn bộ năng lượng khi sử dụng.", "Requires a full Ultimate gauge; consumes all Ultimate energy on use."));
        if (action.stainChange != 0)
            lines.Add(action.stainChange < 0 ? Pick(language, $"Tiêu hao {-(long)action.stainChange} Tinh lực.", $"Costs {-(long)action.stainChange} Stain.") :
                Pick(language, $"Nhận {action.stainChange} Tinh lực.", $"Gain {action.stainChange} Stain."));
        return string.Join("\n", lines);
    }

    // Cheap revision check for live Inspector changes; UI only rebuilds text when relevant data changes.
    public static int Revision(ActionData a)
    {
        if (a == null) return 0;
        unchecked
        {
            int h = a.GetInstanceID();
            h = h * 31 + (int)a.type; h = h * 31 + (int)a.element; h = h * 31 + a.power;
            h = h * 31 + a.damageMultiplier.GetHashCode(); h = h * 31 + a.damageCondition.GetHashCode();
            h = h * 31 + a.damageMultiplierWhenCasterBurned.GetHashCode(); h = h * 31 + a.damageMultiplierWhenCasterNotBurned.GetHashCode();
            h = h * 31 + a.damageMultiplierWhenTargetPoisoned.GetHashCode(); h = h * 31 + a.damageMultiplierWhenTargetNotPoisoned.GetHashCode();
            h = h * 31 + a.ignoreBurnAttackReductionWhenEmpowered.GetHashCode(); h = h * 31 + a.useLifeSteal.GetHashCode(); h = h * 31 + a.lifeStealPercent.GetHashCode();
            h = h * 31 + a.isHeal.GetHashCode(); h = h * 31 + a.isFriendlyAction.GetHashCode(); h = h * 31 + a.isAoE.GetHashCode(); h = h * 31 + a.isSelfOnly.GetHashCode();
            h = h * 31 + a.statusOnly.GetHashCode(); h = h * 31 + a.isSummonSkill.GetHashCode(); h = h * 31 + a.summonCount; h = h * 31 + a.summonDeathOwnerMaxHpCostPercent.GetHashCode();
            h = h * 31 + (a.summonPrefab != null ? (a.summonPrefab.unitName ?? "").GetHashCode() : 0);
            h = h * 31 + a.buffStat.GetHashCode(); h = h * 31 + a.buffAmount; h = h * 31 + a.buffDuration; h = h * 31 + a.cleanse.GetHashCode();
            h = h * 31 + a.isUltimate.GetHashCode(); h = h * 31 + a.stainChange; h = h * 31 + (a.description ?? "").GetHashCode();
            if (a.debuffs != null) foreach (var e in a.debuffs)
            {
                if (e == null) { h *= 31; continue; }
                h = h * 31 + (int)e.type; h = h * 31 + e.turns; h = h * 31 + e.applyChance.GetHashCode(); h = h * 31 + e.burnDamage;
                h = h * 31 + e.burnAttackReductionPercent.GetHashCode(); h = h * 31 + e.paralysisSkipChance.GetHashCode(); h = h * 31 + e.poisonMaxHpPercent.GetHashCode();
            }
            return h;
        }
    }
}
