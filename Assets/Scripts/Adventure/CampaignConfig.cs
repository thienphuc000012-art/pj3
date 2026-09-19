using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Adventure/Campaign Config")]
public class CampaignConfig : ScriptableObject
{
    public string mapScene = "mapgame";
    public string battleScene = "combattest";
    public List<BattleUnit> startingParty = new List<BattleUnit>();
    public BattleUnit demoEnemy;
    public List<AdventureItem> items = new List<AdventureItem>();
    public List<CraftRecipe> recipes = new List<CraftRecipe>();
    public bool defeatReturnsToCheckpoint = true;
    [Min(1)] public int equippedSkillSlots = 4;
    public List<SkillUnlockRule> skillUnlocks = new List<SkillUnlockRule>();
    public SkillUnlockRule SkillRule(ActionData skill) => skillUnlocks?.Find(x => x != null && x.skill == skill);
    public ActionData[] UnlockPrerequisites(ActionData skill) => SkillRule(skill)?.prerequisites?.Where(x => x != null).Distinct().ToArray() ?? new ActionData[0];
    public AdventureItem FindItem(string id) => items.Find(x => x.id == id);
}

[Serializable]
public class SkillUnlockRule
{
    public ActionData skill;
    [Min(1)] public int cost = 1;
    public bool initiallyUnlocked;
    public List<ActionData> prerequisites = new List<ActionData>();
}

[Serializable]
public class AdventureItem
{
    public string id;
    public string displayName;
    public Sprite icon;
    [TextArea] public string description;
    [Min(0)] public int mapHeal;
    public ActionData combatAction;
}

[Serializable]
public class ItemStack
{
    public string id;
    public int count;
    public ItemStack() { }
    public ItemStack(string id, int count) { this.id = id; this.count = count; }
}

[Serializable]
public class CraftRecipe
{
    public string displayName;
    public List<ItemStack> ingredients = new List<ItemStack>();
    public ItemStack output;
}

[Serializable]
public class SkillRank
{
    public string id;
    public int rank;
}

[Serializable]
public class PartyMemberProgress
{
    public int prefabIndex;
    public int level = 1;
    public int xp;
    public int hp = 100;
    public int statPoints;
    public int skillPoints;
    public int hpBonus, atkBonus, defBonus, critBonus, critDamageBonus;
    public List<SkillRank> skills = new List<SkillRank>();
    public bool loadoutInitialized;
    public List<string> equippedSkills = new List<string>();
    public bool TryEquip(int slot, string id, string[] learned, int capacity)
    {
        if (slot < 0 || slot >= capacity || learned == null || (!string.IsNullOrEmpty(id) && !learned.Contains(id))) return false;
        if (equippedSkills == null) equippedSkills = new List<string>();
        while (equippedSkills.Count < capacity) equippedSkills.Add("");
        int previous = string.IsNullOrEmpty(id) ? -1 : equippedSkills.IndexOf(id);
        if (previous >= 0 && previous != slot) equippedSkills[previous] = equippedSkills[slot];
        equippedSkills[slot] = id ?? ""; loadoutInitialized = true; return true;
    }
    public int RequiredXP => 100 + (level - 1) * 50;
    public void GainXP(int amount)
    {
        xp += Mathf.Max(0, amount);
        while (xp >= RequiredXP && level < 100)
        {
            xp -= RequiredXP;
            level++;
            statPoints += 3;
            skillPoints++;
        }
        if (level >= 100) xp = Mathf.Min(xp, RequiredXP);
    }
    public int Rank(ActionData skill) => skill == null ? 0 : skills.Find(x => x.id == skill.name)?.rank ?? 0;
    public bool TryAllocateStats(int[] points, int baseCrit)
    {
        if (points == null || points.Length != 5 || points.Any(x => x < 0) || points.Sum() <= 0 || points.Sum() > statPoints) return false;
        int remainingCrit = Math.Max(0, 100 - baseCrit - critBonus);
        if (points[3] > (remainingCrit + 1) / 2) return false;
        statPoints -= points.Sum();
        hpBonus += points[0] * 10; hp += points[0] * 10;
        atkBonus += points[1] * 2; defBonus += points[2];
        critBonus += Math.Min(points[3] * 2, remainingCrit); critDamageBonus += points[4] * 5;
        return true;
    }
    public bool TryUnlock(ActionData skill, int cost)
    {
        if (skill == null || cost < 1 || skillPoints < cost || Rank(skill) > 0) return false;
        var entry = skills.Find(x => x.id == skill.name);
        if (entry == null) { entry = new SkillRank { id = skill.name }; skills.Add(entry); }
        entry.rank = 1; skillPoints -= cost; return true;
    }
}

[Serializable]
public class CampaignSave
{
    public int version = 2;
    public void MigrateSkillUnlocks()
    {
        if (version >= 2) return;
        foreach (var member in party)
            foreach (var entry in member.skills)
                if (entry.rank > 1) { member.skillPoints += entry.rank - 1; entry.rank = 1; }
        version = 2;
    }
    public List<PartyMemberProgress> party = new List<PartyMemberProgress>();
    public List<ItemStack> inventory = new List<ItemStack>();
    public List<string> collected = new List<string>();
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 checkpoint;
    public Quaternion checkpointRotation = Quaternion.identity;
    public bool hasPosition;
    public bool hasCheckpoint;

    public int Count(string id) => inventory.Find(x => x.id == id)?.count ?? 0;
    public void AddItem(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return;
        var stack = inventory.Find(x => x.id == id);
        if (stack == null) inventory.Add(new ItemStack(id, amount)); else stack.count += amount;
    }
    public bool Consume(string id, int amount)
    {
        if (amount <= 0 || Count(id) < amount) return false;
        var stack = inventory.Find(x => x.id == id);
        stack.count -= amount;
        if (stack.count == 0) inventory.Remove(stack);
        return true;
    }
    public bool HasIngredients(CraftRecipe recipe) => recipe != null && recipe.output != null &&
        !string.IsNullOrEmpty(recipe.output.id) && recipe.output.count > 0 && recipe.ingredients != null && recipe.ingredients.Count > 0 &&
        recipe.ingredients.All(x => x != null && !string.IsNullOrEmpty(x.id) && x.count > 0) &&
        recipe.ingredients.GroupBy(x => x.id).All(g => Count(g.Key) >= g.Sum(x => x.count));

    public bool TryCraft(CraftRecipe recipe)
    {
        // Validate the complete recipe first so failed crafting never consumes materials.
        if (!HasIngredients(recipe)) return false;
        foreach (var group in recipe.ingredients.GroupBy(x => x.id)) Consume(group.Key, group.Sum(x => x.count));
        AddItem(recipe.output.id, recipe.output.count);
        return true;
    }
}
