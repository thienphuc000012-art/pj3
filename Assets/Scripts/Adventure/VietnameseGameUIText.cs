using System.Text.RegularExpressions;

/// <summary>Vietnamese display names for built-in combat actions and status effects.</summary>
public static class VietnameseGameUIText
{
    public static string ActionName(ActionData action)
    {
        if (action == null) return "Kỹ năng";
        string value = (action.actionName ?? string.Empty).Trim();
        string key = value.ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);

        switch (key)
        {
            case "attack": return "Tấn công";
            case "skill": return "Kỹ năng";
            case "item": return "Vật phẩm";
            case "healingpotion": return "Thuốc hồi máu";
            case "attackslashcombo": return "Liên trảm";
            case "clawsattackleft": return "Vồ bằng móng trái";
            case "clawsattackright": return "Vồ bằng móng phải";
            case "clawsattack2hitcombo": return "Combo vuốt hai đòn";
            case "clawsattack2hitcombotograbbite": return "Combo vuốt, vồ và cắn";
            case "flystationaryspitfireball": return "Phun cầu lửa khi bay";
            case "flystationaryspreadfire": return "Phun lửa khi bay";
            case "spitfireball": return "Phun cầu lửa";
            case "spreadfire": return "Phun lửa";
            case "punch": return "Cú đấm";
            case "runaway": return "Bỏ chạy";
            case "parry": return "Đỡ đòn";
            case "b":
            case "fb":
                return action.isUltimate ? "Tuyệt kỹ" : NameFromAsset(action.name);
            default:
                return string.IsNullOrWhiteSpace(value) ? (action.isUltimate ? "Tuyệt kỹ" : "Kỹ năng") : value;
        }
    }

    public static string ActionDescription(ActionData action)
    {
        if (action == null || string.IsNullOrWhiteSpace(action.description)) return "Chưa có mô tả kỹ năng.";
        string value = action.description.Trim();
        switch (value.ToLowerInvariant())
        {
            case "restore 40 hp to one ally. consumes one potion.":
                return "Hồi 40 máu cho một đồng minh. Tiêu hao 1 bình thuốc.";
            case "two-hit sword combo: attack followed by slashattack. power applies to each hit.":
                return "Combo kiếm gồm hai đòn liên tiếp. Sức mạnh áp dụng cho từng đòn.";
            case "anbfaklfbafbna":
                return "Chưa có mô tả kỹ năng.";
            default:
                return value;
        }
    }

    public static string StatusName(ActionData.DebuffType type)
    {
        switch (type)
        {
            case ActionData.DebuffType.Burn: return "Thiêu đốt";
            case ActionData.DebuffType.Paralysis: return "Tê liệt";
            case ActionData.DebuffType.Poison: return "Nhiễm độc";
            default: return "Hiệu ứng bất lợi";
        }
    }

    public static string StatusName(string type)
    {
        if (string.Equals(type, "Burn", System.StringComparison.OrdinalIgnoreCase)) return "Thiêu đốt";
        if (string.Equals(type, "Paralysis", System.StringComparison.OrdinalIgnoreCase)) return "Tê liệt";
        if (string.Equals(type, "Poison", System.StringComparison.OrdinalIgnoreCase)) return "Nhiễm độc";
        return type;
    }

    static string NameFromAsset(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName)) return "Kỹ năng";
        string key = assetName.ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        switch (key)
        {
            case "swordattack1": return "Chém kiếm";
            case "punch": return "Cú đấm";
            case "ulti": return "Tuyệt kỹ";
        }
        if (assetName.StartsWith("vfx", System.StringComparison.OrdinalIgnoreCase))
        {
            string number = Regex.Match(assetName, @"\d+").Value;
            if (!string.IsNullOrEmpty(number)) return "Kỹ năng " + number;
        }
        return "Kỹ năng";
    }
}
